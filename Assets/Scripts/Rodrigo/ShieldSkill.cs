using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona una habilidad que potencia las estadísticas del jugador mientras está activa.
/// La habilidad se activa y desactiva, pidiendo al PlayerStatsManager que aplique los modificadores.
/// </summary>
public class ShieldSkill : MonoBehaviour, PlayerControlls.IAbilitiesActions, IPlayerSpecialAbility
{
    #region Settings

    [Header("Configuration")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerShieldController playerShieldController;
    [SerializeField] private PlayerAudioController playerAudioController;

    [Header("Stamina System")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float baseStaminaDrainRate = 10f; 
    [SerializeField] private float staminaRechargeRate = 20f;
    [SerializeField] private bool requireFullStaminaToActivate = false;
    [SerializeField] private float minStaminaToActivate = 10f;

    [Header("Buffs por Etapa de Vida")]
    [SerializeField] private BuffSettings youngBuffs = new BuffSettings(1.2f, 1.1f, 1.2f, 2f);
    [SerializeField] private BuffSettings adultBuffs = new BuffSettings(1.1f, 1.2f, 1.1f, 2f);
    [SerializeField] private BuffSettings elderBuffs = new BuffSettings(1.1f, 1.12f, 1.1f, 1f);

    [Header("VFX")]
    [Tooltip("El objeto hijo que contiene el Renderer del personaje.")]
    [SerializeField] private GameObject visualModelObject;
    [Tooltip("Material cuando la habilidad está activa y tiene estamina.")]
    [SerializeField] private Material skillActiveMaterial;
    [Tooltip("Material cuando la habilidad sigue activa pero consume vida (sin estamina).")]
    [SerializeField] private Material skillExhaustedMaterial;

    [Header("Low Health Feedback")]
    [SerializeField] private GameObject warningMessagePrefab;
    [SerializeField] private string warningMessageText = "*Tos* *Tos*\n¡No puedo más!";
    [SerializeField] private Color warningMessageColor = Color.red;
    [SerializeField] private float warningMessageOffset = 2.5f;
    [SerializeField] private float warningMessageDuration = 2f;

    #endregion

    #region State

    public bool isSkillActive { get; private set; }
    private float currentStamina;
    private float healthDrainTimer;
    private bool isStaminaConsumptionPrevented = false;
    private const string SHIELD_SKILL_MODIFIER_KEY = "ShieldSkillBuff";
    private const float LOW_HEALTH_THRESHOLD = 10f;
    private float currentStaminaDrainRate;

    private PlayerControlls playerControls;
    private PlayerHealth playerHealth;
    private PlayerHealth.LifeStage lastKnownLifeStage;

    private Renderer modelRenderer;
    private Material storedBaseMaterial;

    public event System.Action OnAbilityActivated;
    public event System.Action OnAbilityDeactivated;

    public bool IsActive => isSkillActive;

    private bool inputBlocked = false; 
    private bool isForcedActive = false;
    private GameObject currentWarningMessage;

    private bool wasHealthTooLow = false;

    private bool hasStaminaBeenFullyDepleted = false;
    public static event System.Action<float, float> OnStaminaChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        statsManager = GetComponent<PlayerStatsManager>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();
        if (playerAudioController == null) playerAudioController = GetComponent<PlayerAudioController>();

        if (playerAudioController == null) Debug.Log("PlayerAudioController no se encuentra en el objeto.");

        currentStamina = maxStamina;

        if (statsManager == null)
        {
            Debug.LogError("PlayerStatsManager no está asignado en ShieldSkill. La habilidad no funcionará.", this);
            enabled = false;
            return;
        }

        if (visualModelObject != null)
        {
            modelRenderer = visualModelObject.GetComponent<Renderer>();
            if (modelRenderer != null)
            {
                storedBaseMaterial = modelRenderer.sharedMaterial;
            }
            else
            {
                Debug.LogWarning("El visualModelObject asignado no tiene un componente Renderer.", this);
            }
        }
        else
        {
            Debug.LogWarning("visualModelObject no asignado en ShieldSkill. No habrá feedback visual de materiales.", this);
        }

        playerControls = new PlayerControlls();
        playerControls.Abilities.SetCallbacks(this);
    }

    private void Start()
    {
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);

        UpdateStaminaConsumptionFromStats();
    }

