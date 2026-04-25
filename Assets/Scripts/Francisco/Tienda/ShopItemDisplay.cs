using UnityEngine;
using UnityEngine.InputSystem;
using static ShopManager;

public class ShopItemDisplay : MonoBehaviour, PlayerControlls.IInteractionsActions
{
    public ShopItem shopItemData;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;
    private PlayerControlls playerControls;
    private PlayerBlockSystem cachedBlockSystem;

    [Header("SFX Configuration")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSound;

    private bool isPlayerInProximity = false;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();

        merchantRoomManager = GetComponentInParent<MerchantRoomManager>();

        if (merchantRoomManager != null)
        {
            audioSource = merchantRoomManager.GetComponentInChildren<AudioSource>();
        }
        else
        {
            Debug.LogWarning("AudioSource no asignado en ShopItemDisplay. Se intentara obtenerlo del MerchantRoomManager.");
        }

        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El item de la tienda no funcionara correctamente.");
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

        if (cachedBlockSystem != null)
        {
            cachedBlockSystem.SetBlockingEnabled(true);
            cachedBlockSystem = null;
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

            cachedBlockSystem = other.GetComponent<PlayerBlockSystem>();
            if (cachedBlockSystem != null)
            {
                cachedBlockSystem.SetBlockingEnabled(false);
            }

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

            if (cachedBlockSystem != null)
            {
                cachedBlockSystem.SetBlockingEnabled(true);
                cachedBlockSystem = null;
            }

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
        Debug.Log($"[ShopItemDisplay] Interaccion detectada para: {shopItemData.itemName}.");
        if (!shopManager.CanAttemptPurchase()) return;

        bool purchaseSuccessful = shopManager.PurchaseItem(shopItemData);

        if (purchaseSuccessful)
        {
            OnPurchaseCompleted();
        }
        else if (shopManager.HasPendingReplacement)
        {
            isPlayerInProximity = false;
            shopManager.RegisterPendingPurchaseCallback(OnPurchaseCompleted);
            shopManager.RegisterPendingCancelCallback(OnPurchaseCancelled);
        }
    }

    private void OnPurchaseCancelled()
    {
        // El jugador canceló el reemplazo: reactivar para que pueda reintentar
        isPlayerInProximity = true;
    }

    private void OnPurchaseCompleted()
    {
        shopManager.ResetCostBar();

        if (merchantRoomManager != null)
        {
            merchantRoomManager.OnItemPurchased();
            if (audioSource != null && purchaseSound != null)
            {
                audioSource.PlayOneShot(purchaseSound);
            }
        }

        if (cachedBlockSystem != null)
        {
            cachedBlockSystem.SetBlockingEnabled(true);
            cachedBlockSystem = null;
        }

        Destroy(gameObject);
    }
}