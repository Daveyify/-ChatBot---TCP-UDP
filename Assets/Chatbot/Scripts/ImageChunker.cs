using System;
using System.Collections.Generic;

/// <summary>
/// Utilidad estática para fragmentar imágenes grandes en chunks
/// que caben dentro del límite de UDP (~65KB por paquete).
/// 
/// Protocolo de mensajes:
///   IMG_START:{id}:{totalChunks}         → avisa que viene una imagen
///   IMG_CHUNK:{id}:{index}:{base64data}  → fragmento de la imagen
///   IMG_END:{id}                         → imagen completa enviada
/// </summary>
public static class ImageChunker
{
    // Tamaño máximo de cada chunk en bytes ANTES de convertir a Base64.
    // Base64 aumenta ~33% el tamaño, así que 40KB de datos → ~53KB en Base64.
    // Dejamos margen bajo el límite de 65KB de UDP.
    private const int CHUNK_SIZE = 40000;

    public const string PREFIX_START = "IMG_START:";
    public const string PREFIX_CHUNK = "IMG_CHUNK:";
    public const string PREFIX_END   = "IMG_END:";

    /// <summary>
    /// Fragmenta un array de bytes en una lista de mensajes UDP listos para enviar.
    /// </summary>
    /// <param name="imageBytes">Bytes de la imagen original.</param>
    /// <param name="transferId">ID único para esta transferencia (evita mezclar imágenes).</param>
    /// <returns>Lista de strings: START, CHUNKs, END.</returns>
    public static List<string> BuildMessages(byte[] imageBytes, string transferId)
    {
        var messages = new List<string>();

        // Dividir los bytes en chunks
        int totalChunks = (int)Math.Ceiling((double)imageBytes.Length / CHUNK_SIZE);

        // 1. Mensaje de inicio
        messages.Add($"{PREFIX_START}{transferId}:{totalChunks}");

        // 2. Chunks
        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * CHUNK_SIZE;
            int length = Math.Min(CHUNK_SIZE, imageBytes.Length - offset);

            byte[] chunkBytes = new byte[length];
            Array.Copy(imageBytes, offset, chunkBytes, 0, length);

            string chunkBase64 = Convert.ToBase64String(chunkBytes);
            messages.Add($"{PREFIX_CHUNK}{transferId}:{i}:{chunkBase64}");
        }

        // 3. Mensaje de fin
        messages.Add($"{PREFIX_END}{transferId}");

        return messages;
    }

    /// <summary>
    /// Determina si un mensaje pertenece al protocolo de imagen.
    /// </summary>
    public static bool IsImageMessage(string message)
    {
        return message.StartsWith(PREFIX_START) ||
               message.StartsWith(PREFIX_CHUNK) ||
               message.StartsWith(PREFIX_END);
    }
}
