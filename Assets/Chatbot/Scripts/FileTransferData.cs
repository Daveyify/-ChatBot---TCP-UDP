using System;
using UnityEngine;

[Serializable]
public class FileTransferData
{
    public string fileName;   
    public string fileType;   
    public string base64Data; 

    public const string FILE_PREFIX = "##FILE##";

    public string ToNetworkString()
    {
        string json = JsonUtility.ToJson(this); 
        return FILE_PREFIX + json;
    }

    public static bool TryParse(string raw, out FileTransferData result)
    {
        result = null;
        if (!raw.StartsWith(FILE_PREFIX)) return false;

        string json = raw.Substring(FILE_PREFIX.Length);
        result = JsonUtility.FromJson<FileTransferData>(json);
        return result != null;
    }

    public byte[] GetBytes() => Convert.FromBase64String(base64Data);

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
