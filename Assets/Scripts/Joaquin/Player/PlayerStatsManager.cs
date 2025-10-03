using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using static PlayerHealth;

/// <summary>
/// Enumeración de los diferentes tipos de estadísticas del jugador.
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
    LifestealOnKill,

    StunnedOnHitChance,
    ShieldCatchRequired,
    SameAttackDamageReduction,
    MissChance,
    ShieldDropChance,
    BerserkerEffect,
    DashRangeMultiplier
}

/// <summary>
/// Clase que maneja las estadísticas del jugador, incluyendo buffs y debuffs temporales (por tiempo o salas/habitaciones/enfrentamientos) o permanentes.
/// Además muestra todas las stats en un TextMeshProUGUI ordenado y permite abrir/cerrar el panel con la tecla P.
/// </summary>
public partial class PlayerStatsManager : MonoBehaviour
{
    [Header("ScriptableObjects")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerStats currentStatSO;

    public PlayerStats _currentStatSO => currentStatSO;

    [Header("Stats (internas)")]
    [SerializeField] private Dictionary<StatType, float> baseStats = new();
    [SerializeField] private Dictionary<StatType, float> currentStats = new();
    private Dictionary<string, Dictionary<StatType, float>> namedModifiers = new();

    // Guarda el estado visual de cada stat:
    //  1 => verde (subió), -1 => rojo (bajó), 0 => neutro
    private Dictionary<StatType, int> statVisualState = new();

    public static event Action<StatType, float> OnStatChanged;

    private PlayerHealth playerHealth;
    private int roomsCompletedSinceStart = 0;

    [Header("UI: Mostrar estadísticas")]
    [SerializeField] private TextMeshProUGUI statsText; // Arrastra aquí el TextMeshProUGUI del canvas
    [Tooltip("GameObject que contiene el panel con el TextMeshProUGUI. Si no se asigna, se intentará usar el parent del statsText.")]
    [SerializeField] private GameObject statsPanel;
    [Tooltip("Si está activo, se mostrará también el valor base entre paréntesis.")]
    [SerializeField] private bool showBaseValues = true;
    [Tooltip("Cantidad de decimales a mostrar para los valores.")]
    [SerializeField] private int decimals = 2;

    private void Awake()
    {
        // Inicializa los estados visuales en 0 para todos los StatType
        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            statVisualState[s] = 0;
        }

        InitializeStats();
        ResetCurrentStatsToBase();
    }

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();

        // Si no se asignó explicitamente el panel, intentamos usar el parent del TextMeshProUGUI
        if (statsPanel == null && statsText != null)
        {
            if (statsText.transform.parent != null)
                statsPanel = statsText.transform.parent.gameObject;
            else
                statsPanel = statsText.gameObject;
        }

        // Asegurarnos de que TMP permita rich text (necesario para colorear valores).
        if (statsText != null)
        {
            statsText.richText = true;
        }

