using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Orquesta el flujo completo del ChatBot:
///
/// [UDP]
///   1. Servidor inicia + Cliente conecta
///   2. Bot envía Mensaje automático 1
///   3. Usuario responde (texto o imagen fragmentada)
///   4. Bot envía Mensaje automático 2
///   5. Transición a TCP
///
/// [TCP]
///   6. TCPServer y TCPClient se conectan
///   7. UDP se desconecta
///   8. Chat libre entre usuario y agente
/// </summary>
public class ChatManager : MonoBehaviour
{
    [Header("Red UDP")]
    [SerializeField] private UDPServer udpServer;
    [SerializeField] private UDPClient udpClient;

    [Header("Red TCP Client")]
    [SerializeField] private TCPClient tcpClient;

    [Header("UDP Settings")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private int udpPort = 5555;
    [SerializeField] private int delayBetweenMessagesMs = 1000;
    [SerializeField] private int delayBetweenChunksMs = 10;

    [Header("TCP Setting")]
    [SerializeField] private int tcpPort = 5556;

    [Header("Bot automatic messages")]
    [SerializeField] private string botMessage1 = "¡Hola! Bienvenido al chat. ¿En qué puedo ayudarte?";
    [SerializeField] private string botMessage2 = "Gracias por tu respuesta. Ahora te voy a conectar con un agente.";

    [Header("UI Client")]
    [SerializeField] private ChatUIManager chatUI;

    // Registrados automáticamente desde la escena del servidor
    private ServerUIManager _serverUI;
    private TCPServer _tcpServer;

    // Estado del flujo
    private bool _handshakeCompleted = false;
    private bool _waitingForUserReply = false;
    private bool _isTCPActive = false;

    // Evita que el eco TCP muestre mensajes dobles.
    // Cuando el servidor envía un mensaje al cliente, el TCPClient lo recibe
    // de vuelta — esta bandera lo ignora porque ya se mostró en la UI.
    private bool _ignorNextTCPClientMessage = false;
    private bool _ignoreNextTCPClientFile = false;

    // Ensamblador de imágenes UDP
    private ImageAssembler _imageAssembler;

    // Main Thread Dispatcher
    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private readonly object _queueLock = new object();

    // ── Unity ────────────────────────────────────────────────────────

    void Start()
    {
        _imageAssembler = new ImageAssembler();
        _imageAssembler.OnImageAssembled += OnUDPImageAssembled;

        udpClient.OnConnected += HandleUDPHandshake;
        udpClient.OnMessageReceived += HandleUDPClientReceived;

        tcpClient.OnMessageReceived += HandleTCPClientReceived;
        tcpClient.OnFileReceived += HandleTCPClientFileReceived;

        chatUI.OnUserSendText += HandleUserSendText;
        chatUI.OnUserSendImage += HandleUserSendImage;
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

    // ── Registro desde escena del servidor ───────────────────────────

    public void RegisterServerUI(ServerUIManager serverUI, TCPServer tcpServer)
    {
        _serverUI = serverUI;
        _tcpServer = tcpServer;

        _serverUI.OnServerSendText += HandleServerSendText;
        _serverUI.OnServerSendImage += HandleServerSendImage;

        _tcpServer.OnMessageReceived += HandleTCPServerReceived;
        _tcpServer.OnFileReceived += HandleTCPServerFileReceived;
        _tcpServer.OnConnected += HandleTCPConnected;

        Debug.Log("[ChatManager] ServerUIManager and TCPServer registered.");
    }

    // ── Punto de entrada UDP ─────────────────────────────────────────

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
        // Mostrar en servidor como mensaje del cliente
        RunOnMainThread(() => _serverUI?.AddClientTextBubble(message));

        if (_isTCPActive)
            await tcpClient.SendMessageAsync(message);
        else
        {
            await udpClient.SendMessageAsync(message);
            if (_waitingForUserReply)
                await ContinueBotFlow();
        }
    }

    private async void HandleUserSendImage(byte[] imageBytes)
    {
        RunOnMainThread(() => _serverUI?.AddClientImageBubble(imageBytes));

        if (_isTCPActive)
        {
            await tcpClient.SendFileDataAsync("image.png", "image", imageBytes);
        }
        else
        {
            string transferId = Guid.NewGuid().ToString("N").Substring(0, 8);
            List<string> messages = ImageChunker.BuildMessages(imageBytes, transferId);
            foreach (string msg in messages)
            {
                await udpClient.SendMessageAsync(msg);
                await Task.Delay(delayBetweenChunksMs);
            }
            if (_waitingForUserReply)
                await ContinueBotFlow();
        }
    }

    // ── Handlers UI Servidor ─────────────────────────────────────────

    private async void HandleServerSendText(string message)
    {
        // Mostrar en UI del cliente directamente — NO esperar el eco TCP
        RunOnMainThread(() => chatUI.AddBotTextBubble(message));

        if (_isTCPActive)
        {
            // Ignorar el próximo mensaje que llegue al TCPClient
            // porque ya lo mostramos arriba
            _ignorNextTCPClientMessage = true;
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
        {
            string transferId = Guid.NewGuid().ToString("N").Substring(0, 8);
            List<string> messages = ImageChunker.BuildMessages(imageBytes, transferId);
            foreach (string msg in messages)
            {
                await udpServer.SendMessageAsync(msg);
                await Task.Delay(delayBetweenChunksMs);
            }
        }
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

    // ── Handlers red TCP ─────────────────────────────────────────────

    private void HandleTCPClientReceived(string message)
    {
        // Ignorar el eco si el mensaje ya fue mostrado por HandleServerSendText
        if (_ignorNextTCPClientMessage)
        {
            _ignorNextTCPClientMessage = false;
            return;
        }
        RunOnMainThread(() => chatUI.AddBotTextBubble(message));
    }

    private void HandleTCPClientFileReceived(FileTransferData file)
    {
        if (_ignoreNextTCPClientFile)
        {
            _ignoreNextTCPClientFile = false;
            return;
        }
        if (file.fileType == "image")
        {
            byte[] imageBytes = file.GetBytes();
            RunOnMainThread(() => chatUI.AddBotImageBubble(imageBytes));
        }
    }

    private void HandleTCPServerReceived(string message)
    {
        RunOnMainThread(() => _serverUI?.AddClientTextBubble(message));
    }

    private void HandleTCPServerFileReceived(FileTransferData file)
    {
        if (file.fileType == "image")
        {
            byte[] imageBytes = file.GetBytes();
            RunOnMainThread(() => _serverUI?.AddClientImageBubble(imageBytes));
        }
    }

    // ── Flujo del bot UDP ────────────────────────────────────────────

    private async Task SendBotMessage(string message)
    {
        await Task.Delay(delayBetweenMessagesMs);
        await udpServer.SendMessageAsync(message);
        RunOnMainThread(() => _serverUI?.AddServerTextBubble(message));
        Debug.Log($"[ChatManager] Bot send: {message}");
    }

    private async Task ContinueBotFlow()
    {
        _waitingForUserReply = false;
        await SendBotMessage(botMessage2);
        SwitchToTCP();
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
            Debug.LogWarning("[ChatManager] Timeout waiting for handshake UDP.");
    }

    void OnDestroy()
    {
        if (_imageAssembler != null)
            _imageAssembler.OnImageAssembled -= OnUDPImageAssembled;

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
        }

        if (_serverUI != null)
        {
            _serverUI.OnServerSendText -= HandleServerSendText;
            _serverUI.OnServerSendImage -= HandleServerSendImage;
        }
    }
}