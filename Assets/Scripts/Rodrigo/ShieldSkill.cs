using UnityEngine;

/// <summary>
/// Clase que maneja el buffo de estadisticas al usar la habilidad del jugador,
/// ahora también cambia el material del MeshRenderer a dorado mientras el buff está activo
/// y lo restaura al desactivarse.
/// Además: aplica/remueve modificadores nombrados en PlayerStatsManager para que la UI (TextMeshPro) se actualice.
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

    [Header("Visuals - material swap")]
    [Tooltip("Si no se asigna, intentará obtener el MeshRenderer del GameObject o hijos.")]
    [SerializeField] private MeshRenderer meshRenderer;
    [Tooltip("Material dorado que se aplicará durante la habilidad.")]
    [SerializeField] private Material goldenMaterial;
    // Guardamos los materiales originales para restaurarlos exactamente como estaban
    private Material[] originalMaterials;

    // Claves para los modificadores nombrados (usadas en PlayerStatsManager)
    private const string KEY_ATK_MELEE = "ShieldSkill_ATK_M";
    private const string KEY_ATK_SHIELD = "ShieldSkill_ATK_S";
    private const string KEY_MOVE_SPEED = "ShieldSkill_MOVE";
    private const string KEY_HEALTH_DRAIN = "ShieldSkill_HEALTH_DRAIN";

    #endregion

    #region Logica

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        // Restaurar material si por alguna razón quedan cambiados al deshabilitar el componente
        RestoreOriginalMaterial();

        // Asegurarnos de remover cualquier modificador que pudiera quedar
        if (statsManager != null)
        {
            statsManager.RemoveNamedModifier(KEY_ATK_MELEE);
            statsManager.RemoveNamedModifier(KEY_ATK_SHIELD);
            statsManager.RemoveNamedModifier(KEY_MOVE_SPEED);
            statsManager.RemoveNamedModifier(KEY_HEALTH_DRAIN);
        }
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

        // Guardar valores base
        if (PMA != null) BaseAttackMelee = PMA.AttackDamage;
        if (PSC != null) BaseAttackShield = PSC.ShieldDamage;
        if (PM != null) BaseSpeed = PM.MoveSpeed;

        // Si no se asignó el MeshRenderer en el inspector, intentamos buscarlo en el GameObject o hijos
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        if (meshRenderer == null)
        {
            Debug.LogWarning("MeshRenderer no encontrado en ShieldSkill. No se hará swap de materiales.");
        }

        if (goldenMaterial == null)
        {
            Debug.LogWarning("goldenMaterial no asignado en ShieldSkill. No se hará swap de materiales.");
        }
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
        // Toggle con Q: si está activo, lo desactiva; si no, lo activa.
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (SkillActive)
            {
                DeactivateSkill();
            }
            else
            {
                ActivateSkill();
            }
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
    /// Ahora además aplica modificadores nombrados en PlayerStatsManager para actualizar la UI.
    /// </summary>
    private void ActivateSkill()
    {
        // Si ya está activo, no hacer nada (seguridad)
        if (SkillActive) return;

        SkillActive = true;
        skillDurationTimer = 0f;
        healthDrainTimer = 0f;

        Debug.Log($"[ACTIVAR] SkillActive cambiado a: {SkillActive}");

        float moveBuff = 1f;
        float attackBuff = 1f;

        float healthDrainMultiplier = 1f;

        if (PH == null)
        {
            Debug.LogWarning("PlayerHealth no encontrado en ShieldSkill. Se asumirá estado Adult por defecto.");
        }

        switch (PH != null ? PH.CurrentLifeStage : PlayerHealth.LifeStage.Adult)
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

        // --- Aplicar cambios a componentes para la jugabilidad ---
        if (PMA != null) PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee * attackBuff);
        if (PSC != null) PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield * attackBuff);
        if (PM != null) PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed * moveBuff);

        // --- Sincronizar con PlayerStatsManager (para UI) mediante modificadores nombrados ---
        if (statsManager != null)
        {
            // Melee Attack Damage
            float currentMelee = statsManager.GetStat(StatType.MeleeAttackDamage);
            float newMelee = Mathf.RoundToInt(BaseAttackMelee * attackBuff);
            float deltaMelee = newMelee - currentMelee;
            statsManager.ApplyNamedModifier(KEY_ATK_MELEE, StatType.MeleeAttackDamage, deltaMelee);

            // Shield Attack Damage
            float currentShieldAtk = statsManager.GetStat(StatType.ShieldAttackDamage);
            float newShieldAtk = Mathf.RoundToInt(BaseAttackShield * attackBuff);
            float deltaShield = newShieldAtk - currentShieldAtk;
            statsManager.ApplyNamedModifier(KEY_ATK_SHIELD, StatType.ShieldAttackDamage, deltaShield);

            // Move Speed
            float currentMove = statsManager.GetStat(StatType.MoveSpeed);
            float newMove = Mathf.RoundToInt(BaseSpeed * moveBuff);
            float deltaMove = newMove - currentMove;
            statsManager.ApplyNamedModifier(KEY_MOVE_SPEED, StatType.MoveSpeed, deltaMove);

            // Health Drain (si aplica multiplicador)
            float currentDrain = statsManager.GetStat(StatType.HealthDrainAmount);
            float newDrain = currentDrain * healthDrainMultiplier;
            float deltaDrain = newDrain - currentDrain;
            statsManager.ApplyNamedModifier(KEY_HEALTH_DRAIN, StatType.HealthDrainAmount, deltaDrain);

            // Actualizar la copia local también (por si el PlayerStatsManager cambió valores)
            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
        }
        else
        {
            // Si no hay statsManager, aplicamos solo la copia local
            healthDrainAmount *= healthDrainMultiplier;
        }

        // Cambiar material a dorado (si se dispone)
        ApplyGoldenMaterial();

        Debug.Log($"[ShieldSkill ACTIVADO] " +
                  $"ATK M: {(PMA != null ? PMA.AttackDamage.ToString() : "N/A")} (Base: {BaseAttackMelee}) | " +
                  $"ATK S: {(PSC != null ? PSC.ShieldDamage.ToString() : "N/A")} (Base: {BaseAttackShield}) | " +
                  $"VEL: {(PM != null ? PM.MoveSpeed.ToString() : "N/A")} (Base: {BaseSpeed}) | " +
                  $"DRENADO: {healthDrainAmount}/s | " +
                  $"Duración: {skillDuration}s | SkillActive: {SkillActive}");
    }

    /// <summary>
    /// Desactiva la habilidad y restaura las estadísticas base.
    /// Ahora además remueve los modificadores nombrados en PlayerStatsManager para actualizar la UI.
    /// </summary>
    private void DeactivateSkill()
    {
        // Si ya está desactivado, no hacer nada (seguridad)
        if (!SkillActive) return;

        SkillActive = false;
        skillDurationTimer = 0f;
        healthDrainTimer = 0f;

        if (PMA != null) PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee);
        if (PSC != null) PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield);
        if (PM != null) PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed);

        if (statsManager != null)
        {
            // Remover modificadores nombrados para restaurar los valores en PlayerStatsManager (y disparar OnStatChanged)
            statsManager.RemoveNamedModifier(KEY_ATK_MELEE);
            statsManager.RemoveNamedModifier(KEY_ATK_SHIELD);
            statsManager.RemoveNamedModifier(KEY_MOVE_SPEED);
            statsManager.RemoveNamedModifier(KEY_HEALTH_DRAIN);

            // Actualizar copia local del drenado según el stat manager
            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
        }
        else
        {
            healthDrainAmount = 0f;
        }

        // Restaurar material original
        RestoreOriginalMaterial();

        Debug.Log($"[ShieldSkill DESACTIVADO] " +
                  $"ATK M: {(PMA != null ? PMA.AttackDamage.ToString() : "N/A")} | " +
                  $"ATK S: {(PSC != null ? PSC.ShieldDamage.ToString() : "N/A")} | " +
                  $"VEL: {(PM != null ? PM.MoveSpeed.ToString() : "N/A")} | " +
                  $"DRENADO: {healthDrainAmount} | SkillActive: {SkillActive}");
    }

    /// <summary>
    /// Aplica el material dorado a todos los sub-materiales del MeshRenderer.
    /// Guarda los materiales originales si todavía no se guardaron.
    /// </summary>
    private void ApplyGoldenMaterial()
    {
        if (meshRenderer == null || goldenMaterial == null) return;

        // Si no hemos guardado los originales, guardarlos ahora
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            // Clonar el array para evitar referencias compartidas
            var mats = meshRenderer.materials;
            originalMaterials = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) originalMaterials[i] = mats[i];
        }

        // Crear array con el material dorado repetido la misma cantidad de sub-materiales
        Material[] goldMats = new Material[originalMaterials.Length];
        for (int i = 0; i < goldMats.Length; i++)
        {
            goldMats[i] = goldenMaterial;
        }

        meshRenderer.materials = goldMats;
    }

    /// <summary>
    /// Restaura los materiales originales si existen.
    /// </summary>
    private void RestoreOriginalMaterial()
    {
        if (meshRenderer == null) return;
        if (originalMaterials == null || originalMaterials.Length == 0) return;

        meshRenderer.materials = originalMaterials;
        originalMaterials = null;
    }

    #endregion
}


