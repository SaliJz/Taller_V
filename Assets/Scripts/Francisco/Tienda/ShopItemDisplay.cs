using UnityEngine;

public class ShopItemDisplay : MonoBehaviour
{
    public ShopItem shopItemData;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        merchantRoomManager = FindAnyObjectByType<MerchantRoomManager>();

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
                float finalCost = shopManager.CalculateFinalCost(shopItemData.cost);

                shopManager.UpdateCostBar(finalCost);
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
                    if (merchantRoomManager != null)
                    {
                        merchantRoomManager.OnItemPurchased();
                    }

                    GameObject objectToDestroy = (transform.parent != null)
                                               ? transform.parent.gameObject
                                               : gameObject;

                    Destroy(objectToDestroy);
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