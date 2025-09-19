using UnityEngine;

/// <summary>
/// Clase que maneja el buffo de estadisticas al usar la habilidad del jugador.
/// </summary>
public class ShieldSkill : MonoBehaviour
{
    #region Variables
    [HideInInspector] private PlayerMeleeAttack PMA;
    [HideInInspector] private PlayerShieldController PSC;
    [HideInInspector] private PlayerHealth PH;
    [HideInInspector] private PlayerMovement PM;

    [SerializeField] private bool SkillActive;

    private float BaseAttackMelee;
    private float BaseAttackShield;
    private float BaseSpeed;

    private float Timer;
    #endregion
    #region Logica
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

    void Update()
    {
        Timer += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q))
            SkillActive = !SkillActive;

        ApplyBuff();
    }
    /// <summary>
    /// aplica el las estadisticas para el buffo de la habilidad
    /// </summary>
    private void ApplyBuff()
    {
        float moveBuff = 1f;
        float attackBuff = 1f;
        int lifeDrain = 0;

        switch (PH.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                moveBuff = 1.2f;
                attackBuff = 1.1f;
                lifeDrain = 2;
                break;
            case PlayerHealth.LifeStage.Adult:
                moveBuff = 1.1f;
                attackBuff = 1.2f;
                lifeDrain = 2;
                break;
            case PlayerHealth.LifeStage.Elder:
                moveBuff = 1.15f;
                attackBuff = 1.15f;
                lifeDrain = 1;
                break;
        }

        if (SkillActive)
        {
            if (Timer >= 1f)
            {
                if (PH != null) PH.TakeDamage(lifeDrain);
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