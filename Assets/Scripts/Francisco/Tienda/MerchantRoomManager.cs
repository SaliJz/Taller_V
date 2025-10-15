using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class MerchantRoomManager : MonoBehaviour
{
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;

    [TextArea] public string[] firstVisitLines;
    [TextArea] public string[] routineLines;
    [TextArea] public string[] purchaseLines;

    [Header("Pact Settings")]
    [TextArea] public string[] pactOfferLines = { "Te interesa ganar algo?" };
    [TextArea] public string[] pactAcceptLines = { "Un alma valiente. Que la maldición te sea leve." };
    [TextArea] public string[] pactDeclineLines = { "Qué sabio. Vive y sé feliz... por ahora." };
    public KeyCode interactKey = KeyCode.E;

    public float itemEffectDuration = 0.5f;
    public bool sequentialItemSpawn = false;
    private bool hasAcceptedPactInCurrentInteraction = false;

    public float dialogueDisplayTime = 5.0f;
    private Coroutine dialogueToggleCoroutine;

    private ShopManager shopManager;
    private PlayerStatsManager playerStatsManager;
    private PlayerHealth playerHealth;
    private int currentDialogueIndex = 0;
    private bool isFirstVisit = true;
    private const string FirstVisitKey = "MerchantFirstVisit";

    private Transform merchantTransform;
    private bool playerIsNearMerchant = false;
    private bool pactOffered = false;
    private bool waitingForAccept = false;
    private bool hasTakenPact = false;
    private Pact currentPactOffer;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        isFirstVisit = PlayerPrefs.GetInt(FirstVisitKey, 1) == 1;

        if (shopManager == null || playerStatsManager == null || playerHealth == null)
        {
            Debug.LogError("ShopManager, PlayerStatsManager, o PlayerHealth no encontrado. El mercader no funcionará.");
        }

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    private void Start()
    {
        GameObject merchantObject = GameObject.FindWithTag("Merchant");
        if (merchantObject != null)
        {
            merchantTransform = merchantObject.transform;
        }
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf && Input.GetKeyDown(KeyCode.Space) && !pactOffered)
        {
            AdvanceDialogue();
            return;
        }

        if (playerIsNearMerchant && Input.GetKeyDown(interactKey))
        {
            if (waitingForAccept)
            {
                OnAcceptPact();
            }
            else if (!pactOffered && !hasTakenPact)
            {
                OfferPact();
            }
        }
    }

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

    public void OnItemPurchased()
    {
        SetDialogue(purchaseLines);
        currentDialogueIndex = 0;
        ShowCurrentDialogueLine();
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

    public void AdvanceDialogue()
    {
        string[] currentLines = GetCurrentDialogueLines();

        if (dialoguePanel.activeSelf)
        {
            currentDialogueIndex = (currentDialogueIndex + 1);

            if (currentDialogueIndex >= currentLines.Length)
            {
                HideDialogue();
                currentDialogueIndex = 0;
                return;
            }
        }
        else
        {
            currentDialogueIndex = 0;
        }

        if (currentLines.Length > 0 && currentLines != purchaseLines)
        {
            SetDialogue(currentLines);
            ShowCurrentDialogueLine();
        }
    }

    private string[] GetCurrentDialogueLines()
    {
        if (pactOffered) return new string[] { };
        if (isFirstVisit) return firstVisitLines;
        return routineLines;
    }

    private void SetDialogue(string[] dialogueLines)
    {
        if (dialoguePanel == null || dialogueText == null || dialogueLines == null || dialogueLines.Length == 0) return;

        dialoguePanel.SetActive(true);
        currentDialogueIndex %= dialogueLines.Length;

        string dialogueToShow = dialogueLines[currentDialogueIndex];

        dialogueText.text = dialogueToShow;
    }

    private void ShowCurrentDialogueLine()
    {
        if (dialoguePanel == null) return;

        dialoguePanel.SetActive(true);

        if (dialogueToggleCoroutine != null)
        {
            StopCoroutine(dialogueToggleCoroutine);
        }

        if (!pactOffered)
        {
            dialogueToggleCoroutine = StartCoroutine(TogglePanelAfterDelay());
        }
    }

    private IEnumerator TogglePanelAfterDelay()
    {
        yield return new WaitForSeconds(dialogueDisplayTime);

        if (!pactOffered)
        {
            HideDialogue();
        }
        else if (pactOffered && hasTakenPact)
        {
            HideDialogue();
            pactOffered = false;
            waitingForAccept = false;
            SendMessage("HidePactPrompt", SendMessageOptions.DontRequireReceiver);
        }
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dialogueToggleCoroutine != null)
        {
            StopCoroutine(dialogueToggleCoroutine);
            dialogueToggleCoroutine = null;
        }
    }

    public void OnPlayerEnterMerchantTrigger(Transform merchantT)
    {
        playerIsNearMerchant = true;
        merchantTransform = merchantT;
        pactOffered = false;
        waitingForAccept = false;

        HidePactUI();

        if (!hasTakenPact)
        {
            SetDialogue(isFirstVisit ? firstVisitLines : routineLines);
            currentDialogueIndex = 0;
            ShowCurrentDialogueLine();
        }
    }

    public void OnPlayerExitMerchantTrigger()
    {
        playerIsNearMerchant = false;

        SendMessage("HidePactPrompt", SendMessageOptions.DontRequireReceiver);

        HidePactUI();
        HideDialogue();
    }

    private void OfferPact()
    {
        if (hasTakenPact) return;

        if (shopManager != null && shopManager.allPacts != null && shopManager.allPacts.Count == 0)
        {
            return;
        }

        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            SetDialogue(new string[] { "Pareces estar en óptimas condiciones. No necesito tu alma por ahora." });
            currentDialogueIndex = 0;
            ShowCurrentDialogueLine();
            return;
        }

        if (dialogueToggleCoroutine != null)
        {
            StopCoroutine(dialogueToggleCoroutine);
            dialogueToggleCoroutine = null;
        }
        HideDialogue();

        currentPactOffer = GenerateRandomPact();

        DisplayPactOffer();
        pactOffered = true;
        waitingForAccept = true;

        SendMessage("ShowPactPrompt", SendMessageOptions.DontRequireReceiver);
    }

    private void DisplayPactOffer()
    {
        if (dialoguePanel == null || dialogueText == null) return;

        dialoguePanel.SetActive(true);

        string offerMessage = pactOfferLines.Length > 0 ? pactOfferLines[0] : "Te interesa ganar algo?";

        string benefit = currentPactOffer.lifeRecoveryAmount > 0
                         ? $"Curación +{currentPactOffer.lifeRecoveryAmount}"
                         : "Ninguno";

        string fullPactMessage = $"{offerMessage}\n\n" +
                                 $"PACTO: {currentPactOffer.pactName}\n" +
                                 $"VIDA QUE RECIBIRÁS: {benefit}\n" +
                                 $"MALDICIÓN (COSTO): ?\n\n" +
                                 $"Presiona '{interactKey}' para ACEPTAR este pacto";

        dialogueText.text = fullPactMessage;
    }

    private void DisplayPactConfirmation()
    {
        if (dialoguePanel == null || dialogueText == null || currentPactOffer == null) return;

        dialoguePanel.SetActive(true);

        string drawback = "Ninguno";
        if (currentPactOffer.drawbacks.Count > 0)
        {
            StatModifier firstDrawback = currentPactOffer.drawbacks[0];

            float displayAmount = Mathf.Abs(firstDrawback.amount);

            string amountText = firstDrawback.isPercentage
                                ? $"{displayAmount:F0}%"
                                : $"{displayAmount}";

            drawback = $"{firstDrawback.type} -{amountText}";
        }

        string acceptLine = pactAcceptLines.Length > 0 ? pactAcceptLines[0] : "Pacto aceptado.";

        string fullPactMessage = $"{acceptLine}\n\n" +
                                 $"PACTO: {currentPactOffer.pactName}\n" +
                                 $"MALDICIÓN REVELADA: {drawback}";

        dialogueText.text = fullPactMessage;

        if (dialogueToggleCoroutine != null)
        {
            StopCoroutine(dialogueToggleCoroutine);
        }
        dialogueToggleCoroutine = StartCoroutine(TogglePanelAfterDelay());
    }

    public void HidePactUI()
    {
        pactOffered = false;
        waitingForAccept = false;
        HideDialogue();
    }

    private Pact GenerateRandomPact()
    {
        if (shopManager != null && shopManager.allPacts != null && shopManager.allPacts.Count > 0)
        {
            int randomIndex = Random.Range(0, shopManager.allPacts.Count);
            return shopManager.allPacts[randomIndex];
        }

        Pact defaultPact = ScriptableObject.CreateInstance<Pact>();
        defaultPact.pactName = "Pacto Fallback";
        defaultPact.lifeRecoveryAmount = 20;
        defaultPact.drawbacks = new List<StatModifier> {
            new StatModifier { type = StatType.MoveSpeed, amount = 0.15f, isPercentage = true } 
        };
        return defaultPact;
    }

    public void OnAcceptPact()
    {
        if (hasAcceptedPactInCurrentInteraction)
        {
            return;
        }

        if (currentPactOffer == null || !waitingForAccept) return;

        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            SetDialogue(new string[] { "¡Espera! No te necesito. Tu alma no está lista para ser curada. Vuelve cuando te falte algo." });
            currentDialogueIndex = 0;
            ShowCurrentDialogueLine();

            pactOffered = false;
            waitingForAccept = false;
            SendMessage("HidePactPrompt", SendMessageOptions.DontRequireReceiver);
            return;
        }

        ApplyPactEffects(currentPactOffer);
        hasTakenPact = true;
        hasAcceptedPactInCurrentInteraction = true;

        DisplayPactConfirmation();

        SendMessage("HidePactPrompt", SendMessageOptions.DontRequireReceiver);
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
}