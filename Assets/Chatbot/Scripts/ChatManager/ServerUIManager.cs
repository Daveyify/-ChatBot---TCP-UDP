using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Threading;

/// <summary>
/// Maneja la UI del lado del servidor:
/// - Muestra los mensajes que llegan del cliente (izquierda)
/// - Muestra los mensajes que envía el servidor (derecha)
/// - Se registra automáticamente en el ChatManager al cargar la escena
/// </summary>
public class ServerUIManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Transform chatContent;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button sendTextButton;
    [SerializeField] private Button sendImageButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Prefab")]
    [SerializeField] private GameObject chatBubblePrefab;

    // Eventos que el ChatManager escucha para enviar por red
    public event System.Action<string> OnServerSendText;
    public event System.Action<byte[]> OnServerSendImage;

    // Buffer para imágenes desde el thread de Windows
    private byte[] _pendingImageBytes = null;
    private readonly object _imageLock = new object();

    void Start()
    {
        sendTextButton.onClick.AddListener(HandleSendText);
        sendImageButton.onClick.AddListener(HandleSendImage);

        // Registrarse automáticamente en el ChatManager
        // El ChatManager vive en DontDestroyOnLoad así que siempre existe
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            chatManager.RegisterServerUI(this);
            Debug.Log("[ServerUI] Registrado en ChatManager.");
        }
        else
        {
            Debug.LogWarning("[ServerUI] No se encontró el ChatManager en la escena.");
        }
    }

    void Update()
    {
        // Procesar imágenes pendientes del file picker en el hilo principal
        lock (_imageLock)
        {
            if (_pendingImageBytes != null)
            {
                byte[] imageBytes = _pendingImageBytes;
                _pendingImageBytes = null;

                AddServerImageBubble(imageBytes);
                OnServerSendImage?.Invoke(imageBytes);
            }
        }
    }

    // ── Métodos públicos para agregar burbujas ────────────────────────

    /// <summary>Mensaje recibido del cliente → burbuja izquierda.</summary>
    public void AddClientTextBubble(string message)
    {
        SpawnBubble().SetText(message, ChatBubble.BubbleSide.Bot); // izquierda
        ScrollToBottom();
    }

    /// <summary>Imagen recibida del cliente → burbuja izquierda.</summary>
    public void AddClientImageBubble(byte[] imageBytes)
    {
        SpawnBubble().SetImage(imageBytes, ChatBubble.BubbleSide.Bot); // izquierda
        ScrollToBottom();
    }

    /// <summary>Mensaje enviado por el servidor → burbuja derecha.</summary>
    public void AddServerTextBubble(string message)
    {
        SpawnBubble().SetText(message, ChatBubble.BubbleSide.User); // derecha
        ScrollToBottom();
    }

    /// <summary>Imagen enviada por el servidor → burbuja derecha.</summary>
    public void AddServerImageBubble(byte[] imageBytes)
    {
        SpawnBubble().SetImage(imageBytes, ChatBubble.BubbleSide.User); // derecha
        ScrollToBottom();
    }

    /// <summary>Actualiza el texto de estado.</summary>
    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    // ── Handlers internos ────────────────────────────────────────────

    private void HandleSendText()
    {
        string text = messageInput.text.Trim();

        if (string.IsNullOrEmpty(text))
        {
            Debug.Log("[ServerUI] El campo de texto está vacío.");
            return;
        }

        AddServerTextBubble(text);
        messageInput.text = "";
        OnServerSendText?.Invoke(text);
    }

    private void HandleSendImage()
    {
        Thread fileThread = new Thread(() =>
        {
            System.Windows.Forms.Application.EnableVisualStyles();

            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title  = "Selecciona una imagen";
                dialog.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.FileName;
                    if (File.Exists(path))
                    {
                        byte[] imageBytes = File.ReadAllBytes(path);
                        lock (_imageLock)
                        {
                            _pendingImageBytes = imageBytes;
                        }
                    }
                }
            }
        });

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
        yield return null;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