//using UnityEngine;

///// <summary>
///// Clase que maneja el buffo de estadisticas al usar la habilidad del jugador,
///// ahora también cambia el material del MeshRenderer a dorado mientras el buff está activo
///// y lo restaura al desactivarse.
///// Además: aplica/remueve modificadores nombrados en PlayerStatsManager para que la UI (TextMeshPro) se actualice.
///// </summary>
//public class ShieldSkill : MonoBehaviour
//{
//    #region Variables

//    [Header("References")]
//    [SerializeField] private PlayerStatsManager statsManager;

//    [Header("Skill Settings")]
//    [SerializeField] private float skillDuration = 10f;

//    [HideInInspector] private PlayerMeleeAttack PMA;
//    [HideInInspector] private PlayerShieldController PSC;
//    [HideInInspector] private PlayerHealth PH;
//    [HideInInspector] private PlayerMovement PM;

//    [SerializeField] private bool SkillActive;

//    private float BaseAttackMelee;
//    private float BaseAttackShield;
//    private float BaseSpeed;
//    private float healthDrainAmount;
//    private float healthDrainTimer;
//    private float skillDurationTimer;

//    [Header("Visuals - material swap")]
//    [Tooltip("Si no se asigna, intentará obtener el MeshRenderer del GameObject o hijos.")]
//    [SerializeField] private MeshRenderer meshRenderer;
//    [Tooltip("Material dorado que se aplicará durante la habilidad.")]
//    [SerializeField] private Material goldenMaterial;
//    // Guardamos los materiales originales para restaurarlos exactamente como estaban
//    private Material[] originalMaterials;

