using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestor centralizado de acciones de combate del jugador.
/// Permite encolar una acción pendiente mientras otra está en ejecución.
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
    [SerializeField] private PlayerBlockSystem playerBlockSystem;
    [SerializeField] private PlayerHealth playerHealth;

    [SerializeField] private float inputBufferWindow = 0.12f; // Ventana de tiempo para bufferizar inputs

    //[Header("Feedback Visual")]
    //[SerializeField] private bool enableVisualFeedback = true;
    //[SerializeField] private GameObject skillRequiredWarningPrefab;
    //[SerializeField] private float warningOffsetY = 2.0f;
    //[SerializeField] private float warningDuration = 1.5f;
    //[SerializeField] private Color warningColor = Color.yellow;
    //[SerializeField] private string meleeBlockedMessage = "¡Activa la habilidad especial!";
    //[SerializeField] private string rangedBlockedMessage = "¡Desactiva la habilidad especial!";

    private bool isMeleeAttackBlocked = false;
    private bool isShieldThrowBlocked = false;
    private bool isDashBlocked = false; 
    private bool isExecutingAction = false;
    private CombatActionType currentAction = CombatActionType.None;
    private CombatActionType queuedAction = CombatActionType.None;
    private float queuedActionTimestamp = -Mathf.Infinity;
    private GameObject currentWarning;

    public bool IsExecutingAction => isExecutingAction;
    public CombatActionType CurrentAction => currentAction;

    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.Combat.SetCallbacks(this);

        meleeAttack = GetComponent<PlayerMeleeAttack>();
        shieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerBlockSystem = GetComponent<PlayerBlockSystem>();

        if (meleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado.", 3);
        if (shieldController == null) ReportDebug("PlayerShieldController no encontrado.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado.", 3);
        if (playerBlockSystem == null) ReportDebug("PlayerBlockSystem no encontrado.", 3);
    }

    private void OnEnable()
    {
        playerControls.Combat.Enable();
    }

    private void OnDisable()
    {
        playerControls.Combat.Disable();
    }

    private void OnDestroy()
    {
        Destroy(currentWarning);
    }

    #region Input Handlers

    public void OnMelee(InputAction.CallbackContext context)
    {
        if (isMeleeAttackBlocked) return;

        if (PauseController.IsGamePaused) return;

        if (!context.started) return;

        ProcessCombatInput(CombatActionType.MeleeAttack);
    }

    public void OnShieldThrow(InputAction.CallbackContext context)
    {
        if (isShieldThrowBlocked) return;

        if (PauseController.IsGamePaused) return;

        if (!context.started) return;

        ProcessCombatInput(CombatActionType.ShieldThrow);
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (isDashBlocked) return;

        if (PauseController.IsGamePaused) return;

        if (!context.started) return;

        ProcessCombatInput(CombatActionType.Dash);
    }

    #endregion

    #region Combat Processing

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

    /// <summary>
    /// Intenta encolar una acción si ya hay una en ejecución.
    /// Solo guarda la primera acción solicitada durante la ejecución actual.
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

    // Función para encolar con buffer y prioridad de sobrescritura opcional
    private bool TryBufferAction(CombatActionType action)
    {
        queuedAction = action;
        queuedActionTimestamp = Time.time;
        ReportDebug($"Acción {action} bufferizada", 1);
        return true;
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

    #endregion

    #region Execution Routines

    private void TryExecuteMeleeAttack()
    {
        if (playerHealth != null)
        {
            if (playerHealth.IsStunned())
            {
                ReportDebug("Accion bloqueada: Jugador aturdido.", 2);
                return;
            }

            if (playerHealth.IsDead())
            {
                ReportDebug("Accion bloqueada: Jugador muerto.", 2);
                return;
            }
        }

        if (playerBlockSystem != null && playerBlockSystem.IsStunned())
        {
            ReportDebug("Acción bloqueada: Jugador aturdido por rotura de escudo.", 2);
            return;
        }

        if (playerBlockSystem != null && playerBlockSystem.IsBlockingState())
        {
            ReportDebug("Acción bloqueada: No se puede esquivar mientras se bloquea.", 2);
            return;
        }

        if (meleeAttack != null && meleeAttack.CanAttack())
        {
            StartCoroutine(ExecuteActionRoutine(CombatActionType.MeleeAttack, meleeAttack.ExecuteAttackFromManager()));
        }
    }

    private void TryExecuteShieldThrow()
    {
        if (playerHealth != null)
        {
            if (playerHealth.IsStunned())
            {
                ReportDebug("Accion bloqueada: Jugador aturdido.", 2);
                return;
            }

            if (playerHealth.IsDead())
            {
                ReportDebug("Accion bloqueada: Jugador muerto.", 2);
                return;
            }
        }

        if (playerBlockSystem != null && playerBlockSystem.IsStunned())
        {
            ReportDebug("Acción bloqueada: Jugador aturdido por rotura de escudo.", 2);
            return;
        }

        if (playerBlockSystem != null && playerBlockSystem.IsBlockingState())
        {
            ReportDebug("Acción bloqueada: No se puede esquivar mientras se bloquea.", 2);
            return;
        }

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
        if (playerHealth != null)
        {
            if (playerHealth.IsStunned())
            {
                ReportDebug("Accion bloqueada: Jugador aturdido.", 2);
                return;
            }

            if (playerHealth.IsDead())
            {
                ReportDebug("Accion bloqueada: Jugador muerto.", 2);
                return;
            }
        }

        if (playerBlockSystem != null && playerBlockSystem.IsStunned())
        {
            ReportDebug("Acción bloqueada: Jugador aturdido por rotura de escudo.", 2);
            return;
        }

        if (playerBlockSystem != null && playerBlockSystem.IsBlockingState())
        {
            ReportDebug("Acción bloqueada: No se puede esquivar mientras se bloquea.", 2);
            return;
        }

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

    /// <summary>
    /// Interrumpe cualquier acción en curso y limpia la cola de inputs.
    /// </summary>
    public void InterruptCombatActions()
    {
        // Limpiar la cola inmediatamente para evitar que se dispare nada después
        queuedAction = CombatActionType.None;
        queuedActionTimestamp = -Mathf.Infinity;

        if (!isExecutingAction) return;

        StopAllCoroutines();

        if (currentAction == CombatActionType.MeleeAttack && meleeAttack != null)
        {
            meleeAttack.CancelAttack();
        }
        else if (currentAction == CombatActionType.ShieldThrow && shieldController != null)
        {
            shieldController.CancelThrow();
        }
        else if (currentAction == CombatActionType.Dash && playerMovement != null)
        {
            playerMovement.CancelDash();
        }

        isExecutingAction = false;
        currentAction = CombatActionType.None;

        ReportDebug("Combate interrumpido por bloqueo/acción defensiva.", 1);
    }

    private bool CanQueueMeleeAttack()
    {
        if (meleeAttack == null) return false;
        
        if (currentAction == CombatActionType.MeleeAttack && meleeAttack.ComboCount == 2)
        {
            ReportDebug("No se puede bufferizar melee: tercer ataque del combo en ejecución.", 1);
            return false;
        }

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

    #region Public Methods

    public void BlockMeleeAttack()
    {
        isMeleeAttackBlocked = true;
    }

    public void UnblockMeleeAttack()
    {
        isMeleeAttackBlocked = false;
    }

    public void BlockShieldThrow()
    {
        isShieldThrowBlocked = true;
    }

    public void UnblockShieldThrow()
    {
        isShieldThrowBlocked = false;
    }

    public void BlockDash()
    {
        isDashBlocked = true;
    }

    public void UnblockDash()
    {
        isDashBlocked = false;
    }

    #endregion

    /*
    #region Visual Feedback

    private void ShowSkillRequiredWarning(string message)
    {
        if (!enableVisualFeedback) return;
        if (skillRequiredWarningPrefab == null) return;

        // Destruir advertencia anterior si existe
        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }

        // Crear nueva advertencia
        Vector3 spawnPosition = transform.position + Vector3.up * warningOffsetY;
        currentWarning = Instantiate(skillRequiredWarningPrefab, spawnPosition, Quaternion.identity);

        // Configurar mensaje
        WarningMessageFloater floater = currentWarning.GetComponent<WarningMessageFloater>();
        if (floater != null)
        {
            floater.SetLifetime(warningDuration);
            floater.SetColor(warningColor);
            floater.SetText(message);
        }

        // Destruir después de duración
        StartCoroutine(DestroyWarningAfterDelay());
    }

    private IEnumerator DestroyWarningAfterDelay()
    {
        yield return new WaitForSeconds(warningDuration);

        if (currentWarning != null)
        {
            Destroy(currentWarning);
            currentWarning = null;
        }
    }

    #endregion
    */
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