    private void UpdateStaminaConsumptionFromStats()
    {
        if (statsManager == null)
        {
            currentStaminaDrainRate = baseStaminaDrainRate;
            return;
        }

        float staminaConsumptionMod = statsManager.GetStat(StatType.StaminaConsumption);

        if (staminaConsumptionMod <= 0f) staminaConsumptionMod = 1f;

        currentStaminaDrainRate = baseStaminaDrainRate * staminaConsumptionMod;

        Debug.Log($"[ShieldSkill] Consumo de stamina actualizado: Base={baseStaminaDrainRate}/s x Mod={staminaConsumptionMod} = {currentStaminaDrainRate}/s");
    }

    private void OnEnable()
    {
        PlayerHealth.OnLifeStageChanged += HandleLifeStageChanged;
        playerControls?.Abilities.Enable();

        if (statsManager != null)
        {
            PlayerStatsManager.OnStatChanged += HandleStaminaStatChanged;
        }
    }

    private void OnDisable()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;
        playerControls?.Abilities.Disable();
        RestoreOriginalMaterial();

        if (isSkillActive) DeactivateSkill();

        if (statsManager != null)
        {
            PlayerStatsManager.OnStatChanged -= HandleStaminaStatChanged;
        }
    }

    private void HandleStaminaStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.StaminaConsumption)
        {
            UpdateStaminaConsumptionFromStats();
        }
    }

    private void OnDestroy()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;
        playerControls?.Dispose();
        RestoreOriginalMaterial();

        if (isSkillActive) DeactivateSkill();

        if (currentWarningMessage != null)
        {
            Destroy(currentWarningMessage);
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
        if (!isForcedActive)
        {
            bool isHealthLowNow = playerHealth.CurrentHealth <= LOW_HEALTH_THRESHOLD;

            if (isHealthLowNow && !wasHealthTooLow)
            {
                if (isSkillActive)
                {
                    ShowLowHealthWarning();
                    DeactivateSkill();
                    Debug.Log("[ShieldSkill] Habilidad desactivada: salud demasiado baja.");
                }

                wasHealthTooLow = true;
            }
            else if (!isHealthLowNow && wasHealthTooLow)
            {
                wasHealthTooLow = false;
                Debug.Log("[ShieldSkill] Salud recuperada. La habilidad puede usarse nuevamente.");
            }
        }

        if (isSkillActive)
        {
            UpdateActiveSkill();
            UpdateMaterialState();
        }
        else
        {
            RechargeStamina();
        }
    }

    #endregion

    #region Core Logic

    public void OnActivateSkill(InputAction.CallbackContext context)
    {
        if (PauseController.IsGamePaused) return;

        if (!context.started) return;

        if (inputBlocked)
        {
            Debug.Log("[ShieldSkill] Input bloqueado durante tutorial.");
            return;
        }

        if (LOW_HEALTH_THRESHOLD >= playerHealth.CurrentHealth)
        {
            Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
            ShowLowHealthWarning();
            return;
        }

        ToggleSkill();
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        Debug.Log($"[ShieldSkill] Input {(blocked ? "BLOQUEADO" : "DESBLOQUEADO")}");
    }

    public void SetForcedActive(bool forced)
    {
        isForcedActive = forced;
        Debug.Log($"[ShieldSkill] Habilidad {(forced ? "FORZADA ACTIVA" : "LIBERADA")}");
    }

    public void ToggleSkillDirectly()
    {
        if (LOW_HEALTH_THRESHOLD >= playerHealth.CurrentHealth)
        {
            ShowLowHealthWarning();
            Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
            return;
        }

        if (!CanActivateSkill())
        {
            ShowNoStaminaWarning();
            return;
        }

        ToggleSkill();
    }

    public void DeactivateSkillPublic()
    {
        if (isForcedActive)
        {
            Debug.Log("[ShieldSkill] No se puede desactivar - está forzada activa");
            return;
        }

        if (isSkillActive) DeactivateSkill();
    }

    private void ToggleSkill()
    {
        if (isForcedActive && isSkillActive)
        {
            Debug.Log("[ShieldSkill] No se puede desactivar - está forzada activa");
            return;
        }

        if (isSkillActive) DeactivateSkill();
        else
        {
            if (!CanActivateSkill())
            {
                ShowNoStaminaWarning();
                return;
            }

            ActivateSkill();
        }
    }

    private void ActivateSkill()
    {
        isSkillActive = true;

        if (playerAudioController != null)
        {
            playerAudioController.PlayBerserkerAbility(true);
        }

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

        UpdateMaterialState();

        OnAbilityActivated?.Invoke();

        Debug.Log($"[HABILIDAD ACTIVADA] Etapa: {lastKnownLifeStage} " +
                  $"- Buffs: Velocidad de movimiento x{currentBuffs.MoveMultiplier}, " +
                  $"Daño de ataques x{currentBuffs.AttackDamageMultiplier}, " +
                  $"Velolicad de ataques x{currentBuffs.AttackSpeedMultiplier} " +
                  $"- Debuff: Cantidad de vida drenada +{currentBuffs.HealthDrainAmount}");
    }

    private void DeactivateSkill()
    {
        isSkillActive = false;

        if (playerAudioController != null)
        {
            playerAudioController.PlayBerserkerAbility(false);
        }

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

        OnAbilityDeactivated?.Invoke();

        Debug.Log("[HABILIDAD DESACTIVADA] Modificadores removidos. " +
                  $"Velocidad de movimiento: {beforeMoveValue} -> {afterMoveValue}, " +
                  $"Daño de ataque melee: {beforeMeleeDmgValue} -> {afterMeleeDmgValue}, " +
                  $"Daño de ataque con escudo: {beforeShieldDmgValue} -> {afterShieldDmgValue}, " +
                  $"Velocidad de ataque melee: {beforeMeleeSpeedValue} -> {afterMeleeSpeedValue}, " +
                  $"Velocidad de ataque con escudo: {beforeShieldSpeedValue} -> {afterShieldSpeedValue}");
    }

    public void DeactivateAbility()
    {
        if (isSkillActive)
        {
            DeactivateSkill();
        }
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
        if (currentStamina > 0)
        {
            ConsumeStamina();
        }
        else
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

    #endregion

    #region Stamina System

    /// <summary>
    /// Verifica si la habilidad puede activarse según las reglas de estamina.
    /// </summary>
    private bool CanActivateSkill()
    {
        if (requireFullStaminaToActivate)
        {
            return currentStamina >= maxStamina;
        }
        else
        {
            if (hasStaminaBeenFullyDepleted)
            {
                return currentStamina >= maxStamina;
            }
            else
            {
                return currentStamina >= minStaminaToActivate;
            }
        }
    }

    /// <summary>
    /// Consume estamina mientras la habilidad está activa.
    /// </summary>
    private void ConsumeStamina()
    {
        if (isStaminaConsumptionPrevented) return;

        UpdateStaminaConsumptionFromStats();

        float previousStamina = currentStamina;

        currentStamina -= currentStaminaDrainRate * Time.deltaTime;
        currentStamina = Mathf.Max(0f, currentStamina);

        if (previousStamina > 0f && currentStamina <= 0f)
        {
            hasStaminaBeenFullyDepleted = true;
            Debug.Log("[ShieldSkill] ¡Estamina completamente agotada! Requerirá recarga total para reactivar.");
        }

        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    /// <summary>
    /// Recarga la estamina cuando la habilidad está inactiva.
    /// </summary>
    private void RechargeStamina()
    {
        if (currentStamina < maxStamina)
        {
            float previousStamina = currentStamina;
            currentStamina += staminaRechargeRate * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina);

            if (hasStaminaBeenFullyDepleted && currentStamina >= maxStamina)
            {
                hasStaminaBeenFullyDepleted = false;
                Debug.Log("[ShieldSkill] Estamina completamente recuperada. Habilidad disponible nuevamente.");
            }

            // Notificar cambio de estamina
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    /// <summary>
    /// Obtiene el porcentaje actual de estamina (0-1).
    /// </summary>
    public float GetStaminaPercentage()
    {
        return maxStamina > 0 ? currentStamina / maxStamina : 0f;
    }

    /// <summary>
    /// Obtiene la estamina actual.
    /// </summary>
    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    /// <summary>
    /// Obtiene la estamina máxima.
    /// </summary>
    public float GetMaxStamina()
    {
        return maxStamina;
    }

    /// <summary>
    /// Recarga instantáneamente la estamina (útil para power-ups o eventos especiales).
    /// </summary>
    public void RechargeStaminaInstantly(float amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        Debug.Log($"[ShieldSkill] Estamina recargada instantáneamente: +{amount}");
    }

    #endregion

    #region PUBLIC_METHODS

    public void PreventStaminaConsumption()
    {
        isStaminaConsumptionPrevented = true;
    }

    public void AllowStaminaConsumption()
    {
        isStaminaConsumptionPrevented = false;
    }

    #endregion

    #region VFX Management

    /// <summary>
    /// Actualiza el material del modelo visual basado en el estado de la habilidad y la estamina.
    /// </summary>
    private void UpdateMaterialState()
    {
        if (modelRenderer == null) return;

        Material targetMaterial;

        if (isSkillActive)
        {
            if (currentStamina > 0)
            {
                targetMaterial = skillActiveMaterial;
            }
            else
            {
                targetMaterial = skillExhaustedMaterial;
            }
        }
        else
        {
            targetMaterial = storedBaseMaterial;
        }

        if (modelRenderer.sharedMaterial != targetMaterial && targetMaterial != null)
        {
            modelRenderer.material = targetMaterial;
        }
    }

    /// <summary>
    /// Restaura el material base guardado al inicio.
    /// </summary>
    private void RestoreOriginalMaterial()
    {
        if (modelRenderer != null && storedBaseMaterial != null)
        {
            modelRenderer.material = storedBaseMaterial;
        }
    }

    #endregion

    private void ShowNoStaminaWarning()
    {
        if (currentWarningMessage == null && warningMessagePrefab != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * warningMessageOffset;
            currentWarningMessage = Instantiate(warningMessagePrefab, spawnPosition, Quaternion.identity);
            WarningMessageFloater floater = currentWarningMessage.GetComponent<WarningMessageFloater>();
            if (floater != null)
            {
                floater.SetLifetime(warningMessageDuration);
                floater.SetColor(warningMessageColor);
                floater.SetText("¡No tengo suficiente energía!");
            }

            StartCoroutine(DestroyWarningMessageAfterDelay());
        }

        Debug.Log("[ShieldSkill] No hay suficiente estamina para activar la habilidad.");
    }

    #region Low Health Feedback

    /// <summary>
    /// Muestra el mensaje de advertencia cuando el jugador intenta usar la habilidad con salud baja.
    /// </summary>
    private void ShowLowHealthWarning()
    {
        // Reproducir sonido de advertencia
        if (playerAudioController != null)
        {
            playerAudioController.PlayBerserkerLowWarningAbility();
        }

        // Crear mensaje visual si no existe uno activo
        if (currentWarningMessage == null && warningMessagePrefab != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * warningMessageOffset;
            currentWarningMessage = Instantiate(warningMessagePrefab, spawnPosition, Quaternion.identity);

            // Configurar el mensaje
            WarningMessageFloater floater = currentWarningMessage.GetComponent<WarningMessageFloater>();
            if (floater != null)
            {
                floater.SetLifetime(warningMessageDuration);
                floater.SetColor(warningMessageColor);
                floater.SetText(warningMessageText);
            }

            // Destruir el mensaje después de la duración especificada
            StartCoroutine(DestroyWarningMessageAfterDelay());
        }

        Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
    }

    /// <summary>
    /// Destruye el mensaje de advertencia después del tiempo especificado.
    /// </summary>
    private IEnumerator DestroyWarningMessageAfterDelay()
    {
        yield return new WaitForSeconds(warningMessageDuration);

        if (currentWarningMessage != null)
        {
            Destroy(currentWarningMessage);
            currentWarningMessage = null;
        }
        else
        {
            yield return null;
        }
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
        public bool DisableShieldThrow; 

        public BuffSettings(float moveMultiplier, float attackDamageMultiplier, float attackSpeedMultiplier, float healthDrainAmount, bool disableShieldThrow = true)
        {
            MoveMultiplier = moveMultiplier;
            AttackDamageMultiplier = attackDamageMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            HealthDrainAmount = healthDrainAmount;
            DisableShieldThrow = disableShieldThrow;
        }
    }

    #endregion
}