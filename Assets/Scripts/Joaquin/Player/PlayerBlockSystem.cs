using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Sistema de bloqueo para el jugador con múltiples tipos de límites configurables.
/// - Bloquea ataques frontales
/// - No bloquea: ataques desde abajo, costados, detrás, o AoE
/// - Límites opcionales: temporal, durabilidad, ángulo, absorción, penalización, guard crush, dinámico
/// </summary>
public class PlayerBlockSystem : MonoBehaviour, PlayerControlls.IDefenseActions
{
    #region Serialized Fields

    [Header("Core Configuration")]
    [SerializeField] private bool blockingEnabled = true;
    [Tooltip("Ángulo frontal de bloqueo (en grados).")]
    [SerializeField, Range(0f, 180f)] private float frontBlockAngle = 150f;

    [Header("Rotation Mode")]
    [Tooltip("Si está activo, rota usando el mouse proyectado en el suelo. Si no, usa input de movimiento.")]
    [SerializeField] private bool useMouseRotation = true;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Durability System")]
    [Tooltip("Daño total que puede absorber antes de romperse.")]
    [SerializeField] private float maxDurability = 30f;
    [SerializeField] private float currentDurability;
    [Tooltip("Tiempo de recarga completa (0% a 100%).")]
    [SerializeField] private float durabilityRechargeTime = 5f;
    [Tooltip("Porcentaje mínimo para poder usar el escudo (0.25 = 25%).")]
    [SerializeField, Range(0f, 1f)] private float minimumDurabilityToUse = 0.25f;
    [Tooltip("Delay antes de iniciar la recarga de durabilidad.")]
    [SerializeField] private float rechargeDelay = 2f;

    [Header("Stun on Break")]
    [Tooltip("Duración del stun cuando el escudo se rompe según la edad.")]
    [SerializeField] private float stunDurationYoung = 1.5f;
    [SerializeField] private float stunDurationAdult = 1.2f;
    [SerializeField] private float stunDurationElder = 0.9f;

    [Header("UI References")]
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private Image durabilityFillImage;
    [SerializeField] private TextMeshProUGUI durabilityPercentageText;
    [SerializeField] private GameObject durabilityUIGroup;
    [SerializeField] private float hideDelay = 2f;

    [Header("Animation Settings")]
    [SerializeField] private bool useSmoothing = true; // Usar interpolación suave
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool pulseWhenLow = true; // Pulsar cuando la estamina está baja
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinScale = 0.95f;
    [SerializeField] private float pulseMaxScale = 1.05f;

    [Header("Visual Settings")]
    [SerializeField] private Color fullDurabilityColor = new Color(0.2f, 0.8f, 1f); // Azul
    [SerializeField] private Color midDurabilityColor = new Color(1f, 0.8f, 0f); // Amarillo
    [SerializeField] private Color lowDurabilityColor = new Color(1f, 0.2f, 0.2f); // Rojo
    [SerializeField] private Color emptyDurabilityColor = new Color(0.5f, 0.5f, 0.5f); // Gris
    [Range(0f, 1f)]
    [SerializeField] private float lowDurabilityThreshold = 0.25f; // 25%
    [Range(0f, 1f)]
    [SerializeField] private float midDurabilityThreshold = 0.5f; // 50%

    [Header("Visual Feedback")]
    [SerializeField] private GameObject blockVFX;
    [SerializeField] private GameObject blockBreakVFX;
    [SerializeField] private ParticleSystem blockParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private AudioClip blockBreakSound;
    [SerializeField] private AudioClip blockHitSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    #endregion

    #region State Variables

    private bool isInitialized = false;

    private PlayerControlls playerControls;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private Camera mainCamera;

    public bool IsBlocking { get; private set; }
    private bool isStunned = false;
    private bool isDurabilityRecharging = false;

    private Coroutine hideDurabilityCoroutine;
    private Coroutine durabilityRechargeCoroutine;
    private Coroutine stunCoroutine;

    private Vector2 movementInput;
    private Vector3 originalScale;

    #endregion

    #region Events

    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnBlockBreak;
    public event Action<float> OnDurabilityChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        isInitialized = true;

        IsBlocking = false;

        playerControls = new PlayerControlls();
        playerControls.Defense.SetCallbacks(this);
        playerControls.Movement.SetCallbacks(null);

        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        audioSource = GetComponent<AudioSource>();
        mainCamera = Camera.main;

