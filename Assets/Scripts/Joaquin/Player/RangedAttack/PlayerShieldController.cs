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
    [SerializeField] public int shieldDamage = 10;
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

    public bool CanShieldRebound => canShieldRebound;

    [Header("Costes")]
    //[SerializeField] private float timeCostToThrow = 5f;

    private bool hasShield = true;

    // private PlayerTimeResource playerTime;

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
        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerShieldController. Usando valores de fallback.", 2);

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

        // playerTime = GetComponent<PlayerTimeResource>();
        shieldPrefab.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) && hasShield)
        {
            // if (playerTime.CurrentTime >= timeCostToThrow)
            // {
            //     playerTime.SpendTime(timeCostToThrow);
            ThrowShield();
            // }
        }
    }

    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.ShieldAttackDamage)
        {
            shieldDamage = Mathf.RoundToInt(newValue);
        }
        else if (statType == StatType.ShieldSpeed)
        {
            shieldSpeed = newValue;
        }
        else if (statType == StatType.ShieldMaxDistance)
        {
            shieldMaxDistance = newValue;
        }
        else if (statType == StatType.ShieldMaxRebounds)
        {
            shieldMaxRebounds = Mathf.RoundToInt(newValue);
        }
        else if (statType == StatType.ShieldReboundRadius)
        {
            shieldReboundRadius = newValue;
        }

        ReportDebug($"Estadística {statType} actualizada a {newValue}.", 1);
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
                shieldInstance.GetComponent<Shield>().Throw(this, direction, canShieldRebound, shieldMaxRebounds, shieldReboundRadius, shieldDamage, shieldSpeed, shieldMaxDistance);
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