//    // Claves para los modificadores nombrados (usadas en PlayerStatsManager)
//    private const string KEY_ATK_MELEE = "ShieldSkill_ATK_M";
//    private const string KEY_ATK_SHIELD = "ShieldSkill_ATK_S";
//    private const string KEY_MOVE_SPEED = "ShieldSkill_MOVE";
//    private const string KEY_HEALTH_DRAIN = "ShieldSkill_HEALTH_DRAIN";

//    #endregion

//    #region Logica

//    private void OnEnable()
//    {
//        PlayerStatsManager.OnStatChanged += HandleStatChanged;
//    }

//    private void OnDisable()
//    {
//        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
//        // Restaurar material si por alguna razón quedan cambiados al deshabilitar el componente
//        RestoreOriginalMaterial();

//        // Asegurarnos de remover cualquier modificador que pudiera quedar
//        if (statsManager != null)
//        {
//            statsManager.RemoveNamedModifier(KEY_ATK_MELEE);
//            statsManager.RemoveNamedModifier(KEY_ATK_SHIELD);
//            statsManager.RemoveNamedModifier(KEY_MOVE_SPEED);
//            statsManager.RemoveNamedModifier(KEY_HEALTH_DRAIN);
//        }
//    }

//    void Start()
//    {
//        PMA = GetComponent<PlayerMeleeAttack>();
//        PSC = GetComponent<PlayerShieldController>();
//        PH = GetComponent<PlayerHealth>();
//        PM = GetComponent<PlayerMovement>();

