using UnityEngine;
using UnityEngine.InputSystem;
using static ShopManager;

public class ShopItemDisplay : MonoBehaviour, PlayerControlls.IInteractionsActions
{
    public ShopItem shopItemData;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;
    private PlayerControlls playerControls;

    [Header("SFX Configuration")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSound;

    private bool isPlayerInProximity = false;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        merchantRoomManager = GetComponentInParent<MerchantRoomManager>();

        audioSource = merchantRoomManager.GetComponentInChildren<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogWarning("AudioSource no asignado en ShopItemDisplay. Se intentará obtenerlo del MerchantRoomManager.");
        }

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

        if (shopManager != null && isPlayerInProximity)
        {
            shopManager.SetInteractionPromptActive(false);
            shopManager.LockAndDisplayItemDetails(null);
            shopManager.ResetCostBar(); 
        }
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

    public void OnAdvanceDialogue(InputAction.CallbackContext context)
    {

    }

    public void DisplayFixedInfo()
    {
        if (shopManager == null) return;

        shopManager.LockAndDisplayItemDetails(shopItemData);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (shopManager != null && shopManager.InteractionMode != ShopInteractionMode.TriggerProximity)
            return;

        if (other.CompareTag("Player"))
        {
            isPlayerInProximity = true;

            if (shopManager != null)
            {
                shopManager.SetInteractionPromptActive(true);
                shopManager.LockAndDisplayItemDetails(shopItemData);
            }
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (shopManager != null && shopManager.InteractionMode != ShopInteractionMode.TriggerProximity)
            return;

        if (!other.CompareTag("Player")) return;

        if (shopManager != null)
        {
            shopManager.SetInteractionPromptActive(true);

            float finalCost = shopManager.CalculateFinalCost(shopItemData.cost);
            shopManager.UpdateCostBar(finalCost);
            shopManager.LockAndDisplayItemDetails(shopItemData);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (shopManager != null && shopManager.InteractionMode != ShopInteractionMode.TriggerProximity)
            return;

        if (other.CompareTag("Player"))
        {
            isPlayerInProximity = false;

            if (shopManager != null)
            {
                shopManager.SetInteractionPromptActive(false);
                shopManager.ResetCostBar();
                shopManager.LockAndDisplayItemDetails(null);
            }
        }
    }

    private void AttemptPurchase()
    {
        Debug.Log($"[ShopItemDisplay] Interacción detectada para: {shopItemData.itemName}.");
        if (shopManager.CanAttemptPurchase())
        {
            bool purchaseSuccessful = shopManager.PurchaseItem(shopItemData);

            if (shopManager != null)
            {
                if (purchaseSuccessful)
                {
                    shopManager.ResetCostBar(); 
                }
            }

            if (purchaseSuccessful)
            {
                if (merchantRoomManager != null)
                {
                    merchantRoomManager.OnItemPurchased();
                    if (audioSource != null && purchaseSound != null)
                    {
                        audioSource.PlayOneShot(purchaseSound);
                        Debug.Log($"<color=yellow>[ShopItemDisplay] Reproduciendo sonido de compra.</color>");
                    }
                }

                Destroy(gameObject);
            }
        }
    }
}