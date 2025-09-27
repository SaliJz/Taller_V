using UnityEngine;

/// <summary>
/// Clase que maneja el buffo de estadisticas al usar la habilidad del jugador.
/// </summary>
public class ShieldSkill : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;

    [Header("Skill Settings")]
    [SerializeField] private float skillDuration = 10f;

    [HideInInspector] private PlayerMeleeAttack PMA;
    [HideInInspector] private PlayerShieldController PSC;
    [HideInInspector] private PlayerHealth PH;
    [HideInInspector] private PlayerMovement PM;

    [SerializeField] private bool SkillActive;

    private float BaseAttackMelee;
    private float BaseAttackShield;
    private float BaseSpeed;
    private float healthDrainAmount;
    private float healthDrainTimer;
    private float skillDurationTimer;

    #endregion

    #region Logica

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    void Start()
    {
        PMA = GetComponent<PlayerMeleeAttack>();
        PSC = GetComponent<PlayerShieldController>();
        PH = GetComponent<PlayerHealth>();
        PM = GetComponent<PlayerMovement>();

        SkillActive = false;

        if (statsManager != null)
        {
            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
        }
        else
        {
            healthDrainAmount = 0f;
            Debug.LogError("PlayerStatsManager no asignado en ShieldSkill.");
        }

        skillDurationTimer = 0f;

        BaseAttackMelee = PMA.AttackDamage;
        BaseAttackShield = PSC.ShieldDamage;
        BaseSpeed = PM.MoveSpeed;
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    /// <param name="statType">Tipo de estadística que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estadística.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.HealthDrainAmount)
        {
            healthDrainAmount = newValue;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && !SkillActive)
        {
            ActivateSkill();
        }

        if (SkillActive)
        {
            if (PH != null && healthDrainAmount > 0)
            {
                healthDrainTimer += Time.deltaTime;

                if (healthDrainTimer >= 1f)
                {
                    PH.TakeDamage(healthDrainAmount);
                    healthDrainTimer = 0f;
                    Debug.Log($"[DRENADO] Vida reducida en {healthDrainAmount}. SkillActive: {SkillActive}");
                }
            }

            skillDurationTimer += Time.deltaTime;

            if (skillDurationTimer >= skillDuration)
            {
                DeactivateSkill();
            }
        }
        else
        {
            healthDrainTimer = 0f;
            skillDurationTimer = 0f;
        }
    }

    /// <summary>
    /// Activa la habilidad aplicando los buffs correspondientes.
    /// </summary>
    private void ActivateSkill()
    {
        SkillActive = true;
        skillDurationTimer = 0f;
        healthDrainTimer = 0f;

        Debug.Log($"[ACTIVAR] SkillActive cambiado a: {SkillActive}");

        float moveBuff = 1f;
        float attackBuff = 1f;

        float healthDrainMultiplier = 1f;

        switch (PH.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                moveBuff = 1.2f;
                attackBuff = 1.1f;
                healthDrainMultiplier = 1f; 
                Debug.Log("Joven: buff 20% velocidad, 10% fuerza");
                break;
            case PlayerHealth.LifeStage.Adult:
                moveBuff = 1.1f;
                attackBuff = 1.2f;
                healthDrainMultiplier = 1f; 
                Debug.Log("Adulto: buff 20% fuerza, 10% velocidad");
                break;
            case PlayerHealth.LifeStage.Elder:
                moveBuff = 1.15f;
                attackBuff = 1.15f;
                healthDrainMultiplier = 1f; 
                Debug.Log("Viejo: buff 15% fuerza y velocidad");
                break;
        }

        PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee * attackBuff);
        PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield * attackBuff);
        PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed * moveBuff);

        healthDrainAmount *= healthDrainMultiplier;

        Debug.Log($"[ShieldSkill ACTIVADO] " +
                  $"ATK M: {PMA.AttackDamage} (Base: {BaseAttackMelee}) | " +
                  $"ATK S: {PSC.ShieldDamage} (Base: {BaseAttackShield}) | " +
                  $"VEL: {PM.MoveSpeed} (Base: {BaseSpeed}) | " +
                  $"DRENADO: {healthDrainAmount}/s | " +
                  $"Duración: {skillDuration}s | SkillActive: {SkillActive}");
    }

    /// <summary>
    /// Desactiva la habilidad y restaura las estadísticas base.
    /// </summary>
    private void DeactivateSkill()
    {
        SkillActive = false;
        skillDurationTimer = 0f;
        healthDrainTimer = 0f;

        PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee);
        PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield);
        PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed);

        if (statsManager != null)
        {
            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
        }
        else
        {
            healthDrainAmount = 0f;
        }

        Debug.Log($"[ShieldSkill DESACTIVADO AUTOMÁTICAMENTE] " +
                  $"ATK M: {PMA.AttackDamage} | " +
                  $"ATK S: {PSC.ShieldDamage} | " +
                  $"VEL: {PM.MoveSpeed} | " +
                  $"DRENADO: {healthDrainAmount} | SkillActive: {SkillActive}");
    }
    #endregion
}