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
///   5. Conexión cambia a TCP
///
/// Notifica a ambas UIs (cliente y servidor) de cada mensaje.
/// </summary>
public class ChatManager : MonoBehaviour
{
    [Header("Red UDP")]
    [SerializeField] private UDPServer udpServer;
    [SerializeField] private UDPClient udpClient;

    [Header("Configuración")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private int serverPort = 5555;
    [SerializeField] private int delayBetweenMessagesMs = 1000;
    [SerializeField] private int delayBetweenChunksMs = 10;

    [Header("Mensajes Automáticos del Bot")]
    [SerializeField] private string botMessage1 = "¡Hola! Bienvenido al chat. ¿En qué puedo ayudarte?";
    [SerializeField] private string botMessage2 = "Gracias por tu respuesta. Ahora te voy a conectar con un agente.";

    [Header("UI Cliente")]
    [SerializeField] private ChatUIManager chatUI;

    // UI del servidor — se registra automáticamente desde la escena del servidor
    private ServerUIManager _serverUI;

    // Estado del flujo
    private bool _handshakeCompleted = false;
    private bool _waitingForUserReply = false;

    // Evento para cambiar a TCP
    public event Action OnSwitchToTCP;

    // Ensamblador de imágenes
    private ImageAssembler _imageAssembler;

    // Main Thread Dispatcher
    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private readonly object _queueLock = new object();

    // ── Unity ────────────────────────────────────────────────────────

    void Start()
    {
        _imageAssembler = new ImageAssembler();
        _imageAssembler.OnImageAssembled += OnImageAssembled;

        udpClient.OnConnected += HandleHandshake;
        udpClient.OnMessageReceived += HandleClientReceived;

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
        lock (_queueLock)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    // ── Registro del ServerUIManager ─────────────────────────────────

    /// <summary>
    /// Llamado automáticamente por ServerUIManager cuando su escena carga.
    /// </summary>
    public void RegisterServerUI(ServerUIManager serverUI)
    {
        _serverUI = serverUI;

        // Suscribirse a los eventos de envío del servidor
        _serverUI.OnServerSendText += HandleServerSendText;
        _serverUI.OnServerSendImage += HandleServerSendImage;

        Debug.Log("[ChatManager] ServerUIManager registrado.");
    }

    // ── Punto de entrada ─────────────────────────────────────────────

    public async void StartChatBot()
    {
        _handshakeCompleted = false;
        _waitingForUserReply = false;

        SetStatus("Iniciando servidor...");
        await udpServer.StartServer(serverPort);

        SetStatus("Conectando cliente...");
        await udpClient.ConnectToServer(serverAddress, serverPort);

        SetStatus("Esperando handshake...");
        await WaitForHandshake();

        await SendBotMessage(botMessage1);

        _waitingForUserReply = true;
        SetStatus("Esperando respuesta del usuario...");
    }

    // ── Handlers de UI Cliente ───────────────────────────────────────

    private async void HandleUserSendText(string message)
    {
        // Mostrar en la UI del servidor como mensaje del cliente
        _serverUI?.AddClientTextBubble(message);

        await udpClient.SendMessageAsync(message);

        if (_waitingForUserReply)
            await ContinueBotFlow();
    }

    private async void HandleUserSendImage(byte[] imageBytes)
    {
        // Mostrar en la UI del servidor como imagen del cliente
        _serverUI?.AddClientImageBubble(imageBytes);

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

    // ── Handlers de UI Servidor ──────────────────────────────────────

    private async void HandleServerSendText(string message)
    {
        // El servidor envía un mensaje manual → mostrarlo en el cliente
        await udpServer.SendMessageAsync(message);
    }

    private async void HandleServerSendImage(byte[] imageBytes)
    {
        string transferId = Guid.NewGuid().ToString("N").Substring(0, 8);
        List<string> messages = ImageChunker.BuildMessages(imageBytes, transferId);

        foreach (string msg in messages)
        {
            await udpServer.SendMessageAsync(msg);
            await Task.Delay(delayBetweenChunksMs);
        }
    }

    // ── Handlers de red ──────────────────────────────────────────────

    private void HandleClientReceived(string message)
    {
        if (_imageAssembler.ProcessMessage(message))
            return;

        // Texto del bot → mostrar en UI del cliente
        RunOnMainThread(() => chatUI.AddBotTextBubble(message));
    }

    private void OnImageAssembled(byte[] imageBytes)
    {
        // Imagen del bot → mostrar en UI del cliente
        RunOnMainThread(() => chatUI.AddBotImageBubble(imageBytes));
    }

    private void HandleHandshake()
    {
        _handshakeCompleted = true;
        Debug.Log("[ChatManager] Handshake completado.");
    }

    // ── Flujo del bot ────────────────────────────────────────────────

    private async Task SendBotMessage(string message)
    {
        await Task.Delay(delayBetweenMessagesMs);
        await udpServer.SendMessageAsync(message);

        // Mostrar en la UI del servidor como mensaje propio
        RunOnMainThread(() => _serverUI?.AddServerTextBubble(message));

        Debug.Log($"[ChatManager] Bot envió: {message}");
    }

    private async Task ContinueBotFlow()
    {
        _waitingForUserReply = false;
        SetStatus("Bot respondiendo...");

        await SendBotMessage(botMessage2);

        SetStatus("Conectando con agente (TCP)...");
        Debug.Log("[ChatManager] Flujo UDP completo. Cambiando a TCP...");
        OnSwitchToTCP?.Invoke();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void SetStatus(string message)
    {
        chatUI.SetStatus(message);
        _serverUI?.SetStatus(message);
    }

    private async Task WaitForHandshake(int timeoutMs = 5000)
    {
        int elapsed = 0;
        while (!_handshakeCompleted && elapsed < timeoutMs)
        {
            await Task.Delay(100);
            elapsed += 100;
        }

        if (!_handshakeCompleted)
            Debug.LogWarning("[ChatManager] Timeout esperando handshake.");
    }

    void OnDestroy()
    {
        if (_imageAssembler != null)
            _imageAssembler.OnImageAssembled -= OnImageAssembled;

        if (udpClient != null)
        {
            udpClient.OnConnected -= HandleHandshake;
            udpClient.OnMessageReceived -= HandleClientReceived;
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