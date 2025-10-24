using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de tooltip para mostrar información rápida al pasar sobre los ítems
/// </summary>
public class InventoryTooltip : MonoBehaviour
{
    public static InventoryTooltip Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipTitle;
    [SerializeField] private TextMeshProUGUI tooltipDescription;
    [SerializeField] private Image tooltipBackground;
    [SerializeField] private RectTransform tooltipRectTransform;

    [Header("Configuración")]
    [SerializeField] private Vector2 offset = new Vector2(10, -10);
    [SerializeField] private float showDelay = 0.3f;
    [SerializeField] private bool followCursor = true;

    private Canvas parentCanvas;
    private Camera uiCamera;
    private float showTimer = 0f;
    private bool isShowing = false;
    private ShopItem currentItem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
            parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (isShowing && showTimer < showDelay)
        {
            showTimer += Time.unscaledDeltaTime;

            if (showTimer >= showDelay && tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);
            }
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf && followCursor)
        {
            UpdatePosition();
        }
    }

    /// <summary>
    /// Muestra el tooltip para un ítem
    /// </summary>
    public void Show(ShopItem item)
    {
        if (item == null) return;

        currentItem = item;
        isShowing = true;
        showTimer = 0f;

        if (tooltipTitle != null)
        {
            tooltipTitle.text = item.itemName;
            tooltipTitle.color = item.GetRarityColor();
        }

        if (tooltipDescription != null)
        {
            string desc = item.description;

            if (item.isTemporary)
            {
                desc += "\n<color=orange>TEMPORAL</color>";
            }

            tooltipDescription.text = desc;
        }

        // Ajustar tamaño del fondo
        Canvas.ForceUpdateCanvases();
        if (tooltipBackground != null && tooltipRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);
        }

        UpdatePosition();
    }

    /// <summary>
    /// Oculta el tooltip
    /// </summary>
    public void Hide()
    {
        isShowing = false;
        showTimer = 0f;
        currentItem = null;

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Actualiza la posición del tooltip siguiendo el cursor
    /// </summary>
    private void UpdatePosition()
    {
        if (tooltipRectTransform == null) return;

        Vector2 mousePosition = Input.mousePosition;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            mousePosition,
            uiCamera,
            out localPoint
        );

        tooltipRectTransform.localPosition = localPoint + offset;

        // Asegurar que el tooltip no salga de la pantalla
        ClampToScreen();
    }

    /// <summary>
    /// Asegura que el tooltip permanezca dentro de la pantalla
    /// </summary>
    private void ClampToScreen()
    {
        if (tooltipRectTransform == null || parentCanvas == null) return;

        Vector3[] corners = new Vector3[4];
        tooltipRectTransform.GetWorldCorners(corners);

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        Vector3[] canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);

        Vector3 localPos = tooltipRectTransform.localPosition;

        // Verificar límites horizontales
        if (corners[2].x > canvasCorners[2].x) // Derecha
        {
            localPos.x -= corners[2].x - canvasCorners[2].x;
        }
        else if (corners[0].x < canvasCorners[0].x) // Izquierda
        {
            localPos.x += canvasCorners[0].x - corners[0].x;
        }

        // Verificar límites verticales
        if (corners[1].y > canvasCorners[1].y) // Arriba
        {
            localPos.y -= corners[1].y - canvasCorners[1].y;
        }
        else if (corners[0].y < canvasCorners[0].y) // Abajo
        {
            localPos.y += canvasCorners[0].y - corners[0].y;
        }

        tooltipRectTransform.localPosition = localPos;
    }
}