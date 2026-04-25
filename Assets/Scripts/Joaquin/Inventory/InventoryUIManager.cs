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
    // Col1: [Abovex2] [Goldenx3] [Belowx2]
    //private const int COL1_ABOVE_COUNT = 2;
    //private const int COL1_BELOW_COUNT = 2;
    //private const int COL2_COUNT = 8;
    //private const int COL3_COUNT = 9;

    #region Inspector References

    [Header("Referencias")]
    [SerializeField] private GameObject itemUIPanel;
    [SerializeField] private GameObject interactionButtonUIPanel;

    [Header("Raices de visibilidad")]
    [Tooltip("Contiene los 3 slots dorados siempre visible")]
    [SerializeField] private Transform goldenSlotsContainer;
    [Tooltip("Contiene col1 pasivos + col2 + col3 oculto cuando cerrado")]
    [SerializeField] private GameObject inventoryExpandRoot;

    [Header("Contenedores de Columnas")]
    [SerializeField] private Transform col1AboveContainer;  // 2 slots encima de los dorados
    [SerializeField] private Transform col1BelowContainer;  // 2 slots debajo de los dorados
    [SerializeField] private Transform column2Container;
    [SerializeField] private Transform column3Container;

    //[Header("Prefab de Slot")]
    //[SerializeField] private GameObject inventorySlotPrefab;
    //[Tooltip("Tamano de cada slot instanciado en runtime")]
    //[SerializeField] private float slotSize = 80f;

    [Header("Slots Dorados")]
    [SerializeField] private Color goldenSlotColor = new Color(1f, 0.84f, 0f);

    [Header("Panel de Detalle")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private Image detailIcon;

    [Header("Panel de Confirmacion de Reemplazo")]
    [SerializeField] private GameObject replaceConfirmPanel;
    [SerializeField] private TextMeshProUGUI replaceConfirmText;
    [SerializeField] private Button confirmReplaceButton;
    [SerializeField] private Button cancelReplaceButton;

    [Header("Hover Highlight")]
    [SerializeField] private Color highlightColor = new Color(0.6f, 0.1f, 0.1f);

    [Header("Animacion de Reveal")]
    [SerializeField] private float slotRevealDuration = 0.12f;
    [SerializeField] private float slotStagger = 0.04f;   // delay entre slots consecutivos
    [SerializeField] private float columnDelay = 0.08f;   // delay antes de cada columna
    [SerializeField] private float slideOffsetY = 35f;     // px col1 pasivos
    [SerializeField] private float slideOffsetX = 50f;     // px col2/col3

    [Header("Feedback de item Anadido")]
    [SerializeField] private float bouncePeak = 1.2f;
    [SerializeField] private float bounceDuration = 0.25f;

    //[Header("Registro de items Mecanicos")]
    //[SerializeField] private List<MechanicItemEntry> mechanicRegistry = new List<MechanicItemEntry>();

    #endregion

    #region Internal State

    private readonly List<InventorySlot> goldenSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col1AboveSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col1BelowSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col2Slots = new List<InventorySlot>();
    private readonly List<InventorySlot> col3Slots = new List<InventorySlot>();

    private readonly ShopItem[] mechanicSlots = new ShopItem[3];
    private float inventoryPreviousTimeScale = 1f;
    private float confirmPreviousTimeScale = 1f;

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
    /// Recoge los InventorySlot ya existentes en cada contenedor de la jerarquia.
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

            // Asegurar CanvasGroup para la animacion de reveal
            if (child.GetComponent<CanvasGroup>() == null)
            {
                child.gameObject.AddComponent<CanvasGroup>();
            }

            // Guardar posicion rest una vez; nunca se sobreescribe
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
        // Evitar abrir/cerrar si el juego esta pausado por el menu de pausa u otro motivo
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;

        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;
        inventoryPreviousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        //Cursor.visible = true;
        //Cursor.lockState = CursorLockMode.None;

        RefreshDisplay();

        if (itemUIPanel != null && itemUIPanel.activeSelf) itemUIPanel.SetActive(false);
        if (interactionButtonUIPanel != null && interactionButtonUIPanel.activeSelf)
        {
            interactionButtonUIPanel.SetActive(false);
        }

        inventoryExpandRoot?.SetActive(true);

        if (pendingReplaceItem != null)
        {
            pendingShopManager?.SetInteractionPromptActive(isOpen);
            pendingShopManager?.ResetCostBar();
            pendingShopManager?.LockAndDisplayItemDetails(null);
            ReportDebug("Abriendo inventario con una compra pendiente de confirmacion. " +
                "Mostrando el panel de confirmacion de reemplazo si no se mostro ya.", 1);
        }

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealSequence(opening: true));

        InventoryAudioManager.Instance?.PlayOpenSound();
    }

    public void CloseInventory()
    {
        isOpen = false;
        Time.timeScale = inventoryPreviousTimeScale;

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
            // Col1: pasivos arriba y abajo simultaneos, luego col2, luego col3
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

        // Usar siempre la posicion rest original, nunca la posicion actual
        if (!restPositions.TryGetValue(rt, out Vector2 restPos))
        {
            restPos = rt.anchoredPosition;  // fallback si no fue colectado
            restPositions[rt] = restPos;
        }

        Vector2 fromPos = opening ? restPos + hiddenOffset : restPos;
        Vector2 toPos = opening ? restPos : restPos + hiddenOffset;
        float fromAlpha = opening ? 0f : 1f;
        float toAlpha = opening ? 1f : 0f;

        // Fijar posicion inicial antes del primer frame para evitar el leve salto
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
    /// Llamar desde ShopManager cuando se anade un item al inventario.
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

    /// <summary>
    /// Retorna el indice de ranura (0=Melee, 1=Ranged, 2=Dash) usando los datos del propio ShopItem.
    /// </summary>
    public int GetMechanicSlotIndex(ShopItem item)
    {
        if (item == null || !item.hasEffectCategory) return -1;
        return item.effectCategory switch
        {
            TypeEffect.Melee => 0,
            TypeEffect.Shield => 1,
            TypeEffect.Dash => 2,
            _ => -1
        };
    }

    /// <summary>
    /// Llamado desde ShopManager antes de ejecutar la compra.
    /// Si ya hay un item en esa ranura, muestra el panel de confirmacion y retorna false.
    /// Si la ranura esta libre, retorna true para que la compra proceda.
    /// </summary>
    public bool RequestMechanicItemPurchase(ShopItem newItem, int slotIndex, ShopManager caller)
    {
        // Asegurar que mechanicSlots refleja el estado real del inventario
        RebuildMechanicState();

        if (mechanicSlots[slotIndex] != null)
        {
            pendingReplaceItem = newItem;
            pendingReplaceSlotIndex = slotIndex;
            pendingShopManager = caller;
            ShowReplaceConfirm(newItem, mechanicSlots[slotIndex]);
            ReportDebug($"Solicitando compra de {newItem.itemName} para slot {slotIndex}, " +
                $"pero ya hay {mechanicSlots[slotIndex].itemName}. Mostrando confirmacion de reemplazo.", 1);
            return false; // bloquear compra hasta que el jugador confirme o cancele
        }
        return true;
    }

    private void ShowReplaceConfirm(ShopItem newItem, ShopItem current)
    {
        if (replaceConfirmPanel == null)
        {
            ReportDebug("No se asigno el panel de confirmacion de reemplazo en el inspector.", 2);
            return;
        }

        isOpen = true; // asegurar que el estado es consistente mientras se muestra el panel

        if (itemUIPanel != null && itemUIPanel.activeSelf) itemUIPanel.SetActive(false);
        if (interactionButtonUIPanel != null && interactionButtonUIPanel.activeSelf)
        {
            interactionButtonUIPanel.SetActive(false);
        }

        replaceConfirmPanel.SetActive(true);
        confirmPreviousTimeScale = Time.timeScale; // guardar antes de pausar
        Time.timeScale = 0f; // pausar el juego mientras se muestra la confirmacion
        if (replaceConfirmText != null)
        {
            replaceConfirmText.text =
                $"Reemplazar <b>{current.itemName}</b> con <b>{newItem.itemName}</b>?\n" +
                "El item anterior sera descartado.";
        }
    }

    private void OnConfirmReplace()
    {
        if (pendingReplaceItem != null)
        {
            ShopItem oldItem = mechanicSlots[pendingReplaceSlotIndex];
            // Delegar al ShopManager, el revierte stats del viejo y aplica los del nuevo
            ReportDebug($"Jugador confirmo reemplazo: {oldItem.itemName} => {pendingReplaceItem.itemName} " +
                $"en slot {pendingReplaceSlotIndex}. Ejecutando reemplazo.", 1);
            pendingShopManager?.ExecuteReplacement(oldItem, pendingReplaceItem);
            pendingReplaceItem = null;
            pendingShopManager = null;
            RefreshDisplay();
        }

        isOpen = false;
        replaceConfirmPanel?.SetActive(false);
        Time.timeScale = confirmPreviousTimeScale;
    }

    private void OnCancelReplace()
    {
        ReportDebug("Jugador cancela el reemplazo de item mecanico. No se realizara ningun cambio.", 1);

        isOpen = false;
        replaceConfirmPanel?.SetActive(false);
        pendingShopManager?.RegisterPendingPurchaseCallback(null);
        pendingReplaceItem = null;
        pendingShopManager = null;
        Time.timeScale = confirmPreviousTimeScale;
        pendingShopManager?.FireCancelCallback();
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

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[InventoryUIManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[InventoryUIManager] {message}");
                break;
            case 3:
                Debug.LogError($"[InventoryUIManager] {message}");
                break;
            default:
                Debug.Log($"[InventoryUIManager] {message}");
                break;
        }
    }
}