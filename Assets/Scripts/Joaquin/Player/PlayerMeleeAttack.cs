using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject visualSphereHit;
    [SerializeField] private GameObject visualBoxHit;
    [SerializeField] private PlayerShieldController playerShieldController;

    [Header("Configuraci�n de Ataque")]
    [SerializeField] private Transform hitPoint;
    [Tooltip("Radio de golpe por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [SerializeField] private float hitRadius = 0.8f;
    [Tooltip("Da�o de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackAttackDamage = 10;
    [SerializeField] private int attackDamage = 10;
    [Tooltip("Velocidad de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [SerializeField] private float fallbackAttackSpeed = 1f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Configuración de Empuje")]
    [SerializeField] private float knockbackYoung = 0.25f;
    [SerializeField] private float knockbackAdult = 0.25f;
    [SerializeField] private float knockbackElder = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool useBoxCollider = false;
    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.2f;

    private float attackCooldown = 0f;
    private int finalAttackDamage;
    private float finalAttackSpeed;

    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;

    public int AttackDamage
    {
        get { return attackDamage; }
        set { attackDamage = value; }
    }

    private PlayerHealth playerHealth;
    //private Animator animator;

    private void Awake()
    {
        if (visualSphereHit != null) visualSphereHit.SetActive(false);
        if (visualBoxHit != null) visualBoxHit.SetActive(false);

        statsManager = GetComponent<PlayerStatsManager>();
        playerHealth = GetComponent<PlayerHealth>();
        playerShieldController = GetComponent<PlayerShieldController>();

        if (statsManager == null) ReportDebug("StatsManager no est� asignado en PlayerMeleeAttack. Usando valores de de fallback.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no se encuentra en el objeto.", 3);
        if (playerShieldController == null) ReportDebug("PlayerShieldController no se encuentra en el objeto.", 3);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void Start()
    {
        showGizmo = false;

        // Inicializar estadisticas del ataque melee desde PlayerStatsManager o usar valores de fallback
        float hitRadiusStat = statsManager != null ? statsManager.GetStat(StatType.MeleeRadius) : fallbackHitRadius;
        hitRadius = hitRadiusStat;

        float attackDamageStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackDamage) : fallbackAttackDamage;
        attackDamage = Mathf.RoundToInt(attackDamageStat);

        float attackSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackSpeed) : fallbackAttackSpeed;
        attackSpeed = attackSpeedStat;

        // Inicializar estadisticas globales que afectan a todos los ataques desde PlayerStatsManager o usar valores fallback
        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        CalculateStats();
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    /// <param name="statType">Tipo de estad�stica que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estad�stica.</param>
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

    // Metodo para calcular las estaditicas finales del ataque.
    private void CalculateStats()
    {
        finalAttackDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        finalAttackSpeed = attackSpeed * speedMultiplier;

        ReportDebug($"Estadisticas recalculadas: Da�o Final = {finalAttackDamage}, Velocidad de Ataque Final = {finalAttackSpeed}", 1);
    }

    private void Update()
    {
        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0) && attackCooldown <= 0f)
        {
            if (playerShieldController != null)
            {
                if (playerShieldController.HasShield) Attack();
                else ReportDebug("No tiene el escudo", 1);
            }
            else
            {
                Attack();
            }
        }
    }

    // Función que inicia el ataque cuerpo a cuerpo.
    private void Attack()
    {
        RotateTowardsMouseInstant();

        PerformHitDetection();

        attackCooldown = 1f / finalAttackSpeed;
    }

    // Rota instantaneamente al mouse proyectado en el plano horizontal (y = transform.position.y)
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
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }

    // FUNCIÓN LLAMADA POR UN ANIMATION EVENT
    /// <summary>
    /// Funci�n que realiza la detecci�n de golpes en un �rea definida alrededor del punto de impacto.
    /// </summary>
    public void PerformHitDetection()
    {
        if (useBoxCollider)
        {
            Vector3 boxHalfExtents = new Vector3(hitRadius, hitRadius, hitRadius);
            Collider[] hitEnemiesBox = Physics.OverlapBox(hitPoint.position, boxHalfExtents, Quaternion.identity, enemyLayer);

            foreach (Collider enemy in hitEnemiesBox)
            {
                ApplyKnockback(enemy);

                HealthController healthController = enemy.GetComponent<HealthController>();
                if (healthController != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    healthController.TakeDamage(Mathf.RoundToInt(finalDamage));

                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de da�o.", 1);
                }

                IDamageable damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    bool isCritical;
                    float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalDamageWithCrit, isCritical);
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalDamage, isCritical);

                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de da�o.", 1);
                }

                BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
                if (bloodKnight != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    bloodKnight.TakeDamage(finalDamage, isCritical);
                    bloodKnight.OnPlayerCounterAttack();

                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de da�o.", 1);
                }
            }
        }
        else
        {
            Collider[] hitEnemies = Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

            foreach (Collider enemy in hitEnemies)
            {
                ApplyKnockback(enemy);

                HealthController healthController = enemy.GetComponent<HealthController>();
                if (healthController != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    healthController.TakeDamage(Mathf.RoundToInt(finalDamage));

                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de da�o.", 1);
                }

                IDamageable damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    bool isCritical;
                    float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalDamageWithCrit, isCritical);
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    damageable.TakeDamage(finalDamage, isCritical);

                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de da�o.", 1);
                }

                BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
                if (bloodKnight != null)
                {
                    bool isCritical;
                    float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                    bloodKnight.TakeDamage(finalDamage, isCritical);
                    bloodKnight.OnPlayerCounterAttack();

                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de da�o.", 1);
                }
            }
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    /// <summary>
    /// Aplica una fuerza de empuje al enemigo.
    /// </summary>
    /// <param name="enemy"> El collider del enemigo golpeado. </param>
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
    /// <summary> 
    /// Funci�n de depuraci�n para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
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