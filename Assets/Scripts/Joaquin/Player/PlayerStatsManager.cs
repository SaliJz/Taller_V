using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using static PlayerHealth;

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

    LuckStack,
    EssenceCostReduction,
    ShopPriceReduction,
    HealthPerRoomRegen,
    CriticalChance,
    LifestealOnKill,

    CriticalDamageMultiplier,
    DashRangeMultiplier
}

/// <summary>
/// Clase que maneja las estad�sticas del jugador, incluyendo buffs y debuffs temporales (por tiempo o salas/habitaciones/enfrentamientos) o permanentes.
/// Adem�s muestra todas las stats en un TextMeshProUGUI ordenado y permite abrir/cerrar el panel con la tecla P.
/// </summary>
public partial class PlayerStatsManager : MonoBehaviour
{
    private struct NamedModifierData
    {
        public bool IsPercentage; // true si value es porcentaje (0.20 = 20%), false si es absoluto
        public float Value;       // porcentaje (0.20) o absoluto (1.25)
        public float AppliedAmount;  // cantidad absoluta que se aplic� a currentStats al momento de aplicar (para remover de forma determinista)
    }

    [Header("ScriptableObjects")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerStats currentStatSO;
    public PlayerStats _currentStatSO => currentStatSO;

    [Header("Stats (internas)")]
    [SerializeField] private Dictionary<StatType, float> baseStats = new();
    [SerializeField] private Dictionary<StatType, float> currentStats = new();
    private Dictionary<string, Dictionary<StatType, NamedModifierData>> namedModifiers = new();

    [Header("UI: Mostrar estad�sticas")]
    [SerializeField] private TextMeshProUGUI statsText;
    [Tooltip("GameObject que contiene el panel con el TextMeshProUGUI. Si no se asigna, se intentar� usar el parent del statsText.")]
    [SerializeField] private GameObject statsPanel;
    [Tooltip("Si est� activo, se mostrar� tambi�n el valor base entre par�ntesis.")]
    [SerializeField] private bool showBaseValues = true;
    [Tooltip("Cantidad de decimales a mostrar para los valores.")]
    [SerializeField] private int decimals = 2;

    private Dictionary<StatType, int> statVisualState = new();

    public static event Action<StatType, float> OnStatChanged;

    private PlayerHealth playerHealth;
    private int roomsCompletedSinceStart = 0;

    private void Awake()
    {
        // Inicializa los estados visuales en 0 para todos los StatType
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            statVisualState[statType] = 0;
        }

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
        PlayerHealth.OnLifeStageChanged += ApplyLifeStageModifiers;
    }

