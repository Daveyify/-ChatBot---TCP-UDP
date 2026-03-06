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
    [SerializeField] private TMP_Text messageText;       // Texto del mensaje
    [SerializeField] private RawImage messageImage;      // Imagen del mensaje
    [SerializeField] private GameObject textContainer;   // Contenedor del texto (se oculta si es imagen)
    [SerializeField] private GameObject imageContainer;  // Contenedor de la imagen (se oculta si es texto)
    [SerializeField] private RectTransform bubbleRect;   // RectTransform de la burbuja para alinear

    // Colores de las burbujas
    [Header("Colores")]
    [SerializeField] private Color botColor = new Color(0.23f, 0.23f, 0.25f); // Gris oscuro
    [SerializeField] private Color userColor = new Color(0.18f, 0.47f, 0.91f); // Azul
    [SerializeField] private Image bubbleBackground; // Fondo de la burbuja

    /// <summary>
    /// Configura la burbuja con un mensaje de texto.
    /// </summary>
    public void SetText(string message, BubbleSide side)
    {
        textContainer.SetActive(true);
        imageContainer.SetActive(false);

        messageText.text = message;
        AlignBubble(side);
    }

    /// <summary>
    /// Configura la burbuja con una imagen recibida como bytes (Texture2D).
    /// </summary>
    public void SetImage(byte[] imageBytes, BubbleSide side)
    {
        textContainer.SetActive(false);
        imageContainer.SetActive(true);

        // Convertir los bytes en una Texture2D para mostrarla
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes); // LoadImage redimensiona automáticamente
        messageImage.texture = texture;

        // Ajustar el tamaño del RawImage al aspect ratio de la imagen
        float aspectRatio = (float)texture.width / texture.height;
        RectTransform imgRect = messageImage.GetComponent<RectTransform>();
        float targetWidth = 200f; // Ancho fijo para imágenes en el chat
        imgRect.sizeDelta = new Vector2(targetWidth, targetWidth / aspectRatio);

        AlignBubble(side);
    }

    /// <summary>
    /// Alinea la burbuja a izquierda (bot) o derecha (usuario)
    /// y aplica el color correspondiente.
    /// </summary>
    private void AlignBubble(BubbleSide side)
    {
        if (side == BubbleSide.Bot)
        {
            // Anclar a la izquierda
            bubbleRect.anchorMin = new Vector2(0f, 0.5f);
            bubbleRect.anchorMax = new Vector2(0f, 0.5f);
            bubbleRect.pivot = new Vector2(0f, 0.5f);
            bubbleRect.anchoredPosition = new Vector2(10f, 0f);

            if (bubbleBackground != null)
                bubbleBackground.color = botColor;
        }
        else
        {
            // Anclar a la derecha
            bubbleRect.anchorMin = new Vector2(1f, 0.5f);
            bubbleRect.anchorMax = new Vector2(1f, 0.5f);
            bubbleRect.pivot = new Vector2(1f, 0.5f);
            bubbleRect.anchoredPosition = new Vector2(-10f, 0f);

            if (bubbleBackground != null)
                bubbleBackground.color = userColor;
        }
    }
}