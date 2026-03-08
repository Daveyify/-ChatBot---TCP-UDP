using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Representa una burbuja individual en el chat.
/// Tres tipos: Bot (izquierda), User (derecha), System (centrado).
/// </summary>
public class ChatBubble : MonoBehaviour
{
    public enum BubbleSide { Bot, User, System }

    [Header("Prefab references")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private RawImage messageImage;
    [SerializeField] private GameObject textContainer;
    [SerializeField] private GameObject imageContainer;

    [Header("Style")]
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private Color botColor = new Color(0.23f, 0.23f, 0.25f);
    [SerializeField] private Color userColor = new Color(0.18f, 0.47f, 0.91f);
    [SerializeField] private Color systemColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

    [Header("Image size")]
    [SerializeField] private float imageWidth = 220f;
    [SerializeField] private float imagePadding = 20f;

    /// <summary>Configura la burbuja con un mensaje de texto.</summary>
    public void SetText(string message, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);
        messageText.text = message;
        AlignBubble(side);
    }

    public void SetSystemMessage(string message)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);

        messageText.text = $" {message} ";
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 14;

        AlignBubble(BubbleSide.System);
    }

    /// <summary>Configura la burbuja con una imagen recibida como bytes.</summary>
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

        LayoutElement layoutElement = messageImage.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = messageImage.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = finalWidth;
        layoutElement.preferredHeight = finalHeight;
        layoutElement.minWidth = finalWidth;
        layoutElement.minHeight = finalHeight;

        AlignBubble(side);
    }

    /// <summary>Alinea la burbuja según el tipo.</summary>
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
                // Centrado, sin fondo o con fondo sutil
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                if (bubbleBackground != null) bubbleBackground.color = systemColor;
                break;
        }
    }
}