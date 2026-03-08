using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Threading;

/// <summary>
/// Maneja la UI del chat del cliente:
/// - Instancia burbujas de texto, imagen y PDF
/// - Controla el ScrollView
/// - Abre explorador de archivos (imágenes y PDFs)
/// </summary>
public class ChatUIManager : MonoBehaviour
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

    // Eventos que el ChatManager escucha
    public event System.Action<string> OnUserSendText;
    public event System.Action<byte[]> OnUserSendImage;
    public event System.Action<byte[], string> OnUserSendPDF; // bytes + fileName

    // Buffer para archivos del file picker
    private byte[] _pendingImageBytes = null;
    private byte[] _pendingPDFBytes = null;
    private string _pendingPDFName = null;
    private readonly object _fileLock = new object();

    void Start()
    {
        sendTextButton.onClick.AddListener(HandleSendText);
        sendImageButton.onClick.AddListener(HandleSendFile); // botón unificado para imagen y PDF
    }

    void Update()
    {
        lock (_fileLock)
        {
            if (_pendingImageBytes != null)
            {
                byte[] imageBytes = _pendingImageBytes;
                _pendingImageBytes = null;
                AddUserImageBubble(imageBytes);
                OnUserSendImage?.Invoke(imageBytes);
            }

            if (_pendingPDFBytes != null)
            {
                byte[] pdfBytes = _pendingPDFBytes;
                string pdfName = _pendingPDFName;
                _pendingPDFBytes = null;
                _pendingPDFName = null;
                AddUserPDFBubble(pdfBytes, pdfName);
                OnUserSendPDF?.Invoke(pdfBytes, pdfName);
            }
        }
    }

    // ── Burbujas públicas ─────────────────────────────────────────────

    public void AddBotTextBubble(string message) => SpawnAndScroll(b => b.SetText(message, ChatBubble.BubbleSide.Bot));
    public void AddUserTextBubble(string message) => SpawnAndScroll(b => b.SetText(message, ChatBubble.BubbleSide.User));
    public void AddBotImageBubble(byte[] bytes) => SpawnAndScroll(b => b.SetImage(bytes, ChatBubble.BubbleSide.Bot));
    public void AddUserImageBubble(byte[] bytes) => SpawnAndScroll(b => b.SetImage(bytes, ChatBubble.BubbleSide.User));
    public void AddBotPDFBubble(byte[] b, string n) => SpawnAndScroll(bub => bub.SetPDF(b, n, ChatBubble.BubbleSide.Bot));
    public void AddUserPDFBubble(byte[] b, string n) => SpawnAndScroll(bub => bub.SetPDF(b, n, ChatBubble.BubbleSide.User));
    public void AddSystemBubble(string message) => SpawnAndScroll(b => b.SetSystemMessage(message));

    public void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    // ── Handlers internos ────────────────────────────────────────────

    private void HandleSendText()
    {
        string text = messageInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        AddUserTextBubble(text);
        messageInput.text = "";
        OnUserSendText?.Invoke(text);
    }

    private void HandleSendFile()
    {
        Thread fileThread = new Thread(() =>
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "Selecciona una imagen o PDF";
                dialog.Filter = "Archivos|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.pdf";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.FileName;
                    if (!File.Exists(path)) return;

                    byte[] bytes = File.ReadAllBytes(path);
                    string ext = Path.GetExtension(path).ToLower();

                    lock (_fileLock)
                    {
                        if (ext == ".pdf")
                        {
                            _pendingPDFBytes = bytes;
                            _pendingPDFName = Path.GetFileName(path);
                        }
                        else
                        {
                            _pendingImageBytes = bytes;
                        }
                    }
                }
            }
        });

        fileThread.SetApartmentState(ApartmentState.STA);
        fileThread.Start();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void SpawnAndScroll(System.Action<ChatBubble> configure)
    {
        ChatBubble bubble = Instantiate(chatBubblePrefab, chatContent)
                                .GetComponent<ChatBubble>();
        configure(bubble);
        ScrollToBottom();
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