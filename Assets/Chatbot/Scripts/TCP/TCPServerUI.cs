// ═══════════════════════════════════════════════════════════════════════════
// TCPServerUI.cs  –  Inspector controller for the TCP server (agent side)
// ═══════════════════════════════════════════════════════════════════════════
using UnityEngine;
using TMPro;

public class TCPServerUI : MonoBehaviour
{
    public int serverPort = 5556; // Different port from UDP
    [SerializeField] private TCPServer serverReference;
    [SerializeField] private TMP_InputField messageInput;

    private IServer _server;

    void Awake()
    {
        _server = serverReference;
    }

    void Start()
    {
        _server.OnMessageReceived += HandleMessageReceived;
        _server.OnConnected += HandleConnection;
        _server.OnDisconnected += HandleDisconnection;

        // Subscribe to file received event
        serverReference.OnFileReceived += HandleFileReceived;
    }

    public void StartServer()
    {
        _server.StartServer(serverPort);
    }

    public void SendServerMessage()
    {
        if (!_server.isServerRunning)
        {
            Debug.Log("[TCPServerUI] Server is not running");
            return;
        }
        if (string.IsNullOrEmpty(messageInput.text))
        {
            Debug.Log("[TCPServerUI] Message is empty");
            return;
        }

        _server.SendMessageAsync(messageInput.text);
        messageInput.text = "";
    }

    void HandleMessageReceived(string text)
    {
        Debug.Log("[TCPServerUI] Message from client: " + text);
    }

    void HandleFileReceived(FileTransferData file)
    {
        Debug.Log($"[TCPServerUI] File received from client – {file.fileName} ({file.fileType})");

        if (file.fileType == "image")
        {
            // Reconstruct texture on agent side if needed
            byte[] bytes = file.GetBytes();
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            Debug.Log($"[TCPServerUI] Image ready: {tex.width}x{tex.height}");
        }
        else if (file.fileType == "pdf")
        {
            // Save PDF to persistent path on server machine
            string path = System.IO.Path.Combine(Application.persistentDataPath, file.fileName);
            System.IO.File.WriteAllBytes(path, file.GetBytes());
            Debug.Log("[TCPServerUI] PDF saved to: " + path);
        }
    }

    void HandleConnection() => Debug.Log("[TCPServerUI] Client connected (human phase)");
    void HandleDisconnection() => Debug.Log("[TCPServerUI] Client disconnected");
}
