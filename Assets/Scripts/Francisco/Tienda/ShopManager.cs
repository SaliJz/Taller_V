using GameJolt.UI.Controllers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    public List<ShopItem> allShopItems;
    public List<ShopItem> safeRelics;
    public List<Pact> allPacts = new List<Pact>();
    public List<ShopItem> allAmulets;

    [Header("Gachapon Effect Pools")]
    public List<GachaponEffectData> allGachaponEffects = new List<GachaponEffectData>();
    private List<GachaponEffectData> availableGachaponEffects = new List<GachaponEffectData>();
    private List<GachaponEffectData> usedGachaponEffects = new List<GachaponEffectData>();

    [Header("Shop Generation Settings")]
    [Range(0f, 1f)] public float normalRarityWeight = 0.65f;
    [Range(0f, 1f)] public float rareRarityWeight = 0.25f;
    [Range(0f, 1f)] public float superRareRarityWeight = 0.1f;

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
    [SerializeField] private float lowHealthThreshold = 10f;
    [SerializeField] private Color lowHealthWarningColor = Color.yellow;
    private bool lowHealthWarningActive = false;

    [Header("Raycast Settings")]
    [SerializeField] private float raycastDistance = 5f;
    [SerializeField] private LayerMask shopItemLayer;

    [Header("Scene Restrictions")]
    public string[] restrictedPurchaseScenes = { "HUB", "TutorialCompleto", "TransitionLevel01" };

    [Header("Reroll Settings")]
    public int baseRerollCost = 10;

    [Header("Gamepad Virtual Cursor")]
    [SerializeField] private bool useVirtualCursor = true;

    [Header("Purchase Cooldown")]
    [SerializeField] private float purchaseCooldown = 0.5f;

    private readonly List<ShopItem> availableItems = new List<ShopItem>();
    private readonly List<ShopItem> spawnedItems = new List<ShopItem>();
    private readonly List<GameObject> _spawnedItemObjects = new List<GameObject>();
    private List<ShopItem> currentShopItems;
    private List<Transform> _shopSpawnLocations;

    private readonly Dictionary<ShopItem, bool> _pendingPurchaseWarning = new Dictionary<ShopItem, bool>();

    public static ShopManager Instance { get; private set; }

    private PlayerHealth playerHealth;
    private PlayerStatsManager playerStatsManager;
    private InventoryManager inventoryManager;
    private PlayerControlls playerControls;

    private float lastPurchaseTime;
    private bool forceGagans = false;
    private bool _amuletPurchasedInRun = false;
    private float merchantPriceModifier = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        playerControls = new PlayerControlls();
    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        playerControls.Disable();
    }

    private void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (shopUIPanel != null)
        {
            shopUIPanel.SetActive(false);
        }

        InitializeShopItemPools();
        InitializeGachaponEffectPool();
    }

    private void Update()
    {
        if (interactionMode == ShopInteractionMode.MouseHover && shopUIPanel != null && shopUIPanel.activeInHierarchy)
        {
            PositionUIPanel();
        }
    }

    public void InitializeShopItemPools()
    {
        availableItems.Clear();
        spawnedItems.Clear();
        if (allShopItems != null)
        {
            availableItems.AddRange(allShopItems);
        }
    }

    public void InitializeGachaponEffectPool()
    {
        availableGachaponEffects.Clear();
        usedGachaponEffects.Clear();

        if (allGachaponEffects != null && allGachaponEffects.Count > 0)
        {
            availableGachaponEffects.AddRange(allGachaponEffects);
            Debug.Log($"Pool de Gachapon inicializado con {availableGachaponEffects.Count} efectos disponibles.");
        }
        else
        {
            Debug.LogWarning("No hay efectos de Gachapon asignados en ShopManager.");
        }
    }

    public GachaponEffectData GetAvailableGachaponEffect(EffectRarity targetRarity)
    {
        if (availableGachaponEffects.Count == 0)
        {
            ResetGachaponEffectPool();
        }

        List<GachaponEffectData> rarityPool = availableGachaponEffects
            .Where(e => e.IsAvailableForRarity(targetRarity))
            .ToList();

        if (rarityPool.Count == 0)
        {
            EffectRarity[] fallbackOrder = { EffectRarity.Legendario, EffectRarity.Epico, EffectRarity.Raro, EffectRarity.Comun };

            foreach (var fallbackRarity in fallbackOrder)
            {
                if (fallbackRarity == targetRarity) continue;

                rarityPool = availableGachaponEffects
                    .Where(e => e.IsAvailableForRarity(fallbackRarity))
                    .ToList();

                if (rarityPool.Count > 0)
                {
                    Debug.LogWarning($"No hay efectos disponibles para rareza {targetRarity}. Usando rareza {fallbackRarity} como fallback.");
                    break;
                }
            }
        }

        if (rarityPool.Count == 0)
        {
            ResetGachaponEffectPool();
            rarityPool = availableGachaponEffects
                .Where(e => e.IsAvailableForRarity(targetRarity))
                .ToList();

            if (rarityPool.Count == 0)
            {
                rarityPool = availableGachaponEffects.ToList();
            }
        }

        if (rarityPool.Count > 0)
        {
            float totalWeight = rarityPool.Sum(e => e.poolProbability);
            float roll = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (GachaponEffectData effect in rarityPool)
            {
                currentWeight += effect.poolProbability;
                if (roll <= currentWeight)
                {
                    return effect;
                }
            }

            return rarityPool.Last();
        }

        Debug.LogError("No se pudo obtener ningún efecto de Gachapon. Asegúrate de que allGachaponEffects tenga elementos.");
        return null;
    }

    public void MarkGachaponEffectAsUsed(GachaponEffectData effect)
    {
        if (effect == null) return;

        if (availableGachaponEffects.Contains(effect))
        {
            availableGachaponEffects.Remove(effect);
            usedGachaponEffects.Add(effect);
            Debug.Log($"Efecto '{effect.effectName}' marcado como usado. Disponibles: {availableGachaponEffects.Count}, Usados: {usedGachaponEffects.Count}");
        }
    }

    private void ResetGachaponEffectPool()
    {
        availableGachaponEffects.Clear();
        availableGachaponEffects.AddRange(allGachaponEffects);
        usedGachaponEffects.Clear();
        Debug.Log($"Pool de Gachapon reiniciado. Todos los efectos están disponibles nuevamente ({availableGachaponEffects.Count} efectos).");
    }

    public int GetAvailableGachaponEffectsCount()
    {
        return availableGachaponEffects.Count;
    }

    public int GetUsedGachaponEffectsCount()
    {
        return usedGachaponEffects.Count;
    }

    public void ResetMerchantRunState()
    {
        _amuletPurchasedInRun = false;
        merchantPriceModifier = 1.0f;
    }

    public void DisableRemainingAmulets()
    {
        if (_spawnedItemObjects != null)
        {
            foreach (var obj in _spawnedItemObjects)
            {
                if (obj != null)
                {
                    ShopItemDisplay display = obj.GetComponent<ShopItemDisplay>();
                    if (display != null && display.shopItemData != null && display.shopItemData.isAmulet)
                    {
                        obj.SetActive(false);
                    }
                }
            }
        }
    }

    public void SetMerchantPriceModifier(float modifier)
    {
        merchantPriceModifier = modifier;
    }

    public void SpawnItemWithEffect(ShopItem item, Transform location, Transform parent)
    {
        GameObject itemPrefab = FindPrefabForItemData(item);
        if (itemPrefab != null)
        {
            StartCoroutine(SpawnItemSequence(itemPrefab, item, location, parent));
        }
    }

    private IEnumerator SpawnItemSequence(GameObject itemPrefab, ShopItem itemData, Transform location, Transform parent)
    {
        if (itemAppearanceEffectPrefab != null)
        {
            Instantiate(itemAppearanceEffectPrefab, location.position, Quaternion.identity, parent);
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            yield return new WaitForSeconds(0.1f);
        }

        GameObject spawnedItem = Instantiate(itemPrefab, location.position, Quaternion.identity, parent);
        _spawnedItemObjects.Add(spawnedItem);

        ShopItemDisplay display = spawnedItem.GetComponent<ShopItemDisplay>();
        if (display != null)
        {
            display.shopItemData = itemData;
        }

        spawnedItems.Add(itemData);
    }

    public void GenerateShopItems(List<Transform> spawnLocations, Transform parent)
    {
        _shopSpawnLocations = spawnLocations;
        currentShopItems = new List<ShopItem>();

        if (spawnLocations == null || spawnLocations.Count == 0 || availableItems.Count == 0)
        {
            return;
        }

        List<ShopItem> itemsToSpawn = new List<ShopItem>();

        Dictionary<ItemRarity, List<ShopItem>> itemsByRarity = new Dictionary<ItemRarity, List<ShopItem>>();
        foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
        {
            itemsByRarity[rarity] = availableItems.Where(item => item.rarity == rarity).ToList();
        }

        float totalWeight = normalRarityWeight + rareRarityWeight + superRareRarityWeight;
        int itemsToSelectCount = Mathf.Min(spawnLocations.Count, availableItems.Count);

        for (int i = 0; i < itemsToSelectCount; i++)
        {
            float roll = Random.Range(0f, totalWeight);
            ItemRarity selectedRarity = ItemRarity.Normal;

            if (roll <= normalRarityWeight)
            {
                selectedRarity = ItemRarity.Normal;
            }
            else if (roll <= normalRarityWeight + rareRarityWeight)
            {
                selectedRarity = ItemRarity.Raro;
            }
            else
            {
                selectedRarity = ItemRarity.SuperRaro;
            }

            if (itemsByRarity[selectedRarity].Count == 0)
            {
                if (itemsByRarity[ItemRarity.Normal].Count > 0)
                    selectedRarity = ItemRarity.Normal;
                else if (itemsByRarity[ItemRarity.Raro].Count > 0)
                    selectedRarity = ItemRarity.Raro;
                else if (itemsByRarity[ItemRarity.SuperRaro].Count > 0)
                    selectedRarity = ItemRarity.SuperRaro;
                else
                    break;
            }

            List<ShopItem> rarityPool = itemsByRarity[selectedRarity];
            if (rarityPool.Count > 0)
            {
                float itemTotalWeight = rarityPool.Sum(item => item.individualRarityWeight);
                float itemRoll = Random.Range(0f, itemTotalWeight);

                ShopItem itemData = null;
                float currentWeight = 0f;

                foreach (var item in rarityPool)
                {
                    currentWeight += item.individualRarityWeight;
                    if (itemRoll <= currentWeight)
                    {
                        itemData = item;
                        break;
                    }
                }

                if (itemData != null)
                {
                    itemsToSpawn.Add(itemData);

                    rarityPool.Remove(itemData);
                    availableItems.Remove(itemData);
                }
            }
        }

        for (int i = 0; i < itemsToSpawn.Count; i++)
        {
            ShopItem itemData = itemsToSpawn[i];
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

    public void DestroyCurrentItems()
    {
        foreach (GameObject obj in _spawnedItemObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        _spawnedItemObjects.Clear();
        spawnedItems.Clear();
    }

    public void RerollShop(List<Transform> spawnLocations, Transform parent)
    {
        if (playerHealth == null) return;

        float currentHealth = playerHealth.GetCurrentHealth();
        if (currentHealth < baseRerollCost)
        {
            if (inventoryManager != null) inventoryManager?.ShowWarningMessage("Vida insuficiente para hacer un 'Reroll'.");
            return;
        }

        playerHealth.TakeDamage(baseRerollCost, true);

        availableItems.AddRange(spawnedItems);

        DestroyCurrentItems();
        InitializeShopItemPools();
        GenerateShopItems(spawnLocations, parent);
    }

    private GameObject FindPrefabForItemData(ShopItem itemData)
    {
        if (shopItemPrefabs.Count > 0)
        {
            return shopItemPrefabs[Random.Range(0, shopItemPrefabs.Count)];
        }
        return null;
    }

    public void HandleMouseInteraction(ShopItem itemData)
    {
        LockAndDisplayItemDetails(itemData);
    }

    public bool CanAttemptPurchase()
    {
        return Time.time > lastPurchaseTime + purchaseCooldown;
    }

    public float CalculateFinalCost(float baseCost)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isRestrictedScene = restrictedPurchaseScenes.Contains(currentSceneName);
        if (isRestrictedScene)
        {
            return 0f;
        }

        float finalCost = baseCost * merchantPriceModifier;

        if (playerStatsManager != null)
        {
            float priceReduction = playerStatsManager.GetStat(StatType.ShopPriceReduction);

            if (priceReduction > 0f)
            {
                float discount = priceReduction / 100f;
                finalCost *= (1f - discount);
            }
        }

        return Mathf.Max(0f, finalCost);
    }

    public void DisplayItemUI(ShopItem itemData, float finalCost)
    {
        if (shopUIPanel != null)
        {
            shopUIPanel.SetActive(true);

            string nameWithColor = $"<color=#{ColorUtility.ToHtmlStringRGB(itemData.GetRarityColor())}>{itemData.itemName.ToUpper()}</color>";

            float baseCost = itemData.cost * merchantPriceModifier;
            string costText;

            if (playerStatsManager != null)
            {
                float priceReduction = playerStatsManager.GetStat(StatType.ShopPriceReduction);

                if (priceReduction > 0f && finalCost < baseCost)
                {
                    costText = $"<s>{Mathf.RoundToInt(baseCost)} HP</s> -> <color=#00FF00>{Mathf.RoundToInt(finalCost)} HP</color> (-{priceReduction:F0}%)";
                }
                else
                {
                    costText = (finalCost > 0) ? $"Costo: {Mathf.RoundToInt(finalCost)} HP" : "Costo: GRATIS";
                }
            }
            else
            {
                costText = (finalCost > 0) ? $"Costo: {Mathf.RoundToInt(finalCost)} HP" : "Costo: GRATIS";
            }

            itemNameText.text = nameWithColor;
            itemCostText.text = costText;
            itemDescriptionText.text = itemData.GetFormattedDescriptionAndStats();
        }
    }

    public void UpdateCostBar(float cost)
    {
        if (playerHealth == null || costBar == null || itemNameText == null || itemCostText == null) return;

        float currentHealth = playerHealth.GetCurrentHealth();
        float finalCost = CalculateFinalCost(cost);
        float fillAmount = 1f;

        if (finalCost > 0)
        {
            fillAmount = Mathf.Clamp01(currentHealth / finalCost);
        }

        costBar.fillAmount = fillAmount;
        costBar.color = (currentHealth > finalCost) ? affordableColor : unaffordableColor;

        if (currentHealth > finalCost)
        {
            if (currentHealth <= lowHealthThreshold && finalCost > 0)
            {
                if (!lowHealthWarningActive)
                {
                    itemNameText.color = lowHealthWarningColor;
                    itemCostText.color = lowHealthWarningColor;
                    lowHealthWarningActive = true;
                }
            }
            else if (lowHealthWarningActive)
            {
                itemNameText.color = Color.white;
                itemCostText.color = Color.white;
                lowHealthWarningActive = false;
            }
        }
        else
        {
            itemNameText.color = Color.white;
            itemCostText.color = unaffordableColor;
            lowHealthWarningActive = false;
        }
    }

    public void HideItemUI()
    {
        if (shopUIPanel != null)
        {
            shopUIPanel.SetActive(false);
        }
    }

    public bool PurchaseItem(ShopItem item)
    {
        if (playerStatsManager == null || playerHealth == null || inventoryManager == null) return false;

        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isRestrictedScene = restrictedPurchaseScenes.Contains(currentSceneName);

        float finalCost = CalculateFinalCost(item.cost);
        bool ignoreDrawbacks = false;

        if (isRestrictedScene)
        {
            finalCost = 0f;
            ignoreDrawbacks = true;
        }

        bool isRestrictedItem = isRestrictedScene || item.isAmulet;

        if (isRestrictedItem)
        {
            if (_amuletPurchasedInRun)
            {
                if (inventoryManager != null)
                {
                    string warningMessage = isRestrictedScene
                        ? "Solo puedes comprar un ítem por visita en esta zona."
                        : "Solo puedes comprar un amuleto por run.";

                    inventoryManager.ShowWarningMessage(warningMessage);
                }
                return false;
            }
        }

        float currentHealth = playerHealth.GetCurrentHealth();

        if (finalCost > 0 && currentHealth <= finalCost)
        {
            if (inventoryManager != null) inventoryManager.ShowWarningMessage("Vida insuficiente para la compra. ¡Debes sobrevivir!");
            return false;
        }

        float healthAfterPurchase = currentHealth - finalCost;

        if (healthAfterPurchase <= lowHealthThreshold && !isRestrictedScene)
        {
            if (!_pendingPurchaseWarning.ContainsKey(item))
            {
                if (inventoryManager != null) inventoryManager.ShowWarningMessage("¡Advertencia! Esta compra te dejará con muy poca vida. Pulsa de nuevo para confirmar.");
                _pendingPurchaseWarning.Add(item, true);
                return false;
            }
            else
            {
                _pendingPurchaseWarning.Remove(item);
            }
        }
        else
        {
            if (_pendingPurchaseWarning.ContainsKey(item))
            {
                _pendingPurchaseWarning.Remove(item);
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

        if (!ignoreDrawbacks)
        {
            foreach (var drawback in item.drawbacks)
            {
                float amount = drawback.amount;

                if (drawback.type != StatType.DamageTaken &&
                    drawback.type != StatType.KnockbackReceived &&
                    drawback.type != StatType.StaminaConsumption)
                {
                    amount *= -1f;
                }

                playerStatsManager.ApplyModifier(drawback.type, amount, isPercentage: drawback.isPercentage,
                                                 isTemporary: item.isTemporary, item.temporaryDuration,
                                                 isByRooms: item.isByRooms, item.temporaryRooms);
            }
        }

        foreach (var effect in item.behavioralEffects)
        {
            effect.ApplyEffect(playerStatsManager);
        }

        if (!item.isAmulet)
        {
            if (allShopItems.Contains(item))
            {
                allShopItems.Remove(item);
                availableItems.Remove(item);
            }
        }

        if (item.isAmulet || isRestrictedScene)
        {
            _amuletPurchasedInRun = true;
        }

        if (finalCost > 0)
        {
            playerHealth.TakeDamage(Mathf.RoundToInt(finalCost), true);
        }

        lastPurchaseTime = Time.time;

        if (GameJoltTrophy.Instance != null)
        {
            GameJoltTrophy.Instance.TrackItemPurchase();
        }

        if (isRestrictedScene)
        {
            DisableRemainingItems();
        }

        if (item.benefits.Any(b => b.type == StatType.ShieldBlockUpgrade))
        {
            playerHealth.EnableShieldBlockUpgrade();
        }

        return true;
    }

    public void DisableRemainingItems()
    {
        if (_spawnedItemObjects != null)
        {
            foreach (var obj in _spawnedItemObjects)
            {
                if (obj != null)
                {
                    if (obj.activeSelf)
                    {
                        obj.SetActive(false);
                    }
                }
            }
        }
    }

    public void SetDistortionActive(DevilDistortionType distortion, bool isCase)
    {
        if (distortion == DevilDistortionType.SealedLuck)
        {
            forceGagans = isCase;
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
        UpdateCostBar(itemData.cost);
    }

    public void SetInteractionPromptActive(bool active)
    {
        HUDManager.Instance.SetInteractionPrompt(active, "Interact", "COMPRAR");
    }

    public ShopItem GetRandomRewardItem()
    {
        if (allShopItems != null && allShopItems.Count > 0)
        {
            int index = Random.Range(0, allShopItems.Count);
            ShopItem selectedItem = allShopItems[index];

            return selectedItem;
        }

        if (allAmulets != null && allAmulets.Count > 0)
        {
            return allAmulets[Random.Range(0, allAmulets.Count)];
        }

        return null;
    }
}