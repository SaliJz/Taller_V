using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
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

    [Header("Raycast Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask shopItemLayer;
    [SerializeField] private float raycastDistance = 100f;

    [Header("Reroll Settings")]
    [SerializeField] private KeyCode reRollKey = KeyCode.R;
    [SerializeField] private bool canReroll = true;

    private readonly List<ShopItem> availableItems = new List<ShopItem>();
    private readonly List<ShopItem> spawnedItems = new List<ShopItem>();
    private readonly List<GameObject> _spawnedItemObjects = new List<GameObject>();
    private List<ShopItem> currentShopItems;
    private List<Transform> _shopSpawnLocations;

    private PlayerStatsManager playerStatsManager;
    private PlayerHealth playerHealth;
    private InventoryManager inventoryManager;
    private ShopItem lastDetectedItem;

    private bool _amuletPurchasedInRun = false;

    private void Awake()
    {
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (playerStatsManager == null || playerHealth == null)
        {
            Debug.LogError("PlayerStatsManager o PlayerHealth no encontrados. La tienda no funcionará.");
        }

        if (inventoryManager == null)
        {
            Debug.LogError("InventoryManager no encontrado. El inventario no funcionará correctamente.");
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (shopUIPanel != null) shopUIPanel.SetActive(false);
        InitializeShopItemPools();
    }

    private void Update()
    {
        HandleMouseInteraction();

        if (canReroll && Input.GetKeyDown(reRollKey))
        {
            RerollShop();
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

        for (int i = 0; i < itemsToSpawn.Count && i < spawnLocations.Count; i++)
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

        if (isFirstVisit)
        {
            // PRIMERA VISITA: Items seguros
            List<ShopItem> safeItemsCopy = new List<ShopItem>(safeRelics);

            for (int i = 0; i < maxItems && i < safeItemsCopy.Count; i++)
            {
                itemsToSpawn.Add(safeItemsCopy[i]);
            }
        }
        else
        {
            List<ShopItem> amuletsToSpawn = new List<ShopItem>(allAmulets);

            for (int i = 0; i < maxItems && amuletsToSpawn.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, amuletsToSpawn.Count);
                itemsToSpawn.Add(amuletsToSpawn[randomIndex]);
                amuletsToSpawn.RemoveAt(randomIndex); 
            }

            if (itemsToSpawn.Count == 0)
            {
                Debug.LogWarning("No hay amuletos disponibles para spawnear en la segunda visita. Usando ítems por defecto (Reliquias).");
                List<ShopItem> allItemsCopy = new List<ShopItem>(allRelics);

                for (int i = 0; i < maxItems && i < allItemsCopy.Count; i++)
                {
                    itemsToSpawn.Add(allItemsCopy[i]);
                }
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
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
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
                    DisplayItemUI(itemDisplay.shopItemData, finalCost);
                    lastDetectedItem = itemDisplay.shopItemData;
                }
                PositionUIPanel(Input.mousePosition);
            }
        }

        if (!hitShopItem && shopUIPanel != null && shopUIPanel.activeSelf)
        {
            HideItemUI();
            lastDetectedItem = null;
        }
    }

    public float CalculateFinalCost(float baseCost)
    {
        if (playerStatsManager == null) return baseCost;

        float priceReductionPercentage = playerStatsManager.GetCurrentStat(StatType.ShopPriceReduction);
        float discountFactor = Mathf.Clamp01(priceReductionPercentage / 100f);

        float finalCost = baseCost * (1f - discountFactor);

        finalCost = Mathf.Max(1f, finalCost);

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

        if (!inventoryManager.TryAddItem(item))
        {
            return false;
        }

        foreach (var benefit in item.benefits)
        {
            playerStatsManager.ApplyModifier(benefit.type, benefit.amount, isTemporary: false, isPercentage: benefit.isPercentage);
        }

        foreach (var drawback in item.drawbacks)
        {
            playerStatsManager.ApplyModifier(drawback.type, drawback.amount, isTemporary: false, isPercentage: drawback.isPercentage);
        }

        foreach (var effect in item.behavioralEffects)
        {
            effect.ApplyEffect(playerStatsManager);
        }

        if (item.isAmulet)
        {
            _amuletPurchasedInRun = true;
            DisableRemainingAmulets(item);
        }

        playerHealth.TakeDamage(Mathf.RoundToInt(finalCost), true);
        Debug.Log($"Compra exitosa de {item.itemName}. Se ha restado {finalCost:F2} de vida (Costo base: {item.cost}).");

        if (item.benefits.Exists(b => b.type == StatType.ShieldBlockUpgrade))
        {
            playerHealth.EnableShieldBlockUpgrade();
        }

        return true;
    }

    private void PositionUIPanel(Vector2 mousePosition)
    {
        if (uiPanelTransform == null || uiPanelTransform.parent == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiPanelTransform.parent.GetComponent<RectTransform>(),
            mousePosition,
            null,
            out localPoint
        );

        uiPanelTransform.anchoredPosition = localPoint;
    }
}