using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.InputSystem;

public class MerchantRoomManager : MonoBehaviour
{
    #region Editor Fields

    [Header("Item Settings")]
    [SerializeField] private float itemEffectDuration = 0.5f;
    [SerializeField] private bool sequentialItemSpawn = false;

    #endregion

    #region Private Dependencies

    private ShopManager shopManager;
    private PlayerStatsManager playerStatsManager;
    private PlayerHealth playerHealth;
    private Sala6MerchantFlow sala6Flow;
    private PlayerControlls playerControls;
    private MerchantDialogHandler dialogHandler;
    private MerchantInteractionTrigger currentTrigger;

    #endregion

    #region State & Constants

    public bool isFirstVisit { get; private set; }
    public bool playerIsNearMerchant { get; private set; } = false;
    public bool hasTakenPact = false;

    private bool hasAcceptedPactInCurrentInteraction = false;
    private const string FirstVisitKey = "MerchantFirstVisit";
    private Pact currentPactOffer;
    private float currentPriceModifier = 1.0f;

    #endregion

    #region Unity & Input Methods

    private void Awake()
    {
        playerControls = new PlayerControlls();

        shopManager = FindAnyObjectByType<ShopManager>();
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        sala6Flow = FindAnyObjectByType<Sala6MerchantFlow>();
        dialogHandler = FindAnyObjectByType<MerchantDialogHandler>();
        isFirstVisit = PlayerPrefs.GetInt(FirstVisitKey, 1) == 1;

        if (dialogHandler != null)
        {
            dialogHandler.ChangeRoomMerchant(this);
            dialogHandler.ResetMerchantState();
        }
    }

    private void OnEnable()
    {
        if (playerControls?.Interactions != null)
        {
            playerControls.Interactions.Enable();
            playerControls.Interactions.Interact.performed += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        if (playerControls?.Interactions != null)
        {
            playerControls.Interactions.Interact.performed -= OnInteractPerformed;
            playerControls.Interactions.Disable();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (playerIsNearMerchant && DialogManager.Instance != null && !DialogManager.Instance.IsActive)
        {
            if (dialogHandler != null)
            {
                dialogHandler.StartMerchantInteraction();

                if (currentTrigger != null)
                {
                    currentTrigger.HidePactPrompt();
                }

                if (sala6Flow != null)
                {
                    sala6Flow.HandleTriggerEnter();
                }
            }
        }
    }

    #endregion

    #region Initialization & Trigger Logic

    public void InitializeMerchantRoom(List<Transform> spawnLocations, Transform parent)
    {
        StartCoroutine(GenerateItemsAndSetDialogue(spawnLocations, parent));
    }

    private IEnumerator GenerateItemsAndSetDialogue(List<Transform> spawnLocations, Transform parent)
    {
        if (shopManager != null)
        {
            yield return StartCoroutine(shopManager.GenerateMerchantItems(spawnLocations, isFirstVisit, itemEffectDuration, sequentialItemSpawn, parent));
        }
    }

    public void CompleteFirstVisit()
    {
        if (isFirstVisit)
        {
            isFirstVisit = false;
            PlayerPrefs.SetInt(FirstVisitKey, 0);
            PlayerPrefs.Save();
        }
    }

    public void OnPlayerEnterMerchantTrigger(MerchantInteractionTrigger trigger)
    {
        playerIsNearMerchant = true;
        if (dialogHandler != null) dialogHandler.ChangeRoomMerchant(this);
        currentTrigger = trigger;
    }

    public void OnPlayerExitMerchantTrigger()
    {
        playerIsNearMerchant = false;
        if (dialogHandler != null) dialogHandler.ChangeRoomMerchant(null);
        currentTrigger = null;
    }

    #endregion

    #region Pact & Shop Logic

    public void OnItemPurchased()
    {
        if (sala6Flow != null)
        {
            sala6Flow.HandleItemPurchased();
        }
    }

    public Pact GenerateRandomPact()
    {
        if (shopManager != null && shopManager.allPacts != null && shopManager.allPacts.Count > 0)
        {
            int randomIndex = Random.Range(0, shopManager.allPacts.Count);
            return shopManager.allPacts[randomIndex];
        }

        Pact defaultPact = ScriptableObject.CreateInstance<Pact>();
        defaultPact.lifeRecoveryAmount = 20;
        defaultPact.drawbacks = new List<StatModifier>();
        return defaultPact;
    }

    public void SetCurrentPactOffer(Pact pact)
    {
        currentPactOffer = pact;
    }

    public Pact GetCurrentPactOffer()
    {
        return currentPactOffer;
    }

    public void OnAcceptPact()
    {
        if (hasAcceptedPactInCurrentInteraction) return;
        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth) return;

        if (currentPactOffer == null) currentPactOffer = GenerateRandomPact();

        ApplyPactEffects(currentPactOffer);
        hasTakenPact = true;
        hasAcceptedPactInCurrentInteraction = true;
    }

    public void ClearCurrentPact()
    {
        currentPactOffer = null;
    }

    private void ApplyPactEffects(Pact pactData)
    {
        if (playerStatsManager == null || playerHealth == null) return;

        InventoryManager inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (pactData.lifeRecoveryAmount > 0)
        {
            playerHealth.Heal(pactData.lifeRecoveryAmount);
        }

        foreach (var drawback in pactData.drawbacks)
        {
            float finalAmount = drawback.amount;

            playerStatsManager.ApplyModifier(drawback.type, finalAmount, isTemporary: false, isPercentage: drawback.isPercentage);
        }

        if (pactData.removeRandomRelic)
        {
            if (inventoryManager != null)
            {
                inventoryManager.RemoveRandomRelic();
            }
        }
    }

    public void SetPriceModifier(float modifier)
    {
        currentPriceModifier = modifier;

        if (shopManager != null)
        {
            shopManager.SetMerchantPriceModifier(modifier);
        }
    }

    public float GetPriceModifier()
    {
        return currentPriceModifier;
    }

    #endregion
}