using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Threading;

/// <summary>
/// Maneja toda la UI del chat:
/// - Instancia burbujas de texto e imagen
/// - Controla el ScrollView
/// - Abre el explorador de archivos del sistema (sin plugins externos)
/// </summary>
public class ChatUIManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Transform chatContent;       // El "Content" del ScrollView
    [SerializeField] private ScrollRect scrollRect;       // El ScrollRect del chat
    [SerializeField] private TMP_InputField messageInput; // Campo de texto del usuario
    [SerializeField] private Button sendTextButton;       // Botón enviar texto
    [SerializeField] private Button sendImageButton;      // Botón enviar imagen
    [SerializeField] private TMP_Text statusText;         // Texto de estado del sistema

    [Header("Prefab")]
    [SerializeField] private GameObject chatBubblePrefab; // Prefab de ChatBubble

    // Eventos que el ChatManager escucha
    public event System.Action<string> OnUserSendText;
    public event System.Action<byte[]> OnUserSendImage;

    // Buffer para pasar la imagen del thread de Windows al hilo principal de Unity
    private byte[] _pendingImageBytes = null;
    private readonly object _imageLock = new object();

    void Start()
    {
        sendTextButton.onClick.AddListener(HandleSendText);
        sendImageButton.onClick.AddListener(HandleSendImage);
    }

    void Update()
    {
        // Unity solo permite tocar la UI desde el hilo principal.
        // Aquí revisamos si el thread de Windows dejó una imagen pendiente.
        lock (_imageLock)
        {
            if (_pendingImageBytes != null)
            {
                byte[] imageBytes = _pendingImageBytes;
                _pendingImageBytes = null;

                // Mostrar burbuja y notificar al ChatManager
                AddUserImageBubble(imageBytes);
                OnUserSendImage?.Invoke(imageBytes);
            }
        }
    }

    // ── Métodos públicos para agregar burbujas ────────────────────────

    /// <summary>Agrega una burbuja de texto del bot (izquierda).</summary>
    public void AddBotTextBubble(string message)
    {
        SpawnBubble().SetText(message, ChatBubble.BubbleSide.Bot);
        ScrollToBottom();
    }

    /// <summary>Agrega una burbuja de texto del usuario (derecha).</summary>
    public void AddUserTextBubble(string message)
    {
        SpawnBubble().SetText(message, ChatBubble.BubbleSide.User);
        ScrollToBottom();
    }

    /// <summary>Agrega una burbuja de imagen del bot (izquierda).</summary>
    public void AddBotImageBubble(byte[] imageBytes)
    {
        SpawnBubble().SetImage(imageBytes, ChatBubble.BubbleSide.Bot);
        ScrollToBottom();
    }

    /// <summary>Agrega una burbuja de imagen del usuario (derecha).</summary>
    public void AddUserImageBubble(byte[] imageBytes)
    {
        SpawnBubble().SetImage(imageBytes, ChatBubble.BubbleSide.User);
        ScrollToBottom();
    }

    /// <summary>Actualiza el texto de estado visible en la UI.</summary>
    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
    public void AddSystemBubble(string message)
    {
        Debug.Log("[ChatUI] AddSystemBubble called: " + message);
        SpawnBubble().SetSystemMessage(message);
        ScrollToBottom();
    }

    // ── Handlers internos ────────────────────────────────────────────

    private void HandleSendText()
    {
        string text = messageInput.text.Trim();

        if (string.IsNullOrEmpty(text))
        {
            Debug.Log("[ChatUI] The field text is empty.");
            return;
        }

        AddUserTextBubble(text);
        messageInput.text = "";
        OnUserSendText?.Invoke(text);
    }

    private void HandleSendImage()
    {
        // Abrir el explorador de archivos en un thread separado
        // para no bloquear el hilo principal de Unity
        Thread fileThread = new Thread(() =>
        {
            // Necesario para que OpenFileDialog funcione correctamente en Windows
            System.Windows.Forms.Application.EnableVisualStyles();

            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "Selecciona una imagen";
                dialog.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.FileName;

                    if (File.Exists(path))
                    {
                        byte[] imageBytes = File.ReadAllBytes(path);

                        // Guardar en el buffer — el Update() del hilo principal lo procesará
                        lock (_imageLock)
                        {
                            _pendingImageBytes = imageBytes;
                        }
                    }
                }
            }
        });

        // STA es requerido por Windows Forms para mostrar diálogos
        fileThread.SetApartmentState(ApartmentState.STA);
        fileThread.Start();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private ChatBubble SpawnBubble()
    {
        GameObject bubbleGO = Instantiate(chatBubblePrefab, chatContent);
        return bubbleGO.GetComponent<ChatBubble>();
    }

    private void ScrollToBottom()
    {
        StartCoroutine(ScrollCoroutine());
    }

    private System.Collections.IEnumerator ScrollCoroutine()
    {
        yield return null; // Esperar un frame para que el layout se actualice
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}