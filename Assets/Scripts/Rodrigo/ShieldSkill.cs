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
    [SerializeField] private BuffSettings youngBuffs = new BuffSettings(1.2f, 1.1f, 1.2f);
    [SerializeField] private BuffSettings adultBuffs = new BuffSettings(1.1f, 1.2f, 1.1f);
    [SerializeField] private BuffSettings elderBuffs = new BuffSettings(1.1f, 1.12f, 1.1f);

    [HideInInspector] private PlayerHealth playerHealth;

    [SerializeField] private bool SkillActive;

    private bool isSkillActive;
    private float healthDrainAmount;
    private float healthDrainTimer;

    private const string SHIELD_SKILL_MODIFIER_KEY = "ShieldSkillBuff";

    #endregion

    #region Logica

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (statsManager == null)
        {
            Debug.LogError("PlayerStatsManager no está asignado en ShieldSkill. La habilidad no funcionará.", this);
            enabled = false;
        }
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

    private void Start()
    {
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

        float baseMoveSpeed = statsManager.GetStat(StatType.MoveSpeed);
        float moveSpeedIncrease = baseMoveSpeed * (currentBuffs.MoveMultiplier - 1.0f);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move", StatType.MoveSpeed, moveSpeedIncrease);

        float baseMeleeDamage = statsManager.GetStat(StatType.MeleeAttackDamage);
        float meleeDamageIncrease = baseMeleeDamage * (currentBuffs.AttackDmgMultiplier - 1.0f);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg", StatType.MeleeAttackDamage, meleeDamageIncrease);

        float baseShieldDamage = statsManager.GetStat(StatType.ShieldAttackDamage);
        float shieldDamageIncrease = baseShieldDamage * (currentBuffs.AttackDmgMultiplier - 1.0f);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg", StatType.ShieldAttackDamage, shieldDamageIncrease);

        float baseMeleeSpeed = statsManager.GetStat(StatType.MeleeAttackSpeed);
        float meleeSpeedIncrease = baseMeleeSpeed * (currentBuffs.AttackSpeedMultiplier - 1.0f);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed", StatType.MeleeAttackSpeed, meleeSpeedIncrease);

        float baseShieldSpeed = statsManager.GetStat(StatType.ShieldSpeed);
        float shieldSpeedIncrease = baseShieldSpeed * (currentBuffs.AttackSpeedMultiplier - 1.0f);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed", StatType.ShieldSpeed, shieldSpeedIncrease);

        Debug.Log($"[HABILIDAD ACTIVADA] - Buffs: Velocidad de movimiento x{currentBuffs.MoveMultiplier}, " +
                  $"Daño de ataque x{currentBuffs.AttackDmgMultiplier}, " +
                  $"Velocidad de ataque x{currentBuffs.AttackSpeedMultiplier}");
    }

    private void DeactivateSkill()
    {
        isSkillActive = false;

        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move");

        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg");

        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed");

        Debug.Log("[HABILIDAD DESACTIVADA] - Estadísticas restauradas a sus valores base.");
    }

    /// <summary>
    /// Contiene la lógica que se ejecuta cada frame mientras la habilidad está activa.
    /// </summary>
    private void UpdateActiveSkill()
    {
        float healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
        if (healthDrainAmount > 0)
        {
            healthDrainTimer += Time.deltaTime;
            if (healthDrainTimer >= 1f)
            {
                playerHealth.TakeDamage(healthDrainAmount);
                healthDrainTimer %= 1f;
                Debug.Log($"Vida drenada: {healthDrainAmount}. Vida actual: {playerHealth.CurrentHealth}");
            }
        }
    }

    #endregion

    #region Stat Management

    /// <summary>
    /// Devuelve los buffs correspondientes a la etapa de vida actual del jugador.
    /// </summary>
    private BuffSettings GetCurrentBuffs()
    {
        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young: return youngBuffs;
            case PlayerHealth.LifeStage.Adult: return adultBuffs;
            case PlayerHealth.LifeStage.Elder: return elderBuffs;
            default: return new BuffSettings(1f, 1f, 1f);
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
    [Range(1f, 3f)] public float AttackDmgMultiplier;
    [Range(1f, 3f)] public float AttackSpeedMultiplier;

    public BuffSettings(float moveMultiplier, float attackDmgMultiplier, float attackSpeedMultiplier)
    {
        MoveMultiplier = moveMultiplier;
        AttackDmgMultiplier = attackDmgMultiplier;
        AttackSpeedMultiplier = attackSpeedMultiplier;
    }
}