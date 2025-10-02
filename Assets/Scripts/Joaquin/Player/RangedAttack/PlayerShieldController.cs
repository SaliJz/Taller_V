using UnityEngine;

/// <summary>
/// Clase que maneja el lanzamiento y recuperación del escudo del jugador.
/// </summary>
public class PlayerShieldController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;

    [Header("Stats")]
    [Tooltip("Daño de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private int fallbackshieldDamage = 10;
    [SerializeField] private int shieldDamage = 10;
    [Tooltip("Velocidad del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldSpeed = 25f;
    [SerializeField] private float shieldSpeed = 25f;
    [Tooltip("Distancia máxima del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldMaxDistance = 30f;
    [SerializeField] private float shieldMaxDistance = 30f;
    [Tooltip("Cantidad máxima de rebotes del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private int fallbackshieldMaxRebounds = 2;
    [SerializeField] private int shieldMaxRebounds = 2;
    [Tooltip("Radio de rebote del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldReboundRadius = 15f;
    [SerializeField] private float shieldReboundRadius = 15f;
    [SerializeField] private bool canShieldRebound = true;

    private int finalAttackDamage;
    private float finalAttackSpeed;
    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;

    public bool CanShieldRebound => canShieldRebound;

    private bool hasShield = true;

    private int currentShieldDamage;
    private float currentShieldSpeed;

    public int ShieldDamage
    {
        get { return shieldDamage; }
        set { shieldDamage = value; }
    }

    public bool HasShield 
    { 
        get { return hasShield; }
        set { hasShield = value; }
    }

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
        // Inicializar estadísticas del ataque a distancia desde PlayerStatsManager o usar valores de fallback
        float shieldDamageStat = statsManager != null ? statsManager.GetStat(StatType.ShieldAttackDamage) : fallbackshieldDamage;
        shieldDamage = Mathf.RoundToInt(shieldDamageStat);

        float shieldSpeedStat = statsManager != null ? statsManager.GetStat(StatType.ShieldSpeed) : fallbackshieldSpeed;
        shieldSpeed = shieldSpeedStat;

        float shieldMaxDistanceStat = statsManager != null ? statsManager.GetStat(StatType.ShieldMaxDistance) : fallbackshieldMaxDistance;
        shieldMaxDistance = shieldMaxDistanceStat;

        float shieldMaxReboundsStat = statsManager != null ? statsManager.GetStat(StatType.ShieldMaxRebounds) : fallbackshieldMaxRebounds;
        shieldMaxRebounds = Mathf.RoundToInt(shieldMaxReboundsStat);

        float shieldReboundRadiusStat = statsManager != null ? statsManager.GetStat(StatType.ShieldReboundRadius) : fallbackshieldReboundRadius;
        shieldReboundRadius = shieldReboundRadiusStat;

        // Inicializar estadísticas globales que afectan a todos los ataques desde PlayerStatsManager o usar valores fallback
        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        shieldPrefab.SetActive(false);

        CalculateStats();
    }

    private void HandleStatChanged(StatType statType, float newValue)
    {
        switch (statType)
        {
            case StatType.ShieldAttackDamage:
                shieldDamage = Mathf.RoundToInt(newValue);
                break;
            case StatType.ShieldSpeed:
                shieldSpeed = newValue;
                break;
            case StatType.ShieldMaxDistance:
                shieldMaxDistance = newValue;
                break;
            case StatType.ShieldMaxRebounds:
                shieldMaxRebounds = Mathf.RoundToInt(newValue);
                break;
            case StatType.ShieldReboundRadius:
                shieldReboundRadius = newValue;
                break;
            case StatType.AttackDamage:
                damageMultiplier = newValue;
                break;
            case StatType.AttackSpeed:
                speedMultiplier = newValue;
                break;
            default:
                return;
        }

        CalculateStats();
        ReportDebug($"Estadística {statType} actualizada a {newValue}.", 1);
    }

    // Metodo para recalcular las estadísticas del escudo
    private void CalculateStats()
    {
        finalAttackDamage = Mathf.RoundToInt(shieldDamage * damageMultiplier);
        finalAttackSpeed = shieldSpeed * speedMultiplier;

        currentShieldDamage = finalAttackDamage;
        currentShieldSpeed = finalAttackSpeed;

        ReportDebug($"Estadísticas recalculadas: Daño de ataque del escudo = {currentShieldDamage}, Velocidad del escudo = {currentShieldSpeed}", 1);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) && hasShield)
        {
            ThrowShield();
        }
    }

    /// <summary>
    /// Función que Lanza el escudo en la dirección del mouse y lo instancia en el punto y altura del spawn point.
    /// </summary>
    private void ThrowShield()
    {
        hasShield = false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, shieldSpawnPoint.position);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 targetPoint = ray.GetPoint(enter);
            Vector3 direction = (targetPoint - shieldSpawnPoint.position).normalized;

            GameObject shieldInstance = ShieldPooler.Instance.GetPooledObject();

            if (shieldInstance != null)
            {
                shieldInstance.transform.position = shieldSpawnPoint.position;
                shieldInstance.transform.rotation = Quaternion.LookRotation(direction);
                shieldInstance.GetComponent<Shield>().Throw(this, direction, canShieldRebound, shieldMaxRebounds, shieldReboundRadius, currentShieldDamage, currentShieldSpeed, shieldMaxDistance);
            }
            else
            {
                hasShield = true;
            }
        }
    }

    // El escudo llama a esta función cuando regresa
    public void CatchShield() => hasShield = true;

    // Permite cambiar si el escudo puede rebotar o no
    public void SetRebound(bool value) => canShieldRebound = value;

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
                Debug.Log($"[PlayerShieldController] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerShieldController] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerShieldController] {message}");
                break;
            default:
                Debug.Log($"[PlayerShieldController] {message}");
                break;
        }
    }
}