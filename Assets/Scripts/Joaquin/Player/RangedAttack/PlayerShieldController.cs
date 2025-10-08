using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el lanzamiento y recuperación del escudo del jugador.
/// </summary>
public class PlayerShieldController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerMovement playerMovement;

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
    private bool isThrowingShield = false;

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

    public bool IsThrowingShield => isThrowingShield;

    private void Awake()
    {
        statsManager = GetComponent<PlayerStatsManager>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerMovement = GetComponent<PlayerMovement>();

        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerShieldController. Usando valores de fallback.", 2);
        if (playerMeleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado. No se podrá verificar estado de ataque melee.", 2);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado. Lock de rotación no funcionará.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void OnDestroy()
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

        if (shieldPrefab != null) shieldPrefab.SetActive(false);

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
        if (Input.GetMouseButtonDown(1) && hasShield && !isThrowingShield)
        {
            // Verificar que no esté en medio de un ataque melee
            if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
            {
                ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
                return;
            }

            StartCoroutine(ThrowShieldSequence());
        }
    }

    /// <summary>
    /// Secuencia de lanzamiento del escudo (similar a AttackSequence del melee):
    /// 1) Obtener dirección del mouse
    /// 2) Lockear rotación snapped a 8 direcciones
    /// 3) Esperar a que la rotación alcance el objetivo
    /// 4) Lanzar el escudo
    /// 5) Mantener bloqueo hasta que el escudo regrese
    /// </summary>
    private IEnumerator ThrowShieldSequence()
    {
        isThrowingShield = true;

        // 1) Dirección objetivo
        Vector3 mouseWorldDir;
        if (!TryGetMouseWorldDirection(out mouseWorldDir))
        {
            mouseWorldDir = transform.forward;
        }
        else
        {
            // fallback: aplicar rotación instantánea snappeada
            RotateTowardsMouseInstant();
        }

        // 2) Lockear rotación snapped
        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(mouseWorldDir, true);
        }

        // 3) Esperar a que se alcance la rotación lockeada
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

        // Asegurar rotación exacta antes de lanzar
        if (playerMovement != null) playerMovement.ForceApplyLockedRotation();

        // 4) Ejecutar lanzamiento
        ThrowShield();

        // 5) Desbloquear rotación inmediatamente después del lanzamiento
        // (el jugador puede moverse mientras el escudo vuela)
        if (playerMovement != null) playerMovement.UnlockFacing();

        isThrowingShield = false;
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

    /// <summary>
    /// Función que Lanza el escudo en la dirección del mouse y lo instancia en el punto y altura del spawn point.
    /// </summary>
    private void ThrowShield()
    {
        hasShield = false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, shieldSpawnPoint.position);

        Vector3 direction = transform.forward;

        GameObject shieldInstance = ShieldPooler.Instance.GetPooledObject();

        if (shieldInstance != null)
        {
            shieldInstance.transform.position = shieldSpawnPoint.position;
            shieldInstance.transform.rotation = Quaternion.LookRotation(direction);
            shieldInstance.GetComponent<Shield>().Throw(this, direction, canShieldRebound, shieldMaxRebounds, shieldReboundRadius, currentShieldDamage, currentShieldSpeed, shieldMaxDistance);

            ReportDebug($"Escudo lanzado en dirección {direction}.", 1);
        }
        else
        {
            hasShield = true;
            ReportDebug("No se pudo obtener instancia del escudo del pool.", 2);
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

    // El escudo llama a esta función cuando regresa
    public void CatchShield()
    {
        hasShield = true;
        ReportDebug("Escudo recuperado.", 1);
    }

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