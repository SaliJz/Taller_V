using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gestor centralizado de acciones de combate del jugador.
/// Permite encolar una acción pendiente mientras otra está en ejecución.
/// </summary>
public class PlayerCombatActionManager : MonoBehaviour
{
    public enum CombatActionType
    {
        None,
        MeleeAttack,
        ShieldThrow,
        Dash
    }

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
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        shieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();

        if (meleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado.", 3);
        if (shieldController == null) ReportDebug("PlayerShieldController no encontrado.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado.", 3);
    }

    private void Update()
    {
        if (!isExecutingAction)
        {
            ProcessCombatInputs();
        }
        else
        {
            TryQueueAction();
        }
    }

    /// <summary>
    /// Procesa los inputs de combate cuando no hay acciones activas.
    /// </summary>
    private void ProcessCombatInputs()
    {
        // Prioridad de inputs
        if (Input.GetMouseButtonDown(0))
        {
            TryExecuteMeleeAttack();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            TryExecuteShieldThrow();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            TryExecuteDash();
        }
    }

    /// <summary>
    /// Intenta encolar una acción si ya hay una en ejecución.
    /// Solo guarda la PRIMERA acción solicitada durante la ejecución actual.
    /// </summary>
    private void TryQueueAction()
    {
        if (queuedAction != CombatActionType.None) return;

        // capturar input del frame y bufferizar acción
        if (Input.GetMouseButtonDown(0) && CanQueueMeleeAttack())
        {
            TryBufferAction(CombatActionType.MeleeAttack);
        }
        else if (Input.GetMouseButtonDown(1) && CanQueueShieldThrow())
        {
            TryBufferAction(CombatActionType.ShieldThrow);
        }
        else if (Input.GetKeyDown(KeyCode.Space) && CanQueueDash())
        {
            TryBufferAction(CombatActionType.Dash);
        }
    }

    // Función para encolar con buffer y prioridad de sobrescritura opcional
    private bool TryBufferAction(CombatActionType action)
    {
        queuedAction = action;
        queuedActionTimestamp = Time.time;
        ReportDebug($"Acción {action} bufferizada", 1);
        return true;
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
            StartCoroutine(ExecuteActionRoutine(CombatActionType.ShieldThrow, shieldController.ExecuteShieldThrowFromManager()));
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
            ReportDebug($"Ejecutando acción encolada: {actionToExecute}", 1);
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