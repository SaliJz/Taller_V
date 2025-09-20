using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;

    [Header("Configuración de Ataque")]
    [SerializeField] private Transform hitPoint;
    [Tooltip("Radio de golpe por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [SerializeField] private float hitRadius = 0.8f;
    [Tooltip("Daño de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackAttackDamage = 10;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.2f;

    private int finalDamage;
    private float finalAttackSpeed;
    // private float currentCooldown = 0f;

    public int AttackDamage
    {
        get { return attackDamage; }
        set { attackDamage = value; }
    }
    //private Animator animator;

    private void Awake()
    {
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
        CalculateStats();

        //animator = GetComponent<Animator>();

        showGizmo = false;

        float hitRadiusStat = statsManager != null ? statsManager.GetStat(StatType.MeleeRadius) : fallbackHitRadius;
        hitRadius = hitRadiusStat;

        float attackDamageStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackDamage) : fallbackAttackDamage;
        attackDamage = Mathf.RoundToInt(attackDamageStat);
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    /// <param name="statType">Tipo de estadística que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estadística.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.MeleeAttackDamage || statType == StatType.AttackDamage || statType == StatType.AttackSpeed)
        {
            CalculateStats();
        }
        else if (statType == StatType.MeleeRadius)
        {
            hitRadius = newValue;
        }

        ReportDebug($"Estadística {statType} actualizada a {newValue}.", 1);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Attack();
        }
    }

    // Función que inicia el ataque cuerpo a cuerpo.
    private void Attack()
    {
        //animator.SetTrigger("Attack");
        PerformHitDetection();
    }

    private void CalculateStats()
    {
        float baseDamage = statsManager.GetStat(StatType.MeleeAttackDamage);
        float damageMultiplier = statsManager.GetStat(StatType.AttackDamage);

        finalDamage = (int)(baseDamage * damageMultiplier);

        ReportDebug($"Daño final de ataque melee: {finalDamage}", 1);

        float baseSpeed = 1f;
        float speedMultiplier = statsManager.GetStat(StatType.AttackSpeed);
        finalAttackSpeed = baseSpeed / speedMultiplier;
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
                float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(finalDamage, out isCritical);

                healthController.TakeDamage(finalDamage);

                ReportDebug("Golpe a " + enemy.name + " por " + attackDamage + " de daño.", 1);
            }

            BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
            if (bloodKnight != null)
            {
                bool isCritical;
                float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(finalDamage, out isCritical);

                bloodKnight.TakeDamage(finalDamageWithCrit, isCritical);
                bloodKnight.OnPlayerCounterAttack();

                ReportDebug("Golpe a " + enemy.name + " por " + finalDamageWithCrit + " de daño.", 1);
            }
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        showGizmo = true;
        yield return new WaitForSeconds(gizmoDuration);
        showGizmo = false;
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