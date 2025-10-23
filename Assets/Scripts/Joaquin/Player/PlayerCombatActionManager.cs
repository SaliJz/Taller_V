using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestor centralizado de acciones de combate del jugador.
/// Permite encolar una acci�n pendiente mientras otra est� en ejecuci�n.
/// </summary>
public class PlayerCombatActionManager : MonoBehaviour, PlayerControlls.ICombatActions
{
    public enum CombatActionType
    {
        None,
        MeleeAttack,
        ShieldThrow,
        Dash
    }

    private PlayerControlls playerControls;

    [Header("Referencias")]
    [SerializeField] private PlayerMeleeAttack meleeAttack;
    [SerializeField] private PlayerShieldController shieldController;
    [SerializeField] private PlayerMovement playerMovement;

    [SerializeField] private float inputBufferWindow = 0.12f; // Ventana de tiempo para bufferizar inputs

    private bool isExecutingAction = false;
    private CombatActionType currentAction = CombatActionType.None;
    private CombatActionType queuedAction = CombatActionType.None;
    private float queuedActionTimestamp = -Mathf.Infinity;

    public bool IsExecutingAction => isExecutingAction;
    public CombatActionType CurrentAction => currentAction;

    private void Awake()
    {
        playerControls = new PlayerControlls();
        
        playerControls.Combat.SetCallbacks(this);

        meleeAttack = GetComponent<PlayerMeleeAttack>();
        shieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();

        if (meleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado.", 3);
        if (shieldController == null) ReportDebug("PlayerShieldController no encontrado.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado.", 3);
    }

    private void OnEnable()
    {
        playerControls.Combat.Enable();
    }

    private void OnDisable()
    {
        playerControls.Combat.Disable();
    }

    /// <summary>
    /// Procesa los inputs de combate cuando no hay acciones activas.
    /// </summary>
    /// <summary>
    /// Intenta encolar una acci�n si ya hay una en ejecuci�n.
    /// Solo guarda la PRIMERA acci�n solicitada durante la ejecuci�n actual.
    /// </summary>
    private void TryQueueAction(CombatActionType actionType)
    {
        if (queuedAction != CombatActionType.None) return;

        bool canQueue = false;
        switch (actionType)
        {
            case CombatActionType.MeleeAttack:
                canQueue = CanQueueMeleeAttack();
                break;
            case CombatActionType.ShieldThrow:
                canQueue = CanQueueShieldThrow();
                break;
            case CombatActionType.Dash:
                canQueue = CanQueueDash();
                break;
        }

        if (canQueue)
        {
            TryBufferAction(actionType);
        }
    }

    // Funci�n para encolar con buffer y prioridad de sobrescritura opcional
    private bool TryBufferAction(CombatActionType action)
    {
        queuedAction = action;
        queuedActionTimestamp = Time.time;
        ReportDebug($"Acci�n {action} bufferizada", 1);
        return true;
    }

    public void OnMelee(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        ProcessCombatInput(CombatActionType.MeleeAttack);
    }

    public void OnShieldThrow(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        ProcessCombatInput(CombatActionType.ShieldThrow);
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        ProcessCombatInput(CombatActionType.Dash);
    }

    private void ProcessCombatInput(CombatActionType actionType)
    {
        if (isExecutingAction)
        {
            TryQueueAction(actionType);
        }
        else
        {
            ExecuteActionImmediately(actionType);
        }
    }

    private void ExecuteActionImmediately(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.MeleeAttack:
                TryExecuteMeleeAttack();
                break;
            case CombatActionType.ShieldThrow:
                TryExecuteShieldThrow();
                break;
            case CombatActionType.Dash:
                TryExecuteDash();
                break;
            case CombatActionType.None:
            default:
                break;
        }
    }

    #region Execution Routines

    private void TryExecuteMeleeAttack()
    {
        if (meleeAttack != null && meleeAttack.CanAttack())
        {
            StartCoroutine(ExecuteActionRoutine(CombatActionType.MeleeAttack, meleeAttack.ExecuteAttackFromManager()));
        }
    }

    private void TryExecuteShieldThrow()
    {
        if (shieldController != null && shieldController.HasShield)
        {
            if (CanQueueShieldThrow())
            {
                StartCoroutine(ExecuteActionRoutine(CombatActionType.ShieldThrow, shieldController.ExecuteShieldThrowFromManager()));
            }
        }
    }

    private void TryExecuteDash()
    {
        if (CanQueueDash())
        {
            StartCoroutine(ExecuteActionRoutine(CombatActionType.Dash, playerMovement.ExecuteDashFromManager()));
        }
    }

    private IEnumerator ExecuteActionRoutine(CombatActionType actionType, IEnumerator actionCoroutine)
    {
        isExecutingAction = true;
        currentAction = actionType;
        yield return StartCoroutine(actionCoroutine);
        FinishAction();
    }

    #endregion

    #region Conditionals

    private bool CanQueueMeleeAttack()
    {
        if (meleeAttack == null) return false;
        if (shieldController != null) return shieldController.HasShield;
        return true;
    }

    private bool CanQueueShieldThrow()
    {
        return shieldController != null && shieldController.CanThrowShield();
    }

    private bool CanQueueDash()
    {
        return playerMovement != null && !playerMovement.IsDashing && !playerMovement.IsDashDisabled && playerMovement.DashCooldownTimer <= 0;
    }


    #endregion

    #region Action Finishers

    private void FinishAction()
    {
        isExecutingAction = false;
        currentAction = CombatActionType.None;

        if (queuedAction != CombatActionType.None && Time.time - queuedActionTimestamp <= inputBufferWindow)
        {
            CombatActionType actionToExecute = queuedAction;
            queuedAction = CombatActionType.None;
            ReportDebug($"Ejecutando acci�n encolada: {actionToExecute}", 1);
            StartCoroutine(ExecuteQueuedAction(actionToExecute));
        }
        else
        {
            queuedAction = CombatActionType.None;
        }
    }

    private IEnumerator ExecuteQueuedAction(CombatActionType action)
    {
        yield return null;
        switch (action)
        {
            case CombatActionType.MeleeAttack:
                TryExecuteMeleeAttack();
                break;
            case CombatActionType.ShieldThrow:
                TryExecuteShieldThrow();
                break;
            case CombatActionType.Dash:
                TryExecuteDash();
                break;
        }
    }

    #endregion

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerCombatActionManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerCombatActionManager] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerCombatActionManager] {message}");
                break;
            default:
                Debug.Log($"[PlayerCombatActionManager] {message}");
                break;
        }
    }
}