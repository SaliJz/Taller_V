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
    #region Public Properties & Events

    public static InventoryUIManager Instance { get; private set; }
    public bool IsOpen => isOpen;

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private GameObject itemUIPanel;
    [SerializeField] private GameObject interactionButtonUIPanel;
    [Tooltip("Canvas padre del inventario, necesario para posicionar tooltip y panel con mando")]
    [SerializeField] private Canvas inventoryCanvas;

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

    #endregion

    #region Inspector - Settings

    //[Header("Prefab de Slot")]
    //[SerializeField] private GameObject inventorySlotPrefab;
    //[Tooltip("Tamano de cada slot instanciado en runtime")]
    //[SerializeField] private float slotSize = 80f;

    [Header("Slots Dorados")]
    [SerializeField] private Color goldenSlotColor = new Color(1f, 0.84f, 0f);

    [Header("Hover Highlight")]
    [SerializeField] private Color highlightColor = new Color(0.6f, 0.1f, 0.1f);

    [Header("Animacion de Reveal")]
    [SerializeField] private float slotRevealDuration = 0.12f;
    [SerializeField] private float slotStagger = 0.04f; // delay entre slots consecutivos
    [SerializeField] private float columnDelay = 0.08f; // delay antes de cada columna
    [SerializeField] private float slideOffsetY = 35f; // px col1 pasivos
    [SerializeField] private float slideOffsetX = 50f; // px col2/col3

    [Header("Feedback de item Anadido")]
    [SerializeField] private float bouncePeak = 1.2f;
    [SerializeField] private float bounceDuration = 0.25f;

    [Header("Mando - Navegacion")]
    [Tooltip("Cooldown en segundos entre repeticiones del joystick izquierdo")]
    [SerializeField] private float joystickRepeatCooldown = 0.2f;

    //[Header("Registro de items Mecanicos")]
    //[SerializeField] private List<MechanicItemEntry> mechanicRegistry = new List<MechanicItemEntry>();

    #endregion

    #region Internal State

    // Estructura de columnas
    // Col1: [Abovex2] [Goldenx3] [Belowx2]
    //private const int COL1_ABOVE_COUNT = 2;
    //private const int COL1_BELOW_COUNT = 2;
    //private const int COL2_COUNT = 8;
    //private const int COL3_COUNT = 9;

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

    /// <summary>Slot cuyo panel descriptivo esta actualmente visible.</summary>
    private InventorySlot selectedSlot;

    /// <summary>
    /// Grilla de navegacion con mando:
    /// [0] = col1 (above + golden + below), [1] = col2, [2] = col3.
    /// Cada sublista esta ordenada de arriba a abajo.
    /// </summary>
    private List<List<InventorySlot>> navGrid;
    private int navCol;
    private int navRow;
    private InventorySlot focusedSlot;
    private float joystickCooldownTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Intentar obtener el canvas del padre si no fue asignado en el Inspector
        if (inventoryCanvas == null) inventoryCanvas = GetComponentInParent<Canvas>();
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

        // Gamepad: navegacion entre slots (solo cuando el inventario esta abierto)
        if (isOpen) UpdateGamepadNavigation();
    }

    #endregion

    #region Initialization & Data Sync

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

    #region Inventory Open/Close Logic

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

        BuildNavGrid();
        if (Gamepad.current != null)
        {
            joystickCooldownTimer = 0f;
            focusedSlot = null;
            navCol = 0;
            navRow = 0;
            // Pequeno delay para que el reveal empiece antes del foco visual
            StartCoroutine(FocusFirstSlotNextFrame());
        }
    }

    public void CloseInventory()
    {
        isOpen = false;
        Time.timeScale = inventoryPreviousTimeScale;

        //Cursor.visible = false;
        //Cursor.lockState = CursorLockMode.Locked;

        if (focusedSlot != null)
        {
            focusedSlot.SimulatePointerExit();
            focusedSlot = null;
        }
        selectedSlot = null;

        detailPanel?.SetActive(false);
        replaceConfirmPanel?.SetActive(false);
        InventoryTooltip.Instance?.Hide();
        InventoryTooltip.Instance?.SetDetailPanelOpen(false);

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealSequence(opening: false));

        InventoryAudioManager.Instance?.PlayCloseSound();
    }

    /// <summary>Espera un frame para aplicar el foco, dejando que el reveal inicie primero.</summary>
    private IEnumerator FocusFirstSlotNextFrame()
    {
        yield return null;
        if (navGrid == null || navGrid.Count == 0) yield break;

        // Encontrar la primera columna y fila con item
        for (int c = 0; c < navGrid.Count; c++)
        {
            int r = FindNearestOccupiedRow(navGrid[c], 0);
            if (r >= 0)
            {
                navCol = c;
                navRow = r;
                NavigateGamepadTo(c, r);
                yield break;
            }
        }
    }

    #endregion

    #region Visual & Audio Effects

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
            restPos = rt.anchoredPosition;
            restPositions[rt] = restPos;
        }

        Vector2 fromPos = opening ? restPos + hiddenOffset : restPos;
        Vector2 toPos = opening ? restPos : restPos + hiddenOffset;
        float fromAlpha = opening ? 0f : 1f;
        float toAlpha = opening ? 1f : 0f;

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

        rt.anchoredPosition = toPos;
        cg.alpha = toAlpha;
    }

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

    #endregion

    #region Display & Item Management

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
            if (p >= passives.Count) break;
            s.SetItem(passives[p++]);
        }

        foreach (var s in col1BelowSlots)
        {
            if (p >= passives.Count) break;
            s.SetItem(passives[p++]);
        }

        foreach (var s in col2Slots)
        {
            if (p >= passives.Count) break;
            s.SetItem(passives[p++]);
        }

        foreach (var s in col3Slots)
        {
            if (p >= passives.Count) break;
            s.SetItem(passives[p++]);
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

    private InventorySlot FindSlotWithItem(ShopItem item)
    {
        foreach (var list in new[] { col1AboveSlots, col1BelowSlots, col2Slots, col3Slots })
        {
            foreach (var s in list)
            {
                if (s.CurrentItem == item) return s;
            }
        }

        foreach (var s in goldenSlots)
        {
            if (s.CurrentItem == item) return s;
        }
        return null;
    }

    #endregion

    #region Mechanic Slots & Replace Logic

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
        RebuildMechanicState();

        if (mechanicSlots[slotIndex] != null)
        {
            pendingReplaceItem = newItem;
            pendingReplaceSlotIndex = slotIndex;
            pendingShopManager = caller;
            ShowReplaceConfirm(newItem, mechanicSlots[slotIndex]);
            ReportDebug($"Solicitando compra de {newItem.itemName} para slot {slotIndex}, " +
                $"pero ya hay {mechanicSlots[slotIndex].itemName}. Mostrando confirmacion de reemplazo.", 1);
            return false;
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

        isOpen = true;

        if (itemUIPanel != null && itemUIPanel.activeSelf) itemUIPanel.SetActive(false);
        if (interactionButtonUIPanel != null && interactionButtonUIPanel.activeSelf)
        {
            interactionButtonUIPanel.SetActive(false);
        }

        replaceConfirmPanel.SetActive(true);
        confirmPreviousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
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

    #region Detail Panel Logic

    /// <summary>
    /// FIX (bug de 3 clics): la seleccion ahora la gestiona el manager.
    /// Si se hace clic en el slot ya seleccionado => cierra el panel.
    /// Si se hace clic en un slot diferente => cambia al nuevo en UN solo clic.
    /// </summary>
    /// <param name="slot">Slot que recibio el clic.</param>
    /// <param name="isGamepad">True si el clic viene del mando (Button North).</param>
    public void OnSlotClicked(InventorySlot slot, bool isGamepad = false)
    {
        if (slot == null || !slot.HasItem) return;

        if (selectedSlot == slot)
        {
            // Toggle: cierra el panel del slot ya seleccionado
            selectedSlot = null;
            HideItemDetails();
        }
        else
        {
            // Cambia inmediatamente al nuevo slot, sin importar cual estaba seleccionado antes
            selectedSlot = slot;
            //RectTransform slotRect = isGamepad ? slot.SlotRect : null;
            ShowItemDetails(slot.CurrentItem, slotRect: null);
        }
    }

    /// <summary>
    /// Muestra el panel descriptivo del item.
    /// <paramref name="slotRect"/> si no es null, posiciona el panel centrado sobre ese slot (modo mando).
    /// </summary>
    public void ShowItemDetails(ShopItem item, RectTransform slotRect = null)
    {
        if (detailPanel == null || item == null) return;

        // Posicionar el panel respecto al slot si venimos del mando
        if (slotRect != null && inventoryCanvas != null)
        {
            PositionPanelAtSlot(detailPanel.GetComponent<RectTransform>(), slotRect);
        }

        detailPanel.SetActive(true);

        if (detailName != null) { detailName.text = item.itemName; detailName.color = item.GetRarityColor(); }
        if (detailDescription != null) detailDescription.text = item.GetFormattedDescriptionAndStats();
        if (detailIcon != null) { detailIcon.sprite = item.itemIcon; detailIcon.enabled = item.itemIcon != null; }

        // Ocultar el tooltip mientras el panel este abierto
        InventoryTooltip.Instance?.SetDetailPanelOpen(true);
    }

    public void HideItemDetails()
    {
        detailPanel?.SetActive(false);
        selectedSlot = null;

        // Reactivar el tooltip al cerrar el panel descriptivo
        InventoryTooltip.Instance?.SetDetailPanelOpen(false);
    }

    /// <summary>
    /// Centra el panel sobre el slot dado, dentro de los limites del canvas.
    /// </summary>
    private void PositionPanelAtSlot(RectTransform panelRect, RectTransform slotRect)
    {
        if (panelRect == null || slotRect == null || inventoryCanvas == null) return;

        Camera cam = (inventoryCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : inventoryCanvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, slotRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inventoryCanvas.transform as RectTransform,
            screenPoint,
            cam,
            out Vector2 localPoint);

        panelRect.localPosition = new Vector3(localPoint.x, localPoint.y, panelRect.localPosition.z);

        // Clamp para que no salga del canvas
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        ClampRectToCanvas(panelRect);
    }

    private void ClampRectToCanvas(RectTransform rect)
    {
        if (rect == null || inventoryCanvas == null) return;

        var corners = new Vector3[4];
        var canvasCorners = new Vector3[4];
        rect.GetWorldCorners(corners);
        (inventoryCanvas.transform as RectTransform).GetWorldCorners(canvasCorners);

        Vector3 pos = rect.localPosition;
        if (corners[2].x > canvasCorners[2].x) pos.x -= corners[2].x - canvasCorners[2].x;
        else if (corners[0].x < canvasCorners[0].x) pos.x += canvasCorners[0].x - corners[0].x;

        if (corners[1].y > canvasCorners[1].y) pos.y -= corners[1].y - canvasCorners[1].y;
        else if (corners[0].y < canvasCorners[0].y) pos.y += canvasCorners[0].y - corners[0].y;
        rect.localPosition = pos;
    }

    #endregion

    #region Gamepad Navigation

    /// <summary>
    /// Construye la grilla de navegacion con mando organizando los slots por columna,
    /// de arriba a abajo:
    ///   columna 0 => col1Above + goldenSlots + col1Below
    ///   columna 1 => col2Slots
    ///   columna 2 => col3Slots
    /// </summary>
    private void BuildNavGrid()
    {
        navGrid = new List<List<InventorySlot>>();

        var col0 = new List<InventorySlot>();
        col0.AddRange(col1AboveSlots);
        col0.AddRange(goldenSlots);
        col0.AddRange(col1BelowSlots);
        navGrid.Add(col0);

        navGrid.Add(new List<InventorySlot>(col2Slots));
        navGrid.Add(new List<InventorySlot>(col3Slots));
    }

    /// <summary>
    /// Procesa el input del mando para la navegacion por el inventario.
    /// Llamado cada frame desde Update() solo mientras el inventario este abierto.
    /// </summary>
    private void UpdateGamepadNavigation()
    {
        if (Gamepad.current == null || navGrid == null) return;

        joystickCooldownTimer -= Time.unscaledDeltaTime;

        int dCol = 0, dRow = 0;

        // D-pad: respuesta inmediata
        if (Gamepad.current.dpad.up.wasPressedThisFrame) dRow = -1;
        else if (Gamepad.current.dpad.down.wasPressedThisFrame) dRow = +1;
        else if (Gamepad.current.dpad.left.wasPressedThisFrame) dCol = -1;
        else if (Gamepad.current.dpad.right.wasPressedThisFrame) dCol = +1;

        // Joystick izquierdo: con cooldown para evitar scroll demasiado rapido
        if (dCol == 0 && dRow == 0 && joystickCooldownTimer <= 0f)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.y > 0.5f) { dRow = -1; joystickCooldownTimer = joystickRepeatCooldown; }
            else if (stick.y < -0.5f) { dRow = +1; joystickCooldownTimer = joystickRepeatCooldown; }
            else if (stick.x < -0.5f) { dCol = -1; joystickCooldownTimer = joystickRepeatCooldown; }
            else if (stick.x > 0.5f) { dCol = +1; joystickCooldownTimer = joystickRepeatCooldown; }
        }

        if (dCol != 0 || dRow != 0)
        {
            NavigateGamepadTo(navCol + dCol, navRow + dRow);
        }

        // Button North (Y / Triangulo): actua como clic sobre el slot enfocado
        if (Gamepad.current.buttonNorth.wasPressedThisFrame && focusedSlot != null)
        {
            focusedSlot.SimulateClick();
        }
    }

    /// <summary>
    /// Mueve el foco del mando a la celda (col, row) de la grilla de navegacion.
    /// Los indices se clampean a los limites validos.
    /// </summary>
    private void NavigateGamepadTo(int col, int row)
    {
        if (navGrid == null || navGrid.Count == 0) return;

        col = Mathf.Clamp(col, 0, navGrid.Count - 1);
        List<InventorySlot> column = navGrid[col];
        if (column.Count == 0) return;

        // Determinar direccion de movimiento vertical
        int dRow = (row > navRow) ? 1 : (row < navRow) ? -1 : 0;
        int dCol = (col > navCol) ? 1 : (col < navCol) ? -1 : 0;

        // Clonar posicion destino y buscar el proximo slot con item
        int targetRow = Mathf.Clamp(row, 0, column.Count - 1);

        // Si se cambia de columna, intentar mantener fila o buscar la mas cercana con item
        if (dCol != 0)
        {
            // Buscar desde la fila actual hacia arriba y abajo en la nueva columna
            targetRow = FindNearestOccupiedRow(column, navRow);
            if (targetRow < 0) return; // columna sin items, no navegar
        }
        else if (dRow != 0)
        {
            // Buscar en la direccion presionada, saltear vacios
            targetRow = FindNextOccupiedRow(column, navRow, dRow);
            if (targetRow < 0) return; // no hay mas slots con item en esa direccion
        }

        // Verificar que el destino final es el mismo que ya esta
        if (col == navCol && targetRow == navRow && focusedSlot == column[targetRow]) return;

        bool oldIsGolden = focusedSlot != null && goldenSlots.Contains(focusedSlot);
        if (focusedSlot != null) focusedSlot.SimulatePointerExit();

        navCol = col;
        navRow = targetRow;
        focusedSlot = column[navRow];

        bool newIsGolden = goldenSlots.Contains(focusedSlot);
        focusedSlot.SimulatePointerEnter(gamepadMode: true);

        if (oldIsGolden && newIsGolden)
        {
            InventoryAudioManager.Instance?.PlaySwitchGoldenSlotSound();
        }
    }

    /// <summary>Busca la fila ocupada mas cercana a 'fromRow' en ambas direcciones.</summary>
    private int FindNearestOccupiedRow(List<InventorySlot> column, int fromRow)
    {
        if (column == null || column.Count == 0) return -1;
        int clamped = Mathf.Clamp(fromRow, 0, column.Count - 1);
        if (column[clamped].HasItem) return clamped;

        for (int offset = 1; offset < column.Count; offset++)
        {
            int up = clamped - offset;
            int down = clamped + offset;
            if (up >= 0 && column[up].HasItem) return up;
            if (down < column.Count && column[down].HasItem) return down;
        }
        return -1;
    }

    /// <summary>Busca el siguiente slot con item desde 'fromRow' en la direccion 'dir' (+1 o -1).</summary>
    private int FindNextOccupiedRow(List<InventorySlot> column, int fromRow, int dir)
    {
        for (int i = fromRow + dir; i >= 0 && i < column.Count; i += dir)
        {
            if (column[i].HasItem) return i;
        }
        return -1; // no encontro slot con item en esa direccion
    }

    #endregion

    #region Helpers

    public Color GetHighlightColor() => highlightColor;

    #endregion

    #region Debugging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[InventoryUIManager] {message}"); break;
            case 2: Debug.LogWarning($"[InventoryUIManager] {message}"); break;
            case 3: Debug.LogError($"[InventoryUIManager] {message}"); break;
            default: Debug.Log($"[InventoryUIManager] {message}"); break;
        }
    }

    #endregion
}