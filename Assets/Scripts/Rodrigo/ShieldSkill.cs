using UnityEngine;

/// <summary>
/// Clase que maneja el buffo de estadisticas al usar la habilidad del jugador.
/// </summary>
public class ShieldSkill : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;

    [HideInInspector] private PlayerMeleeAttack PMA;
    [HideInInspector] private PlayerShieldController PSC;
    [HideInInspector] private PlayerHealth PH;
    [HideInInspector] private PlayerMovement PM;

    [SerializeField] private bool SkillActive;

    private float BaseAttackMelee;
    private float BaseAttackShield;
    private float BaseSpeed;
    private float healthDrainAmount;
    private float Timer;

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
        Timer += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            SkillActive = !SkillActive;
            if (SkillActive)
            {
                if (PH.CurrentLifeStage == PlayerHealth.LifeStage.Young)
                {
                    Debug.Log("Joven :buffo 20% mas de velocidad, 10% mas de fuerza aumentados y redondeados");
                }
                else if (PH.CurrentLifeStage == PlayerHealth.LifeStage.Adult)
                {
                    Debug.Log("adulto :buffo 20% mas de fuerza, 10% mas de velocidad aumentados y redondeados");
                }
                else if (PH.CurrentLifeStage == PlayerHealth.LifeStage.Elder)
                {
                    Debug.Log("Viejo :buffo 15% mas de fuerza y velocidad aumentados y redondeados");
                }
            }
        }
            

        ApplyBuff();

    }
    /// <summary>
    /// aplica el las estadisticas para el buffo de la habilidad
    /// </summary>
    private void ApplyBuff()
    {
        float moveBuff = 1f;
        float attackBuff = 1f;
        int healthDrainBase = 0;

        switch (PH.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                moveBuff = 1.2f;
                attackBuff = 1.1f;
                healthDrainBase = 2;
                healthDrainAmount = healthDrainBase;
                break;
            case PlayerHealth.LifeStage.Adult:
                moveBuff = 1.1f;
                attackBuff = 1.2f;
                healthDrainBase = 2;
                healthDrainAmount = healthDrainBase;
                break;
            case PlayerHealth.LifeStage.Elder:
                moveBuff = 1.15f;
                attackBuff = 1.15f;
                healthDrainBase = 1;
                healthDrainAmount = healthDrainBase;
                break;
        }

        if (SkillActive)
        {
            if (Timer >= 1f)
            {
                if (PH != null) PH.TakeDamage(healthDrainAmount);
                Timer = 0f;
            }
            PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee * attackBuff);
            PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield * attackBuff);
            PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed * moveBuff);
        }
        else
        {
            PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee);
            PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield);
            PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed);

            Timer = 0f;
        }
    }
    #endregion
}