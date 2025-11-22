using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System;

public interface IPlayerSpecialAbility
{
    event Action OnAbilityActivated;
    event Action OnAbilityDeactivated;
    bool IsActive { get; }
    void DeactivateAbility();
}

public class LearningEncounterController : MonoBehaviour
{
    private enum EncounterState
    {
        Phase1_Intro,
        Phase2_AwaitingSkill,
        Phase3_LearningCombat,
        Phase4_Completed
    }
    private EncounterState currentState = EncounterState.Phase1_Intro;

    [Header("Dependencies")]
    [SerializeField] private DummyArmor slimeArmor;
    [SerializeField] private MonoBehaviour playerAbilityScript;
    private IPlayerSpecialAbility playerAbility;

    [Header("Phase 1: Superarmor Intro")]
    [SerializeField] private int requiredInitialHits = 3;
    private int currentHitCount = 0;
    public DialogLine[] dialogue1_Intro;

    [Header("Phase 2 & 3: Special Ability")]
    public DialogLine[] dialogue2_Contract;
    public DialogLine[] dialogue3_DiabloLaugh;
    [SerializeField] private float specialAbilityDuration = 5.0f;
    private Coroutine specialAbilityTimerCoroutine;

    [Header("Events")]
    public UnityEvent OnDisplaySkillReminder;
    public UnityEvent OnDisplayMeleeReminder;
    public UnityEvent OnEncounterComplete;

    private void Start()
    {
        if (slimeArmor == null || playerAbilityScript == null)
        {
            enabled = false;
            return;
        }

        playerAbility = playerAbilityScript as IPlayerSpecialAbility;
        if (playerAbility == null)
        {
            enabled = false;
            return;
        }

        slimeArmor.OnHitByPlayer += HandleSlimeHit;
        slimeArmor.OnDummyDefeated.AddListener(HandleSlimeDefeated);
        playerAbility.OnAbilityActivated += ActivateSpecialMode;
        playerAbility.OnAbilityDeactivated += DeactivateSpecialMode;

        StartCoroutine(InitializeEncounter());
    }

    private IEnumerator InitializeEncounter()
    {
        yield return new WaitForSeconds(0.1f);

        if (DialogManager.Instance != null && dialogue1_Intro.Length > 0)
        {
            DialogManager.Instance.StartDialog(dialogue1_Intro);
            yield return new WaitForSeconds(2f);
        }

        if (slimeArmor != null)
        {
            slimeArmor.gameObject.SetActive(true);
        }

        SetState(EncounterState.Phase1_Intro);
    }

    private void SetState(EncounterState newState)
    {
        currentState = newState;

        switch (currentState)
        {
            case EncounterState.Phase2_AwaitingSkill:
                OnDisplaySkillReminder?.Invoke();
                break;
            case EncounterState.Phase3_LearningCombat:
                if (specialAbilityTimerCoroutine != null) StopCoroutine(specialAbilityTimerCoroutine);
                specialAbilityTimerCoroutine = StartCoroutine(SpecialAbilityTimer());
                OnDisplayMeleeReminder?.Invoke();
                break;
            case EncounterState.Phase4_Completed:
                if (specialAbilityTimerCoroutine != null) StopCoroutine(specialAbilityTimerCoroutine);
                OnEncounterComplete?.Invoke();
                break;
        }
    }

    public void HandleSlimeHit(DamageType damageType, bool isFatal)
    {
        if (currentState == EncounterState.Phase1_Intro)
        {
            if (damageType == DamageType.Shield && !isFatal)
            {
                currentHitCount++;
                if (currentHitCount >= requiredInitialHits)
                {
                    if (DialogManager.Instance != null && dialogue1_Intro.Length > 0)
                    {
                        DialogManager.Instance.StartDialog(dialogue1_Intro);
                    }
                    SetState(EncounterState.Phase2_AwaitingSkill);
                }
            }
        }
    }

    private void HandleSlimeDefeated()
    {
        if (currentState == EncounterState.Phase3_LearningCombat && playerAbility.IsActive)
        {
            SetState(EncounterState.Phase4_Completed);
        }
        else
        {
            if (DialogManager.Instance != null && dialogue3_DiabloLaugh.Length > 0)
            {
                UnityEvent onFinish = new UnityEvent();
                onFinish.AddListener(ReviveSlime);
                DialogManager.Instance.StartDialog(dialogue3_DiabloLaugh, onFinish);
            }
            else
            {
                ReviveSlime();
            }
        }
    }

    public void ActivateSpecialMode()
    {
        if (currentState == EncounterState.Phase2_AwaitingSkill)
        {
            if (DialogManager.Instance != null && dialogue2_Contract.Length > 0)
            {
                DialogManager.Instance.StartDialog(dialogue2_Contract);
            }
            SetState(EncounterState.Phase3_LearningCombat);
        }
    }

    public void DeactivateSpecialMode()
    {
        // Se llama cuando el temporizador termina o el jugador desactiva
    }

    private IEnumerator SpecialAbilityTimer()
    {
        yield return new WaitForSeconds(specialAbilityDuration);
        playerAbility.DeactivateAbility();
    }

    private void ReviveSlime()
    {
        slimeArmor.ResetArmorState();
        currentHitCount = 0;
        SetState(EncounterState.Phase3_LearningCombat);
    }
}