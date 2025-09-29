using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Enumeraci�n de los diferentes tipos de estad�sticas del jugador.
/// </summary>
public enum StatType
{
    MaxHealth,
    MoveSpeed,
    Gravity,
    MeleeAttackDamage,
    MeleeAttackSpeed,
    MeleeRadius,
    ShieldAttackDamage,
    ShieldSpeed,
    ShieldMaxDistance,
    ShieldMaxRebounds,
    ShieldReboundRadius,
    AttackDamage,
    AttackSpeed,
    ShieldBlockUpgrade,
    DamageTaken,
    HealthDrainAmount,

    EssenceCostReduction,
    ShopPriceReduction,
    HealthPerRoomRegen,
    MeleeStunChance,
    RangedSlowStunChance,
    CriticalChance,
    LuckStack,
    FireDashEffect,
    ResidualDashEffect,

    StunnedOnHitChance,
    ShieldCatchRequired,
    SameAttackDamageReduction,
    MissChance,
    ShieldDropChance,
    BerserkerEffect,
    DashRangeMultiplier
}

/// <summary>
/// Clase que maneja las estad�sticas del jugador, incluyendo buffs y debuffs temporales (por tiempo o salas/habitaciones/enfrentamientos) o permanentes.
/// Adem�s muestra todas las stats en un TextMeshProUGUI ordenado y permite abrir/cerrar el panel con la tecla P.
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    [Header("ScriptableObjects")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerStats currentStatSO;

    public PlayerStats _currentStatSO => currentStatSO;

    [Header("Stats (internas)")]
    [SerializeField] private Dictionary<StatType, float> baseStats = new();
    [SerializeField] private Dictionary<StatType, float> currentStats = new();

    public static event Action<StatType, float> OnStatChanged;

    private PlayerHealth playerHealth;
    private int roomsCompletedSinceStart = 0;

    [Header("UI: Mostrar estad�sticas")]
    [SerializeField] private TextMeshProUGUI statsText; // Arrastra aqu� el TextMeshProUGUI del canvas
    [Tooltip("GameObject que contiene el panel con el TextMeshProUGUI. Si no se asigna, se intentar� usar el parent del statsText.")]
    [SerializeField] private GameObject statsPanel;
    [Tooltip("Si est� activo, se mostrar� tambi�n el valor base entre par�ntesis.")]
    [SerializeField] private bool showBaseValues = true;
    [Tooltip("Cantidad de decimales a mostrar para los valores.")]
    [SerializeField] private int decimals = 2;

    private void Awake()
    {
        InitializeStats();
        ResetCurrentStatsToBase();
    }

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();

        // Si no se asign� explicitamente el panel, intentamos usar el parent del TextMeshProUGUI
        if (statsPanel == null && statsText != null)
        {
            if (statsText.transform.parent != null)
                statsPanel = statsText.transform.parent.gameObject;
            else
                statsPanel = statsText.gameObject;
        }

        UpdateStatsDisplay(); // asegurar display inicial
    }

    private void OnEnable()
    {
        OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        OnStatChanged -= HandleStatChanged;
    }

    private void Update()
    {
        // Toggle del panel con la tecla P
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleStatsPanel();
        }
    }

    private void HandleStatChanged(StatType type, float newValue)
    {
        // Simplemente refresca todo el display cuando cambie cualquier stat.
        UpdateStatsDisplay();
    }

    /// <summary>
    /// Alterna el estado (activo/inactivo) del panel de estad�sticas.
    /// </summary>
    private void ToggleStatsPanel()
    {
        if (statsPanel == null)
        {
            // Si no hay panel, intentamos alternar directamente el objeto de statsText
            if (statsText != null)
            {
                statsText.gameObject.SetActive(!statsText.gameObject.activeSelf);
                if (statsText.gameObject.activeSelf) UpdateStatsDisplay();
            }
            return;
        }

        bool newState = !statsPanel.activeSelf;
        statsPanel.SetActive(newState);

        // Si se abre el panel, actualizamos el texto para mostrar valores recientes.
        if (newState)
            UpdateStatsDisplay();
    }

    private void OnEnable()
    {
        DungeonGenerator.OnRoomCompleted += IncrementRoomCount;
    }

    private void OnDisable()
    {
        DungeonGenerator.OnRoomCompleted -= IncrementRoomCount;
    }

    private void IncrementRoomCount()
    {
        roomsCompletedSinceStart++;
        Debug.Log($"[PlayerStatsManager] Sala completada. Contador: {roomsCompletedSinceStart}");
    }

    /// <summary>
    /// Inicializa las estad�sticas base del jugador a partir de un ScriptableObject.
    /// Se llama en Awake para asegurar que siempre haya valores.
    /// </summary>
    private void InitializeStats()
    {
        if (currentStatSO == null)
        {
            return;
        }

        baseStats[StatType.MaxHealth] = currentStatSO.maxHealth;
        baseStats[StatType.MoveSpeed] = currentStatSO.moveSpeed;
        baseStats[StatType.Gravity] = currentStatSO.gravity;
        baseStats[StatType.MeleeAttackDamage] = currentStatSO.meleeAttackDamage;
        baseStats[StatType.MeleeRadius] = currentStatSO.meleeRadius;
        baseStats[StatType.ShieldAttackDamage] = currentStatSO.shieldAttackDamage;
        baseStats[StatType.ShieldSpeed] = currentStatSO.shieldSpeed;
        baseStats[StatType.ShieldMaxDistance] = currentStatSO.shieldMaxDistance;
        baseStats[StatType.ShieldMaxRebounds] = currentStatSO.shieldMaxRebounds;
        baseStats[StatType.ShieldReboundRadius] = currentStatSO.shieldReboundRadius;
        baseStats[StatType.AttackDamage] = currentStatSO.attackDamage;
        baseStats[StatType.AttackSpeed] = currentStatSO.attackSpeed;
        baseStats[StatType.MeleeAttackSpeed] = currentStatSO.meleeSpeed;
        baseStats[StatType.HealthDrainAmount] = currentStatSO.HealthDrainAmount;
        baseStats[StatType.DamageTaken] = 0f;
        baseStats[StatType.ShieldBlockUpgrade] = 0f;
    }

    /// <summary>
    /// Funci�n que reinicia las estad�sticas actuales a sus valores base.
    /// Esto ocurre en cada carga de escena para la nueva instancia.
    /// </summary>
    private void ResetCurrentStatsToBase()
    {
        foreach (var kvp in baseStats)
        {
            currentStats[kvp.Key] = kvp.Value;
            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
        }

        // Adem�s de las invocaciones por stat, actualiza una vez para garantizar consistencia.
        UpdateStatsDisplay();
    }

    public void ResetStatsOnDeath()
    {
        if (playerStats != null && currentStatSO != null)
        {
            CopyStatsToSO(playerStats, currentStatSO);
        }

        InitializeStats();
        ResetCurrentStatsToBase();
    }

    public void ResetRunStatsToDefaults()
    {
        if (playerStats != null && _currentStatSO != null)
        {
            CopyStatsToSO(playerStats, _currentStatSO);

            _currentStatSO.currentHealth = _currentStatSO.maxHealth;

            _currentStatSO.isShieldBlockUpgradeActive = false;

            Debug.Log("[PlayerStatsManager] Reset completo de stats para nueva Run ejecutado. Vida Maxima forzada.");

            InitializeStats();
        }
    }

    private void CopyStatsToSO(PlayerStats source, PlayerStats target)
    {
        target.maxHealth = source.maxHealth;
        target.moveSpeed = source.moveSpeed;
        target.moveSpeed = source.moveSpeed;
        target.gravity = source.gravity;
        target.meleeAttackDamage = source.meleeAttackDamage;
        target.meleeRadius = source.meleeRadius;
        target.shieldAttackDamage = source.shieldAttackDamage;
        target.shieldSpeed = source.shieldSpeed;
        target.shieldMaxDistance = source.shieldMaxDistance;
        target.shieldMaxRebounds = source.shieldMaxRebounds;
        target.shieldReboundRadius = source.shieldReboundRadius;
        target.attackDamage = source.attackDamage;
        target.attackSpeed = source.attackSpeed;
        target.meleeSpeed = source.meleeSpeed;
        target.HealthDrainAmount = source.HealthDrainAmount;
    }

    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

    /// <summary>
    /// Aplica un buff/debuff al stat especificado.
    /// </summary>
    /// <param name="type">Stat a modificar.</param>
    /// <param name="amount">Cantidad (positiva o negativa).</param>
    /// <param name="isPercentage">Si es true, el buff es proporcional al valor base.</param>
    /// <param name="isTemporary">Si es false, el buff es permanente hasta morir.</param>
    /// <param name="duration">Duraci�n en segundos (solo si es temporal por tiempo).</param>
    /// <param name="isByRooms">Si es true, la duraci�n se mide por salas/habitaciones/enfrentamientos.</param>
    /// <param name="roomsDuration">Cantidad de salas/habitaciones/enfrentamientos que debe durar.</param>
    public void ApplyModifier(StatType type, float amount, bool isPercentage = false, bool isTemporary = false, float duration = 0f, bool isByRooms = false, int roomsDuration = 0)
    {
        if (!baseStats.ContainsKey(type))
        {
            return;
        }

        float modifierValue = amount;

        if (isPercentage && type != StatType.ShieldBlockUpgrade)
        {
            float percentageFactor = amount / 100f;
            modifierValue = baseStats[type] * percentageFactor;
        }

        currentStats[type] += modifierValue;

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat '{type}' result� en un valor inv�lido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
        }

        OnStatChanged?.Invoke(type, currentStats[type]);

        if (!isTemporary)
        {
            baseStats[type] = currentStats[type];
            SetStatOnSO(currentStatSO, type, currentStats[type]);

            return;
        }

        if (isByRooms)
        {
            StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, roomsDuration));
        }
        else if (duration > 0f)
        {
            StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
        }
    }

    public void ModifyPermanentStat(StatType type, float modifierValue)
    {
        if (!baseStats.ContainsKey(type)) return;

        currentStats[type] += modifierValue;

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat permanente '{type}' result� en un valor inv�lido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
        }

        baseStats[type] = currentStats[type];
        SetStatOnSO(currentStatSO, type, currentStats[type]);

        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    public void ApplyTemporaryStatByRooms(StatType type, float modifierValue, int rooms)
    {
        currentStats[type] += modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);

        StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, rooms));
    }

    /// <summary>
    /// Aplica una modificaci�n de estad�stica temporal que dura por un tiempo espec�fico.
    /// </summary>
    public void ApplyTemporaryStatByTime(StatType type, float modifierValue, float duration)
    {
        currentStats[type] += modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);

        StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
    }


    private void SetStatOnSO(PlayerStats so, StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHealth: so.maxHealth = value; break;
            case StatType.MoveSpeed: so.moveSpeed = value; break;
            case StatType.Gravity: so.gravity = value; break;
            case StatType.MeleeAttackDamage: so.meleeAttackDamage = (int)value; break;
            case StatType.MeleeAttackSpeed: so.meleeSpeed = value; break;
            case StatType.MeleeRadius: so.meleeRadius = value; break;
            case StatType.ShieldAttackDamage: so.shieldAttackDamage = (int)value; break;
            case StatType.ShieldSpeed: so.shieldSpeed = value; break;
            case StatType.ShieldMaxDistance: so.shieldMaxDistance = value; break;
            case StatType.ShieldMaxRebounds: so.shieldMaxRebounds = (int)value; break;
            case StatType.ShieldReboundRadius: so.shieldReboundRadius = value; break;
            case StatType.AttackDamage: so.attackDamage = value; break;
            case StatType.AttackSpeed: so.attackSpeed = value; break;
            case StatType.HealthDrainAmount: so.HealthDrainAmount = value; break;

            case StatType.DamageTaken:
            case StatType.ShieldBlockUpgrade:
                break;
            default:
                Debug.LogWarning($"El StatType {type} no est� mapeado para la modificaci�n directa del SO.");
                break;
        }
    }

    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
    {
        yield return new WaitForSeconds(duration);

        currentStats[type] -= modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    private IEnumerator RemoveModifierAfterRooms(StatType type, float modifierValue, int rooms)
    {
        int startRoomCount = roomsCompletedSinceStart;
        int targetRoomCount = startRoomCount + rooms;

        while (roomsCompletedSinceStart < targetRoomCount)
        {
            yield return null;
        }

        currentStats[type] -= modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);
        Debug.Log($"Efecto temporal '{type}' removido despu�s de {rooms} habitaciones.");
    }

    public float GetCurrentStat(StatType type)
    {
        if (currentStats.ContainsKey(type))
        {
            return currentStats[type];
        }

        return 0f;
    }

    #region UI Helper: Construir y actualizar el TextMeshPro

    /// <summary>
    /// Construye el texto que se mostrar� en el TextMeshProUGUI con todas las stats ordenadas.
    /// </summary>
    private void UpdateStatsDisplay()
    {
        if (statsText == null)
        {
            return;
        }

        var sb = new StringBuilder();

        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            float current = currentStats.TryGetValue(stat, out var cur) ? cur : 0f;
            float baseVal = baseStats.TryGetValue(stat, out var b) ? b : 0f;

            string formattedCurrent = current.ToString($"F{Mathf.Max(0, decimals)}");
            if (showBaseValues)
            {
                string formattedBase = baseVal.ToString($"F{Mathf.Max(0, decimals)}");
                sb.AppendLine($"{SplitCamelCase(stat.ToString())}: {formattedCurrent} (base: {formattedBase})");
            }
            else
            {
                sb.AppendLine($"{SplitCamelCase(stat.ToString())}: {formattedCurrent}");
            }
        }

        statsText.text = sb.ToString();
    }

    /// <summary>
    /// Convierte un nombre en CamelCase a una cadena con espacios para mejor lectura.
    /// Ej: ShieldMaxDistance -> Shield Max Distance
    /// </summary>
    private string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder();
        sb.Append(input[0]);
        for (int i = 1; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsUpper(c) && !char.IsUpper(input[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    #endregion
}



// Codigo anterior a la modificacion(stats mostrados en un textmeshpro)

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// Enumeraci�n de los diferentes tipos de estad�sticas del jugador.
///// </summary>
//public enum StatType
//{
//    MaxHealth,
//    MoveSpeed,
//    Gravity,
//    MeleeAttackDamage,
//    MeleeAttackSpeed,
//    MeleeRadius,
//    ShieldAttackDamage,
//    ShieldSpeed,
//    ShieldMaxDistance,
//    ShieldMaxRebounds,
//    ShieldReboundRadius,
//    AttackDamage,
//    AttackSpeed,
//    ShieldBlockUpgrade,
//    DamageTaken,
//    HealthDrainAmount,
//}

///// <summary>
///// Clase que maneja las estad�sticas del jugador, incluyendo buffs y debuffs temporales (por tiempo o salas/habitaciones/enfrentamientos) o permanentes.
///// </summary>
//public class PlayerStatsManager : MonoBehaviour
//{
//    [SerializeField] private PlayerStats playerStats;
//    [SerializeField] private PlayerStats currentStatSO;

//    public PlayerStats _currentStatSO => currentStatSO;

//    [SerializeField] private Dictionary<StatType, float> baseStats = new();
//    [SerializeField] private Dictionary<StatType, float> currentStats = new();

//    public static event Action<StatType, float> OnStatChanged;

//    private PlayerHealth playerHealth;

//    private void Awake()
//    {
//        InitializeStats();
//        ResetCurrentStatsToBase();
//    }

//    private void Start()
//    {
//        playerHealth = GetComponent<PlayerHealth>();
//    }

//    /// <summary>
//    /// Inicializa las estad�sticas base del jugador a partir de un ScriptableObject.
//    /// Se llama en Awake para asegurar que siempre haya valores.
//    /// </summary>
//    private void InitializeStats()
//    {
//        if (currentStatSO == null)
//        {
//            return;
//        }

//        baseStats[StatType.MaxHealth] = currentStatSO.maxHealth;
//        baseStats[StatType.MoveSpeed] = currentStatSO.moveSpeed;
//        baseStats[StatType.Gravity] = currentStatSO.gravity;
//        baseStats[StatType.MeleeAttackDamage] = currentStatSO.meleeAttackDamage;
//        baseStats[StatType.MeleeRadius] = currentStatSO.meleeRadius;
//        baseStats[StatType.ShieldAttackDamage] = currentStatSO.shieldAttackDamage;
//        baseStats[StatType.ShieldSpeed] = currentStatSO.shieldSpeed;
//        baseStats[StatType.ShieldMaxDistance] = currentStatSO.shieldMaxDistance;
//        baseStats[StatType.ShieldMaxRebounds] = currentStatSO.shieldMaxRebounds;
//        baseStats[StatType.ShieldReboundRadius] = currentStatSO.shieldReboundRadius;
//        baseStats[StatType.AttackDamage] = currentStatSO.attackDamage;
//        baseStats[StatType.AttackSpeed] = currentStatSO.attackSpeed;
//        baseStats[StatType.MeleeAttackSpeed] = currentStatSO.meleeSpeed;
//        baseStats[StatType.HealthDrainAmount] = currentStatSO.HealthDrainAmount;
//        baseStats[StatType.DamageTaken] = 0f;
//        baseStats[StatType.ShieldBlockUpgrade] = 0f;
//    }

//    /// <summary>
//    /// Funci�n que reinicia las estad�sticas actuales a sus valores base.
//    /// Esto ocurre en cada carga de escena para la nueva instancia.
//    /// </summary>
//    private void ResetCurrentStatsToBase()
//    {
//        foreach (var kvp in baseStats)
//        {
//            currentStats[kvp.Key] = kvp.Value;
//            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
//        }
//    }

//    public void ResetStatsOnDeath()
//    {
//        if (playerStats != null && currentStatSO != null)
//        {
//            CopyStatsToSO(playerStats, currentStatSO);
//        }

//        InitializeStats();
//        ResetCurrentStatsToBase();
//    }

//    public void ResetRunStatsToDefaults()
//    {
//        if (playerStats != null && _currentStatSO != null)
//        {
//            CopyStatsToSO(playerStats, _currentStatSO);

//            _currentStatSO.currentHealth = _currentStatSO.maxHealth;

//            _currentStatSO.isShieldBlockUpgradeActive = false;

//            Debug.Log("[PlayerStatsManager] Reset completo de stats para nueva Run ejecutado. Vida Maxima forzada.");

//            InitializeStats();
//        }
//    }

//    private void CopyStatsToSO(PlayerStats source, PlayerStats target)
//    {
//        target.maxHealth = source.maxHealth;
//        target.moveSpeed = source.moveSpeed;
//        target.moveSpeed = source.moveSpeed;
//        target.gravity = source.gravity;
//        target.meleeAttackDamage = source.meleeAttackDamage;
//        target.meleeRadius = source.meleeRadius;
//        target.shieldAttackDamage = source.shieldAttackDamage;
//        target.shieldSpeed = source.shieldSpeed;
//        target.shieldMaxDistance = source.shieldMaxDistance;
//        target.shieldMaxRebounds = source.shieldMaxRebounds;
//        target.shieldReboundRadius = source.shieldReboundRadius;
//        target.attackDamage = source.attackDamage;
//        target.attackSpeed = source.attackSpeed;
//        target.meleeSpeed = source.meleeSpeed;
//        target.HealthDrainAmount = source.HealthDrainAmount;
//    }

//    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

//    /// <summary>
//    /// Aplica un buff/debuff al stat especificado.
//    /// </summary>
//    /// <param name="type">Stat a modificar.</param>
//    /// <param name="amount">Cantidad (positiva o negativa).</param>
//    /// <param name="isPercentage">Si es true, el buff es proporcional al valor base.</param>
//    /// <param name="isTemporary">Si es false, el buff es permanente hasta morir.</param>
//    /// <param name="duration">Duraci�n en segundos (solo si es temporal por tiempo).</param>
//    /// <param name="isByRooms">Si es true, la duraci�n se mide por salas/habitaciones/enfrentamientos.</param>
//    /// <param name="roomsDuration">Cantidad de salas/habitaciones/enfrentamientos que debe durar.</param>
//    public void ApplyModifier(StatType type, float amount, bool isPercentage = false, bool isTemporary = false, float duration = 0f, bool isByRooms = false, int roomsDuration = 0)
//    {
//        if (!baseStats.ContainsKey(type))
//        {
//            return;
//        }

//        float modifierValue = amount;

//        if (isPercentage && type != StatType.ShieldBlockUpgrade)
//        {
//            float percentageFactor = amount / 100f;
//            modifierValue = baseStats[type] * percentageFactor;
//        }

//        currentStats[type] += modifierValue;

//        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
//        {
//            Debug.LogError($"[PlayerStatsManager] Stat '{type}' result� en un valor inv�lido ({currentStats[type]}). Se ha reseteado al valor base.");
//            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
//        }

//        OnStatChanged?.Invoke(type, currentStats[type]);

//        if (!isTemporary)
//        {
//            baseStats[type] = currentStats[type];
//            SetStatOnSO(currentStatSO, type, currentStats[type]);

//            return;
//        }

//        if (isByRooms)
//        {
//            StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, roomsDuration));
//        }
//        else if (duration > 0f)
//        {
//            StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
//        }
//    }

//    private void SetStatOnSO(PlayerStats so, StatType type, float value)
//    {
//        switch (type)
//        {
//            case StatType.MaxHealth: so.maxHealth = value; break;
//            case StatType.MoveSpeed: so.moveSpeed = value; break;
//            case StatType.Gravity: so.gravity = value; break;
//            case StatType.MeleeAttackDamage: so.meleeAttackDamage = (int)value; break;
//            case StatType.MeleeAttackSpeed: so.meleeSpeed = value; break;
//            case StatType.MeleeRadius: so.meleeRadius = value; break;
//            case StatType.ShieldAttackDamage: so.shieldAttackDamage = (int)value; break;
//            case StatType.ShieldSpeed: so.shieldSpeed = value; break;
//            case StatType.ShieldMaxDistance: so.shieldMaxDistance = value; break;
//            case StatType.ShieldMaxRebounds: so.shieldMaxRebounds = (int)value; break;
//            case StatType.ShieldReboundRadius: so.shieldReboundRadius = value; break;
//            case StatType.AttackDamage: so.attackDamage = value; break;
//            case StatType.AttackSpeed: so.attackSpeed = value; break;
//            case StatType.HealthDrainAmount: so.HealthDrainAmount = value; break;

//            case StatType.DamageTaken:
//            case StatType.ShieldBlockUpgrade:
//                break;
//            default:
//                Debug.LogWarning($"El StatType {type} no est� mapeado para la modificaci�n directa del SO.");
//                break;
//        }
//    }

//    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
//    {
//        yield return new WaitForSeconds(duration);

//        currentStats[type] -= modifierValue;
//        OnStatChanged?.Invoke(type, currentStats[type]);
//    }

//    private IEnumerator RemoveModifierAfterRooms(StatType type, float modifierValue, int rooms)
//    {
//        int completedRooms = 0;

//        // Aqu� deber�a suscribirse a un evento de "sala completada".
//        // Espera frames hasta que "completedRooms" alcance el valor.
//        while (completedRooms < rooms)
//        {
//            yield return null;
//            // Futura linea de codigo para incrementar completedRooms con un evento.
//        }

//        currentStats[type] -= modifierValue;
//        OnStatChanged?.Invoke(type, currentStats[type]);
//    }

//    public float GetCurrentStat(StatType type)
//    {
//        if (currentStats.ContainsKey(type))
//        {
//            return currentStats[type];
//        }

//        return 0f;
//    }
//}