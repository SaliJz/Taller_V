using UnityEngine;
using TMPro;
using System.Collections;
using System;
using UnityEngine.Events;

public class TypewriterEffect : MonoBehaviour
{
    [Serializable]
    public class DialogueEntry
    {
        [TextArea(3, 10)]
        public string Line = "";

        public UnityEvent OnLineEnd;
    }

    public enum ActivationType
    {
        Automatic,
        OnTriggerOrKeyPress,
        ManualCall
    }

    #region [ CONFIGURACIÓN ]

    [Header("Activación")]
    [SerializeField] private ActivationType activationMode = ActivationType.ManualCall;
    [SerializeField] private KeyCode triggerStartKey = KeyCode.E;
    [SerializeField] private bool requiresTrigger = false;

    [Header("Componentes")]
    [SerializeField] private TextMeshProUGUI textComponent;
    [SerializeField] private GameObject dialoguePanel;

    [Header("Control de Velocidad y Flujo")]
    [SerializeField] [Range(1f, 100f)] private float charactersPerSecond = 25f;
    [SerializeField] private bool advanceAutomatically = false;
    [SerializeField] [Range(0.1f, 5f)] private float autoAdvanceDelay = 1.5f;
    [SerializeField] private bool canSkipEffect = true;
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;
    [SerializeField] private bool canAdvanceManually = true;

    [Header("Textos y Eventos")]
    [SerializeField] private DialogueEntry[] dialogue = new DialogueEntry[0];
    [SerializeField] private UnityEvent onDialogueCompletedEvent;

    #endregion

    #region [ ESTADO INTERNO ]

    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private bool playerIsInRange = false;
    private int currentLineIndex = 0;

    public event Action OnTextFinishedTyping;
    public event Action OnDialogueCompleted;

    #endregion

    #region [ MÉTODOS DE UNITY ]

    private void Awake()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TextMeshProUGUI>();
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        textComponent.text = "";
        textComponent.maxVisibleCharacters = 0;
    }

    private void Start()
    {
        if (activationMode == ActivationType.Automatic)
        {
            StartDialogue();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(advanceKey))
        {
            if (isTyping && canSkipEffect)
            {
                SkipTyping();
            }
            else if (!isTyping && !advanceAutomatically && canAdvanceManually)
            {
                AdvanceDialogue();
            }
        }

        if (!isTyping && activationMode == ActivationType.OnTriggerOrKeyPress && currentLineIndex == 0)
        {
            bool triggerCondition = requiresTrigger ? playerIsInRange : true;

            if (triggerCondition && Input.GetKeyDown(triggerStartKey))
            {
                StartDialogue();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requiresTrigger && other.CompareTag("Player"))
        {
            playerIsInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (requiresTrigger && other.CompareTag("Player"))
        {
            playerIsInRange = false;
        }
    }

    #endregion

    #region [ FLUJO DE DIÁLOGO ]

    public void StartDialogue(DialogueEntry[] newDialogue = null)
    {
        if (isTyping) return;

        if (newDialogue != null && newDialogue.Length > 0)
        {
            dialogue = newDialogue;
        }

        if (dialogue.Length == 0) return;

        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        currentLineIndex = 0;
        textComponent.text = "";

        StartTypingLine(dialogue[currentLineIndex]);
    }

    private void AdvanceDialogue()
    {
        currentLineIndex++;

        if (currentLineIndex < dialogue.Length)
        {
            StartTypingLine(dialogue[currentLineIndex]);
        }
        else
        {
            Debug.Log("Diálogo Completo.");

            if (dialoguePanel != null) dialoguePanel.SetActive(false);

            textComponent.text = "";
            textComponent.maxVisibleCharacters = 0;
            currentLineIndex = 0;

            OnDialogueCompleted?.Invoke();
            onDialogueCompletedEvent?.Invoke();
        }
    }

    #endregion

    #region [ TIPEADO Y SALTO ]

    private void StartTypingLine(DialogueEntry entry)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        typingCoroutine = StartCoroutine(RevealTextCoroutine(entry));
    }

    public void SkipTyping()
    {
        if (isTyping && typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            isTyping = false;

            DialogueEntry currentEntry = dialogue[currentLineIndex];

            textComponent.maxVisibleCharacters = int.MaxValue;
            textComponent.text = currentEntry.Line;

            Debug.Log("Efecto de escritura saltado.");

            currentEntry.OnLineEnd?.Invoke();

            if (advanceAutomatically)
            {
                AdvanceDialogue();
            }
        }
    }

    #endregion

    #region [ COROUTINE ]

    private IEnumerator RevealTextCoroutine(DialogueEntry entry)
    {
        isTyping = true;

        textComponent.text = entry.Line;
        textComponent.maxVisibleCharacters = 0;

        int totalCharacters = entry.Line.Length;
        float timePerCharacter = 1f / charactersPerSecond;

        for (int i = 0; i < totalCharacters; i++)
        {
            textComponent.maxVisibleCharacters = i + 1;

            yield return new WaitForSeconds(timePerCharacter);
        }

        textComponent.maxVisibleCharacters = int.MaxValue;

        isTyping = false;
        Debug.Log("Línea terminada.");

        OnTextFinishedTyping?.Invoke();
        entry.OnLineEnd?.Invoke(); 

        if (advanceAutomatically)
        {
            yield return new WaitForSeconds(autoAdvanceDelay);
            AdvanceDialogue();
        }
    }

    #endregion
}