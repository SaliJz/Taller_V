using UnityEngine;

public class ShopItemDisplay : MonoBehaviour
{
    [Header("Item Data")]
    public ShopItem shopItemData;

    private ShopManager shopManager;
    private bool isPlayerInZone;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El item de la tienda no funcionará correctamente.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && isPlayerInZone)
        {
            if (shopManager != null)
            {
                shopManager.UpdateCostBar(shopItemData.cost);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                bool purchaseSuccessful = shopManager.PurchaseItem(shopItemData);
                if (shopManager != null)
                {
                    shopManager.UpdateCostBar(0);
                }
                if (purchaseSuccessful)
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = false;
            if (shopManager != null)
            {
                shopManager.UpdateCostBar(0);
            }
        }
    }
}