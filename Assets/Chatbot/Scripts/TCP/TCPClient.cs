using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using UnityEngine;

public class TCPClient : MonoBehaviour, IClient
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;

    public bool isConnected { get; private set; }

    public event Action<string> OnMessageReceived;
    public event Action<FileTransferData> OnFileReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public async Task ConnectToServer(string ip, int port)
    {
        tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(ip, port);
        networkStream = tcpClient.GetStream();

        isConnected = true;
        Debug.Log("[TCPClient] Connected to server");
        OnConnected?.Invoke();

        _ = ReceiveLoop();
    }

    private async Task ReceiveLoop()
    {
        try
        {
            while (tcpClient != null && tcpClient.Connected)
            {
                byte[] header = new byte[4];
                int headerRead = await ReadExactAsync(header, 4);
                if (headerRead == 0) break; // server disconnected

                int payloadLength = BitConverter.ToInt32(header, 0);
                if (payloadLength <= 0) continue;

                byte[] payload = new byte[payloadLength];
                int payloadRead = await ReadExactAsync(payload, payloadLength);
                if (payloadRead == 0) break;

                string raw = Encoding.UTF8.GetString(payload, 0, payloadRead);

                if (FileTransferData.TryParse(raw, out FileTransferData file))
                {
                    Debug.Log($"[TCPClient] File received: {file.fileName}");
                    OnFileReceived?.Invoke(file);
                }
                else
                {
                    Debug.Log("[TCPClient] Received: " + raw);
                    OnMessageReceived?.Invoke(raw);
                }
            }
        }
        finally
        {
            Disconnect();
        }
    }

    private async Task<int> ReadExactAsync(byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await networkStream.ReadAsync(buffer, totalRead, count - totalRead);
            if (read == 0) return 0; // disconnected
            totalRead += read;
        }
        return totalRead;
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isConnected || networkStream == null)
        {
            Debug.LogWarning("[TCPClient] Not connected to server");
            return;
        }

        await SendFrameAsync(Encoding.UTF8.GetBytes(message));
        Debug.Log("[TCPClient] Sent: " + message);
    }

    public async Task SendFileAsync(string filePath)
    {
        if (!isConnected || networkStream == null)
        {
            Debug.LogWarning("[TCPClient] Not connected – cannot send file.");
            return;
        }

        if (!File.Exists(filePath))
        {
            Debug.LogError("[TCPClient] File not found: " + filePath);
            return;
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        string ext = Path.GetExtension(filePath).ToLower();
        string fileType = (ext == ".pdf") ? "pdf" : "image";
        string fileName = Path.GetFileName(filePath);

        await SendFileDataAsync(fileName, fileType, bytes);
    }

    public async Task SendTextureAsync(Texture2D texture, string fileName = "image.png")
    {
        if (!isConnected || networkStream == null) return;

        byte[] bytes = texture.EncodeToPNG();
        await SendFileDataAsync(fileName, "image", bytes);
    }

    public async Task SendFileDataAsync(string fileName, string fileType, byte[] bytes)
    {
        FileTransferData transfer = FileTransferData.FromBytes(fileName, fileType, bytes);
        string payload = transfer.ToNetworkString();
        await SendFrameAsync(Encoding.UTF8.GetBytes(payload));
        Debug.Log($"[TCPClient] File sent: {fileName} ({bytes.Length} bytes)");
    }

    private async Task SendFrameAsync(byte[] data)
    {
        byte[] header = BitConverter.GetBytes(data.Length); // 4 bytes
        await networkStream.WriteAsync(header, 0, header.Length);
        await networkStream.WriteAsync(data, 0, data.Length);
    }

    public void Disconnect()
    {
        isConnected = false;
        networkStream?.Close();
        tcpClient?.Close();
        networkStream = null;
        tcpClient = null;

        OnDisconnected?.Invoke();
        Debug.Log("[TCPClient] Disconnected");
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}
