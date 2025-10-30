using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona una habilidad que potencia las estad�sticas del jugador mientras est� activa.
/// La habilidad se activa y desactiva, pidiendo al PlayerStatsManager que aplique los modificadores.
/// </summary>
public class ShieldSkill : MonoBehaviour, PlayerControlls.IAbilitiesActions
{
    #region Settings

    [Header("Configuration")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerShieldController playerShieldController;

    [Header("Buffs por Etapa de Vida")]
    [SerializeField] private BuffSettings youngBuffs = new BuffSettings(1.2f, 1.1f, 1.2f, 2f);
    [SerializeField] private BuffSettings adultBuffs = new BuffSettings(1.1f, 1.2f, 1.1f, 2f);
    [SerializeField] private BuffSettings elderBuffs = new BuffSettings(1.1f, 1.12f, 1.1f, 1f);

    [Header("Visuals - material swap")]
    [Tooltip("Si no se asigna, intentara obtener el MeshRenderer del GameObject o hijos.")]
    [SerializeField] private MeshRenderer[] meshRenderers;
    [Tooltip("Material dorado que se aplicara durante la habilidad.")]
    [SerializeField] private Material goldenMaterial;

    [Header("VFX System")]
    [Tooltip("GameObject contenedor del VFX de la habilidad.")]
    [SerializeField] private GameObject vfxContainer;
    [Tooltip("Sistema de part�culas principal de la habilidad.")]
    [SerializeField] private ParticleSystem skillVFX;
    [SerializeField] private Color youngVFXColor = new Color(0.2f, 0.5f, 1f, 1f); // Azul
    [SerializeField] private Color adultVFXColor = new Color(1f, 0.2f, 0.2f, 1f); // Rojo
    [SerializeField] private Color elderVFXColor = new Color(1f, 0.84f, 0f, 1f); // Dorado

    [Header("Low Health Feedback")]
    [SerializeField] private GameObject warningMessagePrefab;
    [SerializeField] private string warningMessageText = "*Tos* *Tos*\n�No puedo m�s!";
    [SerializeField] private Color warningMessageColor = Color.red;
    [SerializeField] private float warningMessageOffset = 2.5f;
    [SerializeField] private float warningMessageDuration = 2f;
    [SerializeField] private AudioClip lowHealthWarningSound;
    [SerializeField] private float warningVolume = 0.7f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    #endregion

    #region State

    public bool isSkillActive { get; private set; }
    private float healthDrainTimer;
    private const string SHIELD_SKILL_MODIFIER_KEY = "ShieldSkillBuff";
    private const float LOW_HEALTH_THRESHOLD = 10f;

    private PlayerControlls playerControls;
    private PlayerHealth playerHealth;
    private Material[][] originalMaterials;
    private PlayerHealth.LifeStage lastKnownLifeStage;

    private bool inputBlocked = false; 
    private bool isForcedActive = false;
    private GameObject currentWarningMessage;

    private bool wasHealthTooLow = false;
    private bool lastVFXState = false;
    private bool isVFXExternallyPaused = false;
    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        statsManager = GetComponent<PlayerStatsManager>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();

        if (statsManager == null)
        {
            Debug.LogError("PlayerStatsManager no est� asignado en ShieldSkill. La habilidad no funcionar�.", this);
            enabled = false;
            return;
        }

        if (meshRenderers == null || meshRenderers.Length == 0)
        {
            meshRenderers = GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers == null || meshRenderers.Length == 0)
            {
                Debug.LogWarning("No se encontraron MeshRenderers en ShieldSkill. La habilidad no tendr� efecto visual.", this);
            }
        }

        if (vfxContainer == null)
        {
            Debug.LogWarning("VFX Container no asignado en ShieldSkill. Los efectos visuales no funcionar�n.", this);
        }

        if (skillVFX == null && vfxContainer != null)
        {
            skillVFX = vfxContainer.GetComponentInChildren<ParticleSystem>();
            if (skillVFX == null)
            {
                Debug.LogWarning("No se encontr� ParticleSystem en VFX Container.", this);
            }
        }

