using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

/// <summary>
/// Representa una burbuja individual en el chat.
/// Tipos: Bot (izquierda), User (derecha), System (centrado).
/// Soporta texto, imagen y PDF (abre al hacer clic).
/// </summary>
public class ChatBubble : MonoBehaviour, IPointerClickHandler
{
    public enum BubbleSide { Bot, User, System }

    [Header("Prefab")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private RawImage messageImage;
    [SerializeField] private GameObject textContainer;
    [SerializeField] private GameObject imageContainer;

    [Header("Style")]
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private Color botColor = new Color(0.23f, 0.23f, 0.25f);
    [SerializeField] private Color userColor = new Color(0.18f, 0.47f, 0.91f);
    [SerializeField] private Color systemColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

    [Header("Size image")]
    [SerializeField] private float imageWidth = 220f;
    [SerializeField] private float imagePadding = 20f;

    // Ruta del PDF guardado — se usa al hacer clic para abrirlo
    private string _pdfPath = null;

    // ── Métodos públicos ─────────────────────────────────────────────

    /// <summary>Configura la burbuja con un mensaje de texto.</summary>
    public void SetText(string message, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);
        messageText.text = message;
        messageText.alignment = TextAlignmentOptions.Left;
        AlignBubble(side);
    }

    /// <summary>Configura la burbuja como mensaje de sistema centrado.</summary>
    public void SetSystemMessage(string message)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);
        messageText.text = $"{message}";
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 14;
        AlignBubble(BubbleSide.System);
    }

    /// <summary>Configura la burbuja con una imagen.</summary>
    public void SetImage(byte[] imageBytes, BubbleSide side)
    {
        textContainer.SetActive(false);
        imageContainer.SetActive(true);

        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes);
        messageImage.texture = texture;

        float finalWidth = imageWidth - imagePadding;
        float aspectRatio = (float)texture.width / texture.height;
        float finalHeight = finalWidth / aspectRatio;

        RectTransform imgRect = messageImage.GetComponent<RectTransform>();
        imgRect.sizeDelta = new Vector2(finalWidth, finalHeight);

        LayoutElement layout = messageImage.GetComponent<LayoutElement>()
                            ?? messageImage.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = finalWidth;
        layout.preferredHeight = finalHeight;
        layout.minWidth = finalWidth;
        layout.minHeight = finalHeight;

        AlignBubble(side);
    }

    /// <summary>
    /// Configura la burbuja como PDF.
    /// Guarda el archivo en persistentDataPath y lo abre al hacer clic.
    /// </summary>
    public void SetPDF(byte[] pdfBytes, string fileName, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);

        // Guardar PDF en disco para poder abrirlo
        _pdfPath = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(_pdfPath, pdfBytes);

        // Mostrar texto clickeable con ícono
        messageText.text = $"📄 {fileName}\n<size=12><color=#aaaaaa>Toca para abrir</color></size>";
        messageText.alignment = TextAlignmentOptions.Left;

        AlignBubble(side);
        Debug.Log($"[ChatBubble] PDF saved in: {_pdfPath}");
    }

    /// <summary>Al hacer clic en la burbuja de PDF, abre el archivo.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_pdfPath != null && File.Exists(_pdfPath))
        {
            System.Diagnostics.Process.Start(_pdfPath);
            Debug.Log("[ChatBubble] Opening PDF: " + _pdfPath);
        }
    }

    // ── Alineación ───────────────────────────────────────────────────

    private void AlignBubble(BubbleSide side)
    {
        RectTransform rt = GetComponent<RectTransform>();

        switch (side)
        {
            case BubbleSide.Bot:
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(10f, 0f);
                if (bubbleBackground != null) bubbleBackground.color = botColor;
                break;

            case BubbleSide.User:
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-10f, 0f);
                if (bubbleBackground != null) bubbleBackground.color = userColor;
                break;

            case BubbleSide.System:
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                if (bubbleBackground != null) bubbleBackground.color = systemColor;
                break;
        }
    }
}