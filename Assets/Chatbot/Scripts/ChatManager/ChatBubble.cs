using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

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
    [SerializeField] private int cornerRadius = 20;

    [Header("Size image")]
    [SerializeField] private float imageWidth = 220f;
    [SerializeField] private float imagePadding = 20f;

    private string _pdfPath = null;

    private void Awake()
    {
        if (bubbleBackground != null)
        {
            bubbleBackground.sprite = CreateRoundedSprite(128, 128, cornerRadius);
            bubbleBackground.type = Image.Type.Sliced;
            bubbleBackground.pixelsPerUnitMultiplier = 1f;
        }
    }

    public void SetText(string message, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);
        messageText.text = message;
        messageText.alignment = TextAlignmentOptions.Left;
        AlignBubble(side);
    }

    public void SetSystemMessage(string message)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);
        messageText.text = $"{message}";
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 14;
        AlignBubble(BubbleSide.System);
    }

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

    public void SetPDF(byte[] pdfBytes, string fileName, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);

        _pdfPath = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(_pdfPath, pdfBytes);

        messageText.text = $"📄 {fileName}\n<size=12><color=#aaaaaa>Tap to open</color></size>";
        messageText.alignment = TextAlignmentOptions.Left;

        AlignBubble(side);
        Debug.Log($"[ChatBubble] PDF saved in: {_pdfPath}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_pdfPath != null && File.Exists(_pdfPath))
        {
            System.Diagnostics.Process.Start(_pdfPath);
            Debug.Log("[ChatBubble] Opening PDF: " + _pdfPath);
        }
    }
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

    private Sprite CreateRoundedSprite(int width, int height, int radius)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = IsInsideRounded(x, y, width, height, radius)
                    ? Color.white
                    : Color.clear;

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius)
        );
    }

    private bool IsInsideRounded(int x, int y, int w, int h, int r)
    {
        if (x < r && y < r) return Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) <= r;
        if (x > w - r - 1 && y < r) return Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, r)) <= r;
        if (x < r && y > h - r - 1) return Vector2.Distance(new Vector2(x, y), new Vector2(r, h - r - 1)) <= r;
        if (x > w - r - 1 && y > h - r - 1) return Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, h - r - 1)) <= r;
        return true;
    }
}