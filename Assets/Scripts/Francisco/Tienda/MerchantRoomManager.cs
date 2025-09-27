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

    public float itemEffectDuration = 0.5f;
    public bool sequentialItemSpawn = false;

    public float dialogueDisplayTime = 5.0f;
    private Coroutine dialogueToggleCoroutine;

    private ShopManager shopManager;
    private int currentDialogueIndex = 0;
    private bool isFirstVisit = true;
    private const string FirstVisitKey = "MerchantFirstVisit";

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        isFirstVisit = PlayerPrefs.GetInt(FirstVisitKey, 1) == 1;

        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El mercader no funcionará.");
        }
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf && Input.GetKeyDown(KeyCode.Space))
        {
            AdvanceDialogue();
        }
    }

    public void InitializeMerchantRoom(List<Transform> spawnLocations)
    {
        StartCoroutine(GenerateItemsAndSetDialogue(spawnLocations));
    }

    private IEnumerator GenerateItemsAndSetDialogue(List<Transform> spawnLocations)
    {
        if (shopManager != null)
        {
            yield return StartCoroutine(shopManager.GenerateMerchantItems(spawnLocations, isFirstVisit, itemEffectDuration, sequentialItemSpawn));

            string[] lines = isFirstVisit ? firstVisitLines : routineLines;

            SetDialogue(lines);
            ShowCurrentDialogueLine();
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
        string[] currentLines = isFirstVisit ? firstVisitLines : routineLines;

        if (dialoguePanel.activeSelf)
        {
            currentDialogueIndex = (currentDialogueIndex + 1) % currentLines.Length;
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

    private void SetDialogue(string[] dialogueLines)
    {
        if (dialoguePanel == null || dialogueText == null || dialogueLines == null || dialogueLines.Length == 0) return;

        dialoguePanel.SetActive(true);
        currentDialogueIndex %= dialogueLines.Length;

        dialogueText.text = dialogueLines[currentDialogueIndex];
    }

    private void ShowCurrentDialogueLine()
    {
        dialoguePanel.SetActive(true);

        if (dialogueToggleCoroutine != null)
        {
            StopCoroutine(dialogueToggleCoroutine);
        }
        dialogueToggleCoroutine = StartCoroutine(TogglePanelAfterDelay());
    }

    private IEnumerator TogglePanelAfterDelay()
    {
        yield return new WaitForSeconds(dialogueDisplayTime);

        HideDialogue();
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }
}