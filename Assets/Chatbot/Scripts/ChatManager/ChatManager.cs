using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    [Header("UDP")]
    [SerializeField] private UDPServer udpServer;
    [SerializeField] private UDPClient udpClient;

    [Header("TCP Client")]
    [SerializeField] private TCPClient tcpClient;

    [Header("Settings UDP")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private int udpPort = 5555;
    [SerializeField] private int delayBetweenMessagesMs = 1000;
    [SerializeField] private int delayBetweenChunksMs = 10;

    [Header("Settings TCP")]
    [SerializeField] private int tcpPort = 5556;

    [Header("Bot automatic message")]
    [SerializeField] private string botMessage1 = "¡Hola! Bienvenido al chat. ¿En qué puedo ayudarte?";
    [SerializeField] private string botMessage2 = "Gracias por tu respuesta. Ahora te voy a conectar con un agente.";

    [Header("UI Client")]
    [SerializeField] private ChatUIManager chatUI;

    private ServerUIManager _serverUI;
    private TCPServer _tcpServer;

    private bool _handshakeCompleted = false;
    private bool _waitingForUserReply = false;
    private bool _isTCPActive = false;

    // Flags para evitar eco TCP
    private bool _ignoreNextTCPClientMessage = false;
    private bool _ignoreNextTCPClientFile = false;

    private ImageAssembler _imageAssembler;

    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private readonly object _queueLock = new object();

    // ── Unity ────────────────────────────────────────────────────────

    void Start()
    {
        _imageAssembler = new ImageAssembler();
        _imageAssembler.OnImageAssembled += OnUDPImageAssembled;
        _imageAssembler.OnPDFAssembled += OnUDPPDFAssembled;

        udpClient.OnConnected += HandleUDPHandshake;
        udpClient.OnMessageReceived += HandleUDPClientReceived;

        tcpClient.OnMessageReceived += HandleTCPClientReceived;
        tcpClient.OnFileReceived += HandleTCPClientFileReceived;

        chatUI.OnUserSendText += HandleUserSendText;
        chatUI.OnUserSendImage += HandleUserSendImage;
        chatUI.OnUserSendPDF += HandleUserSendPDF;
    }

    void Update()
    {
        lock (_queueLock)
        {
            while (_mainThreadQueue.Count > 0)
                _mainThreadQueue.Dequeue().Invoke();
        }
    }

    private void RunOnMainThread(Action action)
    {
        lock (_queueLock) { _mainThreadQueue.Enqueue(action); }
    }

    // ── Registro ─────────────────────────────────────────────────────

    public void RegisterServerUI(ServerUIManager serverUI, TCPServer tcpServer)
    {
        _serverUI = serverUI;
        _tcpServer = tcpServer;

        _serverUI.OnServerSendText += HandleServerSendText;
        _serverUI.OnServerSendImage += HandleServerSendImage;
        _serverUI.OnServerSendPDF += HandleServerSendPDF;

        _tcpServer.OnMessageReceived += HandleTCPServerReceived;
        _tcpServer.OnFileReceived += HandleTCPServerFileReceived;
        _tcpServer.OnConnected += HandleTCPConnected;

        Debug.Log("[ChatManager] ServerUIManager y TCPServer registered.");
    }

    // ── Inicio UDP ───────────────────────────────────────────────────

    public async void StartChatBot()
    {
        _handshakeCompleted = false;
        _waitingForUserReply = false;
        _isTCPActive = false;

        await udpServer.StartServer(udpPort);
        await udpClient.ConnectToServer(serverAddress, udpPort);
        await WaitForHandshake();

        await SendBotMessage(botMessage1);
        _waitingForUserReply = true;
    }

    // ── Transición UDP → TCP ─────────────────────────────────────────

    private async void SwitchToTCP()
    {
        if (_tcpServer == null)
        {
            Debug.LogWarning("[ChatManager] TCPServer not registered.");
            return;
        }

        RunOnMainThread(() =>
        {
            chatUI.AddSystemBubble("Conectando con un agente...");
            _serverUI?.AddSystemBubble("Conectando con un agente...");
        });

        _ = _tcpServer.StartServer(tcpPort);
        await Task.Delay(500);
        await tcpClient.ConnectToServer(serverAddress, tcpPort);

        udpClient.Disconnect();
        udpServer.Disconnect();

        _isTCPActive = true;
        Debug.Log("[ChatManager] TCP active. UDP disconnected.");
    }

    private void HandleTCPConnected()
    {
        Debug.Log("[ChatManager] TCP connected.");
        RunOnMainThread(() =>
        {
            chatUI.AddSystemBubble("Conectado con un agente (TCP)");
            _serverUI?.AddSystemBubble("Cliente conectado (TCP)");
        });
    }

    // ── Handlers UI Cliente ──────────────────────────────────────────

    private async void HandleUserSendText(string message)
    {
        RunOnMainThread(() => _serverUI?.AddClientTextBubble(message));

        if (_isTCPActive)
            await tcpClient.SendMessageAsync(message);
        else
        {
            await udpClient.SendMessageAsync(message);
            if (_waitingForUserReply) await ContinueBotFlow();
        }
    }

    private async void HandleUserSendImage(byte[] imageBytes)
    {
        RunOnMainThread(() => _serverUI?.AddClientImageBubble(imageBytes));

        if (_isTCPActive)
            await tcpClient.SendFileDataAsync("image.png", "image", imageBytes);
        else
            await SendChunked(imageBytes, isImage: true, fileName: null);
    }

    private async void HandleUserSendPDF(byte[] pdfBytes, string fileName)
    {
        RunOnMainThread(() => _serverUI?.AddClientPDFBubble(pdfBytes, fileName));

        if (_isTCPActive)
            await tcpClient.SendFileDataAsync(fileName, "pdf", pdfBytes);
        else
            await SendChunked(pdfBytes, isImage: false, fileName: fileName);

        if (!_isTCPActive && _waitingForUserReply)
            await ContinueBotFlow();
    }

    // ── Handlers UI Servidor ─────────────────────────────────────────

    private async void HandleServerSendText(string message)
    {
        RunOnMainThread(() => chatUI.AddBotTextBubble(message));

        if (_isTCPActive)
        {
            _ignoreNextTCPClientMessage = true;
            await _tcpServer.SendMessageAsync(message);
        }
        else
            await udpServer.SendMessageAsync(message);
    }

    private async void HandleServerSendImage(byte[] imageBytes)
    {
        RunOnMainThread(() => chatUI.AddBotImageBubble(imageBytes));

        if (_isTCPActive)
        {
            _ignoreNextTCPClientFile = true;
            await _tcpServer.SendFileDataAsync("image.png", "image", imageBytes);
        }
        else
            await SendChunkedFromServer(imageBytes, isImage: true, fileName: null);
    }

    private async void HandleServerSendPDF(byte[] pdfBytes, string fileName)
    {
        RunOnMainThread(() => chatUI.AddBotPDFBubble(pdfBytes, fileName));

        if (_isTCPActive)
        {
            _ignoreNextTCPClientFile = true;
            await _tcpServer.SendFileDataAsync(fileName, "pdf", pdfBytes);
        }
        else
            await SendChunkedFromServer(pdfBytes, isImage: false, fileName: fileName);
    }

    // ── Handlers red UDP ─────────────────────────────────────────────

    private void HandleUDPHandshake()
    {
        _handshakeCompleted = true;
        Debug.Log("[ChatManager] UDP Handshake completed.");
        RunOnMainThread(() =>
        {
            chatUI.AddSystemBubble("Conectado al chat (UDP)");
            _serverUI?.AddSystemBubble("Cliente conectado (UDP)");
        });
    }

    private void HandleUDPClientReceived(string message)
    {
        if (_imageAssembler.ProcessMessage(message)) return;
        RunOnMainThread(() => chatUI.AddBotTextBubble(message));
    }

    private void OnUDPImageAssembled(byte[] imageBytes)
    {
        RunOnMainThread(() => chatUI.AddBotImageBubble(imageBytes));
    }

    private void OnUDPPDFAssembled(byte[] pdfBytes, string fileName)
    {
        RunOnMainThread(() => chatUI.AddBotPDFBubble(pdfBytes, fileName));
    }

    // ── Handlers red TCP ─────────────────────────────────────────────

    private void HandleTCPClientReceived(string message)
    {
        if (_ignoreNextTCPClientMessage) { _ignoreNextTCPClientMessage = false; return; }
        RunOnMainThread(() => chatUI.AddBotTextBubble(message));
    }

    private void HandleTCPClientFileReceived(FileTransferData file)
    {
        if (_ignoreNextTCPClientFile) { _ignoreNextTCPClientFile = false; return; }

        byte[] bytes = file.GetBytes();
        if (file.fileType == "image")
            RunOnMainThread(() => chatUI.AddBotImageBubble(bytes));
        else if (file.fileType == "pdf")
            RunOnMainThread(() => chatUI.AddBotPDFBubble(bytes, file.fileName));
    }

    private void HandleTCPServerReceived(string message)
    {
        RunOnMainThread(() => _serverUI?.AddClientTextBubble(message));
    }

    private void HandleTCPServerFileReceived(FileTransferData file)
    {
        byte[] bytes = file.GetBytes();
        if (file.fileType == "image")
            RunOnMainThread(() => _serverUI?.AddClientImageBubble(bytes));
        else if (file.fileType == "pdf")
            RunOnMainThread(() => _serverUI?.AddClientPDFBubble(bytes, file.fileName));
    }

    // ── Flujo bot UDP ────────────────────────────────────────────────

    private async Task SendBotMessage(string message)
    {
        await Task.Delay(delayBetweenMessagesMs);
        await udpServer.SendMessageAsync(message);
        RunOnMainThread(() => _serverUI?.AddServerTextBubble(message));
        Debug.Log($"[ChatManager] Bot sent: {message}");
    }

    private async Task ContinueBotFlow()
    {
        _waitingForUserReply = false;
        await SendBotMessage(botMessage2);
        SwitchToTCP();
    }

    // ── Helpers de fragmentación ──────────────────────────────────────

    private async Task SendChunked(byte[] bytes, bool isImage, string fileName)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        List<string> messages = isImage
            ? ImageChunker.BuildMessages(bytes, id)
            : ImageChunker.BuildPDFMessages(bytes, id, fileName);

        foreach (string msg in messages)
        {
            await udpClient.SendMessageAsync(msg);
            await Task.Delay(delayBetweenChunksMs);
        }
    }

    private async Task SendChunkedFromServer(byte[] bytes, bool isImage, string fileName)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        List<string> messages = isImage
            ? ImageChunker.BuildMessages(bytes, id)
            : ImageChunker.BuildPDFMessages(bytes, id, fileName);

        foreach (string msg in messages)
        {
            await udpServer.SendMessageAsync(msg);
            await Task.Delay(delayBetweenChunksMs);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task WaitForHandshake(int timeoutMs = 5000)
    {
        int elapsed = 0;
        while (!_handshakeCompleted && elapsed < timeoutMs)
        {
            await Task.Delay(100);
            elapsed += 100;
        }
        if (!_handshakeCompleted)
            Debug.LogWarning("[ChatManager] Timeout waiting handshake UDP.");
    }

    void OnDestroy()
    {
        if (_imageAssembler != null)
        {
            _imageAssembler.OnImageAssembled -= OnUDPImageAssembled;
            _imageAssembler.OnPDFAssembled -= OnUDPPDFAssembled;
        }

        if (udpClient != null)
        {
            udpClient.OnConnected -= HandleUDPHandshake;
            udpClient.OnMessageReceived -= HandleUDPClientReceived;
        }

        if (tcpClient != null)
        {
            tcpClient.OnMessageReceived -= HandleTCPClientReceived;
            tcpClient.OnFileReceived -= HandleTCPClientFileReceived;
        }

        if (_tcpServer != null)
        {
            _tcpServer.OnMessageReceived -= HandleTCPServerReceived;
            _tcpServer.OnFileReceived -= HandleTCPServerFileReceived;
            _tcpServer.OnConnected -= HandleTCPConnected;
        }

        if (chatUI != null)
        {
            chatUI.OnUserSendText -= HandleUserSendText;
            chatUI.OnUserSendImage -= HandleUserSendImage;
            chatUI.OnUserSendPDF -= HandleUserSendPDF;
        }

        if (_serverUI != null)
        {
            _serverUI.OnServerSendText -= HandleServerSendText;
            _serverUI.OnServerSendImage -= HandleServerSendImage;
            _serverUI.OnServerSendPDF -= HandleServerSendPDF;
        }
    }
}