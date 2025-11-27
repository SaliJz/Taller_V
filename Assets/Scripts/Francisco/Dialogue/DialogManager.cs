using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class DialogManager : MonoBehaviour
{
    #region Singleton Setup

    public static DialogManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Editor Settings

    [Header("UI Dependencies")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI lineText;
    [SerializeField] private Image profileImage;

    [Header("Input Setup")]
    [SerializeField] private InputActionAsset inputActions;
    private InputAction advanceDialogueAction;

    [Header("Typing/Input Settings")]
    [SerializeField] private float typingSpeed = 0.03f;
    [SerializeField] private float inputBufferTime = 0.2f;

    [Header("System Dependencies")]
    private PlayerMovement playerMovement;

    [Header("Player Action Scripts")]
    [SerializeField] private MonoBehaviour[] playerActionScripts;

    public bool IsActive => isDialogActive;

    #endregion

    #region Private Fields

    private Queue<DialogLine> dialogQueue = new Queue<DialogLine>();
    private bool isDialogActive = false;
    private bool isTyping = false;
    private float lastInputTime = 0f;
    private UnityEvent onDialogFinishedEvent;
    private bool isMerchantFlow = false;

    #endregion

    #region Input Integration & Unity Methods

    private void OnEnable()
    {
        InputActionMap uiMap = inputActions?.FindActionMap("UI");
        if (uiMap != null)
        {
            advanceDialogueAction = uiMap.FindAction("Click");
        }

        if (advanceDialogueAction != null)
        {
            advanceDialogueAction.performed += OnAdvanceDialogue;
            advanceDialogueAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (advanceDialogueAction != null)
        {
            advanceDialogueAction.performed -= OnAdvanceDialogue;
            advanceDialogueAction.Disable();
        }
    }

    private void Start()
    {
        playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
    }

    private void OnAdvanceDialogue(InputAction.CallbackContext context)
    {
        if (!isDialogActive) return;

        if (Time.time < lastInputTime + inputBufferTime)
        {
            return;
        }

        lastInputTime = Time.time;

        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            lineText.maxVisibleCharacters = int.MaxValue;

            if (isMerchantFlow)
            {
                onDialogFinishedEvent?.Invoke();
            }
            return;
        }

        if (isMerchantFlow) return;

        if (dialogQueue.Count > 0)
        {
            DialogLine currentLine = dialogQueue.Peek();
            if (currentLine.WaitForInput)
            {
                DisplayNextLine();
            }
        }
        else if (dialogQueue.Count == 0)
        {
            EndDialog();
        }
    }

    #endregion

    #region Core Dialog Logic

    public void StartDialog(DialogLine[] lines, UnityEvent onFinished = null, bool isMerchant = false)
    {
        if (isDialogActive) return;

        isMerchantFlow = isMerchant;
        StopAllCoroutines();
        LockPlayerControl(true);
        DisablePlayerScripts(true);

        onDialogFinishedEvent = onFinished;

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(true);
        }
        dialogQueue.Clear();
        foreach (DialogLine line in lines)
        {
            dialogQueue.Enqueue(line);
        }

        isDialogActive = true;

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (dialogQueue.Count == 0)
        {
            if (isMerchantFlow)
            {
                onDialogFinishedEvent?.Invoke();
                return;
            }

            EndDialog();
            return;
        }

        DialogLine line = dialogQueue.Dequeue();

        StopAllCoroutines();
        isTyping = true;

        nameText.text = line.CharacterName;
        if (profileImage != null)
        {
            if (line.ProfileImage != null)
            {
                profileImage.sprite = line.ProfileImage;
                profileImage.enabled = true;
            }
            else
            {
                profileImage.enabled = false;
            }
        }

        bool shouldWaitForInput = isMerchantFlow ? false : line.WaitForInput;

        if (typingSpeed > 0)
        {
            StartCoroutine(TypeLine(line.Text, shouldWaitForInput));
        }
        else
        {
            lineText.text = line.Text;
            lineText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;

            if (isMerchantFlow && dialogQueue.Count == 0)
            {
                onDialogFinishedEvent?.Invoke();
            }
            else if (!isMerchantFlow && !line.WaitForInput)
            {
                StartCoroutine(AutoAdvance(dialogQueue.Count > 0 ? inputBufferTime : 0f));
            }
        }
    }

    private IEnumerator TypeLine(string fullText, bool waitForInput)
    {
        lineText.text = fullText;
        lineText.maxVisibleCharacters = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (lineText.maxVisibleCharacters >= fullText.Length) break;

            lineText.maxVisibleCharacters++;
            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        lineText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        if (isMerchantFlow && dialogQueue.Count == 0)
        {
            yield return new WaitForSecondsRealtime(0.1f); 
            onDialogFinishedEvent?.Invoke();
        }
        else if (!isMerchantFlow && !waitForInput)
        {
            StartCoroutine(AutoAdvance(dialogQueue.Count > 0 ? inputBufferTime : 0f));
        }
    }

    private IEnumerator TypeLineForUpdate(string fullText)
    {
        lineText.text = fullText;
        lineText.maxVisibleCharacters = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (lineText.maxVisibleCharacters >= fullText.Length) break;

            lineText.maxVisibleCharacters++;
            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        lineText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        if (isMerchantFlow && onDialogFinishedEvent != null)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            onDialogFinishedEvent.Invoke();
        }
    }

    public void UpdateCurrentDialogText(string newText, string characterName = null, Sprite profileSprite = null)
    {
        if (!isDialogActive) return;

        if (!string.IsNullOrEmpty(characterName) && nameText != null)
        {
            nameText.text = characterName;
        }

        if (profileSprite != null && profileImage != null)
        {
            profileImage.sprite = profileSprite;
            profileImage.enabled = true;
        }

        StopAllCoroutines();

        isTyping = true;
        StartCoroutine(TypeLineForUpdate(newText));
    }

    private void EndDialog()
    {
        if (isMerchantFlow) return;

        isDialogActive = false;
        isMerchantFlow = false;

        UnityEvent tempEvent = onDialogFinishedEvent;
        onDialogFinishedEvent = null;

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        LockPlayerControl(false);
        DisablePlayerScripts(false);

        tempEvent?.Invoke();
    }

    public void ForceEndDialog()
    {
        isDialogActive = false;
        isMerchantFlow = false;

        UnityEvent tempEvent = onDialogFinishedEvent;
        onDialogFinishedEvent = null;

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        LockPlayerControl(false);
        DisablePlayerScripts(false);

        tempEvent?.Invoke();
    }

    private void LockPlayerControl(bool isLocked)
    {
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(!isLocked);
        }
    }

    private void DisablePlayerScripts(bool disable)
    {
        if (playerActionScripts == null || playerActionScripts.Length == 0)
        {
            return;
        }

        foreach (MonoBehaviour script in playerActionScripts)
        {
            if (script != null)
            {
                script.enabled = !disable;
            }
        }
    }

    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        DisplayNextLine();
    }

    #endregion
}