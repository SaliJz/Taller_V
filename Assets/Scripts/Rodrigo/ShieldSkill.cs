using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona una habilidad que potencia las estadísticas del jugador mientras está activa.
/// La habilidad se activa y desactiva, pidiendo al PlayerStatsManager que aplique los modificadores.
/// </summary>
public class ShieldSkill : MonoBehaviour, PlayerControlls.IAbilitiesActions
{
    #region Settings

    [Header("Configuration")]
    [SerializeField] private PlayerStatsManager statsManager;

    [Header("Buffs por Etapa de Vida")]
    [SerializeField] private BuffSettings youngBuffs = new BuffSettings(1.2f, 1.1f, 1.2f, 2f);
    [SerializeField] private BuffSettings adultBuffs = new BuffSettings(1.1f, 1.2f, 1.1f, 2f);
    [SerializeField] private BuffSettings elderBuffs = new BuffSettings(1.1f, 1.12f, 1.1f, 1f);

    [Header("Visuals - material swap")]
    [Tooltip("Si no se asigna, intentara obtener el MeshRenderer del GameObject o hijos.")]
    [SerializeField] private MeshRenderer[] meshRenderers;
    [Tooltip("Material dorado que se aplicara durante la habilidad.")]
    [SerializeField] private Material goldenMaterial;

    #endregion

    #region State

    private bool isSkillActive;
    private float healthDrainTimer;
    private const string SHIELD_SKILL_MODIFIER_KEY = "ShieldSkillBuff";

    private PlayerControlls playerControls;
    private PlayerHealth playerHealth;
    private Material[][] originalMaterials;
    private PlayerHealth.LifeStage lastKnownLifeStage;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        statsManager = GetComponent<PlayerStatsManager>();

        if (statsManager == null)
        {
            Debug.LogError("PlayerStatsManager no está asignado en ShieldSkill. La habilidad no funcionará.", this);
            enabled = false;
            return;
        }