        UpdateStatsDisplay(); // asegurar display inicial
    }

    private void OnEnable()
    {
        OnStatChanged += HandleStatChanged;
        DungeonGenerator.OnRoomCompleted += IncrementRoomCount;
        PlayerHealth.OnLifeStageChanged += ApplyLifeStageModifiers;
    }

    private void OnDisable()
    {
        OnStatChanged -= HandleStatChanged;
        DungeonGenerator.OnRoomCompleted -= IncrementRoomCount;
        PlayerHealth.OnLifeStageChanged -= ApplyLifeStageModifiers;
    }

    private void Update()
    {
        // Toggle del panel con la tecla P
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleStatsPanel();
        }

        bool shiftPresionado = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shiftPresionado && Input.GetKeyDown(KeyCode.E))
        {
            showDebugOnGUI = !showDebugOnGUI;
        }
    }

    private void HandleStatChanged(StatType type, float newValue)
    {
        // Simplemente refresca todo el display cuando cambie cualquier stat.
        UpdateStatsDisplay();
    }

    /// <summary>
    /// Alterna el estado (activo/inactivo) del panel de estadísticas.
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

    private void IncrementRoomCount()
    {
        roomsCompletedSinceStart++;
        Debug.Log($"[PlayerStatsManager] Sala completada. Contador: {roomsCompletedSinceStart}");
    }

    /// <summary>
    /// Inicializa las estadísticas base del jugador a partir de un ScriptableObject.
    /// Se llama en Awake para asegurar que siempre haya valores.
    /// </summary>
    private void InitializeStats()
    {
        if (currentStatSO == null)
        {
            return;
        }

        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            float baseValue = GetStatFromSO(currentStatSO, s);

            baseStats[s] = baseValue;

            if (!statVisualState.ContainsKey(s))
                statVisualState[s] = 0;
        }
    }

    private float GetStatFromSO(PlayerStats so, StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return so.maxHealth;
            case StatType.MoveSpeed: return so.moveSpeed;
            case StatType.Gravity: return so.gravity;
            case StatType.AttackDamage: return so.attackDamage;
            case StatType.AttackSpeed: return so.attackSpeed;
            case StatType.HealthDrainAmount: return so.HealthDrainAmount;
            case StatType.LifestealOnKill: return so.lifestealOnKillAmount;

            case StatType.MeleeAttackDamage: return so.meleeAttackDamage;
            case StatType.MeleeAttackSpeed: return so.meleeSpeed;
            case StatType.MeleeRadius: return so.meleeRadius;
            //case StatType.MeleeStunChance: return so.meleeStunChanceBase; 

            case StatType.ShieldAttackDamage: return so.shieldAttackDamage;
            case StatType.ShieldSpeed: return so.shieldSpeed;
            case StatType.ShieldMaxDistance: return so.shieldMaxDistance;
            case StatType.ShieldMaxRebounds: return so.shieldMaxRebounds;
            case StatType.ShieldReboundRadius: return so.shieldReboundRadius;
            case StatType.ShieldBlockUpgrade: return so.isShieldBlockUpgradeActive ? 1f : 0f;
            //case StatType.ShieldCatchRequired: return so.isShieldCatchRequiredActive ? 1f : 0f; 
            //case StatType.ShieldDropChance: return so.shieldDropChanceBase;

            //case StatType.EssenceCostReduction: return so.essenceCostReductionBase; 
            case StatType.ShopPriceReduction: return so.shopPriceReductionBase; 
            case StatType.HealthPerRoomRegen: return so.healthPerRoomRegenBase;

            //case StatType.RangedSlowStunChance: return so.rangedSlowStunChanceBase; 
            //case StatType.CriticalChance: return so.criticalChanceBase; 
            //case StatType.LuckStack: return so.luckStackBase;
            //case StatType.FireDashEffect: return so.fireDashEffectActive ? 1f : 0f; 
            //case StatType.ResidualDashEffect: return so.residualDashEffectActive ? 1f : 0f;
            //case StatType.DashRangeMultiplier: return so.dashRangeMultiplierBase; 

            //case StatType.StunnedOnHitChance: return so.stunnedOnHitChanceBase;
            //case StatType.SameAttackDamageReduction: return so.sameAttackDamageReductionBase; 
            //case StatType.MissChance: return so.missChanceBase; 
            //case StatType.BerserkerEffect: return so.berserkerEffectActive ? 1f : 0f; 

            case StatType.DamageTaken: return 0f;

            default:
                Debug.LogWarning($"El StatType {type} no está mapeado en GetStatFromSO. Retornando 0.");
                return 0f;
        }
    }


    /// <summary>
    /// Función que reinicia las estadísticas actuales a sus valores base.
    /// Esto ocurre en cada carga de escena para la nueva instancia.
    /// </summary>
    private void ResetCurrentStatsToBase()
    {
        foreach (var kvp in baseStats)
        {
            currentStats[kvp.Key] = kvp.Value;
            // Al resetear, limpiamos el estado visual (neutro).
            statVisualState[kvp.Key] = 0;
            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
        }

        // Además de las invocaciones por stat, actualiza una vez para garantizar consistencia.
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
            //_currentStatSO.isShieldCatchRequiredActive = false; 
            //_currentStatSO.fireDashEffectActive = false; 
            //_currentStatSO.residualDashEffectActive = false; 
            //_currentStatSO.berserkerEffectActive = false; 

            Debug.Log("[PlayerStatsManager] Reset completo de stats para nueva Run ejecutado. Vida Maxima forzada.");

            InitializeStats();
        }
    }

    private void CopyStatsToSO(PlayerStats source, PlayerStats target)
    {
        target.maxHealth = source.maxHealth;
        target.moveSpeed = source.moveSpeed;
        target.gravity = source.gravity;
        target.attackDamage = source.attackDamage;
        target.attackSpeed = source.attackSpeed;
        target.HealthDrainAmount = source.HealthDrainAmount;
        target.healthPerRoomRegenBase = source.healthPerRoomRegenBase; 
        target.shopPriceReductionBase = source.shopPriceReductionBase;
        target.lifestealOnKillAmount = source.lifestealOnKillAmount;

        target.meleeAttackDamage = source.meleeAttackDamage;
        target.meleeSpeed = source.meleeSpeed;
        target.meleeRadius = source.meleeRadius;
        //target.meleeStunChanceBase = source.meleeStunChanceBase;

        // Stats de Escudo
        target.shieldAttackDamage = source.shieldAttackDamage;
        target.shieldSpeed = source.shieldSpeed;
        target.shieldMaxDistance = source.shieldMaxDistance;
        target.shieldMaxRebounds = source.shieldMaxRebounds;
        target.shieldReboundRadius = source.shieldReboundRadius;
        target.isShieldBlockUpgradeActive = source.isShieldBlockUpgradeActive;
        //target.isShieldCatchRequiredActive = source.isShieldCatchRequiredActive; 
        //target.shieldDropChanceBase = source.shieldDropChanceBase; 

        //target.essenceCostReductionBase = source.essenceCostReductionBase; 

        //target.rangedSlowStunChanceBase = source.rangedSlowStunChanceBase; 
        //target.criticalChanceBase = source.criticalChanceBase; 
        //target.luckStackBase = source.luckStackBase; 
        //target.fireDashEffectActive = source.fireDashEffectActive; 
        //target.residualDashEffectActive = source.residualDashEffectActive; 
        //target.dashRangeMultiplierBase = source.dashRangeMultiplierBase; 

        //target.stunnedOnHitChanceBase = source.stunnedOnHitChanceBase; 
        //target.sameAttackDamageReductionBase = source.sameAttackDamageReductionBase;
        //target.missChanceBase = source.missChanceBase;
        //target.berserkerEffectActive = source.berserkerEffectActive; 
    }

    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

    /// <summary>
    /// Marca el estado visual basado en el delta aplicado.
    /// delta > 0 => subió (verde)
    /// delta < 0 => bajó (rojo)
    /// delta == 0 => no cambia el estado (se mantiene)
    /// </summary>
    private void MarkVisualStateFromDelta(StatType type, float delta)
    {
        if (delta > 0f)
            statVisualState[type] = 1;
        else if (delta < 0f)
            statVisualState[type] = -1;
        // si delta == 0 no tocamos el estado: se mantiene la coloración anterior
    }

    /// <summary>
    /// Aplica un buff/debuff al stat especificado.
    /// </summary>
    /// <param name="type">Stat a modificar.</param>
    /// <param name="amount">Cantidad (positiva o negativa).</param>
    /// <param name="isPercentage">Si es true, el buff es proporcional al valor base.</param>
    /// <param name="isTemporary">Si es false, el buff es permanente hasta morir.</param>
    /// <param name="duration">Duración en segundos (solo si es temporal por tiempo).</param>
    /// <param name="isByRooms">Si es true, la duración se mide por salas/habitaciones/enfrentamientos.</param>
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

        // registra valor previo para calcular delta visual
        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat '{type}' resultó en un valor inválido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
            // Al resetear por seguridad, ajustamos visual a neutro
            statVisualState[type] = 0;
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

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat permanente '{type}' resultó en un valor inválido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
            statVisualState[type] = 0;
        }

        baseStats[type] = currentStats[type];
        SetStatOnSO(currentStatSO, type, currentStats[type]);

        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    public void ApplyTemporaryStatByRooms(StatType type, float modifierValue, int rooms)
    {
        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);

        StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, rooms));
    }

    /// <summary>
    /// Aplica una modificación de estadística temporal que dura por un tiempo específico.
    /// </summary>
    public void ApplyTemporaryStatByTime(StatType type, float modifierValue, float duration)
    {
        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

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
            case StatType.AttackDamage: so.attackDamage = value; break;
            case StatType.AttackSpeed: so.attackSpeed = value; break;
            case StatType.HealthDrainAmount: so.HealthDrainAmount = value; break;
            case StatType.HealthPerRoomRegen: so.healthPerRoomRegenBase = value; break; 
            case StatType.ShopPriceReduction: so.shopPriceReductionBase = value; break;
            case StatType.LifestealOnKill: so.lifestealOnKillAmount = value; break;
            //case StatType.EssenceCostReduction: so.essenceCostReductionBase = value; break; 

            case StatType.MeleeAttackDamage: so.meleeAttackDamage = (int)value; break; 
            case StatType.MeleeAttackSpeed: so.meleeSpeed = value; break;
            case StatType.MeleeRadius: so.meleeRadius = value; break;
            //case StatType.MeleeStunChance: so.meleeStunChanceBase = value; break; 

            case StatType.ShieldAttackDamage: so.shieldAttackDamage = (int)value; break; 
            case StatType.ShieldSpeed: so.shieldSpeed = value; break;
            case StatType.ShieldMaxDistance: so.shieldMaxDistance = value; break;
            case StatType.ShieldMaxRebounds: so.shieldMaxRebounds = (int)value; break; 
            case StatType.ShieldReboundRadius: so.shieldReboundRadius = value; break;
            case StatType.ShieldBlockUpgrade: so.isShieldBlockUpgradeActive = value > 0.5f; break; 
            //case StatType.ShieldCatchRequired: so.isShieldCatchRequiredActive = value > 0.5f; break; 
            //case StatType.ShieldDropChance: so.shieldDropChanceBase = value; break; 

            //case StatType.RangedSlowStunChance: so.rangedSlowStunChanceBase = value; break; 
            //case StatType.CriticalChance: so.criticalChanceBase = value; break; 
            //case StatType.LuckStack: so.luckStackBase = value; break; 
            //case StatType.FireDashEffect: so.fireDashEffectActive = value > 0.5f; break; 
            //case StatType.ResidualDashEffect: so.residualDashEffectActive = value > 0.5f; break; 
            //case StatType.DashRangeMultiplier: so.dashRangeMultiplierBase = value; break; 

            //case StatType.StunnedOnHitChance: so.stunnedOnHitChanceBase = value; break; 
            //case StatType.SameAttackDamageReduction: so.sameAttackDamageReductionBase = value; break;
            //case StatType.MissChance: so.missChanceBase = value; break; 
            //case StatType.BerserkerEffect: so.berserkerEffectActive = value > 0.5f; break; 

            case StatType.DamageTaken:
                break;

            default:
                Debug.LogWarning($"El StatType {type} no está mapeado para la modificación directa del SO.");
                break;
        }
    }

    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
    {
        yield return new WaitForSeconds(duration);

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] -= modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

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

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] -= modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);
        Debug.Log($"Efecto temporal '{type}' removido después de {rooms} habitaciones.");
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
    /// Construye el texto que se mostrará en el TextMeshProUGUI con todas las stats ordenadas.
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
            string coloredCurrent = formattedCurrent;

            // Usamos el estado visual persistente en lugar de comparar con base directamente.
            int visualState = statVisualState.ContainsKey(stat) ? statVisualState[stat] : 0;
            if (visualState == 1)
            {
                // verde para aumento persistente
                coloredCurrent = $"<color=#00FF00>{formattedCurrent}</color>";
            }
            else if (visualState == -1)
            {
                // rojo para disminución persistente
                coloredCurrent = $"<color=#FF0000>{formattedCurrent}</color>";
            }
            else
            {
                // neutro: mostramos sin color
                coloredCurrent = formattedCurrent;
            }

            if (showBaseValues)
            {
                string formattedBase = baseVal.ToString($"F{Mathf.Max(0, decimals)}");
                sb.AppendLine($"{SplitCamelCase(stat.ToString())}: {coloredCurrent} (base: {formattedBase})");
            }
            else
            {
                sb.AppendLine($"{SplitCamelCase(stat.ToString())}: {coloredCurrent}");
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

    /// <summary>
    /// Aplica modificadores de estadísticas permanentes basados en la etapa de vida del jugador.
    /// Este método es llamado por el evento OnLifeStageChanged de PlayerHealth.
    /// </summary>
    /// <param name="newStage">La nueva etapa de vida del jugador.</param>
    private void ApplyLifeStageModifiers(LifeStage newStage)
    {
        var affectedStats = new StatType[]
        {
        StatType.MoveSpeed,
        StatType.MeleeAttackSpeed,
        StatType.MeleeAttackDamage,
        StatType.ShieldSpeed,
        StatType.ShieldAttackDamage
        };

        // 1. REVERTIR A VALORES BASE: Limpiamos los modificadores de la etapa anterior.
        foreach (var statType in affectedStats)
        {
            if (baseStats.ContainsKey(statType))
            {
                currentStats[statType] = baseStats[statType];
            }
        }

        // 2. APLICAR NUEVOS MODIFICADORES
        switch (newStage)
        {
            case LifeStage.Young:
                Debug.Log("[PlayerStatsManager] Etapa Joven: Aplicando buffs de velocidad y debuffs de daño.");
                currentStats[StatType.MoveSpeed] += baseStats[StatType.MoveSpeed] * 0.25f;
                currentStats[StatType.MeleeAttackSpeed] += baseStats[StatType.MeleeAttackSpeed] * 0.25f;
                currentStats[StatType.MeleeAttackDamage] -= baseStats[StatType.MeleeAttackDamage] * 0.25f;
                currentStats[StatType.ShieldSpeed] += baseStats[StatType.ShieldSpeed] * 0.25f;
                currentStats[StatType.ShieldAttackDamage] -= baseStats[StatType.ShieldAttackDamage] * 0.25f;
                break;

            case LifeStage.Adult:
                Debug.Log("[PlayerStatsManager] Etapa Adulto: Estadísticas base restauradas.");
                // No se aplican modificadores, ya se revirtió a los valores base.
                break;

            case LifeStage.Elder:
                Debug.Log("[PlayerStatsManager] Etapa Anciano: Aplicando buffs de daño y debuffs de velocidad.");
                currentStats[StatType.MoveSpeed] -= baseStats[StatType.MoveSpeed] * 0.25f;
                currentStats[StatType.MeleeAttackSpeed] -= baseStats[StatType.MeleeAttackSpeed] * 0.25f;
                currentStats[StatType.MeleeAttackDamage] += baseStats[StatType.MeleeAttackDamage] * 0.25f;
                currentStats[StatType.ShieldSpeed] -= baseStats[StatType.ShieldSpeed] * 0.25f;
                currentStats[StatType.ShieldAttackDamage] += baseStats[StatType.ShieldAttackDamage] * 0.25f;
                break;
        }

        // 3. NOTIFICAR CAMBIOS: Es crucial para que todo el juego (incluida la UI) se actualice.
        foreach (var statType in affectedStats)
        {
            if (currentStats.ContainsKey(statType))
            {
                OnStatChanged?.Invoke(statType, currentStats[statType]);
            }
        }
    }

    /// <summary>
    /// Aplica un modificador temporal identificado por una clave única. Si ya existe un modificador con esa clave, se sobrescribe.
    /// </summary>
    /// <param name="key">Una clave única para este modificador (ej. "ShieldSkillBuff").</param>
    /// <param name="type">El tipo de stat a modificar.</param>
    /// <param name="amount">La cantidad a añadir (puede ser negativa).</param>
    public void ApplyNamedModifier(string key, StatType type, float amount)
    {
        // Si ya había un buff con esta clave, lo remueve primero para evitar acumulaciones.
        RemoveNamedModifier(key);

        if (!baseStats.ContainsKey(type)) return;

        currentStats[type] += amount;

        if (!namedModifiers.ContainsKey(key))
        {
            namedModifiers[key] = new Dictionary<StatType, float>();
        }
        namedModifiers[key][type] = amount;

        OnStatChanged?.Invoke(type, currentStats[type]);
        Debug.Log($"[PlayerStatsManager] Modificador '{key}' aplicado a {type}: {amount}");
    }

    /// <summary>
    /// Remueve un modificador previamente aplicado con una clave única.
    /// </summary>
    /// <param name="key">La clave única del modificador a remover.</param>
    public void RemoveNamedModifier(string key)
    {
        if (namedModifiers.TryGetValue(key, out var modifiers))
        {
            foreach (var mod in modifiers)
            {
                currentStats[mod.Key] -= mod.Value;
                OnStatChanged?.Invoke(mod.Key, currentStats[mod.Key]);
            }
            namedModifiers.Remove(key);
            Debug.Log($"[PlayerStatsManager] Modificador '{key}' removido.");
        }
    }
}