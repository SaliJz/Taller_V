using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [Header("Item Pools")]
    public List<ShopItem> allRelics;
    public List<ShopItem> allGagans;

    [Header("Shop Prefabs")]
    public List<GameObject> shopItemPrefabs;

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

    private readonly List<ShopItem> availableItems = new();
    private readonly List<ShopItem> spawnedItems = new();
    private List<ShopItem> currentShopItems;

    private PlayerStatsManager playerStatsManager;
    private PlayerHealth playerHealth;
    private ShopItem lastDetectedItem;

    private void Awake()
    {
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();

        if (playerStatsManager == null || playerHealth == null)
        {
            Debug.LogError("PlayerStatsManager o PlayerHealth no encontrados. La tienda no funcionará.");
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
    }

    public void InitializeShopItemPools()
    {
        availableItems.Clear();
        spawnedItems.Clear();
        availableItems.AddRange(allRelics);
        availableItems.AddRange(allGagans);
    }

    public void GenerateShopItems(List<Transform> spawnLocations)
    {
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
                GameObject spawnedItem = Instantiate(itemPrefab, spawnLocations[i].position, Quaternion.identity);
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
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastDistance, shopItemLayer))
        {
            ShopItemDisplay itemDisplay = hit.collider.GetComponent<ShopItemDisplay>();

            if (itemDisplay != null)
            {
                if (lastDetectedItem != itemDisplay.shopItemData)
                {
                    DisplayItemUI(itemDisplay.shopItemData, showCostBar: false);
                    lastDetectedItem = itemDisplay.shopItemData;
                }
                PositionUIPanel(Input.mousePosition);
            }
        }
        else
        {
            HideItemUI();
            lastDetectedItem = null;
        }
        Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.yellow);
    }

    public void DisplayItemUI(ShopItem itemData, bool showCostBar)
    {
        if (shopUIPanel != null)
        {
            shopUIPanel.SetActive(true);
            itemNameText.text = $"Nombre: {itemData.itemName}";
            itemCostText.text = $"Costo: {itemData.cost} HP";
            itemDescriptionText.text = $"Descripción: {itemData.description}";
        }
    }

    public void UpdateCostBar(float itemCost)
    {
        if (costBar != null)
        {
            float currentHealth = GetPlayerCurrentHealth();
            costBar.fillAmount = Mathf.Clamp01(itemCost / currentHealth);
            costBar.color = currentHealth >= itemCost ? affordableColor : unaffordableColor;
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

    public bool PurchaseItem(ShopItem item)
    {
        if (playerStatsManager == null || playerHealth == null) return false;

        float currentHealth = playerHealth.GetCurrentHealth();
        if (currentHealth < item.cost)
        {
            Debug.LogWarning($"No se puede comprar {item.itemName}. Vida actual ({currentHealth}) es menor que el costo ({item.cost}).");
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

        playerHealth.TakeDamage(item.cost);
        Debug.Log($"Compra exitosa de {item.itemName}. Se ha restado {item.cost} de vida.");

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