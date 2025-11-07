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
    [SerializeField] private ShieldSkill shieldSkill;

    [SerializeField] private float inputBufferWindow = 0.12f; // Ventana de tiempo para bufferizar inputs

    [Header("Feedback Visual")]
    [SerializeField] private bool enableVisualFeedback = true;
    [SerializeField] private GameObject skillRequiredWarningPrefab;
    [SerializeField] private float warningOffsetY = 2.0f;
    [SerializeField] private float warningDuration = 1.5f;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private string meleeBlockedMessage = "¡Activa la habilidad especial!";
    [SerializeField] private string rangedBlockedMessage = "¡Desactiva la habilidad especial!";

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
        shieldSkill = GetComponent<ShieldSkill>();

        if (meleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado.", 3);
        if (shieldController == null) ReportDebug("PlayerShieldController no encontrado.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado.", 3);
        if (shieldSkill == null) ReportDebug("ShieldSkill no encontrado. El control de combate no funcionará correctamente.", 3);
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
        if (!context.started) return;

        if (shieldSkill == null)
        {
            ReportDebug("ShieldSkill no asignado. No se puede determinar modo de combate.", 3);
            return;
        }

        // Determinar qué ataque ejecutar según el estado de la habilidad
        if (shieldSkill.isSkillActive)
        {
            // Habilidad activa: Permitir melee
            ProcessCombatInput(CombatActionType.MeleeAttack);
        }
        else
        {
            // Habilidad inactiva: Bloquear melee y mostrar advertencia
            ShowSkillRequiredWarning(meleeBlockedMessage);
            ReportDebug("Ataque melee bloqueado: habilidad especial no está activa.", 1);
        }
    }

    public void OnShieldThrow(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (shieldSkill == null)
        {
            ReportDebug("ShieldSkill no asignado. No se puede determinar modo de combate.", 3);
            return;
        }

        // Determinar si se puede lanzar el escudo según el estado de la habilidad
        if (!shieldSkill.isSkillActive)
        {
            // Habilidad inactiva: Permitir lanzamiento de escudo
            ProcessCombatInput(CombatActionType.ShieldThrow);
        }
        else
        {
            // Habilidad activa: Bloquear lanzamiento y mostrar advertencia
            ShowSkillRequiredWarning(rangedBlockedMessage);
            ReportDebug("Lanzamiento de escudo bloqueado: habilidad especial está activa.", 1);
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
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
        if (shieldSkill != null && !shieldSkill.isSkillActive)
        {
            ReportDebug("Intento de ejecutar melee sin habilidad activa. Bloqueado.", 2);
            return;
        }

        if (meleeAttack != null && meleeAttack.CanAttack())
        {
            StartCoroutine(ExecuteActionRoutine(CombatActionType.MeleeAttack, meleeAttack.ExecuteAttackFromManager()));
        }
    }

    private void TryExecuteShieldThrow()
    {
        if (shieldSkill != null && shieldSkill.isSkillActive)
        {
            ReportDebug("Intento de lanzar escudo con habilidad activa. Bloqueado.", 2);
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
        
        if (shieldSkill != null && !shieldSkill.isSkillActive) return false;
        
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
        if (shieldSkill != null && shieldSkill.isSkillActive) return false;

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