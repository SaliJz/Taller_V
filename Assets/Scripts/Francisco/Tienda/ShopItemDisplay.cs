using UnityEngine;
using UnityEngine.InputSystem;

public class ShopItemDisplay : MonoBehaviour, PlayerControlls.IInteractionsActions
{
    public GameObject textPanel;
    public ShopItem shopItemData;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;

    private PlayerControlls playerControls;
    private bool isPlayerInProximity = false;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        merchantRoomManager = GetComponentInParent<MerchantRoomManager>();

        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El item de la tienda no funcionará correctamente.");
        }

        playerControls = new PlayerControlls();
        playerControls.Interactions.SetCallbacks(this);
    }

    private void OnEnable()
    {
        playerControls?.Interactions.Enable();
    }

    private void OnDisable()
    {
        playerControls?.Interactions.Disable();

        if (textPanel != null) textPanel.SetActive(false);
        if (shopManager != null) shopManager.UpdateCostBar(0);
    }

    private void OnDestroy()
    {
        playerControls?.Dispose();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (isPlayerInProximity && shopManager != null)
        {
            AttemptPurchase();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInProximity = true;

            if (textPanel != null) textPanel.SetActive(true);
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
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInProximity = false; 

            if (textPanel != null) textPanel.SetActive(false);
            if (shopManager != null)
            {
                shopManager.UpdateCostBar(0);
            }
        }
    }

    private void AttemptPurchase()
    {
        if (shopManager.CanAttemptPurchase())
        {
            bool purchaseSuccessful = shopManager.PurchaseItem(shopItemData);

            if (shopManager != null)
            {
                if (purchaseSuccessful)
                {
                    shopManager.UpdateCostBar(0);
                }
            }

            if (purchaseSuccessful)
            {
                if (merchantRoomManager != null)
                {
                    merchantRoomManager.OnItemPurchased();
                }

                Destroy(gameObject);
            }
        }
    }
}