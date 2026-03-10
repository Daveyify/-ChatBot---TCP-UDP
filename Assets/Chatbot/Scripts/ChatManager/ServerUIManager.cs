using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Threading;

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

    [Header("Red TCP")]
    [SerializeField] private TCPServer tcpServer;

    public event System.Action<string> OnServerSendText;
    public event System.Action<byte[]> OnServerSendImage;
    public event System.Action<byte[], string> OnServerSendPDF;

    // Buffer file picker
    private byte[] _pendingImageBytes = null;
    private byte[] _pendingPDFBytes = null;
    private string _pendingPDFName = null;
    private readonly object _fileLock = new object();

    void Start()
    {
        sendTextButton.onClick.AddListener(HandleSendText);
        sendImageButton.onClick.AddListener(HandleSendFile);

        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
            chatManager.RegisterServerUI(this, tcpServer);
        else
            Debug.LogWarning("[ServerUI] ChatManager not found.");
    }

    void Update()
    {
        lock (_fileLock)
        {
            if (_pendingImageBytes != null)
            {
                byte[] imageBytes = _pendingImageBytes;
                _pendingImageBytes = null;
                AddServerImageBubble(imageBytes);
                OnServerSendImage?.Invoke(imageBytes);
            }

            if (_pendingPDFBytes != null)
            {
                byte[] pdfBytes = _pendingPDFBytes;
                string pdfName = _pendingPDFName;
                _pendingPDFBytes = null;
                _pendingPDFName = null;
                AddServerPDFBubble(pdfBytes, pdfName);
                OnServerSendPDF?.Invoke(pdfBytes, pdfName);
            }
        }
    }

    public void AddClientTextBubble(string message) => SpawnAndScroll(b => b.SetText(message, ChatBubble.BubbleSide.Bot));
    public void AddClientImageBubble(byte[] bytes) => SpawnAndScroll(b => b.SetImage(bytes, ChatBubble.BubbleSide.Bot));
    public void AddClientPDFBubble(byte[] b, string n) => SpawnAndScroll(bub => bub.SetPDF(b, n, ChatBubble.BubbleSide.Bot));
    public void AddServerTextBubble(string message) => SpawnAndScroll(b => b.SetText(message, ChatBubble.BubbleSide.User));
    public void AddServerImageBubble(byte[] bytes) => SpawnAndScroll(b => b.SetImage(bytes, ChatBubble.BubbleSide.User));
    public void AddServerPDFBubble(byte[] b, string n) => SpawnAndScroll(bub => bub.SetPDF(b, n, ChatBubble.BubbleSide.User));
    public void AddSystemBubble(string message) => SpawnAndScroll(b => b.SetSystemMessage(message));

    public void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void HandleSendText()
    {
        string text = messageInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        AddServerTextBubble(text);
        messageInput.text = "";
        OnServerSendText?.Invoke(text);
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

    private void SpawnAndScroll(System.Action<ChatBubble> configure)
    {
        ChatBubble bubble = Instantiate(chatBubblePrefab, chatContent)
                                .GetComponent<ChatBubble>();
        configure(bubble);
        ScrollToBottom();
    }

    private void ScrollToBottom() => StartCoroutine(ScrollCoroutine());

    private System.Collections.IEnumerator ScrollCoroutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}