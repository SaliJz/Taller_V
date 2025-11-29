using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MerchantDialogHandler : MonoBehaviour
{
    #region Dependencies

    [Header("Dependencies")]
    [SerializeField] private DialogManager dialogManager;
    [SerializeField] private MerchantRoomManager merchantManager;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button converseButton;

    #endregion

    #region Dialog Lines

    [Header("Dialog Lines")]
    [SerializeField] private DialogLine[] firstVisitLines;
    [SerializeField] private DialogLine[] routineLines;
    [SerializeField] private DialogLine[] pactOfferLines;
    [SerializeField] private DialogLine[] tooHealthyLines;
    [SerializeField] private DialogLine[] friendlyLines;
    [SerializeField] private DialogLine[] hostileLines;
    [SerializeField] private DialogLine[] blockedLine;
    [SerializeField] private DialogLine[] pactAcceptLines;
    [SerializeField] private DialogLine[] pactShowDetailsLines;

    #endregion

    #region Events & State

    [Header("Events")]
    [SerializeField] private UnityEvent OnDialogCompleteEvent;

    private bool isPactAvailable = false;
    private const float PactHealthThreshold = 0.70f;

    private int conversationCount = 0;
    private int reputation = 0;
    private bool isPactBlocked = false;
    private bool waitingForPactButtons = false;
    private MerchantMood currentMood = MerchantMood.Neutral;

    private enum MerchantMood { Neutral, Friendly, Hostile }

    #endregion

    #region Unity Methods

    private void Start()
    {
        if (dialogManager == null) dialogManager = DialogManager.Instance;
        if (merchantManager == null) merchantManager = FindAnyObjectByType<MerchantRoomManager>();
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();

        if (optionsPanel != null) optionsPanel.SetActive(false);

        acceptButton?.onClick.AddListener(OnAcceptPactOption);
        backButton?.onClick.AddListener(OnBackButton);
        converseButton?.onClick.AddListener(OnConverseOption);

        if (OnDialogCompleteEvent == null)
        {
            OnDialogCompleteEvent = new UnityEvent();
        }
        OnDialogCompleteEvent.AddListener(OnDialogComplete);
    }

    #endregion

    #region Core Interaction Flow

    public void StartMerchantInteraction()
    {
        if (dialogManager == null || merchantManager == null || playerHealth == null || dialogManager.IsActive) return;

        waitingForPactButtons = false;

        isPactAvailable = (playerHealth.CurrentHealth / playerHealth.MaxHealth) < PactHealthThreshold;

        DialogLine[] entryLines;
        bool isMerchantFlow = true;

        if (isPactBlocked)
        {
            entryLines = blockedLine;
        }
        else if (merchantManager.isFirstVisit)
        {
            entryLines = firstVisitLines;
        }
        else if (!isPactAvailable)
        {
            entryLines = tooHealthyLines;
        }
        else
        {
            Pact pactOffer = merchantManager.GenerateRandomPact();
            merchantManager.SetCurrentPactOffer(pactOffer);
            entryLines = pactOfferLines;
        }

        UnityEvent showOptionsEvent = new UnityEvent();
        showOptionsEvent.AddListener(OnEntryDialogFinished);
        dialogManager.StartDialog(entryLines, showOptionsEvent, isMerchantFlow);
    }

    private void OnEntryDialogFinished()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }

        bool canOfferPact = isPactAvailable && !merchantManager.hasTakenPact && !isPactBlocked;

        acceptButton?.gameObject.SetActive(canOfferPact);
        backButton?.gameObject.SetActive(true);

        if (converseButton != null)
        {
            converseButton.gameObject.SetActive(conversationCount < 5);
        }
    }

    #endregion

    #region Button Handlers

    public void OnAcceptPactOption()
    {
        Debug.Log("OnAcceptPactOption INICIO");
        optionsPanel?.SetActive(false);

        Pact currentPact = merchantManager.GetCurrentPactOffer();
        if (currentPact == null)
        {
            Debug.LogError("currentPact es NULL!");
            return;
        }

        merchantManager.OnAcceptPact();

        reputation += 1;
        UpdateMerchantMood();
        UpdateShopPrices();
        Debug.Log($"Reputación actual: {reputation}");

        string pactDetails = FormatPactDetails(currentPact);
        Debug.Log("Detalles del pacto: " + pactDetails);

        string characterName = pactAcceptLines.Length > 0 ? pactAcceptLines[0].CharacterName : "Mercader";
        Sprite profileSprite = pactAcceptLines.Length > 0 ? pactAcceptLines[0].ProfileImage : null;

        waitingForPactButtons = true;

        dialogManager.UpdateCurrentDialogText(pactDetails, characterName, profileSprite);

        Debug.Log("Esperando que termine de tipear para mostrar botón");
    }

    public void OnConverseOption()
    {
        if (converseButton == null) return; 

        optionsPanel?.SetActive(false);

        conversationCount++;

        if (conversationCount <= 3)
        {
            reputation += 2;
            Debug.Log($"Conversación {conversationCount}: +2 reputación");
        }
        else if (conversationCount == 4)
        {
            reputation -= 2;
            Debug.Log($"Conversación {conversationCount}: -2 reputación");
        }
        else if (conversationCount >= 5)
        {
            reputation -= 4;
            isPactBlocked = true;
            Debug.Log($"Conversación {conversationCount}: -4 reputación. PACTOS BLOQUEADOS");
        }

        UpdateMerchantMood();
        UpdateShopPrices();
        Debug.Log($"Reputación actual: {reputation}");

        DialogLine[] linesToUse;
        if (currentMood == MerchantMood.Hostile)
        {
            linesToUse = hostileLines;
        }
        else if (currentMood == MerchantMood.Friendly)
        {
            linesToUse = friendlyLines;
        }
        else
        {
            linesToUse = routineLines;
        }

        if (linesToUse != null && linesToUse.Length > 0)
        {
            DialogLine lineToShow = linesToUse[Random.Range(0, linesToUse.Length)];

            dialogManager.UpdateCurrentDialogText(
                lineToShow.Text,
                lineToShow.CharacterName,
                lineToShow.ProfileImage
            );
        }

        waitingForPactButtons = false;

        Debug.Log("Conversación iniciada, esperando tipeo");
    }

    private void OnDialogComplete()
    {
        if (waitingForPactButtons)
        {
            waitingForPactButtons = false;
            ShowBackButtonOnly();
        }
        else
        {
            ShowConversationButtons();
        }
    }

    private void ShowConversationButtons()
    {
        Debug.Log("ShowConversationButtons - Mostrando botones de conversación");
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            acceptButton?.gameObject.SetActive(false);

            if (converseButton != null)
            {
                converseButton.gameObject.SetActive(conversationCount < 5);
            }

            backButton?.gameObject.SetActive(true);
        }
    }

    public void OnBackButton()
    {
        optionsPanel?.SetActive(false);
        waitingForPactButtons = false;
        merchantManager?.ClearCurrentPact();
        dialogManager.ForceEndDialog();
    }

    public void ResetMerchantState()
    {
        conversationCount = 0;
        isPactBlocked = false;

        UpdateMerchantMood();
        UpdateShopPrices();

        if (merchantManager != null)
        {
            merchantManager.SetPriceModifier(CalculatePriceModifier());
        }
    }

    private void ShowBackButtonOnly()
    {
        Debug.Log("ShowBackButtonOnly - Mostrando botón Salir");
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            acceptButton?.gameObject.SetActive(false);

            if (converseButton != null)
            {
                converseButton.gameObject.SetActive(false);
            }

            backButton?.gameObject.SetActive(true);
        }
    }

    public void ChangeRoomMerchant(MerchantRoomManager refe) => merchantManager = refe;

    private void UpdateMerchantMood()
    {
        if (reputation >= 4)
        {
            currentMood = MerchantMood.Friendly;
        }
        else if (reputation <= -2)
        {
            currentMood = MerchantMood.Hostile;
        }
        else
        {
            currentMood = MerchantMood.Neutral;
        }

        Debug.Log($"Humor del mercader: {currentMood}");
    }

    private void UpdateShopPrices()
    {
        if (merchantManager != null)
        {
            float priceModifier = CalculatePriceModifier();
            merchantManager.SetPriceModifier(priceModifier);
            Debug.Log($"Modificador de precios: {priceModifier:F2}");
        }
    }

    private float CalculatePriceModifier()
    {
        if (reputation >= 6)
        {
            return 0.7f;
        }
        else if (reputation >= 4)
        {
            return 0.85f;
        }
        else if (reputation >= 2)
        {
            return 0.95f;
        }
        else if (reputation <= -6)
        {
            return 1.5f;
        }
        else if (reputation <= -4)
        {
            return 1.3f;
        }
        else if (reputation <= -2)
        {
            return 1.15f;
        }
        else
        {
            return 1.0f;
        }
    }

    #endregion

    #region Helpers

    private string FormatPactDetails(Pact pact)
    {
        string details = $"PACTO: {pact.pactName}\n";

        if (pact.lifeRecoveryAmount > 0)
        {
            details += $"BENEFICIO: Restaura {pact.lifeRecoveryAmount} PV\n";
        }
        else
        {
            details += "BENEFICIO: Ninguno\n";
        }

        details += "MALDICIÓN REVELADA:";

        if (pact.removeRandomRelic)
        {
            details += "Pierdes una Reliquia al azar\n";
        }

        foreach (var drawback in pact.drawbacks)
        {
            float displayAmount = drawback.isPercentage ? drawback.amount : drawback.amount;
            string amountText = drawback.isPercentage ? $"{displayAmount:F0}%" : $"{displayAmount}";
            details += $" {drawback.type} -{amountText}\n";
        }

        return details;
    }

    #endregion
}