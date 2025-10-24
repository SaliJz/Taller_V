using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente auxiliar para centrar correctamente los slots en el Grid Layout Group
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
public class InventorySlotAligner : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool autoCenter = true;
    [SerializeField] private int columnsPerRow = 4;
    [SerializeField] private float cellWidth = 120f;
    [SerializeField] private float cellHeight = 120f;
    [SerializeField] private float spacing = 10f;

    private GridLayoutGroup gridLayout;
    private RectTransform rectTransform;

    private void Awake()
    {
        gridLayout = GetComponent<GridLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();

        if (autoCenter)
        {
            ConfigureGridLayout();
        }
    }

    private void Start()
    {
        if (autoCenter)
        {
            AdjustAlignment();
        }
    }

    /// <summary>
    /// Configura el Grid Layout Group automáticamente
    /// </summary>
    private void ConfigureGridLayout()
    {
        if (gridLayout == null) return;

        gridLayout.cellSize = new Vector2(cellWidth, cellHeight);
        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter; // Centrado
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columnsPerRow;

        Debug.Log($"[InventorySlotAligner] Grid configurado: {columnsPerRow} columnas, celdas de {cellWidth}x{cellHeight}");
    }

    /// <summary>
    /// Ajusta la alineación basándose en el número de hijos
    /// </summary>
    private void AdjustAlignment()
    {
        if (gridLayout == null || rectTransform == null) return;

        int childCount = transform.childCount;

        // Si hay menos de una fila completa, centrar
        if (childCount < columnsPerRow)
        {
            gridLayout.childAlignment = TextAnchor.UpperCenter;
        }
        else
        {
            // Si hay múltiples filas, centrar también
            gridLayout.childAlignment = TextAnchor.UpperCenter;
        }

        // Forzar recalcular layout
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    /// <summary>
    /// Llama esto después de añadir o remover slots
    /// </summary>
    public void RefreshAlignment()
    {
        AdjustAlignment();
    }

    /// <summary>
    /// Calcula el ancho necesario para centrar perfectamente
    /// </summary>
    public float CalculateOptimalWidth()
    {
        float totalCellWidth = columnsPerRow * cellWidth;
        float totalSpacing = (columnsPerRow - 1) * spacing;
        float padding = gridLayout.padding.left + gridLayout.padding.right;

        return totalCellWidth + totalSpacing + padding;
    }

    [ContextMenu("Force Recalculate Layout")]
    private void ForceRecalculate()
    {
        ConfigureGridLayout();
        AdjustAlignment();
        Canvas.ForceUpdateCanvases();
    }

    [ContextMenu("Log Layout Info")]
    private void LogLayoutInfo()
    {
        Debug.Log("=== LAYOUT INFO ===");
        Debug.Log($"Children: {transform.childCount}");
        Debug.Log($"Columns: {columnsPerRow}");
        Debug.Log($"Cell Size: {cellWidth}x{cellHeight}");
        Debug.Log($"Spacing: {spacing}");
        Debug.Log($"Optimal Width: {CalculateOptimalWidth()}");
        Debug.Log($"Current Width: {rectTransform.rect.width}");
        Debug.Log($"Child Alignment: {gridLayout.childAlignment}");
        Debug.Log("==================");
    }
}