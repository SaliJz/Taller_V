using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gestor principal del UI del inventario
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    #region Datos externos

    public static InventoryUIManager Instance { get; private set; }

    // Columnas de slots
    private const int COL1_COUNT = 7;
    private const int COL2_COUNT = 8;
    private const int COL3_COUNT = 9;

    // Índices de los slots dorados dentro de col1
    private static readonly int[] GoldenIndices = { 2, 3, 4 };

    // Orden de llenado de pasivos en col1 (arriba a abajo, saltando dorados)
    private static readonly int[] Col1PassiveOrder = { 0, 1, 5, 6 };

    [Header("Panel Principal")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryAnimator inventoryAnimator;

    [Header("Contenedores de Columnas")]
    [SerializeField] private Transform column1Container;
    [SerializeField] private Transform column2Container;
    [SerializeField] private Transform column3Container;
    [SerializeField] private CanvasGroup column2Group;
    [SerializeField] private CanvasGroup column3Group;

    [Header("Prefab de Slot")]
    [SerializeField] private GameObject inventorySlotPrefab;

    [Header("Slots Dorados")]
    [SerializeField] private Color goldenSlotColor = new Color(1f, 0.84f, 0f);

    [Header("Opacidad de Columnas")]
    [SerializeField] private float col2Alpha = 0.7f;
    [SerializeField] private float col3Alpha = 0.3f;

    [Header("Panel de Detalle de Ítem")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private Image detailIcon;

    [Header("Panel de Confirmación de Reemplazo")]
    [SerializeField] private GameObject replaceConfirmPanel;
    [SerializeField] private TextMeshProUGUI replaceConfirmText;
    [SerializeField] private Button confirmReplaceButton;
    [SerializeField] private Button cancelReplaceButton;

    [Header("Highlight de Hover")]
    [SerializeField] private Color highlightColor = new Color(0.6f, 0.1f, 0.1f);

    [Header("Registro de Ítems Mecánicos")]
    [SerializeField] private List<MechanicItemEntry> mechanicRegistry = new List<MechanicItemEntry>();

    #endregion

    #region Datos internos
    
    private readonly List<InventorySlot> col1Slots = new List<InventorySlot>();
    private readonly List<InventorySlot> col2Slots = new List<InventorySlot>();
    private readonly List<InventorySlot> col3Slots = new List<InventorySlot>();

    // Slot mecánico [0 = Melee, 1 = Ranged, 2 = Dash]
    private readonly ShopItem[] mechanicSlots = new ShopItem[3];

    private bool isOpen;
    private ShopItem pendingReplaceItem;
    private int pendingReplaceSlotIndex;

    #endregion

    #region Ciclo de vida

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject);
            return; 
        }
        Instance = this;
    }

    private void Start()
    {
        BuildSlots();
        ApplyColumnAlpha();

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        if (replaceConfirmPanel != null) replaceConfirmPanel.SetActive(false);

        confirmReplaceButton?.onClick.AddListener(OnConfirmReplace);
        cancelReplaceButton?.onClick.AddListener(OnCancelReplace);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory(); return;
        }

        if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame)
        {
            ToggleInventory();
        }
    }

    #endregion

    #region Construcción de UI
    
    private void BuildSlots()
    {
        CreateSlots(column1Container, COL1_COUNT, col1Slots);
        CreateSlots(column2Container, COL2_COUNT, col2Slots);
        CreateSlots(column3Container, COL3_COUNT, col3Slots);

        foreach (int idx in GoldenIndices)
        {
            if (idx < col1Slots.Count) col1Slots[idx].SetGolden(true, goldenSlotColor);
        }
    }

    private void CreateSlots(Transform container, int count, List<InventorySlot> list)
    {
        if (container == null || inventorySlotPrefab == null) return;

        foreach (Transform child in container) Destroy(child.gameObject);
        list.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(inventorySlotPrefab, container);
            var slot = go.GetComponent<InventorySlot>();
            if (slot != null) { slot.Initialize(this); list.Add(slot); }
        }
    }

    private void ApplyColumnAlpha()
    {
        if (column2Group != null) column2Group.alpha = col2Alpha;
        if (column3Group != null) column3Group.alpha = col3Alpha;
    }

    #endregion

    #region Control de Inventario

    public void ToggleInventory()
    {
        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshDisplay();

        if (inventoryAnimator != null) inventoryAnimator.AnimateOpen();
        else if (inventoryPanel != null) inventoryPanel.SetActive(true);

        InventoryAudioManager.Instance?.PlayOpenSound();
    }

    public void CloseInventory()
    {
        isOpen = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        detailPanel?.SetActive(false);
        replaceConfirmPanel?.SetActive(false);

        if (inventoryAnimator != null)
        {
            inventoryAnimator.AnimateClose(null);
        }
        else if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        InventoryAudioManager.Instance?.PlayCloseSound();
    }

    #endregion

    #region Lógica de Slots

    public void RefreshDisplay()
    {
        // Limpia todo
        foreach (var s in col1Slots) s.ClearSlot();
        foreach (var s in col2Slots) s.ClearSlot();
        foreach (var s in col3Slots) s.ClearSlot();

        // Reconstruye estado mecánico desde CurrentRunItems
        RebuildMechanicState();

        // Slots dorados
        for (int i = 0; i < GoldenIndices.Length; i++)
        {
            int idx = GoldenIndices[i];
            if (idx >= col1Slots.Count) continue;
            col1Slots[idx].SetGolden(true, goldenSlotColor);
            if (mechanicSlots[i] != null) col1Slots[idx].SetItem(mechanicSlots[i]);
        }

        // Slots pasivos
        var passives = CollectPassives();
        int p = 0;

        foreach (int slotIdx in Col1PassiveOrder)
        {
            if (p >= passives.Count) break;
            if (slotIdx < col1Slots.Count) col1Slots[slotIdx].SetItem(passives[p++]);
        }
        for (int i = 0; i < col2Slots.Count && p < passives.Count; i++)
        {
            col2Slots[i].SetItem(passives[p++]);
        }
        for (int i = 0; i < col3Slots.Count && p < passives.Count; i++)
        {
            col3Slots[i].SetItem(passives[p++]);
        }
    }

    private void RebuildMechanicState()
    {
        for (int i = 0; i < mechanicSlots.Length; i++) mechanicSlots[i] = null;

        foreach (var item in InventoryManager.CurrentRunItems)
        {
            int slotIdx = GetMechanicSlotIndex(item);
            if (slotIdx >= 0) mechanicSlots[slotIdx] = item;
        }
    }

    private List<ShopItem> CollectPassives()
    {
        var list = new List<ShopItem>();
        foreach (var item in InventoryManager.CurrentRunItems)
        {
            if (GetMechanicSlotIndex(item) < 0) list.Add(item);
        }
        return list;
    }

    public int GetMechanicSlotIndex(ShopItem item)
    {
        if (item == null || item.category != ItemCategory.SkillEnhancers) return -1;

        foreach (var entry in mechanicRegistry)
        {
            if (entry.item == item) return (int)entry.slotType;
        }

        return -1;
    }

    #endregion

    #region Metodos auxiliares

    /// <summary>
    /// Muestra confirmación si ya hay un ítem en esa ranura.
    /// Retorna true si se puede comprar directamente, false si espera confirmación.
    /// </summary>
    public bool RequestMechanicItemPurchase(ShopItem newItem, int slotIndex)
    {
        if (mechanicSlots[slotIndex] != null)
        {
            pendingReplaceItem = newItem;
            pendingReplaceSlotIndex = slotIndex;
            ShowReplaceConfirm(newItem, mechanicSlots[slotIndex]);
            return false;
        }
        return true;
    }

    private void ShowReplaceConfirm(ShopItem newItem, ShopItem currentItem)
    {
        if (replaceConfirmPanel == null) return;
        replaceConfirmPanel.SetActive(true);

        if (replaceConfirmText != null)
        {
            replaceConfirmText.text = $"¿Reemplazar <b>{currentItem.itemName}</b> " +
                $"con <b>{newItem.itemName}</b>?\n" +
                "El ítem anterior será descartado.";
        }
    }

    private void OnConfirmReplace()
    {
        if (pendingReplaceItem != null)
        {
            // Remueve ítem anterior del inventario y añade el nuevo
            var old = mechanicSlots[pendingReplaceSlotIndex];
            if (old != null) InventoryManager.CurrentRunItems.Remove(old);

            InventoryManager.CurrentRunItems.Add(pendingReplaceItem);
            pendingReplaceItem = null;
            RefreshDisplay();
        }
        replaceConfirmPanel?.SetActive(false);
    }

    private void OnCancelReplace()
    {
        pendingReplaceItem = null;
        replaceConfirmPanel?.SetActive(false);
    }

    public void ShowItemDetails(ShopItem item)
    {
        if (detailPanel == null || item == null) return;
        detailPanel.SetActive(true);

        if (detailName != null)
        {
            detailName.text = item.itemName;
            detailName.color = item.GetRarityColor();
        }

        if (detailDescription != null)
        {
            detailDescription.text = item.GetFormattedDescriptionAndStats();
        }

        if (detailIcon != null)
        {
            detailIcon.sprite = item.itemIcon;
            detailIcon.enabled = item.itemIcon != null;
        }
    }

    public void HideItemDetails() => detailPanel?.SetActive(false);

    public void SetInteractiveOpacity(bool active)
    {
        if (column2Group != null) column2Group.alpha = active ? 1f : col2Alpha;
        if (column3Group != null) column3Group.alpha = active ? 1f : col3Alpha;
    }

    public Color GetHighlightColor() => highlightColor;
    public bool IsOpen => isOpen;

    #endregion
}