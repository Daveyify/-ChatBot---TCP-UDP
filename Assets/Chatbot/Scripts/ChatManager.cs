using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Orquesta el flujo completo del ChatBot:
///
/// [UDP]
///   1. Servidor inicia + Cliente conecta
///   2. Bot envía Mensaje automático 1
///   3. Usuario responde (texto o imagen)
///   4. Bot envía Mensaje automático 2
///   5. Conexión cambia a TCP
///
/// [TCP] — se activa después del flujo UDP
///   6. Chat libre entre usuario y agente
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

    [Header("Mensajes Automáticos del Bot")]
    [SerializeField] private string botMessage1 = "¡Hola! Bienvenido al chat. ¿En qué puedo ayudarte?";
    [SerializeField] private string botMessage2 = "Gracias por tu respuesta. Ahora te voy a conectar con un agente.";

    [Header("UI")]
    [SerializeField] private ChatUIManager chatUI;

    // Prefijo que diferencia un mensaje de imagen de uno de texto en el protocolo UDP/TCP
    private const string IMAGE_PREFIX = "IMG:";

    // Estado del flujo
    private bool _handshakeCompleted = false;
    private bool _waitingForUserReply = false;  // true cuando el bot espera respuesta del usuario
    private int _botMessagesCount = 0;       // cuántos mensajes automáticos ya envió el bot

    // Evento para cuando el flujo UDP termina y hay que cambiar a TCP
    public event Action OnSwitchToTCP;

    void Start()
    {
        // Suscribirse a eventos de red
        udpClient.OnConnected += HandleHandshake;
        udpClient.OnMessageReceived += HandleClientReceived;   // El cliente recibe mensajes del servidor

        // Suscribirse a eventos de UI
        chatUI.OnUserSendText += HandleUserSendText;
        chatUI.OnUserSendImage += HandleUserSendImage;
    }

    // ── Punto de entrada (botón en UI) ───────────────────────────────

    /// <summary>
    /// Llamado por el botón "Iniciar Chat".
    /// Inicia el servidor UDP y conecta el cliente.
    /// </summary>
    public async void StartChatBot()
    {
        _handshakeCompleted = false;
        _waitingForUserReply = false;
        _botMessagesCount = 0;

        chatUI.SetStatus("Iniciando servidor...");
        await udpServer.StartServer(serverPort);

        chatUI.SetStatus("Conectando cliente...");
        await udpClient.ConnectToServer(serverAddress, serverPort);

        chatUI.SetStatus("Esperando handshake...");
        await WaitForHandshake();

        // Flujo iniciado: enviar primer mensaje del bot
        await SendBotMessage(botMessage1);

        // Ahora esperamos que el usuario responda (HandleUserSendText/Image lo continuará)
        _waitingForUserReply = true;
        chatUI.SetStatus("Esperando respuesta del usuario...");
    }

    // ── Handlers de eventos de UI ────────────────────────────────────

    /// <summary>El usuario envió un mensaje de texto desde la UI.</summary>
    private async void HandleUserSendText(string message)
    {
        // Enviar por red (el servidor lo recibe)
        await udpClient.SendMessageAsync(message);

        // Si el bot estaba esperando esta respuesta, continuar el flujo
        if (_waitingForUserReply)
            await ContinueBotFlow();
    }

    /// <summary>El usuario envió una imagen desde la UI.</summary>
    private async void HandleUserSendImage(byte[] imageBytes)
    {
        // Serializar los bytes a Base64 con prefijo para distinguirlo de texto
        string imageMessage = IMAGE_PREFIX + Convert.ToBase64String(imageBytes);
        await udpClient.SendMessageAsync(imageMessage);

        if (_waitingForUserReply)
            await ContinueBotFlow();
    }

    // ── Handlers de eventos de red ───────────────────────────────────

    /// <summary>El cliente recibió un mensaje del servidor (bot). Mostrarlo en UI.</summary>
    private void HandleClientReceived(string message)
    {
        if (message.StartsWith(IMAGE_PREFIX))
        {
            // Es una imagen: decodificar Base64 y mostrar burbuja
            byte[] imageBytes = Convert.FromBase64String(message.Substring(IMAGE_PREFIX.Length));
            chatUI.AddBotImageBubble(imageBytes);
        }
        else
        {
            // Es texto: mostrar burbuja de texto
            chatUI.AddBotTextBubble(message);
        }
    }

    // ── Flujo del bot ────────────────────────────────────────────────

    /// <summary>
    /// Envía un mensaje automático del bot:
    /// - Lo manda por red desde el servidor
    /// - Lo muestra en la UI como burbuja del bot
    /// </summary>
    private async Task SendBotMessage(string message)
    {
        await Task.Delay(delayBetweenMessagesMs);
        await udpServer.SendMessageAsync(message);
        _botMessagesCount++;
        Debug.Log($"[ChatManager] Bot envió mensaje {_botMessagesCount}: {message}");
    }

    /// <summary>
    /// Continúa el flujo después de que el usuario responde.
    /// Envía el segundo mensaje automático y luego cambia a TCP.
    /// </summary>
    private async Task ContinueBotFlow()
    {
        _waitingForUserReply = false;
        chatUI.SetStatus("Bot respondiendo...");

        // Enviar segundo mensaje automático
        await SendBotMessage(botMessage2);

        // Flujo UDP completado → cambiar a TCP
        chatUI.SetStatus("Conectando con agente (TCP)...");
        Debug.Log("[ChatManager] Flujo UDP completo. Cambiando a TCP...");
        OnSwitchToTCP?.Invoke();
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
            Debug.LogWarning("[ChatManager] Timeout esperando handshake.");
    }

    private void HandleHandshake()
    {
        _handshakeCompleted = true;
        Debug.Log("[ChatManager] Handshake completado.");
    }

    void OnDestroy()
    {
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
    }
}