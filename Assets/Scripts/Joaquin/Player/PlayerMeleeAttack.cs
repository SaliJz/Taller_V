// PlayerMeleeAttack.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// Ahora: primero se rota (snap a 8 direcciones), espera la rotación, y luego ejecuta el ataque.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject visualSphereHit;
    [SerializeField] private GameObject visualBoxHit;
    [SerializeField] private PlayerShieldController playerShieldController;
    [SerializeField] private Animator playerAnimator;

    [Header("Attack Configuration")]
    [SerializeField] private Transform hitPoint;
    [Header("Fallback stats (if no StatsManager)")]
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [HideInInspector] private float fallbackAttackDamage = 10;
    [HideInInspector] private float fallbackAttackSpeed = 1f;
    [Header("Calculated stats")]
    [SerializeField] private float hitRadius = 0.8f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool canShowHitGizmo = false;

    [Header("Combo Configuration")]
    [SerializeField] private float comboResetTime = 2f; // Tiempo sin atacar para resetear combo
    [SerializeField] private float[] comboMovementForces = new float[3] { 1.5f, 2f, 1.8f }; // Fuerza de empuje por ataque
    [SerializeField] private float[] comboLockDurations = new float[3] { 0.4f, 0.6f, 0.8f }; // Duración del lock por ataque

    [Header("Attack 1 (Basic)")]
    [SerializeField] private float attack1Duration = 0.4f;

    [Header("Attack 2 (Area/Spin)")]
    [SerializeField] private float attack2Duration = 0.6f;
    [SerializeField] private float attack2SpinSpeed = 900f;

    [Header("Attack 3 (Heavy/Charge)")]
    [SerializeField] private float attack3PreChargeDuration = 0.3f;
    [SerializeField] private float attack3ChargeDuration = 0.3f;
    [SerializeField] private float attack3SpinSpeed = 90f;
    [SerializeField] private float attack3StunDuration = 0.5f;

    [Header("knockback Configuration")]
    [SerializeField] private float knockbackYoung = 0.25f;
    [SerializeField] private float knockbackAdult = 0.5f;
    [SerializeField] private float knockbackElder = 0.75f;
    [SerializeField] private float knockbackMaxDistance = 3f; // Distancia máxima de knockback

    [Header("Melee Impact VFX")]
    [SerializeField] private ParticleSystem meleeImpactVFX;
    [SerializeField] private int impactParticleCount = 20;

    [Header("Debug")]
    [SerializeField] private bool useBoxCollider = false;
    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.2f;

    private float attackCooldown = 0f;
    private int finalAttackDamage;
    private float finalAttackSpeed;

    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;

    private bool isAttacking = false;

    private int comboCount = 0;
    private float lastAttackTime = 0f;
    private int currentAttackIndex = -1;
    
    private HashSet<Collider> hitEnemiesThisCombo = new HashSet<Collider>();
    private Collider[] hitBuffer = new Collider[64];

    public int AttackDamage
    {
        get { return attackDamage; }
        set { attackDamage = value; }
    }

    public bool IsAttacking => isAttacking;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private Material meleeImpactMatInstance;
    private Coroutine cleanupCoroutine;

    private void Awake()
    {
        if (visualSphereHit != null) visualSphereHit.SetActive(false);
        if (visualBoxHit != null) visualBoxHit.SetActive(false);

        statsManager = GetComponent<PlayerStatsManager>();
        playerHealth = GetComponent<PlayerHealth>();
        playerShieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponentInChildren<Animator>();

        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerMeleeAttack. Usando valores de fallback.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no se encuentra en el objeto.", 3);
        if (playerShieldController == null) ReportDebug("PlayerShieldController no se encuentra en el objeto.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no se encuentra en el objeto. Lock de rotación no funcionará.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    //private void OnDisable()
    //{
    //    PlayerStatsManager.OnStatChanged -= HandleStatChanged;

    //    if (cleanupCoroutine != null)
    //    {
    //        StopCoroutine(cleanupCoroutine);
    //        cleanupCoroutine = null;
    //    }

    //    CleanupVFXImmediate();
    //}

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;

        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
        }

        CleanupVFXImmediate();
        StopAllCoroutines();
    }

    private void Start()
    {
        showGizmo = false;

        float hitRadiusStat = statsManager != null ? statsManager.GetStat(StatType.MeleeRadius) : fallbackHitRadius;
        hitRadius = hitRadiusStat;

        float attackDamageStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackDamage) : fallbackAttackDamage;
        attackDamage = Mathf.RoundToInt(attackDamageStat);

        float attackSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackSpeed) : fallbackAttackSpeed;
        attackSpeed = attackSpeedStat;

        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        CalculateStats();
        InitializeMeleeImpactVFX();
    }

    private void HandleStatChanged(StatType statType, float newValue)
    {
        switch (statType)
        {
            case StatType.AttackDamage:
                damageMultiplier = newValue;
                break;
            case StatType.AttackSpeed:
                speedMultiplier = newValue;
                break;
            case StatType.MeleeAttackDamage:
                attackDamage = Mathf.RoundToInt(newValue);
                break;
            case StatType.MeleeAttackSpeed:
                attackSpeed = newValue;
                break;
            case StatType.MeleeRadius:
                hitRadius = newValue;
                break;
            default:
                return;
        }

        CalculateStats();
        ReportDebug($"Estadistica {statType} actualizada a {newValue}.", 1);
    }

    private void CalculateStats()
    {
        finalAttackDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        finalAttackSpeed = attackSpeed * speedMultiplier;

        ReportDebug($"Estadisticas recalculadas: Daño Final = {finalAttackDamage}, Velocidad de Ataque Final = {finalAttackSpeed}", 1);
    }

    private void Update()
    {
        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

        if (Time.time - lastAttackTime > comboResetTime) comboCount = 0;

        //if (Input.GetMouseButtonDown(0) && attackCooldown <= 0f && !isAttacking)
        //{
        //    if (!CanAttack()) return;

        //    lastAttackTime = Time.time;
        //    StartCoroutine(AttackSequence(comboCount));
        //    comboCount = (comboCount + 1) % 3; // Ciclo: 0 -> 1 -> 2 -> 0
        //}
    }

    // Verifica si el jugador puede atacar (tiene escudo y no está lanzándolo).
    public bool CanAttack()
    {
        if (isAttacking) return false;
        if (playerShieldController != null)
        {
            if (!playerShieldController.HasShield)
            {
                ReportDebug("No se puede atacar: escudo no disponible.", 1);
                return false;
            }

            if (playerShieldController.IsThrowingShield)
            {
                ReportDebug("No se puede atacar: escudo está siendo lanzado.", 1);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Método público llamado por PlayerCombatActionManager para ejecutar el ataque.
    /// </summary>
    public IEnumerator ExecuteAttackFromManager()
    {
        if (Time.time - lastAttackTime > comboResetTime)
        {
            comboCount = 0;
        }

        if (!CanAttack()) yield break;

        lastAttackTime = Time.time;
        yield return StartCoroutine(AttackSequence(comboCount));
        comboCount = (comboCount + 1) % 3; // Ciclo: 0 -> 1 -> 2 -> 0
    }

    /// <summary>
    /// Secuencia de ataque:
    /// 1) Obtener dirección del mouse (o forward)
    /// 2) Lockear rotación snapped a 8 direcciones
    /// 3) Esperar a que la rotación alcance el objetivo (o timeout)
    /// 4) Ejecutar el ataque (PerformHitDetection)
    /// 5) Mantener bloqueo durante la duración del ataque y luego desbloquear
    /// </summary>
    private IEnumerator AttackSequence(int attackIndex)
    {
        currentAttackIndex = attackIndex;
        isAttacking = true;

        // Limpiar enemigos golpeados para este nuevo ataque
        hitEnemiesThisCombo.Clear();

        // 1) Dirección objetivo
        Vector3 mouseWorldDir;
        if (!TryGetMouseWorldDirection(out mouseWorldDir))
        {
            mouseWorldDir = transform.forward;
        }

        // 2) Lockear rotación snapped
        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(mouseWorldDir, true);
        }
        else
        {
            // fallback: aplicar rotación instantánea snappeada
            RotateTowardsMouseInstant();
        }

        // Esperar a que se alcance la rotación
        yield return StartCoroutine(WaitForRotationLock());

        // Trigger animación correspondiente
        if (playerAnimator != null) playerAnimator.SetTrigger($"Attack{attackIndex + 1}");

        // Ejecutar ataque específico
        float lockDuration = comboLockDurations[attackIndex];
        attackCooldown = lockDuration;

        switch (attackIndex)
        {
            case 0: yield return StartCoroutine(ExecuteAttack1(lockDuration)); break;
            case 1: yield return StartCoroutine(ExecuteAttack2(lockDuration)); break;
            case 2: yield return StartCoroutine(ExecuteAttack3(lockDuration)); break;
        }

        // Desbloquear y resetear
        if (playerMovement != null) playerMovement.UnlockFacing();

        isAttacking = false;
        hitEnemiesThisCombo.Clear();
    }

    private IEnumerator WaitForRotationLock()
    {
        float maxWait = 0.25f;
        float start = Time.time;
        float angleThreshold = 2f;

        while (Time.time - start < maxWait)
        {
            if (playerMovement != null)
            {
                Quaternion target = playerMovement.GetLockedRotation();
                float angle = Quaternion.Angle(transform.rotation, target);
                if (angle <= angleThreshold) break;
            }
            else
            {
                break;
            }
            yield return null;
        }

        if (playerMovement != null) playerMovement.ForceApplyLockedRotation();
    }

    private IEnumerator ExecuteAttack1(float totalDuration)
    {
        float movementDuration = totalDuration;
        float elapsedTime = 0f;

        Vector3 attackMoveVelocity = (transform.forward * comboMovementForces[0]) / movementDuration;

        while (elapsedTime < movementDuration)
        {
            elapsedTime += Time.deltaTime;

            if (playerMovement != null)
            {
                playerMovement.MoveCharacter(attackMoveVelocity * Time.deltaTime);

            }
            yield return null;
        }

        // Ejecutar hit detection en el medio del movimiento
        PerformHitDetectionWithTracking();

        // Mantener lock
        float lockDuration = comboLockDurations[0];
        attackCooldown = lockDuration;

        float remainingTime = Mathf.Max(0, lockDuration - attack1Duration);
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    private IEnumerator ExecuteAttack2(float totalDuration)
    {
        // Ataque de área: salto y rotación 360
        float movementDuration = 0.2f;
        float spinDuration = attack2Duration - movementDuration;

        // Salto ligero hacia adelante
        float elapsedTime = 0f;
        Vector3 attackMoveVelocity = (transform.forward * comboMovementForces[1]) / movementDuration;

        while (elapsedTime < movementDuration)
        {
            elapsedTime += Time.deltaTime;
            if (playerMovement != null)
            {
                playerMovement.MoveCharacter(attackMoveVelocity * Time.deltaTime);
            }
            yield return null;
        }

        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
        }

        // Rotación mientras ataca
        elapsedTime = 0f;
        while (elapsedTime < spinDuration)
        {
            elapsedTime += Time.deltaTime;
            float spinAmount = attack2SpinSpeed * Time.deltaTime;
            transform.Rotate(0f, spinAmount, 0f, Space.Self);

            // Hacer hit detection durante la rotación
            PerformHitDetectionWithTracking();
            yield return null;
        }

        // El lock duration es el tiempo total que dura el ataque (incluido movimiento + spin)
        float lockDuration = comboLockDurations[1];
        attackCooldown = lockDuration;

        // Ya pasó attack2Duration, esperar el resto del lock
        float remainingTime = Mathf.Max(0, lockDuration - attack2Duration);
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    private IEnumerator ExecuteAttack3(float totalDuration)
    {
        // Ataque pesado: giro lento + carga + golpe
        float preChargeElapsed = 0f;

        // Fase 1: Giro lento
        while (preChargeElapsed < attack3PreChargeDuration)
        {
            preChargeElapsed += Time.deltaTime;
            float spinAmount = attack3SpinSpeed * Time.deltaTime;
            transform.Rotate(0f, spinAmount, 0f, Space.Self);
            yield return null;
        }

        // Fase 2: Movimiento y carga
        float chargeElapsed = 0f;
        Vector3 attackMoveVelocity = (transform.forward * comboMovementForces[2]) / attack3ChargeDuration;

        while (chargeElapsed < attack3ChargeDuration)
        {
            chargeElapsed += Time.deltaTime;
            if (playerMovement != null)
            {
                playerMovement.MoveCharacter(attackMoveVelocity * Time.deltaTime);
            }

            PerformHitDetectionWithTracking();
            yield return null;
        }

        // Hit detection al final
        //PerformHitDetectionWithTracking();

        float lockDuration = comboLockDurations[2];
        attackCooldown = lockDuration;
        yield return new WaitForSeconds(lockDuration - (attack3PreChargeDuration + attack3ChargeDuration));
    }

    // Rota instantaneamente al mouse proyectado en el plano horizontal (y = transform.position.y), con snap a 8 direcciones.
    private void RotateTowardsMouseInstant()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float snapped = Mathf.Round(angle / 45f) * 45f;
                Quaternion snappedRot = Quaternion.Euler(0f, snapped, 0f);
                transform.rotation = snappedRot;
            }
        }
    }

    private bool TryGetMouseWorldDirection(out Vector3 outDir)
    {
        outDir = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                outDir = dir.normalized;
                return true;
            }
        }
        return false;
    }

    public void PerformHitDetectionWithTracking()
    {
        bool hitAnyEnemy = false;
        Collider[] hitEnemies = useBoxCollider
            ? Physics.OverlapBox(hitPoint.position, new Vector3(hitRadius, hitRadius, hitRadius), Quaternion.identity, enemyLayer)
            : Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            if (hitEnemiesThisCombo.Contains(enemy))
            {
                continue;
            }

            hitEnemiesThisCombo.Add(enemy);
            hitAnyEnemy = true;

            ApplyKnockbackSafe(enemy);

            bool isCritical;
            float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

            IDamageable damageable = enemy.GetComponent<IDamageable>();
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();

            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage, isCritical);
                if (currentAttackIndex == 2)
                {
                    enemyHealth.ApplyStun(attack3StunDuration);
                    ReportDebug($"Enemigo {enemy.name} aturdido por {attack3StunDuration} segundos.", 1);
                }
            }
            else if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(Mathf.RoundToInt(finalDamage), isCritical);
                if (currentAttackIndex == 2)
                {
                    enemyHealth.ApplyStun(attack3StunDuration);
                    ReportDebug($"Enemigo {enemy.name} aturdido por {attack3StunDuration} segundos.", 1);
                }
            }
            else
            {
                HealthController healthController = enemy.GetComponent<HealthController>();
                if (healthController != null)
                {
                    healthController.TakeDamage(Mathf.RoundToInt(finalDamage));
                }
            }

            CombatEventsManager.TriggerPlayerHitEnemy(enemy.gameObject, true);

            BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
            if (bloodKnight != null) bloodKnight.OnPlayerCounterAttack();

            PlayImpactVFX(enemy.transform.position);
            ReportDebug($"Golpe a {enemy.name} por {finalDamage} de daño.", 1);
        }

        if (!hitAnyEnemy) PlayImpactVFX(hitPoint.position);

        StartCoroutine(ShowGizmoCoroutine());
    }

    private void ApplyKnockbackSafe(Collider enemy)
    {
        EnemyKnockbackHandler knockbackHandler = enemy.GetComponent<EnemyKnockbackHandler>();
        if (knockbackHandler == null || playerHealth == null) return;

        float knockbackForce = 0f;

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                knockbackForce = knockbackYoung;
                break;
            case PlayerHealth.LifeStage.Adult:
                knockbackForce = knockbackAdult;
                break;
            case PlayerHealth.LifeStage.Elder:
                knockbackForce = knockbackElder;
                break;
        }

        if (knockbackForce > 0)
        {
            Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
            knockbackDirection.y = 0;

            float limitedForce = Mathf.Min(knockbackForce, knockbackMaxDistance);
            knockbackHandler.TriggerKnockback(knockbackDirection, limitedForce, 0.25f);
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        if (!canShowHitGizmo) yield break;

        showGizmo = true;
        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(true);
        else if (visualSphereHit != null) visualSphereHit.SetActive(true);

        yield return new WaitForSeconds(gizmoDuration);

        showGizmo = false;
        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(false);
        else if (visualSphereHit != null) visualSphereHit.SetActive(false);
    }

    #region VFX Methods

    /// <summary>
    /// Inicializa el sistema de partículas de impacto melee si no está asignado.
    /// </summary>
    private void InitializeMeleeImpactVFX()
    {
        if (meleeImpactVFX == null) return;
        var psRenderer = meleeImpactVFX.GetComponent<ParticleSystemRenderer>();
        if (psRenderer == null) return;

        meleeImpactMatInstance = new Material(psRenderer.sharedMaterial);
        psRenderer.material = meleeImpactMatInstance;

        meleeImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        meleeImpactVFX.Clear(true);
    }

    /// <summary>
    /// Reproduce el efecto de impacto en una posición específica.
    /// </summary>
    private void PlayImpactVFX(Vector3 position)
    {
        if (meleeImpactVFX == null) return;

        meleeImpactVFX.transform.position = position;
        meleeImpactVFX.transform.rotation = Quaternion.identity;

        meleeImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        meleeImpactVFX.Clear(true);

        meleeImpactVFX.Emit(impactParticleCount);
    }

    // Limpia los recursos del VFX al desactivar o destruir el objeto.
    private void CleanupVFXImmediate()
    {
        if (meleeImpactVFX != null)
        {
            var psRenderer = meleeImpactVFX.GetComponent<ParticleSystemRenderer>();
            meleeImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            meleeImpactVFX.Clear(true);
            if (psRenderer != null)
            {
                psRenderer.material = null;
            }
        }

        if (meleeImpactMatInstance != null)
        {
            var toDestroy = meleeImpactMatInstance;
            meleeImpactMatInstance = null;
            Destroy(toDestroy, 0.05f);
        }
    }

    private IEnumerator CleanupVFXCoroutine()
    {
        if (meleeImpactVFX != null)
        {
            var psRenderer = meleeImpactVFX.GetComponent<ParticleSystemRenderer>();
            meleeImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            meleeImpactVFX.Clear(true);
            if (psRenderer != null)
            {
                psRenderer.material = null;
            }
        }

        if (meleeImpactMatInstance != null)
        {
            var toDestroy = meleeImpactMatInstance;
            meleeImpactMatInstance = null;
            yield return null;
            Destroy(toDestroy);
        }

        cleanupCoroutine = null;
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (hitPoint == null || !showGizmo) return;

        Gizmos.color = Color.red;

        if (useBoxCollider)
        {
            Vector3 boxCenter = hitPoint.position;
            Vector3 boxHalfExtents = new Vector3(hitRadius, hitRadius, hitRadius);
            Quaternion boxRotation = Quaternion.identity;

            Gizmos.matrix = Matrix4x4.TRS(boxCenter, boxRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
        }
        else
        {
            Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerMeleeAttack] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerMeleeAttack] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerMeleeAttack] {message}");
                break;
            default:
                Debug.Log($"[PlayerMeleeAttack] {message}");
                break;
        }
    }
}