//        SkillActive = false;

//        if (statsManager != null)
//        {
//            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
//        }
//        else
//        {
//            healthDrainAmount = 0f;
//            Debug.LogError("PlayerStatsManager no asignado en ShieldSkill.");
//        }

//        skillDurationTimer = 0f;

//        // Guardar valores base
//        if (PMA != null) BaseAttackMelee = PMA.AttackDamage;
//        if (PSC != null) BaseAttackShield = PSC.ShieldDamage;
//        if (PM != null) BaseSpeed = PM.MoveSpeed;

//        // Si no se asignó el MeshRenderer en el inspector, intentamos buscarlo en el GameObject o hijos
//        if (meshRenderer == null)
//        {
//            meshRenderer = GetComponent<MeshRenderer>();
//            if (meshRenderer == null)
//            {
//                meshRenderer = GetComponentInChildren<MeshRenderer>();
//            }
//        }

//        if (meshRenderer == null)
//        {
//            Debug.LogWarning("MeshRenderer no encontrado en ShieldSkill. No se hará swap de materiales.");
//        }

//        if (goldenMaterial == null)
//        {
//            Debug.LogWarning("goldenMaterial no asignado en ShieldSkill. No se hará swap de materiales.");
//        }
//    }

//    /// <summary>
//    /// Maneja los cambios de stats.
//    /// </summary>
//    /// <param name="statType">Tipo de estadística que ha cambiado.</param>
//    /// <param name="newValue">Nuevo valor de la estadística.</param>
//    private void HandleStatChanged(StatType statType, float newValue)
//    {
//        if (statType == StatType.HealthDrainAmount)
//        {
//            healthDrainAmount = newValue;
//        }
//    }

//    void Update()
//    {
//        // Toggle con Q: si está activo, lo desactiva; si no, lo activa.
//        if (Input.GetKeyDown(KeyCode.Q))
//        {
//            if (SkillActive)
//            {
//                DeactivateSkill();
//            }
//            else
//            {
//                ActivateSkill();
//            }
//        }

//        if (SkillActive)
//        {
//            if (PH != null && healthDrainAmount > 0)
//            {
//                healthDrainTimer += Time.deltaTime;

//                if (healthDrainTimer >= 1f)
//                {
//                    PH.TakeDamage(healthDrainAmount);
//                    healthDrainTimer = 0f;
//                    Debug.Log($"[DRENADO] Vida reducida en {healthDrainAmount}. SkillActive: {SkillActive}");
//                }
//            }

//            skillDurationTimer += Time.deltaTime;

//            if (skillDurationTimer >= skillDuration)
//            {
//                DeactivateSkill();
//            }
//        }
//        else
//        {
//            healthDrainTimer = 0f;
//            skillDurationTimer = 0f;
//        }
//    }

//    /// <summary>
//    /// Activa la habilidad aplicando los buffs correspondientes.
//    /// Ahora además aplica modificadores nombrados en PlayerStatsManager para actualizar la UI.
//    /// </summary>
//    private void ActivateSkill()
//    {
//        // Si ya está activo, no hacer nada (seguridad)
//        if (SkillActive) return;

//        SkillActive = true;
//        skillDurationTimer = 0f;
//        healthDrainTimer = 0f;

