using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class DevilManipulationManager : MonoBehaviour
{
    public static DevilManipulationManager Instance { get; private set; }

    [SerializeField] private DevilConfiguration config;

    private PlayerHealth playerHealthManager;

    private int roomsSinceLastManipulation = 999;
    private int cleanRoomsCounter = 0;

    private List<DevilDistortionType> availableDistortions;
    private List<DevilDistortionType> allDistortionTypes;

    public event Action<DevilAuraType, ResurrectionLevel, float> OnAuraManipulationActivated;
    public event Action<DevilDistortionType> OnDistortionActivated;

    private bool isManipulationActive = false;
    private string activeManipulationName = "Ninguna";

    public float EnemySpeedMultiplier { get; private set; } = 1.0f;
    public float EnemyDamageMultiplier { get; private set; } = 1.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDistortions();
        }
    }

    public void NotifyNonCombatRoom()
    {
        if (roomsSinceLastManipulation != 999)
        {
            roomsSinceLastManipulation++;
        }

        ReportDebug($"Sala no-combate detectada. roomsSinceLastManipulation={roomsSinceLastManipulation}.", 2);
    }

    public void NotifyRoomCleaned(float timeToCleanRoom)
    {
        bool isCleanRoom = timeToCleanRoom <= 180f;

        if (isCleanRoom)
        {
            cleanRoomsCounter++;
            ReportDebug($"Sala limpiada en {timeToCleanRoom:F1}s. cleanRoomsCounter={cleanRoomsCounter}.", 1);
        }
        else
        {
            ReportDebug($"Tiempo de sala ({timeToCleanRoom:F1}s) > 180s. Contador de salas limpias NO se incrementa.", 2);
        }

        if (roomsSinceLastManipulation != 999)
        {
            roomsSinceLastManipulation++;
        }

        CheckManipulationConditions();
    }

    public void TryActivateManipulation(float playerHealthPercent, bool hasMadePact, bool hasObtainedRelic)
    {
        if (roomsSinceLastManipulation < config.CooldownInRooms)
        {
            roomsSinceLastManipulation++;
            ReportDebug($"Enfriamiento activo. Salas restantes: {config.CooldownInRooms - roomsSinceLastManipulation}", 3);
            return;
        }

        bool condition1 = playerHealthPercent >= 0.25f && hasObtainedRelic;
        bool condition3 = hasMadePact;
        bool condition4 = cleanRoomsCounter >= config.MaxCleanRoomsCondition;

        bool condition2 = UnityEngine.Random.Range(0f, 1f) < 0.5f;

        float activationChance = 0f;

        if (condition1 && UnityEngine.Random.Range(0f, 1f) < 0.5f) 
        {
            activationChance = 1f; 
            ReportDebug("Condición 1 (Reliquia + Vida 25%) cumplida.", 2);
        }
        else if (condition2) 
        {
            activationChance = 1f;
            ReportDebug("Condición 2 (No recibir daño) cumplida. Usando prob 50% indep.", 2);
        }
        else if (condition3 && UnityEngine.Random.Range(0f, 1f) < 0.5f) 
        {
            activationChance = 1f;
            ReportDebug("Condición 3 (Pacto) cumplida.", 2);
        }
        else if (condition4)
        {
            activationChance = 1f; 
            ReportDebug($"Condición 4 ({config.MaxCleanRoomsCondition} salas limpias en tiempo) cumplida.", 2);
        }

        if (activationChance > 0f)
        {
            roomsSinceLastManipulation = 0;
            cleanRoomsCounter = 0;

            if (UnityEngine.Random.Range(0f, 1f) < config.AuraProbability)
            {
                ActivateAuraManipulation(ChooseRandomAura(), ChooseRandomResurrectionLevel());
            }
            else
            {
                ActivateDistortion(ChooseRandomDistortion());
            }
        }
        else
        {
            if (roomsSinceLastManipulation != 999)
            {
                roomsSinceLastManipulation++;
            }

            int displayRooms = (roomsSinceLastManipulation == 999) ? 0 : roomsSinceLastManipulation;

            ReportDebug("Ninguna condición de manipulación se cumplió. Salas desde la última: " + displayRooms, 2);
        }
    }

    private DevilAuraType ChooseRandomAura()
    {
        var allAuras = Enum.GetValues(typeof(DevilAuraType))
                                 .Cast<DevilAuraType>()
                                 .Where(a => a != DevilAuraType.None && a != DevilAuraType.PartialResurrection)
                                 .ToList();

        if (allAuras.Count == 0) return DevilAuraType.None;

        return allAuras[UnityEngine.Random.Range(0, allAuras.Count)];
    }

    private ResurrectionLevel ChooseRandomResurrectionLevel()
    {
        if (UnityEngine.Random.Range(0f, 1f) < config.ResurrectionChance)
        {
            return (ResurrectionLevel)UnityEngine.Random.Range(1, 4);
        }
        return ResurrectionLevel.None;
    }

    private DevilDistortionType ChooseRandomDistortion()
    {
        if (availableDistortions.Count == 0)
        {
            availableDistortions = allDistortionTypes.Where(d => d != DevilDistortionType.None).ToList();
            ReportDebug("Todas las distorsiones han ocurrido. La lista ha sido reestablecida.", 1);
        }

        float totalWeight = availableDistortions.Sum(type => GetDistortionChance(type));

        if (totalWeight <= 0) return DevilDistortionType.None;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;
        DevilDistortionType chosenDistortion = DevilDistortionType.None;

        foreach (var type in availableDistortions)
        {
            float chance = GetDistortionChance(type);
            cumulativeWeight += chance;
            if (roll < cumulativeWeight)
            {
                chosenDistortion = type;
                break;
            }
        }

        if (chosenDistortion != DevilDistortionType.None)
        {
            availableDistortions.Remove(chosenDistortion);
        }

        return chosenDistortion;
    }

    private void ActivateAuraManipulation(DevilAuraType aura, ResurrectionLevel level)
    {
        isManipulationActive = true; 
        string auraName = GetAuraName(aura);
        activeManipulationName = auraName; 

        OnAuraManipulationActivated?.Invoke(aura, level, config.AuraEnemyCoveragePercent);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowManipulationText($"¡Aura Demoníaca Activada: {auraName}!");
        }
        ReportDebug($"Aura {aura} activada (Nivel Res: {level}).", 1);
    }

    private void ActivateDistortion(DevilDistortionType distortion)
    {
        isManipulationActive = true; 
        string distortionName = GetDistortionName(distortion);
        activeManipulationName = distortionName; 

        OnDistortionActivated?.Invoke(distortion);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowManipulationText($"¡Distorsión del Diablo: {distortionName}!");
        }
        ReportDebug($"Distorsión {distortion} activada.", 1);
    }

    private string GetDistortionName(DevilDistortionType distortion)
    {
        return distortion.ToString(); 
    }

    private string GetAuraName(DevilAuraType aura)
    {
        return aura.ToString(); 
    }

    public Color GetCurrentLevelColor()
    {
        return Color.white;
    }
    private void OnEnable()
    {
        PlayerHealth.OnPlayerInstantiated += HandlePlayerRegistration;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerInstantiated -= HandlePlayerRegistration;
    }

    private void InitializeDistortions()
    {
        allDistortionTypes = Enum.GetValues(typeof(DevilDistortionType))
                                 .Cast<DevilDistortionType>()
                                 .Where(d => d != DevilDistortionType.None)
                                 .ToList();

        availableDistortions = new List<DevilDistortionType>(allDistortionTypes);
    }

    private void HandlePlayerRegistration(PlayerHealth player)
    {
        playerHealthManager = player;
        ReportDebug("Referencia de PlayerHealth obtenida por registro de evento.", 1);
    }

    public void OnRoomEntered()
    {
        isManipulationActive = false;
        activeManipulationName = "Ninguna";
        if (UIManager.Instance != null) UIManager.Instance.ClearManipulationText(); 

        if (roomsSinceLastManipulation < config.CooldownInRooms)
        {
            roomsSinceLastManipulation++;
            ReportDebug($"Cooldown del Diablo: {roomsSinceLastManipulation} / {config.CooldownInRooms} salas completadas.", 1);
        }
    }

    public void CheckManipulationConditions()
    {
        if (roomsSinceLastManipulation < config.CooldownInRooms)
        {
            return;
        }

        bool condition1 = CheckRelicAndHealthCondition();
        bool condition2 = CheckDamageCondition();
        bool condition3 = CheckPactCondition();
        bool condition4 = CheckCleanRoomsCondition();

        if (condition1 || condition2 || condition3 || condition4)
        {
            ApplyDevilManipulation();
        }
    }

    public void ResetCleanRoomsCounter()
    {
        cleanRoomsCounter = 0;
        ReportDebug("Contador de Salas Limpias reseteado.", 1);
    }

    public void SpawnDevilLarva(Vector3 position, float enemyBaseHealth)
    {
        float speedMult = Instance.EnemySpeedMultiplier;
        float damageMult = Instance.EnemyDamageMultiplier;
        Color levelColor = Instance.GetCurrentLevelColor();

        if (config == null)
        {
            Debug.LogError("DevilConfiguration no está asignado en el Inspector de DevilManipulationManager.");
            return;
        }
        GameObject larvaPrefab = config.EscurridizoPrefab;

        if (larvaPrefab != null)
        {
            GameObject larvaGO = Instantiate(larvaPrefab, position, Quaternion.identity);

            if (larvaGO.TryGetComponent<ResurrectedDevilLarva>(out var devilLarva))
            {
                devilLarva.Initialize(enemyBaseHealth, speedMult, damageMult, levelColor);
            }
        }
    }

    private bool CheckRelicAndHealthCondition()
    {
        if (playerHealthManager != null && playerHealthManager.CurrentHealthPercent >= 0.25f)
        {
            return UnityEngine.Random.value < 0.5f;
        }
        return false;
    }

    private bool CheckDamageCondition()
    {
        return UnityEngine.Random.value < 0.5f;
    }

    private bool CheckPactCondition()
    {
        return UnityEngine.Random.value < 0.5f;
    }

    private bool CheckCleanRoomsCondition()
    {
        cleanRoomsCounter++;
        return cleanRoomsCounter >= config.MaxCleanRoomsCondition;
    }

    private void ApplyDevilManipulation()
    {
        ResetCleanRoomsCounter();
        roomsSinceLastManipulation = 0;

        float roll = UnityEngine.Random.value;

        if (roll <= config.AuraProbability)
        {
            ActivateEnemyAuraManipulation();
        }
        else
        {
            ActivateDistortionManipulation();
        }
    }

    private void ActivateEnemyAuraManipulation()
    {
        var auras = Enum.GetValues(typeof(DevilAuraType))
                        .Cast<DevilAuraType>()
                        .Where(a => a != DevilAuraType.None)
                        .ToArray();

        DevilAuraType chosenAura = auras[UnityEngine.Random.Range(0, auras.Length)];

        ResurrectionLevel level = ResurrectionLevel.None;
        if (chosenAura == DevilAuraType.PartialResurrection)
        {
            level = (ResurrectionLevel)UnityEngine.Random.Range(1, 4);
        }

        OnAuraManipulationActivated?.Invoke(
            chosenAura,
            level,
            config.AuraEnemyCoveragePercent);

        ReportDebug($"Manipulación del Diablo: Aura Enemiga '{chosenAura}' (Nivel {level}) activada para el {config.AuraEnemyCoveragePercent * 100}% de enemigos.", 2);
    }

    private void ActivateDistortionManipulation()
    {
        if (availableDistortions.Count == 0)
        {
            availableDistortions = new List<DevilDistortionType>(allDistortionTypes);
            ReportDebug("Todas las Distorsiones han ocurrido. La lista se ha reiniciado.", 1);
        }

        DevilDistortionType chosenDistortion = ChooseDistortionByWeight();

        if (chosenDistortion != DevilDistortionType.None)
        {
            availableDistortions.Remove(chosenDistortion);
            OnDistortionActivated?.Invoke(chosenDistortion);
            ReportDebug($"Manipulación del Diablo: Distorsión '{chosenDistortion}' activada.", 3);
        }
    }

    private DevilDistortionType ChooseDistortionByWeight()
    {
        List<(DevilDistortionType type, float chance)> candidates = new List<(DevilDistortionType, float)>();
        float totalWeight = 0f;

        foreach (var type in availableDistortions)
        {
            float chance = GetDistortionChance(type);
            candidates.Add((type, chance));
            totalWeight += chance;
        }

        if (totalWeight <= 0) return DevilDistortionType.None;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var candidate in candidates)
        {
            cumulativeWeight += candidate.chance;
            if (roll < cumulativeWeight)
            {
                return candidate.type;
            }
        }

        return DevilDistortionType.None;
    }

    private float GetDistortionChance(DevilDistortionType type)
    {
        return type switch
        {
            DevilDistortionType.AbyssalConfusion => config.AbyssalConfusion_Chance,
            DevilDistortionType.FloorOfTheDamned => config.FloorOfTheDamned_Chance,
            DevilDistortionType.DeceptiveDarkness => config.DeceptiveDarkness_Chance,
            DevilDistortionType.SealedLuck => config.SealedLuck_Chance,
            DevilDistortionType.WitheredBloodthirst => config.WitheredBloodthirst_Chance,
            DevilDistortionType.InfernalJudgement => config.InfernalJudgement_Chance,
            _ => 0f
        };
    }

    public string GetCurrentManipulationStatus()
    {
        if (isManipulationActive)
        {
            return $"ESTA SIENDO MANIPULADO: {activeManipulationName}";
        }
        else
        {
            return "NO ESTA MANIPULADO";
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[DevilManager] {message}"); break;
            case 2: Debug.LogWarning($"[DevilManager] {message}"); break;
            case 3: Debug.LogError($"[DevilManager] {message}"); break;
        }
    }
}