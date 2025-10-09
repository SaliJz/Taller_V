// PlayerMeleeAttack.cs
using UnityEngine;
using System.Collections;

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

    [Header("Attack Configuration")]
    [SerializeField] private Transform hitPoint;
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [SerializeField] private float hitRadius = 0.8f;
    [HideInInspector] private float fallbackAttackDamage = 10;
    [SerializeField] private int attackDamage = 10;
    [HideInInspector] private float fallbackAttackSpeed = 1f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool canShowHitGizmo = false;

    [Header("knockback Configuration")]
    [SerializeField] private float knockbackYoung = 0.25f;
    [SerializeField] private float knockbackAdult = 0.25f;
    [SerializeField] private float knockbackElder = 0.5f;

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

    public int AttackDamage
    {
        get { return attackDamage; }
        set { attackDamage = value; }
    }

    public bool IsAttacking => isAttacking;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;

    private Material meleeImpactMatInstance;

    private void Awake()
    {
        if (visualSphereHit != null) visualSphereHit.SetActive(false);
        if (visualBoxHit != null) visualBoxHit.SetActive(false);

        statsManager = GetComponent<PlayerStatsManager>();
        playerHealth = GetComponent<PlayerHealth>();
        playerShieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();

        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerMeleeAttack. Usando valores de fallback.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no se encuentra en el objeto.", 3);
        if (playerShieldController == null) ReportDebug("PlayerShieldController no se encuentra en el objeto.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no se encuentra en el objeto. Lock de rotación no funcionará.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        if (meleeImpactVFX != null)
        {
            meleeImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            meleeImpactVFX.Clear(true);
        }

        if (meleeImpactMatInstance != null)
        {
            Destroy(meleeImpactMatInstance);
            meleeImpactMatInstance = null;
        }
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        if (meleeImpactVFX != null)
        {
            meleeImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            meleeImpactVFX.Clear(true);
        }

        if (meleeImpactMatInstance != null)
        {
            Destroy(meleeImpactMatInstance);
            meleeImpactMatInstance = null;
        }
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

        if (Input.GetMouseButtonDown(0) && attackCooldown <= 0f && !isAttacking)
        {
            if (playerShieldController != null)
            {
                if (!playerShieldController.HasShield)
                {
                    ReportDebug("No se puede atacar: el escudo no está disponible.", 1);
                    return;
                }

                if (playerShieldController.IsThrowingShield)
                {
                    ReportDebug("No se puede atacar: el escudo está siendo lanzado.", 1);
                    return;
                }
            }

            StartCoroutine(AttackSequence());
        }
    }

    /// <summary>
    /// Secuencia de ataque:
    /// 1) Obtener dirección del mouse (o forward)
    /// 2) Lockear rotación snapped a 8 direcciones
    /// 3) Esperar a que la rotación alcance el objetivo (o timeout)
    /// 4) Ejecutar el ataque (PerformHitDetection)
    /// 5) Mantener bloqueo durante la duración del ataque y luego desbloquear
    /// </summary>
    private IEnumerator AttackSequence()
    {
        isAttacking = true;

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

        // 3) Esperar a que se alcance la rotación lockeada (o timeout corto)
        float maxWait = 0.25f; // tiempo máximo a esperar para rotación (ajustable)
        float start = Time.time;
        float angleThreshold = 2f; // grados para considerar que llegó
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
                // si no hay playerMovement, break inmediatamente
                break;
            }
            yield return null;
        }

        // asegurar rotación exacta justo antes del ataque (evita pequeños deslices)
        if (playerMovement != null) playerMovement.ForceApplyLockedRotation();

        // 4) Ejecutar ataque (hit detection) inmediatamente después de rotar
        PerformHitDetection();

        // 5) Mantener bloqueo durante la duración del ataque (1 / finalAttackSpeed)
        float lockDuration = 1f / finalAttackSpeed;
        attackCooldown = lockDuration;

        // Opcional: si tienes una animación, aquí deberías disparar el trigger (ej: animator.SetTrigger("Attack"))
        // y preferiblemente usar un AnimationEvent para UnlockFacing() al final de la animación.
        yield return new WaitForSeconds(lockDuration);

        // 6) Desbloquear rotación
        if (playerMovement != null) playerMovement.UnlockFacing();

        isAttacking = false;
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

    public void PerformHitDetection()
    {
        bool hitAnyEnemy = false;

        if (useBoxCollider)
        {
            Vector3 boxHalfExtents = new Vector3(hitRadius, hitRadius, hitRadius);
            Collider[] hitEnemiesBox = Physics.OverlapBox(hitPoint.position, boxHalfExtents, Quaternion.identity, enemyLayer);

            foreach (Collider enemy in hitEnemiesBox)
            {
                hitAnyEnemy = true;
                ApplyKnockback(enemy);

                HealthController healthController = enemy.GetComponent<HealthController>();
                if (healthController != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    healthController.TakeDamage(Mathf.RoundToInt(finalAttackDamage));
                    ReportDebug("Golpe a " + enemy.name + " por " + finalAttackDamage + " de daño.", 1);
                }

                IDamageable damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    bool isCritical;
                    float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalDamageWithCrit, isCritical);
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalAttackDamage);

                    ReportDebug("Golpe a " + enemy.name + " por " + finalAttackDamage + " de daño.", 1);
                }

                BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
                if (bloodKnight != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    bloodKnight.TakeDamage(finalAttackDamage);
                    bloodKnight.OnPlayerCounterAttack();

                    ReportDebug("Golpe a " + enemy.name + " por " + finalAttackDamage + " de daño.", 1);
                }

                PlayImpactVFX(enemy.transform.position);
            }
        }
        else
        {
            Collider[] hitEnemies = Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

            foreach (Collider enemy in hitEnemies)
            {
                hitAnyEnemy = true;
                ApplyKnockback(enemy);

                HealthController healthController = enemy.GetComponent<HealthController>();
                if (healthController != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    healthController.TakeDamage(Mathf.RoundToInt(finalAttackDamage));

                    ReportDebug("Golpe a " + enemy.name + " por " + finalAttackDamage + " de daño.", 1);
                }

                IDamageable damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    bool isCritical;
                    float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalDamageWithCrit, isCritical);
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalAttackDamage);

                    ReportDebug("Golpe a " + enemy.name + " por " + finalAttackDamage + " de daño.", 1);
                }

                BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
                if (bloodKnight != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    bloodKnight.TakeDamage(finalAttackDamage);
                    bloodKnight.OnPlayerCounterAttack();

                    ReportDebug("Golpe a " + enemy.name + " por " + finalAttackDamage + " de daño.", 1);
                }

                PlayImpactVFX(enemy.transform.position);
            }
        }

        if (!hitAnyEnemy)
        {
            PlayImpactVFX(hitPoint.position);
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private void ApplyKnockback(Collider enemy)
    {
        EnemyKnockbackHandler knockbackHandler = enemy.GetComponent<EnemyKnockbackHandler>();
        if (knockbackHandler == null || playerHealth == null) return;

        float knockbackForce = 0f;
        float knockbackDuration = 0f;

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                knockbackForce = knockbackYoung;
                knockbackDuration = 0.25f;
                break;
            case PlayerHealth.LifeStage.Adult:
                knockbackForce = knockbackAdult;
                knockbackDuration = 0.25f;
                break;
            case PlayerHealth.LifeStage.Elder:
                knockbackForce = knockbackElder;
                knockbackDuration = 0.25f;
                break;
        }

        if (knockbackForce > 0)
        {
            Vector3 knockbackDirection = (enemy.transform.position - transform.position);
            knockbackDirection.y = 0;
            knockbackDirection.Normalize();

            knockbackHandler.TriggerKnockback(knockbackDirection, knockbackForce, knockbackDuration);
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        if (canShowHitGizmo == false) yield break;

        showGizmo = true;

        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(true);
        else
        {
            if (visualSphereHit != null) visualSphereHit.SetActive(true);
        }

        yield return new WaitForSeconds(gizmoDuration);

        showGizmo = false;

        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(false);
        else
        {
            if (visualSphereHit != null) visualSphereHit.SetActive(false);
        }
    }

    #region VFX Methods

    /// <summary>
    /// Inicializa el sistema de partículas de impacto melee si no está asignado.
    /// </summary>
    private void InitializeMeleeImpactVFX()
    {
        if (meleeImpactVFX == null) return;

        meleeImpactMatInstance = new Material(meleeImpactVFX.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        meleeImpactVFX.GetComponent<ParticleSystemRenderer>().material = meleeImpactMatInstance;

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
