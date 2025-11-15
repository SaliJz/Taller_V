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

    [Header("References")]
    [SerializeField] private Animator playerAnimator;

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

    [Header("Young Block - Reflection")]
    [SerializeField] private LayerMask projectileLayer;
    [SerializeField] private float reflectionConeAngle = 90f;
    [SerializeField] private float reflectionDetectionRadius = 5f;

    [Header("Adult Block - Counter")]
    [SerializeField] private float counterChargeTime = 0.5f;
    [SerializeField] private float counterReleaseRange = 3f;
    [SerializeField] private float counterKnockbackForce = 2f;
    [SerializeField] private GameObject counterVFXPrefab;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Elder Block - Charged Explosion")]
    [SerializeField] private float explosionChargeTime = 3f;
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private int explosionDamage = 8;
    [SerializeField] private float explosionKnockback = 3f;
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private GameObject chargingVFXPrefab;
    private GameObject activeChargingVFX;

    [Header("Charge UI - Adult & Elder")]
    [SerializeField] private Slider chargeSlider;
    [SerializeField] private Image chargeFillImage;
    [SerializeField] private GameObject chargeUIGroup;
    [SerializeField] private Color adultChargeColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color elderChargeColor = new Color(0.8f, 0.2f, 1f);

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

    private float blockStartTime = 0f;
    private bool isCharging = false;
    private float chargeProgress = 0f;

    private Coroutine chargeCoroutine;
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
        playerAnimator = GetComponent<Animator>();
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
            UpdateChargeUI();
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
        if (playerAnimator != null) playerAnimator.SetBool("Block", true);

        blockStartTime = Time.time;
        isCharging = false;
        chargeProgress = 0f;

        if (durabilityRechargeCoroutine != null)
        {
            StopCoroutine(durabilityRechargeCoroutine);
            durabilityRechargeCoroutine = null;
            isDurabilityRecharging = false;
        }

        if (chargeUIGroup != null && playerHealth != null &&
            (playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Adult ||
            playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Elder))
        {
            chargeUIGroup.SetActive(true);
        }

        if (hideDurabilityCoroutine != null)
        {
            StopCoroutine(hideDurabilityCoroutine);
            hideDurabilityCoroutine = null;
        }

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        if (durabilityUIGroup != null)
        {
            durabilityUIGroup.SetActive(true);
        }

        if (blockVFX != null) blockVFX.SetActive(true);
        if (blockParticles != null) blockParticles.Play();
        if (audioSource != null && blockSound != null) audioSource.PlayOneShot(blockSound);

        if (playerHealth != null && playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Elder)
        {
            StartElderCharge();
        }

        OnBlockStart?.Invoke();
        ReportDebug("Bloqueo iniciado.", 1);
    }

    private void StopBlocking()
    {
        if (playerHealth != null &&
            playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Adult &&
            Time.time - blockStartTime >= counterChargeTime)
        {
            ExecuteAdultCounter();
        }

        if (chargeUIGroup != null)
        {
            chargeUIGroup.SetActive(false);
        }

        if (playerHealth != null &&
            playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Elder &&
            isCharging && chargeProgress >= 1f)
        {
            ExecuteElderExplosion();
        }

        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }
        isCharging = false;
        chargeProgress = 0f;

        if (activeChargingVFX != null)
        {
            Destroy(activeChargingVFX);
            activeChargingVFX = null;
        }

        IsBlocking = false;
        if (playerAnimator != null) playerAnimator.SetBool("Block", false);

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
            playerMovement.UnlockFacing();
        }

        if (hideDurabilityCoroutine != null)
        {
            StopCoroutine(hideDurabilityCoroutine);
            hideDurabilityCoroutine = null;
        }

        if (durabilityUIGroup != null && !isStunned)
        {
            HideDurabilityBar(hideDelay);
        }

        if (blockVFX != null) blockVFX.SetActive(false);
        if (blockParticles != null) blockParticles.Stop();

        if (durabilityRechargeCoroutine == null && currentDurability < maxDurability)
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

    // Rota el jugador hacia la posición del mouse proyectada en el suelo
    private void RotateTowardsMouse()
    {
        if (mainCamera == null) return;

        // Crear un rayo desde la cámara hacia la posición del mouse
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Definir un plano horizontal en la posición del jugador
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float enter))
        {
            // Obtener el punto en el plano donde el rayo intersecta
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            direction.Normalize();

            // Rotar suavemente hacia esa dirección con velocidad constante
            if (direction != Vector3.zero)
            {
                playerMovement.LockFacingTo8Directions(direction, true);
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
    public float ProcessBlockedAttack(float incomingDamage, GameObject attacker = null)
    {
        if (!IsBlocking) return incomingDamage;

        if (playerAnimator != null) playerAnimator.SetTrigger("BlockSuccess");

        if (playerHealth != null && playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Young)
        {
            if (attacker != null)
            {
                var projectileScript = attacker.GetComponent<MorlockProjectile>();
                if (projectileScript != null && !projectileScript.WasReflected)
                {
                    ReflectProjectile(attacker);
                    ReportDebug($"Proyectil {attacker.name} reflejado.", 1);

                    if (audioSource != null && blockHitSound != null)
                    {
                        audioSource.PlayOneShot(blockHitSound);
                    }

                    // No se si esto deberia, pero por si acaso incluyo que apesar de reflejar el proyectil igualmente le gasta la energia de escudo
                    currentDurability -= incomingDamage;
                    OnDurabilityChanged?.Invoke(GetDurabilityPercentage());

                    return 0f;
                }
            }
        }

        currentDurability -= incomingDamage;
        OnDurabilityChanged?.Invoke(GetDurabilityPercentage());

        if (audioSource != null && blockHitSound != null)
        {
            audioSource.PlayOneShot(blockHitSound);
        }

        ReportDebug($"Ataque bloqueado: {incomingDamage} daño absorbido. Durabilidad: {currentDurability}/{maxDurability}", 1);

        if (currentDurability <= 0)
        {
            currentDurability = 0;
            BreakBlock();
            return 0f;
        }

        return 0f; 
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
    private void UpdateChargeUI()
    {
        if (chargeUIGroup == null) return;

        bool shouldShow = IsBlocking && playerHealth != null &&
                         (playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Adult ||
                          playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Elder);

        chargeUIGroup.SetActive(shouldShow);

        if (!shouldShow) return;

        if (chargeSlider != null)
        {
            if (playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Adult)
            {
                float progress = Mathf.Clamp01((Time.time - blockStartTime) / counterChargeTime);
                chargeSlider.value = progress;

                if (chargeFillImage != null)
                    chargeFillImage.color = adultChargeColor;
            }
            else if (playerHealth.CurrentLifeStage == PlayerHealth.LifeStage.Elder)
            {
                chargeSlider.value = chargeProgress;

                if (chargeFillImage != null)
                    chargeFillImage.color = elderChargeColor;
            }
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

        if (durabilityUIGroup != null && !IsBlocking)
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

    public bool IsBlockingState()
    {
        return IsBlocking;
    }

    #endregion

    #region Young - Projectile Reflection

    private void ReflectProjectile(GameObject projectile)
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, reflectionDetectionRadius, enemyLayer);

        Transform nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        Vector3 forward = transform.forward;

        foreach (Collider col in nearbyEnemies)
        {
            Vector3 toEnemy = (col.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(forward, toEnemy);

            if (angle <= reflectionConeAngle * 0.5f)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = col.transform;
                }
            }
        }

        var projectileScript = projectile.GetComponent<MorlockProjectile>();

        if (projectileScript != null)
        {
            if (nearestEnemy != null)
            {
                Vector3 directionToEnemy = (nearestEnemy.position - projectile.transform.position).normalized;
                projectileScript.Redirect(directionToEnemy);
                ReportDebug($"Proyectil reflejado hacia {nearestEnemy.name}", 1);
            }
            else
            {
                Vector3 reflectDirection = transform.forward;
                projectileScript.Redirect(reflectDirection);
                ReportDebug("Proyectil reflejado hacia adelante (sin objetivo específico)", 1);
            }
        }
        else
        {
            Vector3 reflectDirection = nearestEnemy != null
                ? (nearestEnemy.position - projectile.transform.position).normalized
                : transform.forward;

            projectile.transform.forward = reflectDirection;

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = reflectDirection * rb.linearVelocity.magnitude;
            }

            ReportDebug("Proyectil sin MorlockProjectile reflejado (fallback)", 1);
        }
    }

    #endregion

    #region Adult - Counter Attack

    private void ExecuteAdultCounter()
    {
        ReportDebug("Ejecutando embestida de Adulto (EffectBlock)...", 1);

        if (counterVFXPrefab != null)
        {
            GameObject dashInstance = Instantiate(
                counterVFXPrefab,
                transform.position,
                Quaternion.LookRotation(transform.forward)
            );

            if (dashInstance.TryGetComponent<EffectBlock>(out var effectBlockScript))
            {
                effectBlockScript.Initialize(
                    transform.forward,
                    transform.position,
                    transform.rotation
                );
            }
            else
            {
                ReportDebug("Error: El 'counterVFXPrefab' no contiene el script 'EffectBlock'. Asegúrate de que está asignado en el prefab.", 3);
                Destroy(dashInstance);
            }
        }
        else
        {
            ReportDebug("Error: 'counterVFXPrefab' no está asignado. No se puede ejecutar la embestida.", 3);
        }
    }

    public Vector3 GetReflectionDirection(Vector3 projectilePosition, float reflectionAngle)
    {
        return Vector3.zero;
    }

    #endregion

    #region Elder - Charged Explosion

    private void StartElderCharge()
    {
        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
        }

        chargeCoroutine = StartCoroutine(ChargeExplosion());
    }

    private IEnumerator ChargeExplosion()
    {
        isCharging = true;
        chargeProgress = 0f;

        if (chargingVFXPrefab != null)
        {
            activeChargingVFX = Instantiate(chargingVFXPrefab, transform.position + Vector3.up, Quaternion.identity);
            activeChargingVFX.transform.SetParent(transform);
        }

        float elapsedTime = 0f;

        while (elapsedTime < explosionChargeTime && IsBlocking)
        {
            elapsedTime += Time.deltaTime;
            chargeProgress = elapsedTime / explosionChargeTime;

            if (activeChargingVFX != null)
            {
                activeChargingVFX.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, chargeProgress);
            }

            yield return null;
        }

        chargeProgress = 1f;
        ReportDebug("Explosión de Viejo completamente cargada.", 1);

        chargeCoroutine = null;
    }

    private void ExecuteElderExplosion()
    {
        ReportDebug("Ejecutando explosión defensiva de Viejo...", 1);

        if (activeChargingVFX != null)
        {
            Destroy(activeChargingVFX);
            activeChargingVFX = null;
        }

        Vector3 explosionPosition = transform.position + transform.forward * 1.5f;

        if (explosionVFXPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVFXPrefab, explosionPosition, Quaternion.identity);

            if (vfx.TryGetComponent<ExplosionDamage>(out var damageScript))
            {
                damageScript.SetDamage(explosionDamage);
                damageScript.SetKnockback(explosionKnockback, 0.5f);
            }

            if (vfx.TryGetComponent<ExplosionScaleOverTime>(out var scaleScript))
            {
                scaleScript.EndScale = Vector3.one * explosionRadius * 2f;
            }

            Destroy(vfx, 3f);
        }

        ReportDebug("Explosión de Viejo instanciada.", 1);
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

    public BlockConfig GetBlockConfigForCurrentStage()
    {
        if (playerHealth == null)
        {
            return new BlockConfig
            {
                canReflectProjectiles = false,
                canCounterMelee = false,
                canChargeExplosion = false,
                explosionDamage = 0,
                explosionRadius = 0,
                explosionKnockback = 0,
                chargeTime = 0
            };
        }

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                return new BlockConfig
                {
                    canReflectProjectiles = true,
                    reflectionAngle = 90f,
                    canCounterMelee = false,
                    canChargeExplosion = false,
                    explosionDamage = 0,
                    explosionRadius = 0,
                    explosionKnockback = 0,
                    chargeTime = 0
                };

            case PlayerHealth.LifeStage.Adult:
                return new BlockConfig
                {
                    canReflectProjectiles = false,
                    reflectionAngle = 0f,
                    canCounterMelee = true,
                    counterRange = 3f,
                    counterKnockback = 2f,
                    canChargeExplosion = false,
                    explosionDamage = 0,
                    explosionRadius = 0,
                    explosionKnockback = 0,
                    chargeTime = 0
                };

            case PlayerHealth.LifeStage.Elder:
                return new BlockConfig
                {
                    canReflectProjectiles = false,
                    reflectionAngle = 0f,
                    canCounterMelee = false,
                    canChargeExplosion = true,
                    explosionDamage = 8,
                    explosionRadius = 2.5f,
                    explosionKnockback = 3f,
                    chargeTime = 3f
                };

            default:
                return new BlockConfig
                {
                    canReflectProjectiles = false,
                    canCounterMelee = false,
                    canChargeExplosion = false,
                    explosionDamage = 0,
                    explosionRadius = 0,
                    explosionKnockback = 0,
                    chargeTime = 0
                };
        }
    }

    public struct BlockConfig
    {
        public bool canReflectProjectiles;
        public float reflectionAngle;
        public bool canCounterMelee;
        public float counterRange;
        public float counterKnockback;
        public bool canChargeExplosion;
        public int explosionDamage;
        public float explosionRadius;
        public float explosionKnockback;
        public float chargeTime;
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