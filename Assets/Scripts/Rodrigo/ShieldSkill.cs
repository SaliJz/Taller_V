using UnityEngine;

/// <summary>
/// Gestiona una habilidad que potencia las estadísticas del jugador mientras está activa.
/// La habilidad se activa y desactiva presionando una tecla (toggle).
/// </summary>
public class ShieldSkill : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [Tooltip("Referencia al gestor de estadísticas del jugador.")]
    [SerializeField] private PlayerStatsManager statsManager;

    [Header("Configuration")]
    [Tooltip("La tecla para activar/desactivar la habilidad.")]
    [SerializeField] private KeyCode skillKey = KeyCode.Q;

    [Header("Skill Settings")]
    [Header("Buffs por Etapa de Vida")]
    [SerializeField] private BuffSettings youngBuffs = new BuffSettings(1.2f, 1.1f);
    [SerializeField] private BuffSettings adultBuffs = new BuffSettings(1.1f, 1.2f);
    [SerializeField] private BuffSettings elderBuffs = new BuffSettings(1.15f, 1.15f);

    [HideInInspector] private PlayerMeleeAttack playerMeleeAttack;
    [HideInInspector] private PlayerShieldController playerShieldController;
    [HideInInspector] private PlayerHealth playerHealth;
    [HideInInspector] private PlayerMovement playerMovement;

    [SerializeField] private bool SkillActive;

    private bool isSkillActive;
    private float healthDrainAmount;
    private float healthDrainTimer;

    private float baseMoveSpeed;
    private int baseMeleeAttackDamage;
    private int baseShieldDamage;

    #endregion

    #region Logica

    private void Awake()
    {
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        if (statsManager != null) PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        if (statsManager != null) PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        if (isSkillActive)
        {
            DeactivateSkill();
        }
    }

    void Start()
    {
        CacheBaseStats();

        if (statsManager != null)
        {
            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
        }
        else
        {
            Debug.LogError("PlayerStatsManager no está asignado en el inspector.", this);
            healthDrainAmount = 2f;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(skillKey))
        {
            ToggleSkill();
        }

        if (isSkillActive)
        {
            UpdateActiveSkill();
        }
    }

    /// <summary>
    /// Activa o desactiva la habilidad dependiendo de su estado actual.
    /// </summary>
    private void ToggleSkill()
    {
        if (isSkillActive)
        {
            DeactivateSkill();
        }
        else
        {
            ActivateSkill();
        }
    }

    /// <summary>
    /// Activa la habilidad aplicando los buffs correspondientes.
    /// </summary>
    private void ActivateSkill()
    {
        isSkillActive = true;
        healthDrainTimer = 0f;

        BuffSettings currentBuffs = GetCurrentBuffs();
        ApplyBuffs(currentBuffs);

        Debug.Log($"[HABILIDAD ACTIVADA] - Buffs: Velocidad x{currentBuffs.MoveMultiplier}, Ataque x{currentBuffs.AttackMultiplier}");
    }

    private void DeactivateSkill()
    {
        isSkillActive = false;
        RestoreBaseStats();

        Debug.Log("[HABILIDAD DESACTIVADA] - Estadísticas restauradas a sus valores base.");
    }

    /// <summary>
    /// Contiene la lógica que se ejecuta cada frame mientras la habilidad está activa.
    /// </summary>
    private void UpdateActiveSkill()
    {
        // Drenaje de vida por segundo.
        if (healthDrainAmount > 0)
        {
            healthDrainTimer += Time.deltaTime;
            if (healthDrainTimer >= 1f)
            {
                playerHealth.TakeDamage(healthDrainAmount);
                healthDrainTimer %= 1f; // Resetea el timer conservando el exceso de tiempo.
                Debug.Log($"Vida drenada: {healthDrainAmount}. Vida actual: {playerHealth.CurrentHealth}");
            }
        }
    }

    #endregion

    #region Stat Management

    /// <summary>
    /// Almacena los valores originales de las estadísticas del jugador.
    /// </summary>
    private void CacheBaseStats()
    {
        baseMeleeAttackDamage = playerMeleeAttack.AttackDamage;
        baseShieldDamage = playerShieldController.ShieldDamage;
        baseMoveSpeed = playerMovement.MoveSpeed;
    }

    /// <summary>
    /// Restaura las estadísticas del jugador a sus valores originales.
    /// </summary>
    private void RestoreBaseStats()
    {
        playerMeleeAttack.AttackDamage = baseMeleeAttackDamage;
        playerShieldController.ShieldDamage = baseShieldDamage;
        playerMovement.MoveSpeed = baseMoveSpeed;
    }

    /// <summary>
    /// Aplica los multiplicadores de buff a las estadísticas del jugador.
    /// </summary>
    private void ApplyBuffs(BuffSettings buffs)
    {
        playerMeleeAttack.AttackDamage = Mathf.RoundToInt(baseMeleeAttackDamage * buffs.AttackMultiplier);
        playerShieldController.ShieldDamage = Mathf.RoundToInt(baseShieldDamage * buffs.AttackMultiplier);
        playerMovement.MoveSpeed = baseMoveSpeed * buffs.MoveMultiplier;
    }

    /// <summary>
    /// Devuelve los buffs correspondientes a la etapa de vida actual del jugador.
    /// </summary>
    private BuffSettings GetCurrentBuffs()
    {
        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                return youngBuffs;
            case PlayerHealth.LifeStage.Adult:
                return adultBuffs;
            case PlayerHealth.LifeStage.Elder:
                return elderBuffs;
            default:
                Debug.LogWarning("Etapa de vida no reconocida. No se aplicarán buffs.");
                return new BuffSettings(1f, 1f); // No buff
        }
    }

    /// <summary>
    /// Actualiza el valor de drenado de vida si cambia en el StatsManager.
    /// </summary>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.HealthDrainAmount)
        {
            healthDrainAmount = newValue;
        }
    }

    #endregion
}

/// <summary>
/// Estructura para almacenar los multiplicadores de buff de forma organizada.
/// </summary>
[System.Serializable]
public struct BuffSettings
{
    [Range(1f, 3f)] public float MoveMultiplier;
    [Range(1f, 3f)] public float AttackMultiplier;

    public BuffSettings(float moveMultiplier, float attackMultiplier)
    {
        MoveMultiplier = moveMultiplier;
        AttackMultiplier = attackMultiplier;
    }
}