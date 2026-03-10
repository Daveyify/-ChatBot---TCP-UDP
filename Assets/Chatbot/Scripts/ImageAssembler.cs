using System;
using System.Collections.Generic;
using UnityEngine;

public class ImageAssembler
{
    public event Action<byte[]> OnImageAssembled;
    public event Action<byte[], string> OnPDFAssembled; // bytes + fileName

    private readonly Dictionary<string, TransferData> _transfers = new Dictionary<string, TransferData>();

    private class TransferData
    {
        public int TotalChunks;
        public string FileName;
        public bool IsPDF;
        public Dictionary<int, string> Chunks = new Dictionary<int, string>();
    }

    public bool ProcessMessage(string message)
    {
        if (message.StartsWith(ImageChunker.PREFIX_START)) { HandleStart(message, false); return true; }
        if (message.StartsWith(ImageChunker.PREFIX_CHUNK)) { HandleChunk(message, ImageChunker.PREFIX_CHUNK); return true; }
        if (message.StartsWith(ImageChunker.PREFIX_END)) { HandleEnd(message, ImageChunker.PREFIX_END, false); return true; }
        if (message.StartsWith(ImageChunker.PREFIX_PDF_START)) { HandleStart(message, true); return true; }
        if (message.StartsWith(ImageChunker.PREFIX_PDF_CHUNK)) { HandleChunk(message, ImageChunker.PREFIX_PDF_CHUNK); return true; }
        if (message.StartsWith(ImageChunker.PREFIX_PDF_END)) { HandleEnd(message, ImageChunker.PREFIX_PDF_END, true); return true; }
        return false;
    }

    private void HandleStart(string message, bool isPDF)
    {
        string prefix = isPDF ? ImageChunker.PREFIX_PDF_START : ImageChunker.PREFIX_START;
        string payload = message.Substring(prefix.Length);
        string[] parts = payload.Split(':');
        if (parts.Length < 2) return;

        string id = parts[0];
        int totalChunks = int.Parse(parts[1]);
        string fileName = isPDF && parts.Length >= 3 ? parts[2] : "document.pdf";

        _transfers[id] = new TransferData { TotalChunks = totalChunks, FileName = fileName, IsPDF = isPDF };
        Debug.Log($"[Assembler] Started {(isPDF ? "PDF" : "IMG")} '{id}' — {totalChunks} chunks.");
    }

    private void HandleChunk(string message, string prefix)
    {
        string payload = message.Substring(prefix.Length);
        int firstColon = payload.IndexOf(':');
        int secondColon = payload.IndexOf(':', firstColon + 1);
        if (firstColon < 0 || secondColon < 0) return;

        string id = payload.Substring(0, firstColon);
        int chunkIndex = int.Parse(payload.Substring(firstColon + 1, secondColon - firstColon - 1));
        string base64 = payload.Substring(secondColon + 1);

        if (!_transfers.ContainsKey(id)) return;
        _transfers[id].Chunks[chunkIndex] = base64;
    }

    private void HandleEnd(string message, string prefix, bool isPDF)
    {
        string id = message.Substring(prefix.Length);
        if (!_transfers.ContainsKey(id)) return;

        TransferData transfer = _transfers[id];
        if (transfer.Chunks.Count != transfer.TotalChunks)
        {
            Debug.LogWarning($"[Assembler] Tranfer '{id}' incomplete.");
            _transfers.Remove(id);
            return;
        }

        var allBytes = new List<byte>();
        for (int i = 0; i < transfer.TotalChunks; i++)
            allBytes.AddRange(Convert.FromBase64String(transfer.Chunks[i]));

        string fileName = transfer.FileName;
        _transfers.Remove(id);
        Debug.Log($"[Assembler] '{id}' rebuilt — {allBytes.Count} bytes.");

        if (isPDF)
            OnPDFAssembled?.Invoke(allBytes.ToArray(), fileName);
        else
            OnImageAssembled?.Invoke(allBytes.ToArray());
    }
}