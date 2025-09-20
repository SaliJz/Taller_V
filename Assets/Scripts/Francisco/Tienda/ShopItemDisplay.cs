using UnityEngine;

public class ShopItemDisplay : MonoBehaviour
{
    [Header("Item Data")]
    public ShopItem shopItemData;

    private ShopManager shopManager;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El item de la tienda no funcionará correctamente.");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (shopManager != null)
            {
                shopManager.UpdateCostBar(shopItemData.cost);
            }

            if (Input.GetKey(KeyCode.E))
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
            if (shopManager != null)
            {
                shopManager.UpdateCostBar(0);
            }
        }
    }
}