//        Debug.Log($"[ACTIVAR] SkillActive cambiado a: {SkillActive}");

//        float moveBuff = 1f;
//        float attackBuff = 1f;

//        float healthDrainMultiplier = 1f;

//        if (PH == null)
//        {
//            Debug.LogWarning("PlayerHealth no encontrado en ShieldSkill. Se asumirá estado Adult por defecto.");
//        }

//        switch (PH != null ? PH.CurrentLifeStage : PlayerHealth.LifeStage.Adult)
//        {
//            case PlayerHealth.LifeStage.Young:
//                moveBuff = 1.2f;
//                attackBuff = 1.1f;
//                healthDrainMultiplier = 1f;
//                Debug.Log("Joven: buff 20% velocidad, 10% fuerza");
//                break;
//            case PlayerHealth.LifeStage.Adult:
//                moveBuff = 1.1f;
//                attackBuff = 1.2f;
//                healthDrainMultiplier = 1f;
//                Debug.Log("Adulto: buff 20% fuerza, 10% velocidad");
//                break;
//            case PlayerHealth.LifeStage.Elder:
//                moveBuff = 1.15f;
//                attackBuff = 1.15f;
//                healthDrainMultiplier = 1f;
//                Debug.Log("Viejo: buff 15% fuerza y velocidad");
//                break;
//        }

//        // --- Aplicar cambios a componentes para la jugabilidad ---
//        if (PMA != null) PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee * attackBuff);
//        if (PSC != null) PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield * attackBuff);
//        if (PM != null) PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed * moveBuff);

//        // --- Sincronizar con PlayerStatsManager (para UI) mediante modificadores nombrados ---
//        if (statsManager != null)
//        {
//            // Melee Attack Damage
//            float currentMelee = statsManager.GetStat(StatType.MeleeAttackDamage);
//            float newMelee = Mathf.RoundToInt(BaseAttackMelee * attackBuff);
//            float deltaMelee = newMelee - currentMelee;
//            statsManager.ApplyNamedModifier(KEY_ATK_MELEE, StatType.MeleeAttackDamage, deltaMelee);

//            // Shield Attack Damage
//            float currentShieldAtk = statsManager.GetStat(StatType.ShieldAttackDamage);
//            float newShieldAtk = Mathf.RoundToInt(BaseAttackShield * attackBuff);
//            float deltaShield = newShieldAtk - currentShieldAtk;
//            statsManager.ApplyNamedModifier(KEY_ATK_SHIELD, StatType.ShieldAttackDamage, deltaShield);

//            // Move Speed
//            float currentMove = statsManager.GetStat(StatType.MoveSpeed);
//            float newMove = Mathf.RoundToInt(BaseSpeed * moveBuff);
//            float deltaMove = newMove - currentMove;
//            statsManager.ApplyNamedModifier(KEY_MOVE_SPEED, StatType.MoveSpeed, deltaMove);

//            // Health Drain (si aplica multiplicador)
//            float currentDrain = statsManager.GetStat(StatType.HealthDrainAmount);
//            float newDrain = currentDrain * healthDrainMultiplier;
//            float deltaDrain = newDrain - currentDrain;
//            statsManager.ApplyNamedModifier(KEY_HEALTH_DRAIN, StatType.HealthDrainAmount, deltaDrain);

//            // Actualizar la copia local también (por si el PlayerStatsManager cambió valores)
//            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
//        }
//        else
//        {
//            // Si no hay statsManager, aplicamos solo la copia local
//            healthDrainAmount *= healthDrainMultiplier;
//        }

//        // Cambiar material a dorado (si se dispone)
//        ApplyGoldenMaterial();

