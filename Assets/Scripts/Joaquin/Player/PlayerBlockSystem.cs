using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de bloqueo para el jugador con múltiples tipos de límites configurables.
/// - Bloquea ataques frontales
/// - No bloquea: ataques desde abajo, costados, detrás, o AoE
/// - Límites opcionales: temporal, durabilidad, ángulo, absorción, penalización, guard crush, dinámico
/// </summary>
public class PlayerBlockSystem : MonoBehaviour, PlayerControlls.IDefenseActions
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private bool blockingEnabled = true;
    [Tooltip("Ángulo frontal de bloqueo (en grados). 180° = frontal completo, 90° = cono estrecho.")]
    [SerializeField, Range(0f, 180f)] private float frontBlockAngle = 120f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    [Header("Limit Types")]
    [Tooltip("Límite 1: Bloqueo con duración máxima.")]
    [SerializeField] private bool useTemporalLimit = false;
    [Tooltip("Límite 2: Resistencia/Durabilidad del escudo.")]
    [SerializeField] private bool useDurabilityLimit = false;
    [Tooltip("Límite 3: Ángulo de protección limitado.")]
    [SerializeField] private bool useAngleLimit = false;
    [Tooltip("Límite 4: Absorción máxima de daño.")]
    [SerializeField] private bool useAbsorptionLimit = false;
    [Tooltip("Límite 5: Penalización progresiva.")]
    [SerializeField] private bool useProgressivePenalty = false;
    [Tooltip("Límite 6: Guard Crush (sobrecarga por golpes consecutivos).")]
    [SerializeField] private bool useGuardCrush = false;
    [Tooltip("Límite 7: Sistema dinámico contextual.")]
    [SerializeField] private bool useDynamicContextual = false;

    [Header("Temporal Limit (1)")]
    [SerializeField] private float maxBlockDuration = 2f;
    [SerializeField] private float blockCooldownDuration = 1f;

    [Header("Durability Limit (2)")]
    [SerializeField] private float maxDurability = 100f;
    [SerializeField] private float currentDurability;
    [SerializeField] private float durabilityRegenRate = 10f;
    [SerializeField] private float durabilityRegenDelay = 2f;
    [SerializeField] private float heavyAttackDurabilityMultiplier = 2f;

    [Header("Angle Limit (3)")]
    [SerializeField, Range(0f, 180f)] private float limitedBlockAngle = 90f;

    [Header("Absorption Limit (4)")]
    [SerializeField] private float maxAbsorption = 200f;
    [SerializeField] private float currentAbsorption;
    [SerializeField] private float absorptionCooldown = 3f;

    [Header("Progressive Penalty (5)")]
    [SerializeField] private float penaltyStartTime = 1f;
    [SerializeField] private float movementPenaltyPerSecond = 0.1f;
    [SerializeField] private float maxMovementPenalty = 0.5f;

    [Header("Guard Crush (6)")]
    [SerializeField] private int maxConsecutiveBlocks = 3;
    [SerializeField] private float guardCrushWindow = 1f;
    [SerializeField] private float guardCrushStunDuration = 1.5f;

    [Header("Dynamic Contextual (7)")]
    [SerializeField] private float blockAbuseThreshold = 5f;
    [SerializeField] private float blockAbuseWindow = 10f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float enemyDetectionRadius = 15f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject blockVFX;
    [SerializeField] private GameObject blockBreakVFX;
    [SerializeField] private ParticleSystem blockParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private AudioClip blockBreakSound;

    #endregion

    #region State Variables

    private PlayerControlls playerControls;
    private PlayerMovement playerMovement;
    private PlayerStatsManager statsManager;

    public bool IsBlocking { get; private set; }
    private bool isOnCooldown;
    private bool isGuardCrushed;
    private bool canBlock = true;

    // Temporal limit state
    private float blockTimer;
    private float cooldownTimer;

    // Durability limit state
    private Coroutine durabilityRegenCoroutine;

    // Guard crush state
    private List<float> recentBlockTimes = new List<float>();

    // Progressive penalty state
    private float continuousBlockTime;
    private float currentMovementPenalty;

    // Dynamic contextual state
    private List<float> blockUsageTimes = new List<float>();
    private bool enemiesNotifiedOfBlockAbuse;

    // Conflict detection
    private bool hasConflicts;

    #endregion

    #region Events

    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnBlockBreak;
    public event Action OnGuardCrush;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.Defense.SetCallbacks(this);

        playerMovement = GetComponent<PlayerMovement>();
        statsManager = GetComponent<PlayerStatsManager>();
        audioSource = GetComponent<AudioSource>();

        ValidateLimitConfiguration();
        InitializeLimits();
    }

    private void OnEnable()
    {
        playerControls?.Defense.Enable();
    }

    private void OnDisable()
    {
        playerControls?.Defense.Disable();
        if (IsBlocking) StopBlocking();
    }

    private void OnDestroy()
    {
        playerControls?.Dispose();
    }

    private void Update()
    {
        if (!blockingEnabled) return;

        UpdateBlockState();
        UpdateLimits();
    }

    #endregion

    #region Input Handling

    public void OnShieldBlock(InputAction.CallbackContext context)
    {
        if (!blockingEnabled || !canBlock) return;

        // Manejar inicio y fin de bloqueo
        if (context.started || context.performed) 
        {
            if (!IsBlocking && !isOnCooldown && !isGuardCrushed)
            {
                StartBlocking();
            }
        }
        else if (context.canceled)
        {
            if (IsBlocking)
            {
                StopBlocking();
            }
        }
    }

    #endregion

    #region Blocking Logic

    private void StartBlocking()
    {
        if (hasConflicts)
        {
            ReportDebug("Bloqueo deshabilitado debido a conflictos en configuración de límites.", 2);
            return;
        }

        IsBlocking = true;
        blockTimer = 0f;
        continuousBlockTime = 0f;

        // Registrar uso para sistema dinámico
        if (useDynamicContextual)
        {
            blockUsageTimes.Add(Time.time);
            CleanOldBlockUsages();
        }

        // Visual feedback
        if (blockVFX != null) blockVFX.SetActive(true);
        if (blockParticles != null) blockParticles.Play();
        if (audioSource != null && blockSound != null) audioSource.PlayOneShot(blockSound);

        OnBlockStart?.Invoke();
        ReportDebug("Bloqueo iniciado.", 1);
    }

    private void StopBlocking()
    {
        IsBlocking = false;
        continuousBlockTime = 0f;
        currentMovementPenalty = 0f;

        // Visual feedback
        if (blockVFX != null) blockVFX.SetActive(false);
        if (blockParticles != null) blockParticles.Stop();

        // Iniciar cooldown si aplica
        if (useTemporalLimit)
        {
            StartCooldown();
        }

        // Iniciar regeneración de durabilidad
        if (useDurabilityLimit && durabilityRegenCoroutine == null)
        {
            durabilityRegenCoroutine = StartCoroutine(RegenerateDurability());
        }

        OnBlockEnd?.Invoke();
        ReportDebug("Bloqueo finalizado.", 1);
    }

    private void BreakBlock(string reason)
    {
        ReportDebug($"Bloqueo roto: {reason}", 1);

        StopBlocking();

        // Visual/Audio feedback
        if (blockBreakVFX != null)
        {
            Instantiate(blockBreakVFX, transform.position, Quaternion.identity);
        }
        if (audioSource != null && blockBreakSound != null)
        {
            audioSource.PlayOneShot(blockBreakSound);
        }

        OnBlockBreak?.Invoke();

        // Iniciar cooldown forzado
        if (useTemporalLimit)
        {
            StartCooldown();
        }
    }

    #endregion

    #region Block Validation

    /// <summary>
    /// Determina si el jugador puede bloquear un ataque basándose en su dirección.
    /// </summary>
    public bool CanBlockAttack(Vector3 attackDirection, bool isAoE = false)
    {
        if (!IsBlocking) return false;
        if (isAoE) return false; // No se pueden bloquear AoE

        // Calcular ángulo del ataque
        float angle = Vector3.Angle(transform.forward, attackDirection);

        // Determinar ángulo efectivo según límites activos
        float effectiveAngle = useAngleLimit ? limitedBlockAngle : frontBlockAngle;

        bool canBlock = angle <= effectiveAngle / 2f;

        if (!canBlock)
        {
            ReportDebug($"Ataque no bloqueado: ángulo {angle:F1}° excede límite de {effectiveAngle / 2f:F1}°", 1);
        }

        return canBlock;
    }

    /// <summary>
    /// Procesa el bloqueo de un ataque y aplica efectos de límites.
    /// </summary>
    public float ProcessBlockedAttack(float incomingDamage, bool isHeavyAttack = false)
    {
        if (!IsBlocking) return incomingDamage;

        // Aplicar límite de durabilidad
        if (useDurabilityLimit)
        {
            float durabilityDamage = isHeavyAttack ? incomingDamage * heavyAttackDurabilityMultiplier : incomingDamage;
            currentDurability -= durabilityDamage;

            if (currentDurability <= 0)
            {
                currentDurability = 0;
                BreakBlock("Durabilidad agotada");
                return incomingDamage; // Daño completo pasa
            }
        }

        // Aplicar límite de absorción
        if (useAbsorptionLimit)
        {
            currentAbsorption += incomingDamage;

            if (currentAbsorption >= maxAbsorption)
            {
                BreakBlock("Capacidad de absorción excedida");
                StartCoroutine(ResetAbsorptionAfterCooldown());
                return incomingDamage;
            }
        }

        // Aplicar límite de guard crush
        if (useGuardCrush)
        {
            recentBlockTimes.Add(Time.time);
            CleanOldBlockTimes();

            if (recentBlockTimes.Count >= maxConsecutiveBlocks)
            {
                TriggerGuardCrush();
                return incomingDamage;
            }
        }

        ReportDebug($"Ataque bloqueado: {incomingDamage} de daño absorbido.", 1);
        return 0f; // Daño completamente bloqueado
    }

    #endregion

    #region Limit Updates

    private void UpdateBlockState()
    {
        if (!IsBlocking) return;

        // Actualizar límite temporal
        if (useTemporalLimit)
        {
            blockTimer += Time.deltaTime;
            if (blockTimer >= maxBlockDuration)
            {
                BreakBlock("Duración máxima alcanzada");
            }
        }

        // Actualizar penalización progresiva
        if (useProgressivePenalty)
        {
            continuousBlockTime += Time.deltaTime;
            if (continuousBlockTime >= penaltyStartTime)
            {
                float penaltyTime = continuousBlockTime - penaltyStartTime;
                currentMovementPenalty = Mathf.Min(penaltyTime * movementPenaltyPerSecond, maxMovementPenalty);
                ApplyMovementPenalty(currentMovementPenalty);
            }
        }
    }

    private void UpdateLimits()
    {
        // Actualizar cooldown
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                isOnCooldown = false;
                ReportDebug("Cooldown de bloqueo finalizado.", 1);
            }
        }

        // Verificar abuso de bloqueo para sistema dinámico
        if (useDynamicContextual && !enemiesNotifiedOfBlockAbuse)
        {
            if (blockUsageTimes.Count >= blockAbuseThreshold)
            {
                NotifyEnemiesOfBlockAbuse();
            }
        }
    }

    #endregion

    #region Limit Implementations

    private void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = blockCooldownDuration;
        ReportDebug($"Cooldown de bloqueo iniciado: {blockCooldownDuration}s", 1);
    }

    private IEnumerator RegenerateDurability()
    {
        yield return new WaitForSeconds(durabilityRegenDelay);

        while (currentDurability < maxDurability && !IsBlocking)
        {
            currentDurability += durabilityRegenRate * Time.deltaTime;
            currentDurability = Mathf.Min(currentDurability, maxDurability);
            yield return null;
        }

        durabilityRegenCoroutine = null;
    }

    private IEnumerator ResetAbsorptionAfterCooldown()
    {
        yield return new WaitForSeconds(absorptionCooldown);
        currentAbsorption = 0f;
        ReportDebug("Absorción reseteada.", 1);
    }

    private void TriggerGuardCrush()
    {
        isGuardCrushed = true;
        BreakBlock("Guard Crush activado");

        OnGuardCrush?.Invoke();
        StartCoroutine(RecoverFromGuardCrush());

        ReportDebug($"¡Guard Crush! Aturdido por {guardCrushStunDuration}s", 2);
    }

    private IEnumerator RecoverFromGuardCrush()
    {
        yield return new WaitForSeconds(guardCrushStunDuration);
        isGuardCrushed = false;
        recentBlockTimes.Clear();
        ReportDebug("Recuperado de Guard Crush.", 1);
    }

    private void ApplyMovementPenalty(float penalty)
    {
        if (playerMovement != null)
        {
            // Aplicar penalización temporal a velocidad de movimiento
            // Esto debería integrarse con tu sistema de stats
            ReportDebug($"Penalización de movimiento aplicada: {penalty * 100:F0}%", 1);
        }
    }

    private void NotifyEnemiesOfBlockAbuse()
    {
        enemiesNotifiedOfBlockAbuse = true;

        Collider[] enemies = Physics.OverlapSphere(transform.position, enemyDetectionRadius, enemyLayer);

        foreach (var enemy in enemies)
        {
            // Intentar notificar al comportamiento del enemigo
            IAdaptiveEnemy adaptiveEnemy = enemy.GetComponent<IAdaptiveEnemy>();
            if (adaptiveEnemy != null)
            {
                adaptiveEnemy.OnPlayerBlockAbuse();
            }
        }

        ReportDebug($"Enemigos notificados de abuso de bloqueo. {enemies.Length} enemigos afectados.", 2);

        // Reset después de un tiempo
        StartCoroutine(ResetBlockAbuseNotification());
    }

    private IEnumerator ResetBlockAbuseNotification()
    {
        yield return new WaitForSeconds(blockAbuseWindow);
        enemiesNotifiedOfBlockAbuse = false;
        blockUsageTimes.Clear();
    }

    private void CleanOldBlockTimes()
    {
        float currentTime = Time.time;
        recentBlockTimes.RemoveAll(t => currentTime - t > guardCrushWindow);
    }

    private void CleanOldBlockUsages()
    {
        float currentTime = Time.time;
        blockUsageTimes.RemoveAll(t => currentTime - t > blockAbuseWindow);
    }

    #endregion

    #region Configuration

    private void ValidateLimitConfiguration()
    {
        // Detectar conflictos entre límites
        hasConflicts = false;

        // Conflicto: Temporal + Durabilidad (podrían competir)
        if (useTemporalLimit && useDurabilityLimit)
        {
            ReportDebug("ADVERTENCIA: Límite Temporal y Durabilidad activos simultáneamente. Pueden generar comportamiento inesperado.", 2);
        }

        // Conflicto: Guard Crush + Temporal muy corto
        if (useGuardCrush && useTemporalLimit && maxBlockDuration < guardCrushWindow)
        {
            ReportDebug("CONFLICTO DETECTADO: Guard Crush requiere tiempo de bloqueo mayor que límite temporal.", 3);
            hasConflicts = true;
        }

        if (hasConflicts)
        {
            ReportDebug("Sistema de bloqueo desactivado por conflictos. Revise configuración.", 3);
            blockingEnabled = false;
        }
    }

    private void InitializeLimits()
    {
        if (useDurabilityLimit) currentDurability = maxDurability;
        if (useAbsorptionLimit) currentAbsorption = 0f;
    }

    #endregion

    #region Public API

    public void SetBlockingEnabled(bool enabled)
    {
        blockingEnabled = enabled;
        if (!enabled && IsBlocking) StopBlocking();
    }

    public float GetDurabilityPercentage()
    {
        if (!useDurabilityLimit) return 100f;
        return (currentDurability / maxDurability) * 100f;
    }

    public float GetAbsorptionPercentage()
    {
        if (!useAbsorptionLimit) return 0f;
        return (currentAbsorption / maxAbsorption) * 100f;
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        // Dibujar ángulo de bloqueo
        Gizmos.color = IsBlocking ? Color.green : Color.yellow;
        float angle = useAngleLimit ? limitedBlockAngle : frontBlockAngle;

        Vector3 leftBoundary = Quaternion.Euler(0, -angle / 2f, 0) * transform.forward * 3f;
        Vector3 rightBoundary = Quaternion.Euler(0, angle / 2f, 0) * transform.forward * 3f;

        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Dibujar radio de detección para sistema dinámico
        if (useDynamicContextual)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, enemyDetectionRadius);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerBlockSystem] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerBlockSystem] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerBlockSystem] {message}");
                break;
        }
    }
}

/// <summary>
/// Interfaz para enemigos que pueden adaptar su comportamiento al bloqueo del jugador.
/// </summary>
public interface IAdaptiveEnemy
{
    void OnPlayerBlockAbuse();
}