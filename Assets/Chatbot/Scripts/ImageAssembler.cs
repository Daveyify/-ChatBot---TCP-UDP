using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Recolecta los chunks de imagen que llegan por UDP y los reconstruye
/// cuando están todos presentes.
/// 
/// Uso:
///   1. Llama a ProcessMessage() con cada mensaje que llegue
///   2. Suscríbete a OnImageAssembled para recibir la imagen completa
/// </summary>
public class ImageAssembler
{
    // Evento que se dispara cuando una imagen está completa
    public event Action<byte[]> OnImageAssembled;

    // Diccionario: transferId → datos de la transferencia en curso
    private readonly Dictionary<string, TransferData> _transfers = new Dictionary<string, TransferData>();

    private class TransferData
    {
        public int TotalChunks;
        public Dictionary<int, string> Chunks = new Dictionary<int, string>(); // index → base64
    }

    /// <summary>
    /// Procesa un mensaje UDP. Si no es de imagen lo ignora.
    /// Devuelve true si el mensaje fue procesado como parte de una imagen.
    /// </summary>
    public bool ProcessMessage(string message)
    {
        if (message.StartsWith(ImageChunker.PREFIX_START))
        {
            HandleStart(message);
            return true;
        }

        if (message.StartsWith(ImageChunker.PREFIX_CHUNK))
        {
            HandleChunk(message);
            return true;
        }

        if (message.StartsWith(ImageChunker.PREFIX_END))
        {
            HandleEnd(message);
            return true;
        }

        return false;
    }

    // ── Handlers internos ────────────────────────────────────────────

    private void HandleStart(string message)
    {
        // Formato: IMG_START:{id}:{totalChunks}
        string payload = message.Substring(ImageChunker.PREFIX_START.Length);
        string[] parts = payload.Split(':');

        if (parts.Length < 2) return;

        string id          = parts[0];
        int    totalChunks = int.Parse(parts[1]);

        _transfers[id] = new TransferData { TotalChunks = totalChunks };
        Debug.Log($"[ImageAssembler] Iniciando transferencia '{id}' — {totalChunks} chunks esperados.");
    }

    private void HandleChunk(string message)
    {
        // Formato: IMG_CHUNK:{id}:{index}:{base64data}
        string payload = message.Substring(ImageChunker.PREFIX_CHUNK.Length);

        // Solo dividir en 3 partes (el base64 puede contener ':' en teoría aunque es raro)
        int firstColon  = payload.IndexOf(':');
        int secondColon = payload.IndexOf(':', firstColon + 1);

        if (firstColon < 0 || secondColon < 0) return;

        string id         = payload.Substring(0, firstColon);
        int    chunkIndex = int.Parse(payload.Substring(firstColon + 1, secondColon - firstColon - 1));
        string base64     = payload.Substring(secondColon + 1);

        if (!_transfers.ContainsKey(id))
        {
            Debug.LogWarning($"[ImageAssembler] Chunk recibido para transferencia desconocida '{id}'.");
            return;
        }

        _transfers[id].Chunks[chunkIndex] = base64;
        Debug.Log($"[ImageAssembler] Chunk {chunkIndex + 1}/{_transfers[id].TotalChunks} recibido.");
    }

    private void HandleEnd(string message)
    {
        // Formato: IMG_END:{id}
        string id = message.Substring(ImageChunker.PREFIX_END.Length);

        if (!_transfers.ContainsKey(id))
        {
            Debug.LogWarning($"[ImageAssembler] IMG_END para transferencia desconocida '{id}'.");
            return;
        }

        TransferData transfer = _transfers[id];

        // Verificar que llegaron todos los chunks
        if (transfer.Chunks.Count != transfer.TotalChunks)
        {
            Debug.LogWarning($"[ImageAssembler] Transferencia '{id}' incompleta: " +
                             $"{transfer.Chunks.Count}/{transfer.TotalChunks} chunks.");
            _transfers.Remove(id);
            return;
        }

        // Reconstruir la imagen juntando los chunks en orden
        var allBytes = new System.Collections.Generic.List<byte>();
        for (int i = 0; i < transfer.TotalChunks; i++)
        {
            byte[] chunkBytes = Convert.FromBase64String(transfer.Chunks[i]);
            allBytes.AddRange(chunkBytes);
        }

        _transfers.Remove(id);
        Debug.Log($"[ImageAssembler] Imagen '{id}' reconstruida — {allBytes.Count} bytes.");

        OnImageAssembled?.Invoke(allBytes.ToArray());
    }
}
