using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Representa una burbuja individual en el chat.
/// Puede mostrar texto o una imagen, y se alinea a izquierda (bot) o derecha (usuario).
/// </summary>
public class ChatBubble : MonoBehaviour
{
    public enum BubbleSide { Bot, User }

    [Header("Referencias del Prefab")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private RawImage messageImage;
    [SerializeField] private GameObject textContainer;
    [SerializeField] private GameObject imageContainer;

    [Header("Apariencia")]
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private Color botColor = new Color(0.23f, 0.23f, 0.25f);
    [SerializeField] private Color userColor = new Color(0.18f, 0.47f, 0.91f);

    [Header("Tamaño de imagen")]
    [SerializeField] private float imageWidth = 220f; // Ancho fijo para imágenes en el chat

    /// <summary>Configura la burbuja con un mensaje de texto.</summary>
    public void SetText(string message, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);
        messageText.text = message;
        AlignBubble(side);
    }

    /// <summary>Configura la burbuja con una imagen recibida como bytes.</summary>
    public void SetImage(byte[] imageBytes, BubbleSide side)
    {
        textContainer.SetActive(false);
        imageContainer.SetActive(true);

        // 1. Convertir bytes a Texture2D
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes);
        messageImage.texture = texture;

        // 2. Calcular altura manteniendo aspect ratio
        float aspectRatio = (float)texture.width / texture.height;
        float imageHeight = imageWidth / aspectRatio;

        // 3. Asignar tamaño al RawImage
        RectTransform imgRect = messageImage.GetComponent<RectTransform>();
        imgRect.sizeDelta = new Vector2(imageWidth, imageHeight);

        // 4. Asignar tamaño al Panel raíz sumando el padding
        float padding = 20f;
        RectTransform bubbleRect = GetComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(imageWidth + padding, imageHeight + padding);

        // 4. Usar LayoutElement para que el Vertical Layout Group
        //    del Panel padre lea el tamaño correcto y se ajuste
        LayoutElement layoutElement = messageImage.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = messageImage.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = imageWidth;
        layoutElement.preferredHeight = imageHeight;
        layoutElement.minWidth = imageWidth;
        layoutElement.minHeight = imageHeight;

        AlignBubble(side);
    }

    /// <summary>Alinea la burbuja a izquierda (bot) o derecha (usuario).</summary>
    private void AlignBubble(BubbleSide side)
    {
        RectTransform rt = GetComponent<RectTransform>();

        if (side == BubbleSide.Bot)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f);

            if (bubbleBackground != null)
                bubbleBackground.color = botColor;
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-10f, 0f);

            if (bubbleBackground != null)
                bubbleBackground.color = userColor;
        }
    }
}