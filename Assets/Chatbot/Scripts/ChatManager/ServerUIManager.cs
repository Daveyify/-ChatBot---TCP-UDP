using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Threading;

/// <summary>
/// Maneja la UI del lado del servidor y registra el TCPServer en el ChatManager.
/// Vive en la escena del servidor (cargada aditivamente).
/// </summary>
public class ServerUIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform chatContent;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button sendTextButton;
    [SerializeField] private Button sendImageButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Prefab")]
    [SerializeField] private GameObject chatBubblePrefab;

    [Header("TCP Red")]
    [SerializeField] private TCPServer tcpServer; // El TCPServer de esta escena

    // Eventos que el ChatManager escucha
    public event System.Action<string> OnServerSendText;
    public event System.Action<byte[]> OnServerSendImage;

    // Buffer para imágenes del file picker
    private byte[] _pendingImageBytes = null;
    private readonly object _imageLock = new object();

    void Start()
    {
        sendTextButton.onClick.AddListener(HandleSendText);
        sendImageButton.onClick.AddListener(HandleSendImage);

        // Registrarse en el ChatManager (vive en DontDestroyOnLoad)
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            chatManager.RegisterServerUI(this, tcpServer);
            Debug.Log("[ServerUI] Registered in ChatManager.");
        }
        else
        {
            Debug.LogWarning("[ServerUI] ChatManager not found.");
        }
    }

    void Update()
    {
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

    // ── Burbujas ─────────────────────────────────────────────────────

    /// <summary>Mensaje del cliente → izquierda.</summary>
    public void AddClientTextBubble(string message)
    {
        SpawnBubble().SetText(message, ChatBubble.BubbleSide.Bot);
        ScrollToBottom();
    }

    /// <summary>Imagen del cliente → izquierda.</summary>
    public void AddClientImageBubble(byte[] imageBytes)
    {
        SpawnBubble().SetImage(imageBytes, ChatBubble.BubbleSide.Bot);
        ScrollToBottom();
    }

    /// <summary>Mensaje del servidor/agente → derecha.</summary>
    public void AddServerTextBubble(string message)
    {
        SpawnBubble().SetText(message, ChatBubble.BubbleSide.User);
        ScrollToBottom();
    }

    /// <summary>Imagen del servidor/agente → derecha.</summary>
    public void AddServerImageBubble(byte[] imageBytes)
    {
        SpawnBubble().SetImage(imageBytes, ChatBubble.BubbleSide.User);
        ScrollToBottom();
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    public void AddSystemBubble(string message)
    {
        SpawnBubble().SetSystemMessage(message);
        ScrollToBottom();
    }

    // ── Handlers internos ────────────────────────────────────────────

    private void HandleSendText()
    {
        string text = messageInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

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
                dialog.Title = "Selecciona una imagen";
                dialog.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.FileName;
                    if (File.Exists(path))
                    {
                        lock (_imageLock)
                        {
                            _pendingImageBytes = File.ReadAllBytes(path);
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