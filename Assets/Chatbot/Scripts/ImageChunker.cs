using System;
using System.Collections.Generic;

public static class ImageChunker
{
    private const int CHUNK_SIZE = 40000;

    public const string PREFIX_START = "IMG_START:";
    public const string PREFIX_CHUNK = "IMG_CHUNK:";
    public const string PREFIX_END = "IMG_END:";

    public const string PREFIX_PDF_START = "PDF_START:";
    public const string PREFIX_PDF_CHUNK = "PDF_CHUNK:";
    public const string PREFIX_PDF_END = "PDF_END:";

    public static List<string> BuildMessages(byte[] imageBytes, string transferId)
    {
        return BuildMessagesInternal(imageBytes, transferId,
            PREFIX_START, PREFIX_CHUNK, PREFIX_END, null);
    }

    public static List<string> BuildPDFMessages(byte[] pdfBytes, string transferId, string fileName)
    {
        return BuildMessagesInternal(pdfBytes, transferId,
            PREFIX_PDF_START, PREFIX_PDF_CHUNK, PREFIX_PDF_END, fileName);
    }

    private static List<string> BuildMessagesInternal(byte[] bytes, string transferId,
        string prefixStart, string prefixChunk, string prefixEnd, string fileName)
    {
        var messages = new List<string>();
        int totalChunks = (int)Math.Ceiling((double)bytes.Length / CHUNK_SIZE);

        string startMsg = fileName != null
            ? $"{prefixStart}{transferId}:{totalChunks}:{fileName}"
            : $"{prefixStart}{transferId}:{totalChunks}";
        messages.Add(startMsg);

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * CHUNK_SIZE;
            int length = Math.Min(CHUNK_SIZE, bytes.Length - offset);
            byte[] chunkBytes = new byte[length];
            Array.Copy(bytes, offset, chunkBytes, 0, length);
            messages.Add($"{prefixChunk}{transferId}:{i}:{Convert.ToBase64String(chunkBytes)}");
        }

        messages.Add($"{prefixEnd}{transferId}");
        return messages;
    }

    public static bool IsChunkedMessage(string message)
    {
        return message.StartsWith(PREFIX_START) ||
               message.StartsWith(PREFIX_CHUNK) ||
               message.StartsWith(PREFIX_END) ||
               message.StartsWith(PREFIX_PDF_START) ||
               message.StartsWith(PREFIX_PDF_CHUNK) ||
               message.StartsWith(PREFIX_PDF_END);
    }
}