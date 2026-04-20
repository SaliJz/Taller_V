using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de tooltip que aparece al pasar el cursor sobre un ítem del inventario.
/// </summary>
public class InventoryTooltip : MonoBehaviour
{
    public static InventoryTooltip Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipTitle;
    [SerializeField] private TextMeshProUGUI tooltipDescription;
    [SerializeField] private RectTransform tooltipRectTransform;

    [Header("Configuración")]
    [SerializeField] private Vector2 offset = new Vector2(12f, -12f);
    [SerializeField] private float showDelay = 0.25f;

    private Canvas parentCanvas;
    private Camera uiCamera;
    private float showTimer;
    private bool isShowing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null &&
            (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
             parentCanvas.renderMode == RenderMode.WorldSpace))
        {
            uiCamera = parentCanvas.worldCamera;
        }

        tooltipPanel?.SetActive(false);
    }

    private void Update()
    {
        if (!isShowing) return;

        showTimer += Time.unscaledDeltaTime;
        if (showTimer >= showDelay)
        {
            tooltipPanel?.SetActive(true);
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdatePosition();
        }
    }

    public void Show(ShopItem item)
    {
        if (item == null) return;

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
            if (item.isTemporary) desc += "\n<color=orange>TEMPORAL</color>";
            tooltipDescription.text = desc;
        }

        Canvas.ForceUpdateCanvases();
        if (tooltipRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);
        }
        UpdatePosition();
    }

    public void Hide()
    {
        isShowing = false;
        showTimer = 0f;
        tooltipPanel?.SetActive(false);
    }

    private void UpdatePosition()
    {
        if (tooltipRectTransform == null || parentCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            Input.mousePosition,
            uiCamera,
            out Vector2 local);

        tooltipRectTransform.localPosition = local + offset;
        ClampToCanvas();
    }

    private void ClampToCanvas()
    {
        if (tooltipRectTransform == null) return;

        var corners = new Vector3[4];
        var canvasCorners = new Vector3[4];
        tooltipRectTransform.GetWorldCorners(corners);
        (parentCanvas.transform as RectTransform).GetWorldCorners(canvasCorners);

        Vector3 pos = tooltipRectTransform.localPosition;

        if (corners[2].x > canvasCorners[2].x) pos.x -= corners[2].x - canvasCorners[2].x;
        else if (corners[0].x < canvasCorners[0].x) pos.x += canvasCorners[0].x - corners[0].x;

        if (corners[1].y > canvasCorners[1].y) pos.y -= corners[1].y - canvasCorners[1].y;
        else if (corners[0].y < canvasCorners[0].y) pos.y += canvasCorners[0].y - corners[0].y;

        tooltipRectTransform.localPosition = pos;
    }
}