//        Debug.Log($"[ShieldSkill ACTIVADO] " +
//                  $"ATK M: {(PMA != null ? PMA.AttackDamage.ToString() : "N/A")} (Base: {BaseAttackMelee}) | " +
//                  $"ATK S: {(PSC != null ? PSC.ShieldDamage.ToString() : "N/A")} (Base: {BaseAttackShield}) | " +
//                  $"VEL: {(PM != null ? PM.MoveSpeed.ToString() : "N/A")} (Base: {BaseSpeed}) | " +
//                  $"DRENADO: {healthDrainAmount}/s | " +
//                  $"Duración: {skillDuration}s | SkillActive: {SkillActive}");
//    }

//    /// <summary>
//    /// Desactiva la habilidad y restaura las estadísticas base.
//    /// Ahora además remueve los modificadores nombrados en PlayerStatsManager para actualizar la UI.
//    /// </summary>
//    private void DeactivateSkill()
//    {
//        // Si ya está desactivado, no hacer nada (seguridad)
//        if (!SkillActive) return;

//        SkillActive = false;
//        skillDurationTimer = 0f;
//        healthDrainTimer = 0f;

//        if (PMA != null) PMA.AttackDamage = Mathf.RoundToInt(BaseAttackMelee);
//        if (PSC != null) PSC.ShieldDamage = Mathf.RoundToInt(BaseAttackShield);
//        if (PM != null) PM.MoveSpeed = Mathf.RoundToInt(BaseSpeed);

//        if (statsManager != null)
//        {
//            // Remover modificadores nombrados para restaurar los valores en PlayerStatsManager (y disparar OnStatChanged)
//            statsManager.RemoveNamedModifier(KEY_ATK_MELEE);
//            statsManager.RemoveNamedModifier(KEY_ATK_SHIELD);
//            statsManager.RemoveNamedModifier(KEY_MOVE_SPEED);
//            statsManager.RemoveNamedModifier(KEY_HEALTH_DRAIN);

//            // Actualizar copia local del drenado según el stat manager
//            healthDrainAmount = statsManager.GetStat(StatType.HealthDrainAmount);
//        }
//        else
//        {
//            healthDrainAmount = 0f;
//        }

//        // Restaurar material original
//        RestoreOriginalMaterial();

//        Debug.Log($"[ShieldSkill DESACTIVADO] " +
//                  $"ATK M: {(PMA != null ? PMA.AttackDamage.ToString() : "N/A")} | " +
//                  $"ATK S: {(PSC != null ? PSC.ShieldDamage.ToString() : "N/A")} | " +
//                  $"VEL: {(PM != null ? PM.MoveSpeed.ToString() : "N/A")} | " +
//                  $"DRENADO: {healthDrainAmount} | SkillActive: {SkillActive}");
//    }

//    /// <summary>
//    /// Aplica el material dorado a todos los sub-materiales del MeshRenderer.
//    /// Guarda los materiales originales si todavía no se guardaron.
//    /// </summary>
//    private void ApplyGoldenMaterial()
//    {
//        if (meshRenderer == null || goldenMaterial == null) return;

//        // Si no hemos guardado los originales, guardarlos ahora
//        if (originalMaterials == null || originalMaterials.Length == 0)
//        {
//            // Clonar el array para evitar referencias compartidas
//            var mats = meshRenderer.materials;
//            originalMaterials = new Material[mats.Length];
//            for (int i = 0; i < mats.Length; i++) originalMaterials[i] = mats[i];
//        }

//        // Crear array con el material dorado repetido la misma cantidad de sub-materiales
//        Material[] goldMats = new Material[originalMaterials.Length];
//        for (int i = 0; i < goldMats.Length; i++)
//        {
//            goldMats[i] = goldenMaterial;
//        }

//        meshRenderer.materials = goldMats;
//    }

//    /// <summary>
//    /// Restaura los materiales originales si existen.
//    /// </summary>
//    private void RestoreOriginalMaterial()
//    {
//        if (meshRenderer == null) return;
//        if (originalMaterials == null || originalMaterials.Length == 0) return;

//        meshRenderer.materials = originalMaterials;
//        originalMaterials = null;
//    }

//    #endregion
//}

