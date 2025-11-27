using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public enum ShopInteractionMode
    {
        MouseHover,
        TriggerProximity
    }

    [Header("Interaction Mode")]
    [SerializeField] private ShopInteractionMode interactionMode = ShopInteractionMode.MouseHover;
    public ShopInteractionMode InteractionMode => interactionMode;

    [Header("Item Pools")]
    public List<ShopItem> allRelics;
    public List<ShopItem> allGagans;
    public List<ShopItem> safeRelics;
    public List<Pact> allPacts = new List<Pact>();
    public List<ShopItem> allAmulets;

    [Header("Shop Prefabs")]
    public List<GameObject> shopItemPrefabs;
    public GameObject itemAppearanceEffectPrefab;

    [Header("UI References")]
    [SerializeField] private GameObject shopUIPanel;
    [SerializeField] private RectTransform uiPanelTransform;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemCostText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private Image costBar;

    [Header("Cost Bar Colors")]
    [SerializeField] private Color affordableColor = Color.green;
    [SerializeField] private Color unaffordableColor = Color.red;

    [Header("Purchase Risk Settings")] 
    [SerializeField] private float lowHealthThresholdPercentage = 20f;

    [Header("Raycast Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask shopItemLayer;
    [SerializeField] private float raycastDistance = 100f;

    [Header("Reroll Settings")]
    [SerializeField] private KeyCode reRollKey = KeyCode.R;
    [SerializeField] private bool canReroll = true;

    [Header("Gamepad Virtual Cursor")]
    [SerializeField] private RectTransform virtualCursorRect;

    [Header("Purchase Cooldown")]
    [SerializeField] private float purchaseCooldownTime = 0.5f; 
    private float _lastPurchaseAttemptTime = -999f;

    private readonly List<ShopItem> availableItems = new List<ShopItem>();
    private readonly List<ShopItem> spawnedItems = new List<ShopItem>();
    private readonly List<GameObject> _spawnedItemObjects = new List<GameObject>();
    private List<ShopItem> currentShopItems;
    private List<Transform> _shopSpawnLocations;

    private readonly Dictionary<ShopItem, bool> _pendingPurchaseWarning = new Dictionary<ShopItem, bool>();

    public static ShopManager Instance { get; private set; }

    private PlayerControlls playerControls;
    private PlayerStatsManager playerStatsManager;
    private PlayerHealth playerHealth;
    private InventoryManager inventoryManager;
    private ShopItem lastDetectedItem;
    private InventoryUIManager inventoryUIManager;

    private bool forceGagans = false;
    private bool _amuletPurchasedInRun = false;
    private float merchantPriceModifier = 1.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        playerControls = new PlayerControlls();

        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        inventoryManager = FindAnyObjectByType<InventoryManager>();
        inventoryUIManager = FindAnyObjectByType<InventoryUIManager>();

        if (playerStatsManager == null || playerHealth == null)
        {
            Debug.LogError("PlayerStatsManager o PlayerHealth no encontrados. La tienda no funcionará.");
        }

        if (inventoryManager == null)
        {
            Debug.LogError("InventoryManager no encontrado. El inventario no funcionará correctamente.");
        }

        if (inventoryUIManager == null)
        {
            Debug.LogError("InventoryUIManager no encontrado. La UI del inventario no funcionará correctamente.");
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (shopUIPanel != null) shopUIPanel.SetActive(false);
        InitializeShopItemPools();
    }

    private void OnEnable()
    {
        playerControls?.UI.Enable();
    }

    private void OnDisable()
    {
        playerControls?.UI.Disable();
    }

    private void Update()
    {
        if (interactionMode == ShopInteractionMode.MouseHover)
        {
            HandleMouseInteraction();
        }

        if (canReroll && Input.GetKeyDown(reRollKey))
        {
            RerollShop();
        }

        if (interactionMode == ShopInteractionMode.MouseHover)
        {
            PositionUIPanel();
        }
    }

    public void InitializeShopItemPools()
    {
        availableItems.Clear();
        spawnedItems.Clear();
        availableItems.AddRange(allRelics);
        availableItems.AddRange(allGagans);
    }

    public void ResetMerchantRunState()
    {
        _amuletPurchasedInRun = false;
    }

    public void DisableRemainingAmulets(ShopItem purchasedAmulet)
    {
        if (purchasedAmulet == null || !purchasedAmulet.isAmulet) return;

        List<GameObject> itemsToDestroy = new List<GameObject>();

        foreach (GameObject itemObject in _spawnedItemObjects)
        {
            if (itemObject != null)
            {
                ShopItemDisplay display = itemObject.GetComponent<ShopItemDisplay>();

                if (display != null && display.shopItemData.isAmulet && display.shopItemData != purchasedAmulet)
                {
                    itemsToDestroy.Add(itemObject);
                }
            }
        }

        foreach (GameObject item in itemsToDestroy)
        {
            if (item != null)
            {
                _spawnedItemObjects.Remove(item);
                Destroy(item);
            }
        }
    }

    public void SetMerchantPriceModifier(float modifier)
    {
        merchantPriceModifier = modifier;
        Debug.Log($"Modificador de precios del mercader actualizado: {modifier:F2}");
    }

    public IEnumerator SpawnItemWithEffect(ShopItem itemData, Transform spawnLocation, float effectDuration, Transform parent)
    {
        GameObject itemPrefab = FindPrefabForItemData(itemData);

        if (itemPrefab != null)
        {
            if (itemAppearanceEffectPrefab != null)
            {
                GameObject effect = Instantiate(itemAppearanceEffectPrefab, spawnLocation.position, Quaternion.identity);
                yield return new WaitForSeconds(effectDuration);
                Destroy(effect);
            }
            else
            {
                yield return null;
            }

            GameObject spawnedItem = Instantiate(itemPrefab, spawnLocation.position, Quaternion.identity, parent);

            _spawnedItemObjects.Add(spawnedItem);

            ShopItemDisplay display = spawnedItem.GetComponent<ShopItemDisplay>();
            if (display != null)
            {
                display.shopItemData = itemData;
            }

            spawnedItems.Add(itemData);
            currentShopItems.Add(itemData);
        }
        else
        {
            yield return null;
        }
    }

    public void GenerateShopItems(List<Transform> spawnLocations, Transform parent)
    {
        _shopSpawnLocations = spawnLocations;
        currentShopItems = new List<ShopItem>();

        if (spawnLocations == null || spawnLocations.Count == 0 || availableItems.Count < 3)
        {
            Debug.LogError("No hay suficientes ubicaciones de spawn o items para generar (se requieren 3).");
            return;
        }

        List<ShopItem> normalItems = new List<ShopItem>();
        List<ShopItem> drawbackItems = new List<ShopItem>();

        foreach (var item in availableItems)
        {
            if (item.drawbacks == null || item.drawbacks.Count == 0)
            {
                normalItems.Add(item);
            }
            else
            {
                drawbackItems.Add(item);
            }
        }

        List<ShopItem> itemsToSpawn = new List<ShopItem>();

        if (forceGagans)
        {
            if (drawbackItems.Count < 3)
            {
                itemsToSpawn.AddRange(drawbackItems);
                itemsToSpawn.AddRange(normalItems);
            }
            else
            {
                itemsToSpawn.AddRange(drawbackItems);
            }
        }
        else
        {
            if (normalItems.Count >= 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    int randomIndex = Random.Range(0, normalItems.Count);
                    itemsToSpawn.Add(normalItems[randomIndex]);
                    normalItems.RemoveAt(randomIndex);
                }
            }
            else
            {
                itemsToSpawn.AddRange(normalItems);
            }

            if (drawbackItems.Count >= 1)
            {
                int randomIndex = Random.Range(0, drawbackItems.Count);
                itemsToSpawn.Add(drawbackItems[randomIndex]);
            }
            else
            {
                if (normalItems.Count > 0)
                {
                    int randomIndex = Random.Range(0, normalItems.Count);
                    itemsToSpawn.Add(normalItems[randomIndex]);
                }
            }
        }

        List<ShopItem> finalSelection = itemsToSpawn
            .OrderBy(_ => Random.value)
            .Take(Mathf.Min(itemsToSpawn.Count, spawnLocations.Count))
            .ToList();

        for (int i = 0; i < finalSelection.Count; i++)
        {
            ShopItem itemData = finalSelection[i];
            GameObject itemPrefab = FindPrefabForItemData(itemData);

            if (itemPrefab != null)
            {
                GameObject spawnedItem = Instantiate(itemPrefab, spawnLocations[i].position, Quaternion.identity, parent);

                _spawnedItemObjects.Add(spawnedItem);

                ShopItemDisplay display = spawnedItem.GetComponent<ShopItemDisplay>();
                if (display != null)
                {
                    display.shopItemData = itemData;
                }

                spawnedItems.Add(itemData);
                currentShopItems.Add(itemData);
            }
        }
    }

    private void DestroyCurrentItems()
    {
        foreach (GameObject itemObject in _spawnedItemObjects)
        {
            if (itemObject != null)
            {
                Destroy(itemObject);
            }
        }
        _spawnedItemObjects.Clear();
    }

    public void RerollShop()
    {
        if (_shopSpawnLocations == null || _shopSpawnLocations.Count == 0)
        {
            Debug.LogWarning("No hay ubicaciones de spawn registradas para regenerar la tienda.");
            return;
        }

        DestroyCurrentItems();
        InitializeShopItemPools();
        GenerateShopItems(_shopSpawnLocations, null);

        Debug.Log("Tienda regenerada con éxito.");
    }

    public IEnumerator GenerateMerchantItems(List<Transform> spawnLocations, bool isFirstVisit, float effectDuration, bool sequentialSpawn, Transform parent)
    {
        ResetMerchantRunState();

        currentShopItems = new List<ShopItem>();
        List<ShopItem> itemsToSpawn = new List<ShopItem>();
        int maxItems = spawnLocations.Count;

        if (maxItems > 3) maxItems = 3;

        if (maxItems == 0)
        {
            Debug.LogWarning("No hay ubicaciones de spawn configuradas para el mercader o es cero.");
            yield break;
        }

        List<ShopItem> primaryPool = new List<ShopItem>();
        bool usedFallback = false;

        if (isFirstVisit)
        {
            if (safeRelics != null && safeRelics.Count > 0)
            {
                primaryPool.AddRange(safeRelics);
            }
            else
            {
                Debug.LogWarning("No hay SafeRelics disponibles en la primera visita. Usando Amuletos como alternativa.");
                if (allAmulets != null && allAmulets.Count > 0)
                {
                    primaryPool.AddRange(allAmulets);
                    usedFallback = true;
                }
            }
        }
        else
        {
            if (allAmulets != null)
            {
                primaryPool.AddRange(allAmulets);
            }
        }

        if (primaryPool.Count == 0 && !isFirstVisit)
        {
            Debug.LogWarning("No hay Amuletos disponibles para spawnear. Usando ítems por defecto (Reliquias).");
            if (allRelics != null)
            {
                primaryPool.AddRange(allRelics);
            }
        }
        else if (primaryPool.Count == 0 && isFirstVisit && safeRelics.Count == 0 && !usedFallback)
        {
            Debug.LogWarning("No hay SafeRelics ni Amuletos disponibles en la primera visita. No se generará ningún ítem.");
            yield break;
        }

        if (isFirstVisit && !usedFallback)
        {
            for (int i = 0; i < maxItems && i < primaryPool.Count; i++)
            {
                itemsToSpawn.Add(primaryPool[i]);
            }
        }
        else
        {
            List<ShopItem> dynamicPoolCopy = new List<ShopItem>(primaryPool);

            for (int i = 0; i < maxItems && dynamicPoolCopy.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, dynamicPoolCopy.Count);
                itemsToSpawn.Add(dynamicPoolCopy[randomIndex]);
                dynamicPoolCopy.RemoveAt(randomIndex);
            }
        }


        List<Coroutine> itemSpawnCoroutines = new List<Coroutine>();

        for (int i = 0; i < itemsToSpawn.Count && i < spawnLocations.Count; i++)
        {
            if (sequentialSpawn)
            {
                yield return StartCoroutine(SpawnItemWithEffect(itemsToSpawn[i], spawnLocations[i], effectDuration, parent));
            }
            else
            {
                itemSpawnCoroutines.Add(StartCoroutine(SpawnItemWithEffect(itemsToSpawn[i], spawnLocations[i], effectDuration, parent)));
            }
        }

        if (!sequentialSpawn)
        {
            foreach (Coroutine coroutine in itemSpawnCoroutines)
            {
                yield return coroutine;
            }
        }
    }

    private GameObject FindPrefabForItemData(ShopItem itemData)
    {
        foreach (GameObject prefab in shopItemPrefabs)
        {
            ShopItemDisplay display = prefab.GetComponent<ShopItemDisplay>();
            if (display != null && display.shopItemData != null && display.shopItemData == itemData)
            {
                return prefab;
            }
        }
        Debug.LogWarning($"Prefab no encontrado para el ítem: {itemData.itemName}");
        return null;
    }

    private void HandleMouseInteraction()
    {
        if (mainCamera == null || playerControls == null) return;
        if (inventoryUIManager != null && inventoryUIManager.IsInventoryOpen)
        {
            if (shopUIPanel != null && shopUIPanel.activeSelf)
            {
                HideItemUI();
            }
            return;
        }
        Vector2 screenPosition;
        Canvas parentCanvas = uiPanelTransform.GetComponentInParent<Canvas>();
        bool isGamepadActive = Gamepad.current != null && virtualCursorRect != null && virtualCursorRect.gameObject.activeInHierarchy;
        if (isGamepadActive)
        {
            Camera usedCamera = (parentCanvas != null && (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera || parentCanvas.renderMode == RenderMode.WorldSpace))
                                ? mainCamera
                                : null;
            screenPosition = RectTransformUtility.WorldToScreenPoint(usedCamera, virtualCursorRect.position);
        }
        else
        {
            screenPosition = playerControls.UI.Point.ReadValue<Vector2>();
        }
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        bool hitShopItem = false;

        if (Physics.Raycast(ray, out hit, raycastDistance, shopItemLayer))
        {
            ShopItemDisplay itemDisplay = hit.collider.GetComponent<ShopItemDisplay>();
            if (itemDisplay != null)
            {
                hitShopItem = true;
                float finalCost = CalculateFinalCost(itemDisplay.shopItemData.cost);
                if (lastDetectedItem != itemDisplay.shopItemData)
                {
                    if (lastDetectedItem != null && _pendingPurchaseWarning.ContainsKey(lastDetectedItem))
                    {
                        _pendingPurchaseWarning[lastDetectedItem] = false;
                    }
                    DisplayItemUI(itemDisplay.shopItemData, finalCost);
                    UpdateCostBar(finalCost);
                    lastDetectedItem = itemDisplay.shopItemData;
                }
            }
        }
        if (!hitShopItem && shopUIPanel != null && shopUIPanel.activeSelf)
        {
            HideItemUI();
            UpdateCostBar(0);
            if (lastDetectedItem != null && _pendingPurchaseWarning.ContainsKey(lastDetectedItem))
            {
                _pendingPurchaseWarning[lastDetectedItem] = false;
            }
            lastDetectedItem = null;
        }
    }

    public bool CanAttemptPurchase()
    {
        if (Time.time > _lastPurchaseAttemptTime + purchaseCooldownTime)
        {
            _lastPurchaseAttemptTime = Time.time; 
            return true;
        }
        return false;
    }

    public float CalculateFinalCost(float baseCost)
    {
        if (playerStatsManager == null) return baseCost;

        float priceReductionPercentage = playerStatsManager.GetCurrentStat(StatType.ShopPriceReduction);
        float discountFactor = Mathf.Clamp01(priceReductionPercentage / 100f);

        float finalCost = baseCost * (1f - discountFactor);

        finalCost *= merchantPriceModifier;

        finalCost = Mathf.Max(0f, finalCost);

        return finalCost;
    }

    public void DisplayItemUI(ShopItem itemData, float finalCost)
    {
        if (shopUIPanel != null)
        {
            shopUIPanel.SetActive(true);
            itemNameText.text = $"Nombre: {itemData.itemName}";
            itemCostText.text = $"Costo: {Mathf.RoundToInt(finalCost)} HP";
            itemDescriptionText.text = $"Descripción: {itemData.description}";
        }
    }

    public void UpdateCostBar(float cost)
    {
        if (costBar == null || playerHealth == null) return;

        float finalCost = cost;

        float currentHealth = GetPlayerCurrentHealth();
        float maxHealth = GetPlayerMaxHealth();

        bool shouldDisplay = finalCost > 0 && maxHealth > 0;
        costBar.gameObject.SetActive(shouldDisplay);
        if (!shouldDisplay) return;

        RectTransform costBarRect = costBar.GetComponent<RectTransform>();
        float parentWidth = costBarRect.parent.GetComponent<RectTransform>().rect.width;

        float currentHealthRatio = Mathf.Clamp(currentHealth / maxHealth, 0f, 1f);
        float costRatio = Mathf.Clamp(finalCost / maxHealth, 0f, 1f);

        if (currentHealth > finalCost)
        {
            costBarRect.localScale = new Vector3(costRatio, costBarRect.localScale.y, costBarRect.localScale.z);

            costBarRect.anchoredPosition = new Vector2(currentHealthRatio * parentWidth, costBarRect.anchoredPosition.y);

            costBar.color = affordableColor;
        }
        else
        {
            costBarRect.localScale = new Vector3(costRatio, costBarRect.localScale.y, costBarRect.localScale.z);

            costBarRect.anchoredPosition = new Vector2(costRatio * parentWidth, costBarRect.anchoredPosition.y);

            costBar.color = unaffordableColor;
        }
    }

    public void HideItemUI()
    {
        if (shopUIPanel != null)
        {
            shopUIPanel.SetActive(false);
        }
    }

    public float GetPlayerCurrentHealth()
    {
        return playerHealth.GetCurrentHealth();
    }

    public float GetPlayerMaxHealth()
    {
        return playerHealth.GetMaxHealth();
    }

    public bool PurchaseItem(ShopItem item)
    {
        if (playerStatsManager == null || playerHealth == null || inventoryManager == null) return false;

        if (item.isAmulet)
        {
            if (_amuletPurchasedInRun)
            {
                inventoryManager.ShowWarningMessage("Solo puedes comprar un amuleto por visita/run en el tutorial.");
                return false;
            }
        }

        float finalCost = CalculateFinalCost(item.cost);
        float currentHealth = playerHealth.GetCurrentHealth();

        if (currentHealth <= finalCost)
        {
            if (inventoryManager != null)
            {
                inventoryManager.ShowWarningMessage("Vida insuficiente para la compra. ¡Debes sobrevivir!");
            }
            Debug.LogWarning($"No se puede comprar {item.itemName}. Vida actual ({currentHealth}) es menor o igual que el costo final ({finalCost}).");
            return false;
        }

        if (inventoryManager.GetCurrentItemCount() >= InventoryManager.MaxInventorySize)
        {
            inventoryManager.ShowWarningMessage("Inventario lleno.");
            return false;
        }

        float healthAfterPurchase = currentHealth - finalCost;
        float maxHealth = playerHealth.GetMaxHealth();
        float lowHealthThreshold = maxHealth * (lowHealthThresholdPercentage / 100f);

        _pendingPurchaseWarning.TryAdd(item, false);
        bool warningActive = _pendingPurchaseWarning[item];

        if (healthAfterPurchase <= lowHealthThreshold) 
        {
            if (!warningActive)
            {
                inventoryManager.ShowWarningMessage("¡ES DEMASIADO ARRIESGADO! La compra te dejaría con muy poca vida. Presiona [E] de nuevo para confirmar.");
                _pendingPurchaseWarning[item] = true;
                Debug.Log($"Advertencia de bajo riesgo para {item.itemName}. Primer intento bloqueado.");
                return false;
            }
            else
            {
                Debug.Log($"Advertencia de bajo riesgo anulada para {item.itemName}. Compra procesada.");
                _pendingPurchaseWarning[item] = false;
            }
        }
        else
        {
            if (warningActive)
            {
                _pendingPurchaseWarning[item] = false;
            }
        }

        if (!inventoryManager.TryAddItem(item))
        {
            return false;
        }

        foreach (var benefit in item.benefits)
        {
            playerStatsManager.ApplyModifier(benefit.type, benefit.amount, isPercentage: benefit.isPercentage, 
                                             isTemporary: item.isTemporary, item.temporaryDuration, 
                                             isByRooms: item.isByRooms, item.temporaryRooms);
        }

        foreach (var drawback in item.drawbacks)
        {
            playerStatsManager.ApplyModifier(drawback.type, drawback.amount, isPercentage: drawback.isPercentage,
                                             isTemporary: item.isTemporary, item.temporaryDuration, 
                                             isByRooms: item.isByRooms, item.temporaryRooms);
        }

        foreach (var effect in item.behavioralEffects)
        {
            effect.ApplyEffect(playerStatsManager);

            if (item.isAmulet)
            {
                inventoryManager.AddActiveAmuletEffect(effect);
            }
        }

        Pact purchasedPactReference = allPacts.Find(pact => pact.pactName == item.itemName);

        if (purchasedPactReference != null)
        {
            allPacts.Remove(purchasedPactReference);
            Debug.Log($"Pacto comprado ({item.itemName}) removido de la lista de Pactos disponibles. Quedan {allPacts.Count}.");
        }

        if (item.isAmulet)
        {
            _amuletPurchasedInRun = true;
            DisableRemainingAmulets(item);
        }
        else
        {
            DevilManipulationManager.Instance?.RelicAcquired();
        }

        playerHealth.TakeDamage(Mathf.RoundToInt(finalCost), true);
        Debug.Log($"Compra exitosa de {item.itemName}. Se ha restado {finalCost:F2} de vida (Costo base: {item.cost}).");

        if (item.benefits.Exists(b => b.type == StatType.ShieldBlockUpgrade))
        {
            playerHealth.EnableShieldBlockUpgrade();
        }

        return true;
    }

    public void SetDistortionActive(DevilDistortionType distortion, bool isCase)
    {
        if (distortion == DevilDistortionType.SealedLuck)
        {
            forceGagans = isCase;
            Debug.Log($"[Distorsión] SealedLuck: {isCase}.");
        }
    }

    private void PositionUIPanel()
    {
        if (uiPanelTransform == null || uiPanelTransform.parent == null) return;

        Vector2 mousePosition = playerControls.UI.Point.ReadValue<Vector2>();
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiPanelTransform.parent.GetComponent<RectTransform>(),
            mousePosition,
            null,
            out localPoint))
        {
            uiPanelTransform.anchoredPosition = localPoint;
        }
    }

    public void LockAndDisplayItemDetails(ShopItem itemData)
    {
        if (itemData == null)
        {
            if (shopUIPanel != null)
            {
                shopUIPanel.SetActive(false);
            }
            return;
        }

        float finalCost = CalculateFinalCost(itemData.cost);
        DisplayItemUI(itemData, finalCost);
    }

    public void SetInteractionPromptActive(bool active)
    {
        HUDManager.Instance?.SetInteractionPrompt(active, "[E] COMPRAR");
    }
}