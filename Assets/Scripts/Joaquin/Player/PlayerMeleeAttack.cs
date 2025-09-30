using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject visualHit;

    [Header("Configuración de Ataque")]
    [SerializeField] private Transform hitPoint;
    [Tooltip("Radio de golpe por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [SerializeField] private float hitRadius = 0.8f;
    [Tooltip("Daño de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackAttackDamage = 10;
    [SerializeField] private int attackDamage = 10;
    [Tooltip("Velocidad de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [SerializeField] private float fallbackAttackSpeed = 1f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private LayerMask enemyLayer;

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
    //private Animator animator;

    private void Awake()
    {
        if (visualHit != null) visualHit.SetActive(false);
        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerMeleeAttack. Usando valores de de fallback.", 2);
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
        //animator = GetComponent<Animator>();

        showGizmo = false;

        // Inicializar estadísticas del ataque melee desde PlayerStatsManager o usar valores de fallback
        float hitRadiusStat = statsManager != null ? statsManager.GetStat(StatType.MeleeRadius) : fallbackHitRadius;
        hitRadius = hitRadiusStat;

        float attackDamageStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackDamage) : fallbackAttackDamage;
        attackDamage = Mathf.RoundToInt(attackDamageStat);

        float attackSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackSpeed) : fallbackAttackSpeed;
        attackSpeed = attackSpeedStat;

        // Inicializar estadísticas globales que afectan a todos los ataques desde PlayerStatsManager o usar valores fallback
        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        CalculateStats();
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    /// <param name="statType">Tipo de estadística que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estadística.</param>
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
        ReportDebug($"Estadística {statType} actualizada a {newValue}.", 1);
    }

    // Metodo para calcular las estadísticas finales del ataque.
    private void CalculateStats()
    {
        finalAttackDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        finalAttackSpeed = attackSpeed * speedMultiplier;

        ReportDebug($"Estadísticas recalculadas: Daño Final = {finalAttackDamage}, Velocidad de Ataque Final = {finalAttackSpeed}", 1);
    }

    private void Update()
    {
        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0) && attackCooldown <= 0f)
        {
            Attack();
        }
    }

    // Función que inicia el ataque cuerpo a cuerpo.
    private void Attack()
    {
        //animator.SetTrigger("Attack");
        PerformHitDetection();

        attackCooldown = 1f / finalAttackSpeed;
    }

    // FUNCIÓN LLAMADA POR UN ANIMATION EVENT
    /// <summary>
    /// Función que realiza la detección de golpes en un área definida alrededor del punto de impacto.
    /// </summary>
    public void PerformHitDetection()   
    {
        Collider[] hitEnemies = Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            HealthController healthController = enemy.GetComponent<HealthController>();
            if (healthController != null)
            {
                bool isCritical;
                float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                healthController.TakeDamage(Mathf.RoundToInt(finalDamage));

                ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de daño.", 1);
            }

            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null) 
            {
                bool isCritical;
                float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);
                
                damageable.TakeDamage(finalDamage, isCritical);

                ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de daño.", 1);
            }

            BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
            if (bloodKnight != null)
            {
                bool isCritical;
                float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

                bloodKnight.TakeDamage(finalDamage, isCritical);
                bloodKnight.OnPlayerCounterAttack();

                ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de daño.", 1);
            }
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        showGizmo = true;
        if (visualHit != null) visualHit.SetActive(true);
        yield return new WaitForSeconds(gizmoDuration);
        showGizmo = false;
        if (visualHit != null) visualHit.SetActive(false);
    }

    private void OnDrawGizmos()
    {
        if (hitPoint == null || !showGizmo) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
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