        if (meshRenderers == null || meshRenderers.Length == 0)
        {
            meshRenderers = GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers == null || meshRenderers.Length == 0)
            {
                Debug.LogWarning("No se encontraron MeshRenderers en ShieldSkill. La habilidad no tendrá efecto visual.", this);
            }
        }

        playerControls = new PlayerControlls();
        playerControls.Abilities.SetCallbacks(this);
    }

    private void OnEnable()
    {
        PlayerHealth.OnLifeStageChanged += HandleLifeStageChanged;
        playerControls?.Abilities.Enable(); 
    }

    private void OnDisable()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;
        playerControls?.Abilities.Disable(); 

        RestoreOriginalMaterial();

        if (isSkillActive)
        {
            DeactivateSkill();
        }
    }

    private void OnDestroy()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;

        playerControls?.Dispose();

        RestoreOriginalMaterial();

        if (isSkillActive)
        {
            DeactivateSkill();
        }
    }

    /// <summary>
    /// Maneja el cambio de etapa de vida mientras la habilidad está activa.
    /// </summary>
    private void HandleLifeStageChanged(PlayerHealth.LifeStage newStage)
    {
        if (!isSkillActive)
        {
            lastKnownLifeStage = newStage;
            return;
        }

        Debug.Log($"[ShieldSkill] Cambio de etapa detectado durante habilidad: {lastKnownLifeStage} -> {newStage}");

        lastKnownLifeStage = newStage;

        StartCoroutine(ReapplySkillModifiersNextFrame());
    }

    private void Update()
    {
        if (10 >= playerHealth.CurrentHealth)
        {
            if (isSkillActive) DeactivateSkill();
            Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
            return;
        }

        if (isSkillActive)
        {
            UpdateActiveSkill();
        }
    }

    #endregion

    #region Core Logic

    public void OnActivateSkill(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (10 >= playerHealth.CurrentHealth)
        {
            Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
            return;
        }

        ToggleSkill();
    }

    private void ToggleSkill()
    {
        if (isSkillActive) DeactivateSkill();
        else ActivateSkill();
    }

    private void ActivateSkill()
    {
        isSkillActive = true;
        healthDrainTimer = 0f;
        lastKnownLifeStage = playerHealth.CurrentLifeStage;

        BuffSettings currentBuffs = GetCurrentBuffs();

        RemoveHealthDrainModifier();

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move", StatType.MoveSpeed, currentBuffs.MoveMultiplier - 1.0f, true);

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg", StatType.MeleeAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg", StatType.ShieldAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed", StatType.MeleeAttackSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed", StatType.ShieldSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "HealthDrain", StatType.HealthDrainAmount, currentBuffs.HealthDrainAmount);

        ApplyGoldenMaterial();

        Debug.Log($"[HABILIDAD ACTIVADA] Etapa: {lastKnownLifeStage} " +
                  $"- Buffs: Velocidad de movimiento x{currentBuffs.MoveMultiplier}, " +
                  $"Daño de ataques x{currentBuffs.AttackDamageMultiplier}, " +
                  $"Velolicad de ataques x{currentBuffs.AttackSpeedMultiplier} " +
                  $"- Debuff: Cantidad de vida drenada +{currentBuffs.HealthDrainAmount}");
    }

    private void DeactivateSkill()
    {
        isSkillActive = false;

        float beforeMoveValue = statsManager.GetStat(StatType.MoveSpeed);

        float beforeMeleeDmgValue = statsManager.GetStat(StatType.MeleeAttackDamage);
        float beforeShieldDmgValue = statsManager.GetStat(StatType.ShieldAttackDamage);

        float beforeMeleeSpeedValue = statsManager.GetStat(StatType.MeleeAttackSpeed);
        float beforeShieldSpeedValue = statsManager.GetStat(StatType.ShieldSpeed);

        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move");

        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg");

        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed");

        RemoveHealthDrainModifier();

        RestoreOriginalMaterial();

        float afterMoveValue = statsManager.GetStat(StatType.MoveSpeed);

        float afterMeleeDmgValue = statsManager.GetStat(StatType.MeleeAttackDamage);
        float afterShieldDmgValue = statsManager.GetStat(StatType.ShieldAttackDamage);

        float afterMeleeSpeedValue = statsManager.GetStat(StatType.MeleeAttackSpeed);
        float afterShieldSpeedValue = statsManager.GetStat(StatType.ShieldSpeed);

        Debug.Log("[HABILIDAD DESACTIVADA] Modificadores removidos. " +
                  $"Velocidad de movimiento: {beforeMoveValue} -> {afterMoveValue}, " +
                  $"Daño de ataque melee: {beforeMeleeDmgValue} -> {afterMeleeDmgValue}, " +
                  $"Daño de ataque con escudo: {beforeShieldDmgValue} -> {afterShieldDmgValue}, " +
                  $"Velocidad de ataque melee: {beforeMeleeSpeedValue} -> {afterMeleeSpeedValue}, " +
                  $"Velocidad de ataque con escudo: {beforeShieldSpeedValue} -> {afterShieldSpeedValue}");
    }

    private IEnumerator ReapplySkillModifiersNextFrame()
    {
        yield return null;

        BuffSettings currentBuffs = GetCurrentBuffs();

        RemoveHealthDrainModifier();

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move", StatType.MoveSpeed, currentBuffs.MoveMultiplier - 1.0f, true);

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg", StatType.MeleeAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg", StatType.ShieldAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed", StatType.MeleeAttackSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed", StatType.ShieldSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);

        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "HealthDrain", StatType.HealthDrainAmount, currentBuffs.HealthDrainAmount);

        Debug.Log($"[ShieldSkill] Modificadores reaplicados para etapa {lastKnownLifeStage}");
    }

    private void UpdateActiveSkill()
    {
        float currentHealthDrain = statsManager.GetStat(StatType.HealthDrainAmount);

        if (currentHealthDrain > 0)
        {
            healthDrainTimer += Time.deltaTime;
            if (healthDrainTimer >= 1f)
            {
                playerHealth.TakeDamage(currentHealthDrain, true);
                healthDrainTimer %= 1f; 
            }
        }
    }

    /// <summary>
    /// Remueve el modificador de HealthDrainAmount.
    /// </summary>
    private void RemoveHealthDrainModifier()
    {
        float beforeValue = statsManager.GetStat(StatType.HealthDrainAmount);
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "HealthDrain");
        float afterValue = statsManager.GetStat(StatType.HealthDrainAmount);

        Debug.Log($"[ShieldSkill] HealthDrainAmount removido: {beforeValue} -> {afterValue}");
    }

    /// <summary>
    /// Aplica el material dorado a todos los sub-materiales de cada MeshRenderer.
    /// Guarda los materiales originales si todavía no se guardaron.
    /// </summary>
    private void ApplyGoldenMaterial()
    {
        if (meshRenderers == null || meshRenderers.Length == 0 || goldenMaterial == null) return;

        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            originalMaterials = new Material[meshRenderers.Length][];
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var mats = meshRenderers[i].materials;
                originalMaterials[i] = new Material[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    originalMaterials[i][j] = mats[j];
                }
            }
        }

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            var mats = meshRenderers[i].materials;
            Material[] goldMats = new Material[mats.Length];
            for (int j = 0; j < goldMats.Length; j++)
            {
                goldMats[j] = goldenMaterial;
            }
            meshRenderers[i].materials = goldMats;
        }
    }

    /// <summary>
    /// Restaura los materiales originales si existen.
    /// </summary>
    private void RestoreOriginalMaterial()
    {
        if (meshRenderers == null || originalMaterials == null || originalMaterials.Length == 0) return;

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (originalMaterials[i] != null)
            {
                meshRenderers[i].materials = originalMaterials[i];
            }
        }
        originalMaterials = null;
    }

    #endregion

    #region Helpers

    private BuffSettings GetCurrentBuffs()
    {
        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young: return youngBuffs;
            case PlayerHealth.LifeStage.Adult: return adultBuffs;
            case PlayerHealth.LifeStage.Elder: return elderBuffs;
            default: return new BuffSettings(1f, 1f, 1f, 1f);
        }
    }

    [System.Serializable]
    public struct BuffSettings
    {
        [Range(1f, 3f)] public float MoveMultiplier;
        [Range(1f, 3f)] public float AttackDamageMultiplier;
        [Range(1f, 3f)] public float AttackSpeedMultiplier;
        [Range(1f, 3f)] public float HealthDrainAmount;

        public BuffSettings(float moveMultiplier, float attackDamageMultiplier, float attackSpeedMultiplier, float healthDrainAmount)
        {
            MoveMultiplier = moveMultiplier;
            AttackDamageMultiplier = attackDamageMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            HealthDrainAmount = healthDrainAmount;
        }
    }

    #endregion
}