        currentDurability = maxDurability;

        originalScale = durabilityUIGroup != null ? durabilityUIGroup.transform.localScale : Vector3.one;

        if (durabilityUIGroup != null) durabilityUIGroup.SetActive(false);

        if (durabilityFillImage != null)
        {
            durabilityFillImage.color = fullDurabilityColor;
        }

        if (blockingEnabled)
        {
            currentDurability = maxDurability;
        }
        else
        {
            if (durabilityUIGroup != null) durabilityUIGroup.SetActive(false);
        }

        UpdateUI();
    }

    private void OnEnable()
    {
        playerControls?.Defense.Enable();
        playerControls?.Movement.Enable();
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        playerControls?.Defense.Disable();
        playerControls?.Movement.Disable();

        if (IsBlocking) StopBlocking();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();

        playerControls?.Dispose();

        if (IsBlocking) StopBlocking();
    }

    private void Update()
    {
        if (blockingEnabled)
        {
            UpdateUI();
        }

        if (!blockingEnabled || !IsBlocking) return;

        // Leer el input de movimiento directamente para la rotación
        movementInput = playerControls.Movement.Move.ReadValue<Vector2>();

        HandleRotationWhileBlocking();
    }

    #endregion

    #region Input Handling

    public void OnShieldBlock(InputAction.CallbackContext context)
    {
        if (!blockingEnabled || isStunned) return;

        if (context.started || context.performed)
        {
            if (!IsBlocking && CanUseBlock())
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

    private bool CanUseBlock()
    {
        float minimumDurability = maxDurability * minimumDurabilityToUse;

        if (currentDurability < minimumDurability)
        {
            ReportDebug($"No se puede usar el escudo. Durabilidad insuficiente: {currentDurability}/{minimumDurability:F1}", 2);
            return false;
        }

        return true;
    }

    private void StartBlocking()
    {
        IsBlocking = true;

        // Detener recarga si está activa
        if (durabilityRechargeCoroutine != null)
        {
            StopCoroutine(durabilityRechargeCoroutine);
            durabilityRechargeCoroutine = null;
            isDurabilityRecharging = false;
        }

        // Inmovilizar al jugador
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        if (durabilityUIGroup != null)
        {
            durabilityUIGroup.SetActive(true);
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

        // Restaurar movimiento
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }

        if (durabilityUIGroup != null)
        {
            HideDurabilityBar(hideDelay);
        }

        // Visual feedback
        if (blockVFX != null) blockVFX.SetActive(false);
        if (blockParticles != null) blockParticles.Stop();

        // Iniciar recarga con delay
        if (durabilityRechargeCoroutine == null)
        {
            durabilityRechargeCoroutine = StartCoroutine(RechargeDurability());
        }

        OnBlockEnd?.Invoke();
        ReportDebug("Bloqueo finalizado.", 1);
    }

    private void BreakBlock()
    {
        ReportDebug("¡Escudo roto! Iniciando stun...", 2);

        StopBlocking();

        // Visual/Audio feedback
        if (blockBreakVFX != null)
        {
            Instantiate(blockBreakVFX, transform.position + Vector3.up, Quaternion.identity);
        }
        if (audioSource != null && blockBreakSound != null)
        {
            audioSource.PlayOneShot(blockBreakSound);
        }

        OnBlockBreak?.Invoke();

        // Aplicar stun según edad
        ApplyStunBasedOnAge();

        // La durabilidad queda en 0 y comenzará a recargarse después del stun
        currentDurability = 0f;
        OnDurabilityChanged?.Invoke(GetDurabilityPercentage());
    }

    #endregion

    #region Rotation While Blocking

    private void HandleRotationWhileBlocking()
    {
        if (useMouseRotation)
        {
            RotateTowardsMouse();
        }
        else
        {
            RotateWithMovementInput();
        }
    }

    private void RotateTowardsMouse()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 targetPosition = hit.point;
            targetPosition.y = transform.position.y;

            Vector3 direction = (targetPosition - transform.position).normalized;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void RotateWithMovementInput()
    {
        if (movementInput.sqrMagnitude < 0.01f) return;

        // Calcular dirección basada en la cámara (igual que PlayerMovement)
        Vector3 cameraForward = mainCamera != null ? mainCamera.transform.forward : Vector3.forward;
        Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 direction = (cameraForward * movementInput.y + cameraRight * movementInput.x).normalized;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region Block Validation

    /// <summary>
    /// Determina si el jugador puede bloquear un ataque basándose en la posición del atacante.
    /// </summary>
    /// <param name="attackerPosition">Posición del atacante</param>
    /// <returns>True si el ataque está en el arco frontal de bloqueo</returns>
    public bool CanBlockAttack(Vector3 attackerPosition)
    {
        if (!IsBlocking) return false;

        // Calcular dirección hacia el atacante
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0;
        toAttacker.Normalize();

        // Dirección frontal del escudo
        Vector3 shieldForward = transform.forward;
        shieldForward.y = 0;
        shieldForward.Normalize();

        // Calcular ángulo
        float angle = Vector3.Angle(shieldForward, toAttacker);

        bool canBlock = angle <= (frontBlockAngle * 0.5f);

        if (debugMode)
        {
            ReportDebug($"Ángulo de ataque: {angle:F1}°, Límite: {frontBlockAngle * 0.5f}°, {(canBlock ? "BLOQUEADO" : "NO BLOQUEADO")}", 1);
        }

        return canBlock;
    }

    /// <summary>
    /// Procesa un ataque bloqueado y reduce la durabilidad.
    /// </summary>
    /// <param name="incomingDamage">Daño del ataque</param>
    /// <returns>Daño que pasa al jugador (0 si se bloquea completamente)</returns>
    public float ProcessBlockedAttack(float incomingDamage)
    {
        if (!IsBlocking) return incomingDamage;

        // Reducir durabilidad
        currentDurability -= incomingDamage;
        OnDurabilityChanged?.Invoke(GetDurabilityPercentage());
        
        // Audio de impacto
        if (audioSource != null && blockHitSound != null)
        {
            audioSource.PlayOneShot(blockHitSound);
        }

        ReportDebug($"Ataque bloqueado: {incomingDamage} daño absorbido. Durabilidad: {currentDurability}/{maxDurability}", 1);

        // Verificar si se rompió
        if (currentDurability <= 0)
        {
            currentDurability = 0;
            BreakBlock();
            return 0f; // El daño ya se absorbió antes de romperse
        }

        return 0f; // Daño completamente bloqueado
    }

    #endregion

    #region Durability System

    private IEnumerator RechargeDurability()
    {
        // Esperar delay antes de iniciar recarga
        yield return new WaitForSeconds(rechargeDelay);

        isDurabilityRecharging = true;
        ReportDebug("Iniciando recarga de durabilidad...", 1);

        float rechargeRate = maxDurability / durabilityRechargeTime;

        while (currentDurability < maxDurability)
        {
            currentDurability += rechargeRate * Time.deltaTime;
            currentDurability = Mathf.Min(currentDurability, maxDurability);

            OnDurabilityChanged?.Invoke(GetDurabilityPercentage());

            yield return null;
        }

        currentDurability = maxDurability;
        isDurabilityRecharging = false;
        durabilityRechargeCoroutine = null;

        ReportDebug("Durabilidad completamente recargada.", 1);
    }

    public float GetDurabilityPercentage()
    {
        return (currentDurability / maxDurability) * 100f;
    }

    public float GetCurrentDurability()
    {
        return currentDurability;
    }

    public float GetMaxDurability()
    {
        return maxDurability;
    }

    #endregion

    public void UpdateUI()
    {
        if (!isInitialized) return;

        if (blockingEnabled)
        {
            UpdateDurabilityUI();
        }
    }

    private void UpdateDurabilityUI()
    {
        float targetFill = currentDurability;
        float currentDisplayFill = durabilitySlider != null ? durabilitySlider.value : targetFill;

        float fillPercent = currentDurability / maxDurability;

        // Actualizar barra de durabilidad
        if (durabilitySlider != null)
        {
            if (useSmoothing && Mathf.Abs(currentDisplayFill - targetFill) > 0.01f)
            {
                currentDisplayFill = Mathf.Lerp(currentDisplayFill, targetFill, smoothSpeed * Time.deltaTime);
                durabilitySlider.value = currentDisplayFill;
            }
            else
            {
                durabilitySlider.value = targetFill;
            }

            durabilitySlider.maxValue = maxDurability;
        }

        // Actualizar color de la barra
        if (durabilityFillImage != null)
        {
            UpdateDurabilityColor(fillPercent);
        }

        // Actualizar texto de porcentaje
        if (durabilityPercentageText != null)
        {
            durabilityPercentageText.text = $"{(fillPercent * 100f):F0}%";
        }

        // Efecto de pulso
        if (pulseWhenLow && fillPercent <= (minimumDurabilityToUse / maxDurability) && durabilityUIGroup != null)
        {
            float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f);
            durabilityUIGroup.transform.localScale = originalScale * scale;
        }
        else if (durabilityUIGroup != null)
        {
            // Resetea la escala si no debe pulsar
            durabilityUIGroup.transform.localScale = originalScale;
        }
    }

    private void UpdateDurabilityColor(float fillAmount)
    {
        if (durabilityFillImage == null) return;

        Color targetColor;

        if (fillAmount <= 0.01f)
        {
            targetColor = emptyDurabilityColor;
        }
        else if (fillAmount <= lowDurabilityThreshold)
        {
            targetColor = lowDurabilityColor;
        }
        else if (fillAmount <= midDurabilityThreshold)
        {
            float t = (fillAmount - lowDurabilityThreshold) / (midDurabilityThreshold - lowDurabilityThreshold);
            targetColor = Color.Lerp(lowDurabilityColor, midDurabilityColor, t);
        }
        else
        {
            float t = (fillAmount - midDurabilityThreshold) / (1f - midDurabilityThreshold);

            targetColor = Color.Lerp(midDurabilityColor, fullDurabilityColor, t);
        }

        durabilityFillImage.color = targetColor;
    }

    private void HideDurabilityBar(float duration)
    {
        if (hideDurabilityCoroutine != null)
        {
            StopCoroutine(hideDurabilityCoroutine);
        }

        hideDurabilityCoroutine = StartCoroutine(HideDurabilityBarTimer(duration));
    }

    private IEnumerator HideDurabilityBarTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        if (durabilityUIGroup != null)
        {
            durabilityUIGroup.SetActive(false);
        }

        hideDurabilityCoroutine = null;
    }

    #region Stun System

    private void ApplyStunBasedOnAge()
    {
        if (playerHealth == null)
        {
            ReportDebug("PlayerHealth no encontrado. No se puede determinar edad para stun.", 3);
            return;
        }

        float stunDuration = stunDurationAdult;

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                stunDuration = stunDurationYoung;
                break;
            case PlayerHealth.LifeStage.Adult:
                stunDuration = stunDurationAdult;
                break;
            case PlayerHealth.LifeStage.Elder:
                stunDuration = stunDurationElder;
                break;
        }

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
        }

        stunCoroutine = StartCoroutine(StunRoutine(stunDuration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        // Inmovilizar completamente
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        ReportDebug($"Jugador aturdido por {duration}s debido a rotura de escudo.", 2);

        yield return new WaitForSeconds(duration);

        isStunned = false;

        // Restaurar movimiento
        if (playerMovement != null && !IsBlocking)
        {
            playerMovement.SetCanMove(true);
        }

        // Iniciar recarga después del stun
        if (durabilityRechargeCoroutine == null)
        {
            durabilityRechargeCoroutine = StartCoroutine(RechargeDurability());
        }

        stunCoroutine = null;
        ReportDebug("Stun finalizado. Recarga de durabilidad iniciada.", 1);
    }

    public bool IsStunned()
    {
        return isStunned;
    }

    #endregion

    #region Public API

    public void SetBlockingEnabled(bool enabled)
    {
        blockingEnabled = enabled;
        if (!enabled && IsBlocking) StopBlocking();
    }

    public bool IsRecharging()
    {
        return isDurabilityRecharging;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        // Dibujar ángulo de bloqueo
        Gizmos.color = IsBlocking ? Color.green : Color.yellow;

        Vector3 forward = transform.forward;
        float halfAngle = frontBlockAngle * 0.5f;

        Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * forward * 2f;
        Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * forward * 2f;

        Gizmos.DrawLine(transform.position, transform.position + forward * 2f);
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Dibujar arco
        int segments = 20;
        Vector3 prevPoint = transform.position + leftBoundary;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = transform.position + dir * 2f;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // Color según estado
        if (isStunned)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
        else if (currentDurability < maxDurability * minimumDurabilityToUse)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
    }

    #endregion

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