    private void OnDisable()
    {
        OnStatChanged -= HandleStatChanged;
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

    // Incrementa el contador de habitaciones completadas.
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

        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            float baseValue = GetStatFromSO(currentStatSO, s);

            baseStats[s] = baseValue;

            if (!statVisualState.ContainsKey(s))
                statVisualState[s] = 0;
        }
    }

    private float GetStatFromSO(PlayerStats statsSO, StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return statsSO.maxHealth;
            case StatType.MoveSpeed: return statsSO.moveSpeed;
            case StatType.Gravity: return statsSO.gravity;
            case StatType.AttackDamage: return statsSO.attackDamage;
            case StatType.AttackSpeed: return statsSO.attackSpeed;
            case StatType.HealthDrainAmount: return statsSO.HealthDrainAmount;
            case StatType.LifestealOnKill: return statsSO.lifestealOnKillAmount;

            case StatType.MeleeAttackDamage: return statsSO.meleeAttackDamage;
            case StatType.MeleeAttackSpeed: return statsSO.meleeSpeed;
            case StatType.MeleeRadius: return statsSO.meleeRadius;

            case StatType.ShieldAttackDamage: return statsSO.shieldAttackDamage;
            case StatType.ShieldSpeed: return statsSO.shieldSpeed;
            case StatType.ShieldMaxDistance: return statsSO.shieldMaxDistance;
            case StatType.ShieldMaxRebounds: return statsSO.shieldMaxRebounds;
            case StatType.ShieldReboundRadius: return statsSO.shieldReboundRadius;
            case StatType.ShieldBlockUpgrade: return statsSO.isShieldBlockUpgradeActive ? 1f : 0f;

            //case StatType.EssenceCostReduction: return so.essenceCostReductionBase; 
            case StatType.LuckStack: return statsSO.luckStackBase;
            case StatType.ShopPriceReduction: return statsSO.shopPriceReductionBase;
            case StatType.HealthPerRoomRegen: return statsSO.healthPerRoomRegenBase;

            case StatType.CriticalChance: return statsSO.criticalChanceBase;
            case StatType.CriticalDamageMultiplier: return statsSO.criticalDamageMultiplierBase;
            case StatType.DashRangeMultiplier: return statsSO.dashRangeMultiplierBase;

            case StatType.DamageTaken: return 0f;

            default:
                Debug.LogWarning($"El StatType {type} no está mapeado en GetStatFromSO. Retornando 0.");
                return 0f;
        }
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
            // Al resetear, limpiamos el estado visual (neutro).
            statVisualState[kvp.Key] = 0;
            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
        }

        // Adem�s de las invocaciones por stat, actualiza una vez para garantizar consistencia.
        UpdateStatsDisplay();
    }

    /// <summary>
    /// Resetea las estad�sticas al morir el jugador.
    /// </summary> 
    public void ResetStatsOnDeath()
    {
        if (playerStats != null && currentStatSO != null)
        {
            CopyStatsToSO(playerStats, currentStatSO);
        }

        InitializeStats();
        ResetCurrentStatsToBase();
    }

    /// <summary>
    /// Resetea las estad�sticas al iniciar una nueva Run.
    /// </summary>
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

    private void CopyStatsToSO(PlayerStats sourceStats, PlayerStats target)
    {
        target.maxHealth = sourceStats.maxHealth;
        target.moveSpeed = sourceStats.moveSpeed;
        target.gravity = sourceStats.gravity;
        target.attackDamage = sourceStats.attackDamage;
        target.attackSpeed = sourceStats.attackSpeed;
        target.HealthDrainAmount = sourceStats.HealthDrainAmount;
        target.healthPerRoomRegenBase = sourceStats.healthPerRoomRegenBase;
        target.shopPriceReductionBase = sourceStats.shopPriceReductionBase;
        target.lifestealOnKillAmount = sourceStats.lifestealOnKillAmount;

        target.meleeAttackDamage = sourceStats.meleeAttackDamage;
        target.meleeSpeed = sourceStats.meleeSpeed;
        target.meleeRadius = sourceStats.meleeRadius;

        // Stats de Escudo
        target.shieldAttackDamage = sourceStats.shieldAttackDamage;
        target.shieldSpeed = sourceStats.shieldSpeed;
        target.shieldMaxDistance = sourceStats.shieldMaxDistance;
        target.shieldMaxRebounds = sourceStats.shieldMaxRebounds;
        target.shieldReboundRadius = sourceStats.shieldReboundRadius;
        target.isShieldBlockUpgradeActive = sourceStats.isShieldBlockUpgradeActive;
        target.luckStackBase = sourceStats.luckStackBase;
        
        //target.essenceCostReductionBase = source.essenceCostReductionBase; 
        
        target.criticalChanceBase = sourceStats.criticalChanceBase;
        target.criticalDamageMultiplierBase = sourceStats.criticalDamageMultiplierBase;
        target.dashRangeMultiplierBase = sourceStats.dashRangeMultiplierBase;
    }

    /// <summary>
    /// Lee el valor actual de la estad�stica especificada.
    /// </summary> 
    /// <param name="type"> Stat a consultar.</param>
    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

    /// <summary>
    /// Marca el estado visual basado en el delta aplicado.
    /// delta > 0 => subi� (verde)
    /// delta < 0 => baj� (rojo)
    /// delta == 0 => no cambia el estado (se mantiene)
    /// </summary>
    private void MarkVisualStateFromDelta(StatType type, float delta)
    {
        if (delta > 0f)
            statVisualState[type] = 1;
        else if (delta < 0f)
            statVisualState[type] = -1;
        // si delta == 0 no tocamos el estado: se mantiene la coloraci�n anterior
    }

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

        // registra valor previo para calcular delta visual
        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat '{type}' result� en un valor inv�lido ({currentStats[type]}). Se ha reseteado al valor base.");
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
            Debug.LogError($"[PlayerStatsManager] Stat permanente '{type}' result� en un valor inv�lido ({currentStats[type]}). Se ha reseteado al valor base.");
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
    /// Aplica una modificaci�n de estad�stica temporal que dura por un tiempo espec�fico.
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

    /// <summary>
    /// Setea el valor de la estad�stica en el ScriptableObject actual.
    /// </summary>
    /// <param name="so"> ScriptableObject a modificar.</param>
    /// <param name="type"> Stat a modificar.</param>
    /// <param name="value"> Nuevo valor.</param>
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
            case StatType.LuckStack: so.luckStackBase = value; break;
            //case StatType.EssenceCostReduction: so.essenceCostReductionBase = value; break; 

            case StatType.MeleeAttackDamage: so.meleeAttackDamage = (int)value; break;
            case StatType.MeleeAttackSpeed: so.meleeSpeed = value; break;
            case StatType.MeleeRadius: so.meleeRadius = value; break;

            case StatType.ShieldAttackDamage: so.shieldAttackDamage = (int)value; break;
            case StatType.ShieldSpeed: so.shieldSpeed = value; break;
            case StatType.ShieldMaxDistance: so.shieldMaxDistance = value; break;
            case StatType.ShieldMaxRebounds: so.shieldMaxRebounds = (int)value; break;
            case StatType.ShieldReboundRadius: so.shieldReboundRadius = value; break;
            case StatType.ShieldBlockUpgrade: so.isShieldBlockUpgradeActive = value > 0.5f; break;

            case StatType.CriticalChance: so.criticalChanceBase = value; break;
            case StatType.CriticalDamageMultiplier: so.criticalDamageMultiplierBase = value; break;
            case StatType.DashRangeMultiplier: so.dashRangeMultiplierBase = value; break;

            case StatType.DamageTaken:
                break;

            default:
                Debug.LogWarning($"El StatType {type} no está mapeado para la modificación directa del SO.");
                break;
        }
    }

    /// <summary>
    /// Remueve un modificador temporal despu�s de que pase el tiempo especificado.
    /// </summary>
    /// <param name="type"> Stat a modificar.</param>
    /// <param name="modifierValue"> Valor del modificador a remover.</param>
    /// <param name="duration"> Duraci�n en segundos.</param>
    /// <returns> IEnumerator para la corrutina.</returns>
    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
    {
        yield return new WaitForSeconds(duration);

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] -= modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    /// <summary>
    /// Remueve un modificador temporal despu�s de que se completen la cantidad especificada de habitaciones.
    /// </summary>
    /// <param name="type"> Stat a modificar.</param>
    /// <param name="modifierValue"> Valor del modificador a remover.</param>
    /// <param name="duration"> Duraci�n en segundos.</param>
    /// <returns> IEnumerator para la corrutina.</returns>
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
        Debug.Log($"Efecto temporal '{type}' removido despu�s de {rooms} habitaciones.");
    }

    public void RemoveAllBehavioralEffects(List<ItemEffectBase> effectsToClean)
    {
        if (effectsToClean == null) return;

        foreach (ItemEffectBase effect in effectsToClean)
        {
            effect.RemoveEffect(this);
            Debug.Log($"Efecto de amuleto revertido y limpiado: {effect.name}");
        }
    }

    public void ClearAllNamedModifiers()
    {
        List<string> keysToRemove = new List<string>(namedModifiers.Keys);
        foreach (string key in keysToRemove)
        {
            RemoveNamedModifier(key);
        }
        Debug.Log("Todos los modificadores de estadísticas nombrados han sido limpiados.");
    }
    /// <summary>
    /// Lee el valor actual de la estad�stica especificada.
    /// </summary> 
    /// <param name="type"> Stat a consultar.</param>
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
                // rojo para disminuci�n persistente
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

    #region Modificadores por etapa de vida

    /// <summary>
    /// Aplica modificadores de estad�sticas permanentes basados en la etapa de vida del jugador.
    /// Este m�todo es llamado por el evento OnLifeStageChanged de PlayerHealth.
    /// CORRECCI�N: ahora aplica solo incrementos positivos seg�n la etapa (no resta).
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

        var tempNamedModifiers = new Dictionary<string, Dictionary<StatType, NamedModifierData>>(StringComparer.Ordinal);
        foreach (var kvp in namedModifiers)
        {
            var inner = new Dictionary<StatType, NamedModifierData>();
            foreach (var innerKvp in kvp.Value) inner[innerKvp.Key] = innerKvp.Value;
            tempNamedModifiers[kvp.Key] = inner;
        }

        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            if (baseStats.TryGetValue(s, out var baseVal)) currentStats[s] = baseVal;
            else currentStats[s] = 0f;
        }

        switch (newStage)
        {
            case LifeStage.Young:
                Debug.Log("[PlayerStatsManager] Etapa Joven: Aplicando buffs de velocidad y debuffs de da�o.");
                if (baseStats.ContainsKey(StatType.MoveSpeed))
                    currentStats[StatType.MoveSpeed] += baseStats[StatType.MoveSpeed] * 0.25f;
                if (baseStats.ContainsKey(StatType.MeleeAttackSpeed))
                    currentStats[StatType.MeleeAttackSpeed] += baseStats[StatType.MeleeAttackSpeed] * 0.25f;
                if (baseStats.ContainsKey(StatType.MeleeAttackDamage))
                    currentStats[StatType.MeleeAttackDamage] -= baseStats[StatType.MeleeAttackDamage] * 0.25f;
                if (baseStats.ContainsKey(StatType.ShieldSpeed))
                    currentStats[StatType.ShieldSpeed] += baseStats[StatType.ShieldSpeed] * 0.25f;
                if (baseStats.ContainsKey(StatType.ShieldAttackDamage))
                    currentStats[StatType.ShieldAttackDamage] -= baseStats[StatType.ShieldAttackDamage] * 0.25f;
                break;

            case LifeStage.Adult:
                Debug.Log("[PlayerStatsManager] Etapa Adulto: Estad�sticas base restauradas.");
                break;

            case LifeStage.Elder:
                Debug.Log("[PlayerStatsManager] Etapa Anciano: Aplicando buffs de da�o y debuffs de velocidad.");
                if (baseStats.ContainsKey(StatType.MoveSpeed))
                    currentStats[StatType.MoveSpeed] -= baseStats[StatType.MoveSpeed] * 0.25f;
                if (baseStats.ContainsKey(StatType.MeleeAttackSpeed))
                    currentStats[StatType.MeleeAttackSpeed] -= baseStats[StatType.MeleeAttackSpeed] * 0.25f;
                if (baseStats.ContainsKey(StatType.MeleeAttackDamage))
                    currentStats[StatType.MeleeAttackDamage] += baseStats[StatType.MeleeAttackDamage] * 0.25f;
                if (baseStats.ContainsKey(StatType.ShieldSpeed))
                    currentStats[StatType.ShieldSpeed] -= baseStats[StatType.ShieldSpeed] * 0.25f;
                if (baseStats.ContainsKey(StatType.ShieldAttackDamage))
                    currentStats[StatType.ShieldAttackDamage] += baseStats[StatType.ShieldAttackDamage] * 0.25f;
                break;
        }

        namedModifiers.Clear();
        foreach (var modifierKey in tempNamedModifiers.Keys)
        {
            foreach (var statPair in tempNamedModifiers[modifierKey])
            {
                var statType = statPair.Key;
                var modifierData = statPair.Value;

                float applied = modifierData.IsPercentage
                    ? (currentStats.TryGetValue(statType, out var curBase) ? curBase * modifierData.Value : 0f)
                    : modifierData.Value;

                if (!currentStats.ContainsKey(statType)) currentStats[statType] = 0f;

                currentStats[statType] += applied;

                if (!namedModifiers.ContainsKey(modifierKey)) namedModifiers[modifierKey] = new Dictionary<StatType, NamedModifierData>();

                namedModifiers[modifierKey][statType] = new NamedModifierData
                {
                    IsPercentage = modifierData.IsPercentage,
                    Value = modifierData.Value,
                    AppliedAmount = applied
                };
            }
        }

        foreach (var statType in affectedStats)
        {
            if (currentStats.ContainsKey(statType)) OnStatChanged?.Invoke(statType, currentStats[statType]);
        }

        foreach (var kvp in namedModifiers)
        {
            foreach (var kv in kvp.Value)
            {
                var st = kv.Key;
                if (Array.IndexOf(affectedStats, st) < 0) // si no estaba en la lista anterior
                {
                    OnStatChanged?.Invoke(st, currentStats[st]);
                }
            }
        }

        Debug.Log($"[PlayerStatsManager] Cambio de etapa completado. Modificadores nombrados re-aplicados: {namedModifiers.Count}");
    }

    /// <summary>
    /// Aplica un modificador temporal identificado por una clave �nica. Si ya existe un modificador con esa clave, se sobrescribe.
    /// </summary>
    /// <param name="key">Una clave �nica para este modificador (ej. "ShieldSkillBuff").</param>
    /// <param name="type">El tipo de stat a modificar.</param>
    /// <param name="amount">La cantidad a a�adir (puede ser negativa).</param>
    public void ApplyNamedModifier(string key, StatType type, float amount, bool isPercentage = false)
    {
        RemoveNamedModifier(key);

        if (!baseStats.ContainsKey(type))
        {
            if (!currentStats.ContainsKey(type)) currentStats[type] = 0f;
        }

        float referenceBase = currentStats.TryGetValue(type, out var curVal) ? curVal : (baseStats.TryGetValue(type, out var b) ? b : 0f);

        float appliedAmount = isPercentage ? referenceBase * amount : amount;

        if (!namedModifiers.ContainsKey(key)) namedModifiers[key] = new Dictionary<StatType, NamedModifierData>();

        namedModifiers[key][type] = new NamedModifierData
        {
            IsPercentage = isPercentage,
            Value = amount,
            AppliedAmount = appliedAmount
        };

        currentStats[type] = referenceBase + appliedAmount;
        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    /// <summary>
    /// Remueve un modificador previamente aplicado con una clave �nica.
    /// </summary>
    /// <param name="key">La clave �nica del modificador a remover.</param>
    public void RemoveNamedModifier(string key)
    {
        if (namedModifiers.TryGetValue(key, out var modifiers))
        {
            foreach (var modifier in modifiers)
            {
                var stat = modifier.Key;
                var modifiersData = modifier.Value;

                float amountToRemove = modifiersData.AppliedAmount;
                currentStats[stat] = currentStats.TryGetValue(stat, out var cur) ? cur - amountToRemove : -amountToRemove;
                OnStatChanged?.Invoke(stat, currentStats[stat]);
            }
            namedModifiers.Remove(key);
            Debug.Log($"[PlayerStatsManager] Modificador '{key}' removido.");
        }
    }

    #endregion

    #region Efectos temporales por tiempo

    /// <summary>
    /// Aplica un modificador a un stat durante un tiempo determinado y luego lo revierte.
    /// sIRVE para efectos como venenos, ralentizaciones, o buffs.
    /// </summary>
    /// <param name="key">Una clave única para este efecto (ej. "EnemySlow_123").</param>
    /// <param name="type">El stat a modificar.</param>
    /// <param name="amount">La cantidad a añadir (negativa para un debuff).</param>
    /// <param name="duration">La duración del efecto en segundos.</param>
    public void ApplyTimedModifier(string key, StatType type, float amount, float duration)
    {
        StartCoroutine(TimedModifierCoroutine(key, type, amount, duration));
    }

    private IEnumerator TimedModifierCoroutine(string key, StatType type, float amount, float duration)
    {
        Debug.Log($"[PlayerStatsManager] Aplicando efecto temporal '{key}' a {type} ({amount}) por {duration}s.");
        ApplyNamedModifier(key, type, amount);

        yield return new WaitForSeconds(duration);

        Debug.Log($"[PlayerStatsManager] Efecto temporal '{key}' ha expirado. Revirtiendo.");
        RemoveNamedModifier(key);
    }

    #endregion
}