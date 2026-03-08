using System;
using System.Collections.Generic;

/// <summary>
/// Fragmenta imágenes y PDFs en chunks para envío por UDP.
/// 
/// Protocolo:
///   IMG_START:{id}:{totalChunks}         → inicio de imagen
///   IMG_CHUNK:{id}:{index}:{base64data}  → fragmento de imagen
///   IMG_END:{id}                         → fin de imagen
///
///   PDF_START:{id}:{totalChunks}:{fileName} → inicio de PDF
///   PDF_CHUNK:{id}:{index}:{base64data}     → fragmento de PDF
///   PDF_END:{id}                            → fin de PDF
/// </summary>
public static class ImageChunker
{
    private const int CHUNK_SIZE = 40000;

    // Prefijos imagen
    public const string PREFIX_START = "IMG_START:";
    public const string PREFIX_CHUNK = "IMG_CHUNK:";
    public const string PREFIX_END = "IMG_END:";

    // Prefijos PDF
    public const string PREFIX_PDF_START = "PDF_START:";
    public const string PREFIX_PDF_CHUNK = "PDF_CHUNK:";
    public const string PREFIX_PDF_END = "PDF_END:";

    /// <summary>Fragmenta una imagen en mensajes UDP.</summary>
    public static List<string> BuildMessages(byte[] imageBytes, string transferId)
    {
        return BuildMessagesInternal(imageBytes, transferId,
            PREFIX_START, PREFIX_CHUNK, PREFIX_END, null);
    }

    /// <summary>Fragmenta un PDF en mensajes UDP incluyendo el nombre del archivo.</summary>
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

        // START — incluye fileName si es PDF
        string startMsg = fileName != null
            ? $"{prefixStart}{transferId}:{totalChunks}:{fileName}"
            : $"{prefixStart}{transferId}:{totalChunks}";
        messages.Add(startMsg);

        // CHUNKS
        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * CHUNK_SIZE;
            int length = Math.Min(CHUNK_SIZE, bytes.Length - offset);
            byte[] chunkBytes = new byte[length];
            Array.Copy(bytes, offset, chunkBytes, 0, length);
            messages.Add($"{prefixChunk}{transferId}:{i}:{Convert.ToBase64String(chunkBytes)}");
        }

        // END
        messages.Add($"{prefixEnd}{transferId}");
        return messages;
    }

    /// <summary>Devuelve true si el mensaje pertenece al protocolo de imagen o PDF.</summary>
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