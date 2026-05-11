using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class DialogueInputBridge : MonoBehaviour
{
    [Header("Start Dialogue")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private DialogLine[] startDialogue;
    [SerializeField] private UnityEvent OnStartDialogueBegin;
    [SerializeField] private UnityEvent OnStartDialogueEnd;

    [Header("Input Wait")]
    [SerializeField] private InputActionReference actionToContinue;
    [SerializeField] private int requiredPresses = 1;
    [SerializeField] private float maxTimeBetweenPresses = 0f;
    [SerializeField] private UnityEvent OnStartWaitingForInput;
    [SerializeField] private UnityEvent OnInputPressed;
    [SerializeField] private UnityEvent OnRequiredInputsCompleted;

    [Header("End Dialogue")]
    [SerializeField] private DialogLine[] endDialogue;
    [SerializeField] private UnityEvent OnEndDialogueBegin;
    [SerializeField] private UnityEvent OnEndDialogueEnd;

    [Header("State")]
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool hasPlayed;
    [SerializeField] private int currentPresses;

    private bool waitingForDialogue;

    private void OnEnable()
    {
        if (actionToContinue != null && actionToContinue.action != null)
        {
            actionToContinue.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (actionToContinue != null && actionToContinue.action != null)
        {
            actionToContinue.action.Disable();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlaySequence();
        }
    }

    public void PlaySequence()
    {
        if (isRunning) return;
        if (playOnlyOnce && hasPlayed) return;

        StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        isRunning = true;
        hasPlayed = true;

        OnStartDialogueBegin?.Invoke();
        yield return StartCoroutine(PlayDialogue(startDialogue));
        OnStartDialogueEnd?.Invoke();

        OnStartWaitingForInput?.Invoke();
        yield return StartCoroutine(WaitForInputSequence());

        OnRequiredInputsCompleted?.Invoke();

        OnEndDialogueBegin?.Invoke();
        yield return StartCoroutine(PlayDialogue(endDialogue));
        OnEndDialogueEnd?.Invoke();

        isRunning = false;
    }

    private IEnumerator WaitForInputSequence()
    {
        currentPresses = 0;
        float timer = 0f;
        int targetPresses = Mathf.Max(1, requiredPresses);

        while (currentPresses < targetPresses)
        {
            if (WasContinueActionPressed())
            {
                currentPresses++;
                timer = 0f;
                OnInputPressed?.Invoke();
            }

            if (maxTimeBetweenPresses > 0f && currentPresses > 0)
            {
                timer += Time.deltaTime;

                if (timer > maxTimeBetweenPresses)
                {
                    currentPresses = 0;
                    timer = 0f;
                }
            }

            yield return null;
        }
    }

    private bool WasContinueActionPressed()
    {
        return actionToContinue != null &&
               actionToContinue.action != null &&
               actionToContinue.action.WasPressedThisFrame();
    }

    private IEnumerator PlayDialogue(DialogLine[] dialogue)
    {
        if (DialogManager.Instance == null || dialogue == null || dialogue.Length == 0)
        {
            yield break;
        }

        waitingForDialogue = true;

        UnityEvent onFinish = new UnityEvent();
        onFinish.AddListener(() => waitingForDialogue = false);

        DialogManager.Instance.StartDialog(dialogue, onFinish);

        yield return new WaitUntil(() => !waitingForDialogue);
    }

    public void ResetSequence()
    {
        StopAllCoroutines();
        isRunning = false;
        hasPlayed = false;
        waitingForDialogue = false;
        currentPresses = 0;
    }
}