        if (vfxContainer != null)
        {
            vfxContainer.SetActive(false);
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
        DeactivateVFX();
        if (isSkillActive) DeactivateSkill();
    }

    private void OnDestroy()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;
        playerControls?.Dispose();
        RestoreOriginalMaterial();
        DeactivateVFX();
        if (isSkillActive) DeactivateSkill();

        if (currentWarningMessage != null)
        {
            Destroy(currentWarningMessage);
        }
    }

    /// <summary>
    /// Maneja el cambio de etapa de vida mientras la habilidad est� activa.
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

        UpdateVFXColor();

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
            UpdateVFXVisibility();
        }
    }

    #endregion

    #region Core Logic

    public void OnActivateSkill(InputAction.CallbackContext context)
    {
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

        ToggleSkill();
    }

    public void DeactivateSkillPublic()
    {
        if (isForcedActive)
        {
            Debug.Log("[ShieldSkill] No se puede desactivar - est� forzada activa");
            return;
        }

        if (isSkillActive) DeactivateSkill();
    }

    private void ToggleSkill()
    {
        if (isForcedActive && isSkillActive)
        {
            Debug.Log("[ShieldSkill] No se puede desactivar - est� forzada activa");
            return;
        }

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
        ActivateVFX();

        Debug.Log($"[HABILIDAD ACTIVADA] Etapa: {lastKnownLifeStage} " +
                  $"- Buffs: Velocidad de movimiento x{currentBuffs.MoveMultiplier}, " +
                  $"Da�o de ataques x{currentBuffs.AttackDamageMultiplier}, " +
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
        DeactivateVFX();

        float afterMoveValue = statsManager.GetStat(StatType.MoveSpeed);
        float afterMeleeDmgValue = statsManager.GetStat(StatType.MeleeAttackDamage);
        float afterShieldDmgValue = statsManager.GetStat(StatType.ShieldAttackDamage);
        float afterMeleeSpeedValue = statsManager.GetStat(StatType.MeleeAttackSpeed);
        float afterShieldSpeedValue = statsManager.GetStat(StatType.ShieldSpeed);

        Debug.Log("[HABILIDAD DESACTIVADA] Modificadores removidos. " +
                  $"Velocidad de movimiento: {beforeMoveValue} -> {afterMoveValue}, " +
                  $"Da�o de ataque melee: {beforeMeleeDmgValue} -> {afterMeleeDmgValue}, " +
                  $"Da�o de ataque con escudo: {beforeShieldDmgValue} -> {afterShieldDmgValue}, " +
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

    #endregion

    #region VFX Management

    /// <summary>
    /// Activa el VFX de la habilidad con el color correspondiente a la etapa de vida.
    /// </summary>
    private void ActivateVFX()
    {
        if (vfxContainer == null) return;

        isVFXExternallyPaused = false;

        vfxContainer.SetActive(true);

        if (skillVFX != null)
        {
            UpdateVFXColor();

            if (!skillVFX.isPlaying)
            {
                skillVFX.Play(true);
            }
        }

        Debug.Log($"[ShieldSkill] VFX activado para etapa {lastKnownLifeStage}");
    }

    /// <summary>
    /// Desactiva el VFX de la habilidad.
    /// </summary>
    private void DeactivateVFX()
    {
        if (vfxContainer == null) return;

        isVFXExternallyPaused = false;

        if (skillVFX != null && skillVFX.isPlaying)
        {
            skillVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (vfxContainer != null)
        {
            vfxContainer.SetActive(false);
        }

        Debug.Log("[ShieldSkill] VFX desactivado");
    }

    /// <summary>
    /// Actualiza la visibilidad del VFX seg�n el estado del jugador.
    /// Se ejecuta autom�ticamente en Update cuando la habilidad est� activa.
    /// </summary>
    private void UpdateVFXVisibility()
    {
        if (!isSkillActive) return;

        bool shouldShowVFX = true;

        // Ocultar VFX si est�n pausados externamente
        if (isVFXExternallyPaused)
        {
            shouldShowVFX = false;
        }

        // Ocultar VFX durante ataque melee
        else if(playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        {
            shouldShowVFX = false;
        }

        // Ocultar VFX durante lanzamiento de escudo
        else if(playerShieldController != null && !playerShieldController.CanThrowShield())
        {
            shouldShowVFX = false;
        }

        // Solo actualizar si el estado cambi�
        if (shouldShowVFX != lastVFXState)
        {
            if (vfxContainer != null)
            {
                vfxContainer.SetActive(shouldShowVFX);
            }

            if (skillVFX != null)
            {
                if (shouldShowVFX && !skillVFX.isPlaying)
                {
                    skillVFX.Play(true);
                }
                else if (!shouldShowVFX && skillVFX.isPlaying)
                {
                    skillVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            lastVFXState = shouldShowVFX;
            Debug.Log($"[ShieldSkill] VFX {(shouldShowVFX ? "mostrado" : "ocultado")} autom�ticamente");
        }
    }

    /// <summary>
    /// Actualiza el color del VFX seg�n la etapa de vida actual.
    /// </summary>
    private void UpdateVFXColor()
    {
        if (skillVFX == null) return;

        Color targetColor = GetVFXColorForLifeStage(lastKnownLifeStage);

        var main = skillVFX.main;
        main.startColor = targetColor;

        Debug.Log($"[ShieldSkill] Color de VFX actualizado a {targetColor} para etapa {lastKnownLifeStage}");
    }

    /// <summary>
    /// Obtiene el color del VFX seg�n la etapa de vida.
    /// </summary>
    private Color GetVFXColorForLifeStage(PlayerHealth.LifeStage stage)
    {
        switch (stage)
        {
            case PlayerHealth.LifeStage.Young:
                return youngVFXColor;
            case PlayerHealth.LifeStage.Adult:
                return adultVFXColor;
            case PlayerHealth.LifeStage.Elder:
                return elderVFXColor;
            default:
                return Color.red;
        }
    }

    /// <summary>
    /// M�todo p�blico para que otras clases desactiven temporalmente el VFX.
    /// </summary>
    public void SetVFXActive(bool active)
    {
        if (!isSkillActive) return;

        isVFXExternallyPaused = !active;

        Debug.Log($"[ShieldSkill] VFX {(active ? "reactivado" : "pausado")} externamente");
    }

    /// <summary>
    /// Obtiene el GameObject del VFX container para referencias externas.
    /// </summary>
    public GameObject GetVFXContainer()
    {
        return vfxContainer;
    }

    #endregion

    #region Low Health Feedback

    /// <summary>
    /// Muestra el mensaje de advertencia cuando el jugador intenta usar la habilidad con salud baja.
    /// </summary>
    private void ShowLowHealthWarning()
    {
        // Reproducir sonido de advertencia
        if (audioSource != null && lowHealthWarningSound != null)
        {
            audioSource.PlayOneShot(lowHealthWarningSound, warningVolume);
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

            // Destruir el mensaje despu�s de la duraci�n especificada
            StartCoroutine(DestroyWarningMessageAfterDelay());
        }

        Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
    }

    /// <summary>
    /// Destruye el mensaje de advertencia despu�s del tiempo especificado.
    /// </summary>
    private IEnumerator DestroyWarningMessageAfterDelay()
    {
        yield return new WaitForSeconds(warningMessageDuration);

        if (currentWarningMessage != null)
        {
            Destroy(currentWarningMessage);
            currentWarningMessage = null;
        }
    }

    #endregion

    #region Material Management

    /// <summary>
    /// Aplica el material dorado a todos los sub-materiales de cada MeshRenderer.
    /// Guarda los materiales originales si todav�a no se guardaron.
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