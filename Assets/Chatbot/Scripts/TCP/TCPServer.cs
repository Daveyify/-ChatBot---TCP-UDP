using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// TCP Server extended with file transfer using length-prefix framing.
/// [4 bytes length][payload] – matches TCPClient framing.
/// </summary>
public class TCPServer : MonoBehaviour, IServer
{
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream networkStream;

    public bool isServerRunning { get; private set; }

    public event Action<string> OnMessageReceived;
    public event Action<FileTransferData> OnFileReceived; // fired when client sends a file
    public event Action OnConnected;
    public event Action OnDisconnected;

    // ── Start ──────────────────────────────────────────────────────────────────

    public async Task StartServer(int port)
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();

        Debug.Log("[TCPServer] Started, waiting for connections...");
        isServerRunning = true;

        connectedClient = await tcpListener.AcceptTcpClientAsync();
        Debug.Log("[TCPServer] Client connected: " + connectedClient.Client.RemoteEndPoint);
        OnConnected?.Invoke();

        networkStream = connectedClient.GetStream();
        _ = ReceiveLoop();
    }

    // ── Receive (length-prefixed frames) ──────────────────────────────────────

    private async Task ReceiveLoop()
    {
        try
        {
            while (connectedClient != null && connectedClient.Connected)
            {
                // Read 4-byte length header
                byte[] header = new byte[4];
                int headerRead = await ReadExactAsync(header, 4);
                if (headerRead == 0)
                {
                    Debug.Log("[TCPServer] Client disconnected");
                    break;
                }

                int payloadLength = BitConverter.ToInt32(header, 0);
                if (payloadLength <= 0) continue;

                // Read full payload
                byte[] payload = new byte[payloadLength];
                int payloadRead = await ReadExactAsync(payload, payloadLength);
                if (payloadRead == 0) break;

                string raw = Encoding.UTF8.GetString(payload, 0, payloadRead);

                if (FileTransferData.TryParse(raw, out FileTransferData file))
                {
                    Debug.Log($"[TCPServer] File received from client: {file.fileName} ({file.fileType})");
                    OnFileReceived?.Invoke(file);
                }
                else
                {
                    Debug.Log("[TCPServer] Received: " + raw);
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
            if (read == 0) return 0;
            totalRead += read;
        }
        return totalRead;
    }

    // ── Send text ──────────────────────────────────────────────────────────────

    public async Task SendMessageAsync(string message)
    {
        if (networkStream == null || !connectedClient.Connected)
        {
            Debug.LogWarning("[TCPServer] No client connected");
            return;
        }

        await SendFrameAsync(Encoding.UTF8.GetBytes(message));
        Debug.Log("[TCPServer] Sent: " + message);
    }

    // ── Send file (agent → client) ─────────────────────────────────────────────

    public async Task SendFileDataAsync(string fileName, string fileType, byte[] bytes)
    {
        if (networkStream == null || !connectedClient.Connected)
        {
            Debug.LogWarning("[TCPServer] No client connected – cannot send file.");
            return;
        }

        FileTransferData transfer = FileTransferData.FromBytes(fileName, fileType, bytes);
        string payload = transfer.ToNetworkString();
        await SendFrameAsync(Encoding.UTF8.GetBytes(payload));
        Debug.Log($"[TCPServer] File sent to client: {fileName} ({bytes.Length} bytes)");
    }

    // ── Framing helper ─────────────────────────────────────────────────────────

    private async Task SendFrameAsync(byte[] data)
    {
        byte[] header = BitConverter.GetBytes(data.Length);
        await networkStream.WriteAsync(header, 0, header.Length);
        await networkStream.WriteAsync(data, 0, data.Length);
    }

    // ── Disconnect ─────────────────────────────────────────────────────────────

    public void Disconnect()
    {
        networkStream?.Close();
        connectedClient?.Close();
        networkStream = null;
        connectedClient = null;

        Debug.Log("[TCPServer] Disconnected");
        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}
