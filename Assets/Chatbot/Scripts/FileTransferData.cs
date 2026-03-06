using System;
using UnityEngine;

/// <summary>
/// Shared structure to identify and transport files over the network.
/// Serialized as JSON for both UDP and TCP transfers.
/// Supported types: image (png/jpg) and pdf.
/// </summary>
[Serializable]
public class FileTransferData
{
    public string fileName;   // e.g. "photo.png"
    public string fileType;   // "image" | "pdf"
    public string base64Data; // File bytes encoded as Base64

    // Helper: build prefix tag used in raw messages to detect file payloads
    public const string FILE_PREFIX = "##FILE##";

    /// <summary>Serialize this object to a prefixed JSON string ready to send.</summary>
    public string ToNetworkString()
    {
        string json = JsonUtility.ToJson(this); // UnityEngine.JsonUtility – no extra deps
        return FILE_PREFIX + json;
    }

    /// <summary>Returns true and populates 'result' if the raw message is a file payload.</summary>
    public static bool TryParse(string raw, out FileTransferData result)
    {
        result = null;
        if (!raw.StartsWith(FILE_PREFIX)) return false;

        string json = raw.Substring(FILE_PREFIX.Length);
        result = JsonUtility.FromJson<FileTransferData>(json);
        return result != null;
    }

    /// <summary>Decode base64 back to raw bytes.</summary>
    public byte[] GetBytes() => Convert.FromBase64String(base64Data);

    /// <summary>Build a FileTransferData from raw bytes.</summary>
    public static FileTransferData FromBytes(string fileName, string fileType, byte[] bytes)
    {
        return new FileTransferData
        {
            fileName = fileName,
            fileType = fileType,
            base64Data = Convert.ToBase64String(bytes)
        };
    }
}
