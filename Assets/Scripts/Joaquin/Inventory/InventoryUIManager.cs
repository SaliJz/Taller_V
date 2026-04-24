using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gestor principal del UI del inventario.
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    public static InventoryUIManager Instance { get; private set; }

    // Estructura de columnas
    // Col1: [Above×2] [Golden×3] [Below×2]
    //private const int COL1_ABOVE_COUNT = 2;
    //private const int COL1_BELOW_COUNT = 2;
    //private const int COL2_COUNT = 8;
    //private const int COL3_COUNT = 9;

    #region Inspector References

    [Header("Raíces de visibilidad")]
    [Tooltip("Contiene los 3 slots dorados — siempre visible")]
    [SerializeField] private Transform goldenSlotsContainer;
    [Tooltip("Contiene col1 pasivos + col2 + col3 — oculto cuando cerrado")]
    [SerializeField] private GameObject inventoryExpandRoot;

    [Header("Contenedores de Columnas")]
    [SerializeField] private Transform col1AboveContainer;  // 2 slots encima de los dorados
    [SerializeField] private Transform col1BelowContainer;  // 2 slots debajo de los dorados
    [SerializeField] private Transform column2Container;
    [SerializeField] private Transform column3Container;

    //[Header("Prefab de Slot")]
    //[SerializeField] private GameObject inventorySlotPrefab;
    //[Tooltip("Tamaño de cada slot instanciado en runtime")]
    //[SerializeField] private float slotSize = 80f;

    [Header("Slots Dorados")]
    [SerializeField] private Color goldenSlotColor = new Color(1f, 0.84f, 0f);

    [Header("Panel de Detalle")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private Image detailIcon;

    [Header("Panel de Confirmación de Reemplazo")]
    [SerializeField] private GameObject replaceConfirmPanel;
    [SerializeField] private TextMeshProUGUI replaceConfirmText;
    [SerializeField] private Button confirmReplaceButton;
    [SerializeField] private Button cancelReplaceButton;

    [Header("Hover Highlight")]
    [SerializeField] private Color highlightColor = new Color(0.6f, 0.1f, 0.1f);

    [Header("Animación de Reveal")]
    [SerializeField] private float slotRevealDuration = 0.12f;
    [SerializeField] private float slotStagger = 0.04f;   // delay entre slots consecutivos
    [SerializeField] private float columnDelay = 0.08f;   // delay antes de cada columna
    [SerializeField] private float slideOffsetY = 35f;     // px col1 pasivos
    [SerializeField] private float slideOffsetX = 50f;     // px col2/col3

    [Header("Feedback – Ítem Añadido")]
    [SerializeField] private float bouncePeak = 1.2f;
    [SerializeField] private float bounceDuration = 0.25f;

    [Header("Registro de Ítems Mecánicos")]
    [SerializeField] private List<MechanicItemEntry> mechanicRegistry = new List<MechanicItemEntry>();

    #endregion

    #region Internal State

    private readonly List<InventorySlot> goldenSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col1AboveSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col1BelowSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col2Slots = new List<InventorySlot>();
    private readonly List<InventorySlot> col3Slots = new List<InventorySlot>();

    private readonly ShopItem[] mechanicSlots = new ShopItem[3];

    private readonly Dictionary<RectTransform, Vector2> restPositions = new Dictionary<RectTransform, Vector2>();

    private bool isOpen;
    private bool ltWasHeld;
    private ShopItem pendingReplaceItem;
    private ShopManager pendingShopManager;
    private int pendingReplaceSlotIndex;
    private Coroutine revealCoroutine;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CollectSlots();
        inventoryExpandRoot?.SetActive(false);
        detailPanel?.SetActive(false);
        replaceConfirmPanel?.SetActive(false);

        confirmReplaceButton?.onClick.AddListener(OnConfirmReplace);
        cancelReplaceButton?.onClick.AddListener(OnCancelReplace);
    }

    private void Update()
    {
        // Teclado: Tab
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory();
            return;
        }

        // Gamepad: LT (leftTrigger)
        if (Gamepad.current != null)
        {
            bool ltHeld = Gamepad.current.leftTrigger.IsActuated();
            if (ltHeld && !ltWasHeld) ToggleInventory();
            ltWasHeld = ltHeld;
        }
    }

    #endregion

    #region Slots Setup

    /// <summary>
    /// Recoge los InventorySlot ya existentes en cada contenedor de la jerarquía.
    /// Los slots deben estar colocados manualmente en el Editor.
    /// </summary>
    private void CollectSlots()
    {
        CollectFromContainer(goldenSlotsContainer, goldenSlots);
        CollectFromContainer(col1AboveContainer, col1AboveSlots);
        CollectFromContainer(col1BelowContainer, col1BelowSlots);
        CollectFromContainer(column2Container, col2Slots);
        CollectFromContainer(column3Container, col3Slots);

        foreach (var s in goldenSlots) s.SetGolden(true, goldenSlotColor);
    }

    private void CollectFromContainer(Transform container, List<InventorySlot> list)
    {
        if (container == null) return;
        list.Clear();
        foreach (Transform child in container)
        {
            var slot = child.GetComponent<InventorySlot>();
            if (slot == null) continue;

            // Asegurar CanvasGroup para la animación de reveal
            if (child.GetComponent<CanvasGroup>() == null)
            {
                child.gameObject.AddComponent<CanvasGroup>();
            }

            // Guardar posición rest una vez; nunca se sobreescribe
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && !restPositions.ContainsKey(rt))
            {
                restPositions[rt] = rt.anchoredPosition;
            }

            slot.Initialize(this);
            list.Add(slot);
        }
    }

    #endregion

    #region Open/Close Logic

    public void ToggleInventory()
    {
        // Evitar abrir/cerrar si el juego está pausado por el menú de pausa u otro motivo
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;

        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;
        Time.timeScale = 0f;
        //Cursor.visible = true;
        //Cursor.lockState = CursorLockMode.None;

        RefreshDisplay();
        inventoryExpandRoot?.SetActive(true);

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealSequence(opening: true));

        InventoryAudioManager.Instance?.PlayOpenSound();
    }

    public void CloseInventory()
    {
        isOpen = false;
        //Cursor.visible = false;
        //Cursor.lockState = CursorLockMode.Locked;

        detailPanel?.SetActive(false);
        replaceConfirmPanel?.SetActive(false);

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealSequence(opening: false));

        InventoryAudioManager.Instance?.PlayCloseSound();
    }

    #endregion

    #region Reveal Animation

    private IEnumerator RevealSequence(bool opening)
    {
        if (opening)
        {
            // Col1: pasivos arriba y abajo simultáneos, luego col2, luego col3
            var above = StartCoroutine(StaggerSlots(col1AboveSlots, new Vector2(0, -slideOffsetY), opening));
            var below = StartCoroutine(StaggerSlots(col1BelowSlots, new Vector2(0, slideOffsetY), opening));
            yield return above;
            yield return below;

            yield return new WaitForSecondsRealtime(columnDelay);
            yield return StartCoroutine(StaggerSlots(col2Slots, new Vector2(slideOffsetX, 0), opening));

            yield return new WaitForSecondsRealtime(columnDelay);
            yield return StartCoroutine(StaggerSlots(col3Slots, new Vector2(slideOffsetX, 0), opening));
        }
        else
        {
            // Inverso
            yield return StartCoroutine(StaggerSlots(col3Slots, new Vector2(slideOffsetX, 0), opening));
            yield return new WaitForSecondsRealtime(columnDelay);
            yield return StartCoroutine(StaggerSlots(col2Slots, new Vector2(slideOffsetX, 0), opening));
            yield return new WaitForSecondsRealtime(columnDelay);

            var above = StartCoroutine(StaggerSlots(col1AboveSlots, new Vector2(0, -slideOffsetY), opening));
            var below = StartCoroutine(StaggerSlots(col1BelowSlots, new Vector2(0, slideOffsetY), opening));
            yield return above;
            yield return below;

            inventoryExpandRoot?.SetActive(false);
            Time.timeScale = 1f;
        }

        revealCoroutine = null;
    }

    private IEnumerator StaggerSlots(List<InventorySlot> slots, Vector2 hiddenOffset, bool opening)
    {
        var order = opening ? slots : new List<InventorySlot>(slots);
        if (!opening) order.Reverse();

        foreach (var slot in order)
        {
            StartCoroutine(TweenSlot(slot.GetComponent<RectTransform>(), hiddenOffset, opening));
            yield return new WaitForSecondsRealtime(slotStagger);
        }
        yield return new WaitForSecondsRealtime(slotRevealDuration);
    }

    private IEnumerator TweenSlot(RectTransform rt, Vector2 hiddenOffset, bool opening)
    {
        if (rt == null) yield break;

        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        // Usar siempre la posición rest original, nunca la posición actual
        if (!restPositions.TryGetValue(rt, out Vector2 restPos))
        {
            restPos = rt.anchoredPosition;  // fallback si no fue colectado
            restPositions[rt] = restPos;
        }

        Vector2 fromPos = opening ? restPos + hiddenOffset : restPos;
        Vector2 toPos = opening ? restPos : restPos + hiddenOffset;
        float fromAlpha = opening ? 0f : 1f;
        float toAlpha = opening ? 1f : 0f;

        // Fijar posición inicial antes del primer frame para evitar el leve salto
        rt.anchoredPosition = fromPos;
        cg.alpha = fromAlpha;

        float elapsed = 0f;
        while (elapsed < slotRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slotRevealDuration);
            rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
            cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }

        // Asegurar estado final exacto
        rt.anchoredPosition = toPos;
        cg.alpha = toAlpha;
    }

    #endregion

    #region Feedback - Item added

    /// <summary>
    /// Llamar desde ShopManager cuando se añade un ítem al inventario.
    /// </summary>
    public void NotifyItemAdded(ShopItem item)
    {
        if (isOpen)
        {
            var slot = FindSlotWithItem(item);
            if (slot != null) StartCoroutine(BounceTransform(slot.transform));
        }
        else
        {
            foreach (var gs in goldenSlots) StartCoroutine(BounceTransform(gs.transform));
        }
        InventoryAudioManager.Instance?.PlayItemAddedSound();
    }

    private IEnumerator BounceTransform(Transform t)
    {
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = 1f + Mathf.Sin(elapsed / bounceDuration * Mathf.PI) * (bouncePeak - 1f);
            t.localScale = Vector3.one * scale;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    private InventorySlot FindSlotWithItem(ShopItem item)
    {
        foreach (var list in new[] { col1AboveSlots, col1BelowSlots, col2Slots, col3Slots })
        {
            foreach (var s in list)
            {
                if (s.CurrentItem == item) return s;
            }
        }
        foreach (var s in goldenSlots) if (s.CurrentItem == item) return s;
        return null;
    }

    #endregion

    #region Display Logic

    public void RefreshDisplay()
    {
        foreach (var s in goldenSlots) s.ClearSlot();
        foreach (var s in col1AboveSlots) s.ClearSlot();
        foreach (var s in col1BelowSlots) s.ClearSlot();
        foreach (var s in col2Slots) s.ClearSlot();
        foreach (var s in col3Slots) s.ClearSlot();

        RebuildMechanicState();

        for (int i = 0; i < goldenSlots.Count && i < mechanicSlots.Length; i++)
        {
            goldenSlots[i].SetGolden(true, goldenSlotColor);
            if (mechanicSlots[i] != null) goldenSlots[i].SetItem(mechanicSlots[i]);
        }

        var passives = CollectPassives();
        int p = 0;
        foreach (var s in col1AboveSlots) 
        { 
            if (p >= passives.Count) break; s.SetItem(passives[p++]); 
        }
        foreach (var s in col1BelowSlots) 
        { 
            if (p >= passives.Count) break; s.SetItem(passives[p++]); 
        }
        foreach (var s in col2Slots) 
        { 
            if (p >= passives.Count) break; s.SetItem(passives[p++]); 
        }
        foreach (var s in col3Slots) 
        { 
            if (p >= passives.Count) break; s.SetItem(passives[p++]); 
        }
    }

    private void RebuildMechanicState()
    {
        for (int i = 0; i < mechanicSlots.Length; i++) mechanicSlots[i] = null;
        foreach (var item in InventoryManager.CurrentRunItems)
        {
            int idx = GetMechanicSlotIndex(item);
            if (idx >= 0) mechanicSlots[idx] = item;
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

    #endregion

    #region Mechanic Slots replace logic

    public int GetMechanicSlotIndex(ShopItem item)
    {
        if (item == null || item.category != ItemCategory.AttributeModifiers) return -1;
        foreach (var entry in mechanicRegistry)
        {
            if (entry.item == item) return (int)entry.slotType;
        }
        return -1;
    }

    public bool RequestMechanicItemPurchase(ShopItem newItem, int slotIndex, ShopManager caller)
    {
        if (mechanicSlots[slotIndex] != null)
        {
            pendingReplaceItem = newItem;
            pendingReplaceSlotIndex = slotIndex;
            pendingShopManager = caller;   // guardar referencia
            ShowReplaceConfirm(newItem, mechanicSlots[slotIndex]);
            return false;
        }
        return true;
    }

    private void ShowReplaceConfirm(ShopItem newItem, ShopItem current)
    {
        if (replaceConfirmPanel == null) return;
        replaceConfirmPanel.SetActive(true);
        if (replaceConfirmText != null)
        {
            replaceConfirmText.text = $"¿Reemplazar <b>{current.itemName}</b> con <b>{newItem.itemName}</b>?\n" +
                                      "El ítem anterior será descartado.";
        }
    }

    private void OnConfirmReplace()
    {
        if (pendingReplaceItem != null)
        {
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

    #endregion

    #region Detail Panel

    public void ShowItemDetails(ShopItem item)
    {
        if (detailPanel == null || item == null) return;
        detailPanel.SetActive(true);
        if (detailName != null) { detailName.text = item.itemName; detailName.color = item.GetRarityColor(); }
        if (detailDescription != null) detailDescription.text = item.GetFormattedDescriptionAndStats();
        if (detailIcon != null) { detailIcon.sprite = item.itemIcon; detailIcon.enabled = item.itemIcon != null; }
    }

    public void HideItemDetails() => detailPanel?.SetActive(false);

    #endregion

    #region Helpers

    public Color GetHighlightColor() => highlightColor;
    public bool IsOpen => isOpen;

    #endregion
}