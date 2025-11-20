using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Clase que maneja la salud del jugador, incluyendo dano, curacion y etapas de vida.
/// Archivo adaptado para soportar: veneno por tiempo y ralentizacion aplicada por areas (acido).
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    // Tipos de etapas de vida del jugador.
    public enum LifeStage
    {
        Young,
        Adult,
        Elder
    }

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private PlayerBlockSystem blockSystem;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private PlayerAudioController playerAudioController;
    [SerializeField] private Animator playerAnimator;

    [Header("Configuracion de Vida")]
    [Tooltip("Vida maxima por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackMaxHealth = 100;
    [SerializeField] private float damageInvulnerabilityTime = 0.5f;
    private float currentHealth;

    [Header("Configuracion de Muerte")]
    [SerializeField] private string sceneToLoadOnDeath = "Tuto";
    [SerializeField] private Color deathFadeColor = Color.red;

    [Header("Configuracion de Modelo por Etapa")]
    [Tooltip("El objeto hijo que contiene el modelo visual del jugador.")]
    [SerializeField] private Transform playerModelTransform;
    [SerializeField] private Vector3 scaleYoung = new Vector3(0.75f, 0.75f, 0.75f);
    [SerializeField] private Vector3 scaleAdult = new Vector3(1f, 1f, 1f);
    [SerializeField] private Vector3 scaleElder = new Vector3(1.25f, 1.25f, 1.25f);
    [Tooltip("El desplazamiento vertical para mantener los pies en el suelo. Depende de la altura base del modelo.")]
    [SerializeField] private float yOffsetYoung = 0.375f;
    [SerializeField] private float yOffsetAdult = 0.5f;
    [SerializeField] private float yOffsetElder = 0.625f;

    [Header("Damage VFX")]
    [SerializeField] private Color damageEmissionColor = Color.red;
    [SerializeField] private float damageEmissionIntensity = 2f;
    [SerializeField] private float damageFlashDuration = 0.1f;
    [SerializeField] private int damageFlashCount = 3;

    [Header("Mejora de Escudo")]
    [SerializeField] private float shieldBlockCooldown = 18f;
    private bool isShieldBlockReady = true;

    [Header("Vida Temporal")]
    [SerializeField] private float temporaryHealthDuration = 10f;
    [SerializeField] private float temporaryHealthDecaySpeed = 0.5f;

    [Header("UI")]
    [SerializeField] private TMP_Text lifeStageText;

    private float currentTemporaryHealth = 0f;
    private float maxTemporaryHealthLimit = 0f;
    private Coroutine temporaryHealthDecayCoroutine;

    public bool IsKillHealBlocked { get; private set; } = false;
    public bool HasAmuletOfEndurance { get; private set; } = false;
    public static PlayerHealth Instance { get; private set; }
    public bool HasShieldBlockUpgrade { get; private set; } = false;
    public bool IsInvulnerable { get; set; } = false;
    private bool isDamageInvulnerable = false;

    public float MaxHealth => statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
    public float CurrentHealth => currentHealth;
    public float CurrentHealthPercent
    {
        get
        {
            float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

            if (maxHealth <= 0) return 0f;

            return currentHealth / maxHealth;
        }
    }

    public LifeStage CurrentLifeStage { get; private set; }

    private bool isInitialized = false;
    private bool isDying = false;

    public static event Action<float, float> OnHealthChanged;
    public static event Action<LifeStage> OnLifeStageChanged;

    private PlayerMovement playerMovement;
    private PlayerMeleeAttack playerMeleeAttack;
    private PlayerShieldController playerShieldController;
    private InventoryManager inventoryManager;
    private Coroutine damageInvulnerabilityCoroutine;
    private Coroutine damageFlashCoroutine;

    private MeshRenderer[] cachedMeshRenderers;
    private Dictionary<MeshRenderer, MaterialPropertyBlock> materialPropertyBlocks;
    private Dictionary<MeshRenderer, Color[]> originalEmissionColors;
    private Dictionary<MeshRenderer, bool> originalEmissionEnabled;

    public Transform PlayerModelTransform => playerModelTransform;
    public Vector3 CurrentModelLocalScale => playerModelTransform != null ? playerModelTransform.localScale : Vector3.one;
    public Vector3 CurrentModelWorldScale => playerModelTransform != null ? playerModelTransform.lossyScale : Vector3.one;
    public float CurrentModelYOffset => playerModelTransform != null ? playerModelTransform.localPosition.y : 0f;

    public event Action<float> OnDamageReceived;
    public bool IsLowHealth => currentHealth < (MaxHealth * 0.25f);

    public static event Action<PlayerHealth> OnPlayerInstantiated;

    // Variables para el veneno de Morlock
    private int morlockHitCounter = 0;
    private Coroutine morlockPoisonResetCoroutine;
    private Coroutine morlockPoisonHitCoroutine;

    // Variables para el stun
    private bool isStunned = false;
    private Coroutine stunCoroutine;

    // Referencia al PlayerCombatActionManager para interacciones de combate
    private PlayerCombatActionManager combatActionManager;

    // Variable por si el jugador ha sido marcado por el Pulso Carnal
    public bool IsMarked { get; set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        statsManager = GetComponent<PlayerStatsManager>();
        // FindAnyObjectByType puede devolver null; lo guardamos y comprobamos antes de usar.
        inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (statsManager == null) ReportDebug("StatsManager no esta asignado en PlayerHealth. Usando vida maxima de fallback.", 2);

        playerMovement = GetComponent<PlayerMovement>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();
        blockSystem = GetComponent<PlayerBlockSystem>();
        combatActionManager = GetComponent<PlayerCombatActionManager>();
        playerAnimator = GetComponentInChildren<Animator>();

        if (playerAudioController == null)
        {
            playerAudioController = GetComponent<PlayerAudioController>();
            if (playerAudioController == null)
            {
                ReportDebug("PlayerAudioController no encontrado en PlayerHealth.", 2);
            }
        }

        InitializeMaterialCache();

        OnPlayerInstantiated?.Invoke(this);
    }

    private void Start()
    {
        bool isTutoScene = SceneManager.GetActiveScene().name == "Tuto";
        bool isTutoSceneComplete = SceneManager.GetActiveScene().name == "TutorialCompleto";
        if ((isTutoScene || isTutoSceneComplete) && statsManager != null)
        {
            statsManager.ResetRunStatsToDefaults();

            if (inventoryManager != null)
            {
                inventoryManager.ClearInventory();
            }
            else
            {
                ReportDebug("InventoryManager es null en Start() al reiniciar tutorial.", 2);
            }

            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
            if (statsManager._currentStatSO != null)
            {
                statsManager._currentStatSO.currentHealth = maxHealth;
                ReportDebug($"Vida del SO forzada a MaxHealth ({maxHealth}) para el reinicio en la escena {sceneToLoadOnDeath}.", 1);
            }
            else
            {
                ReportDebug("currentStatSO es null en Start() al forzar vida en tutorial.", 2);
            }
        }

        InitializeCurrentHealthFromSO();
        InitializeShieldUpgradeFromSO();

        SyncCurrentHealthToSO();

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        UpdateLifeStage(true);

        isInitialized = true;
    }

    private void OnDestroy()
    {
        CleanupMaterialCache();
    }

    #region Material Cache Management

    /// <summary>
    /// Inicializa el cache de materiales para evitar crear nuevos MaterialPropertyBlocks en cada frame.
    /// </summary>
    private void InitializeMaterialCache()
    {
        if (playerModelTransform == null) return;

        cachedMeshRenderers = playerModelTransform.GetComponentsInChildren<MeshRenderer>();
        materialPropertyBlocks = new Dictionary<MeshRenderer, MaterialPropertyBlock>();
        originalEmissionColors = new Dictionary<MeshRenderer, Color[]>();
        originalEmissionEnabled = new Dictionary<MeshRenderer, bool>();

        foreach (var renderer in cachedMeshRenderers)
        {
            if (renderer == null) continue;

            // Crear un MaterialPropertyBlock por renderer
            materialPropertyBlocks[renderer] = new MaterialPropertyBlock();

            // Guardar colores de emisión originales
            Material[] materials = renderer.sharedMaterials;
            Color[] colors = new Color[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                {
                    if (materials[i].HasProperty("_EmissionColor"))
                    {
                        colors[i] = materials[i].GetColor("_EmissionColor");
                        originalEmissionEnabled[renderer] = materials[i].IsKeywordEnabled("_EMISSION");
                    }
                    else
                    {
                        colors[i] = Color.black;
                        originalEmissionEnabled[renderer] = false;
                    }
                }
            }
            originalEmissionColors[renderer] = colors;
        }

        ReportDebug($"Cache de materiales inicializado con {cachedMeshRenderers.Length} renderers.", 1);
    }

    /// <summary>
    /// Limpia el cache de materiales para prevenir memory leaks.
    /// </summary>
    private void CleanupMaterialCache()
    {
        if (materialPropertyBlocks != null)
        {
            materialPropertyBlocks.Clear();
            materialPropertyBlocks = null;
        }

        if (originalEmissionColors != null)
        {
            originalEmissionColors.Clear();
            originalEmissionColors = null;
        }

        if (originalEmissionEnabled != null)
        {
            originalEmissionEnabled.Clear();
            originalEmissionEnabled = null;
        }

        cachedMeshRenderers = null;
    }

    /// <summary>
    /// Aplica emisión roja a todos los MeshRenderers usando MaterialPropertyBlocks.
    /// </summary>
    private void ApplyDamageEmission()
    {
        if (cachedMeshRenderers == null || materialPropertyBlocks == null) return;

        Color emissionColor = damageEmissionColor * damageEmissionIntensity;

        foreach (var renderer in cachedMeshRenderers)
        {
            if (renderer == null || !materialPropertyBlocks.ContainsKey(renderer)) continue;

            MaterialPropertyBlock mpb = materialPropertyBlocks[renderer];
            renderer.GetPropertyBlock(mpb);

            // Aplicar emisión
            mpb.SetColor("_EmissionColor", emissionColor);
            renderer.SetPropertyBlock(mpb);

            // Habilitar keyword de emisión en el material si no está habilitado
            Material[] materials = renderer.sharedMaterials;
            foreach (var mat in materials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }
    }

    /// <summary>
    /// Restaura la emisión original de todos los MeshRenderers.
    /// </summary>
    private void RestoreOriginalEmission()
    {
        if (cachedMeshRenderers == null || materialPropertyBlocks == null) return;

        foreach (var renderer in cachedMeshRenderers)
        {
            if (renderer == null || !materialPropertyBlocks.ContainsKey(renderer)) continue;

            MaterialPropertyBlock mpb = materialPropertyBlocks[renderer];
            renderer.GetPropertyBlock(mpb);

            // Restaurar emisión original
            if (originalEmissionColors.ContainsKey(renderer))
            {
                Color[] originalColors = originalEmissionColors[renderer];
                if (originalColors != null && originalColors.Length > 0)
                {
                    // Usamos el primer color como referencia (podrías expandir esto para multi-material)
                    mpb.SetColor("_EmissionColor", originalColors[0]);
                    renderer.SetPropertyBlock(mpb);
                }
            }

            // Restaurar estado de keyword de emisión
            if (originalEmissionEnabled.ContainsKey(renderer))
            {
                Material[] materials = renderer.sharedMaterials;
                foreach (var mat in materials)
                {
                    if (mat != null && mat.HasProperty("_EmissionColor"))
                    {
                        if (originalEmissionEnabled[renderer])
                        {
                            mat.EnableKeyword("_EMISSION");
                        }
                        else
                        {
                            mat.DisableKeyword("_EMISSION");
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Initialization

    private void InitializeShieldUpgradeFromSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            HasShieldBlockUpgrade = statsManager._currentStatSO.isShieldBlockUpgradeActive;
        }
    }

    private void InitializeCurrentHealthFromSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            float maxHealthValue = MaxHealth;
            float soCurrentHealth = statsManager._currentStatSO.currentHealth;

            bool isTutoScene = SceneManager.GetActiveScene().name == sceneToLoadOnDeath;

            if (isTutoScene)
            {
                currentHealth = maxHealthValue;
                statsManager._currentStatSO.currentHealth = maxHealthValue;
                ReportDebug($"Escena de reinicio ({SceneManager.GetActiveScene().name}) detectada. Vida restaurada a MaxHealth: {currentHealth}", 1);
            }
            else
            {
                currentHealth = Mathf.Clamp(soCurrentHealth, 0, maxHealthValue);
                ReportDebug($"Vida actual cargada desde SO en escena {SceneManager.GetActiveScene().name}: {currentHealth}/{maxHealthValue}", 1);
            }
        }
        else
        {
            currentHealth = fallbackMaxHealth;
        }
    }

    #endregion
    
    private void SyncCurrentHealthToSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.currentHealth = currentHealth;
        }
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    /// <param name="statType">Tipo de estadistica que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estadistica.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.MaxHealth)
        {
            float maxHealthBeforeChange = MaxHealth;
            float percentage = currentHealth / Mathf.Max(1, maxHealthBeforeChange);

            currentHealth = Mathf.Clamp(newValue * percentage, 0, newValue);

            if (isInitialized)
            {
                SyncCurrentHealthToSO();
            }
            else
            {
                ReportDebug("Sincronizacion de vida omitida debido a inicializacion temprana (currentHealth=0).", 1);
            }

            OnHealthChanged?.Invoke(currentHealth, newValue);

            UpdateLifeStage();

            ReportDebug($"Nueva vida maxima: {newValue}, vida actual ajustada a {currentHealth}", 1);
        }
    }

    private void ChangeLifeStage(LifeStage newStage)
    {
        OnLifeStageChanged?.Invoke(newStage);
    }

    /// <summary>
    /// Funcion que aplica dano al jugador.
    /// </summary>
    /// <param name="damageAmount"> Cantidad de dano a aplicar. </param>
    public void TakeDamage(float damageAmount, bool isCostDamage = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (isDying) return;

        if (!isCostDamage && (isDamageInvulnerable || IsInvulnerable))
        {
            ReportDebug("El jugador es invulnerable y no recibe dano.", 1);
            return;
        }

        if (!isCostDamage && HasShieldBlockUpgrade)
        {
            if (isShieldBlockReady)
            {
                isShieldBlockReady = false;
                ReportDebug("El escudo ha bloqueado el dano entrante.", 1);

                StartCoroutine(ShieldBlockCooldownRoutine());
                return;
            }
        }

        if (IsMarked && !isCostDamage)
        {
            damageAmount *= 2f; // Duplicar el daño entrante
            IsMarked = false;   // Consumir la marca inmediatamente

            ReportDebug($"<color=red>Marca consumida: El jugador recibe el doble de daño ({damageAmount}).</color>", 1);
        }

        float damageToApply = damageAmount;

        if (currentTemporaryHealth > 0f)
        {
            float remainingTemporaryHealth = currentTemporaryHealth - damageToApply;
            if (remainingTemporaryHealth >= 0f)
            {
                currentTemporaryHealth = remainingTemporaryHealth;
                damageToApply = 0f; 
            }
            else
            {
                damageToApply = -remainingTemporaryHealth; 
                currentTemporaryHealth = 0f;
            }

            if (currentTemporaryHealth <= 0f && temporaryHealthDecayCoroutine != null)
            {
                StopCoroutine(temporaryHealthDecayCoroutine);
                temporaryHealthDecayCoroutine = null;
            }

            OnDamageReceived?.Invoke(damageToApply);
            ReportDebug($"Vida temporal restante después del golpe: {currentTemporaryHealth}. Daño restante a vida normal: {damageToApply}", 1);
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        if (damageToApply > 0f)
        {
            OnDamageReceived?.Invoke(damageToApply);

            if (playerAnimator) playerAnimator.SetTrigger("GetHit");
            if (playerAudioController != null)
            {
                playerAudioController.PlayDamageSound();
            }

            currentHealth -= damageToApply;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El jugador ha recibido {damageToApply} de dano. Vida actual: {currentHealth}/{maxHealth}", 1);

            if (!isCostDamage)
            {
                if (damageFlashCoroutine != null) StopCoroutine(damageFlashCoroutine);
                damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());

                isDamageInvulnerable = true;
                if (damageInvulnerabilityCoroutine != null) StopCoroutine(damageInvulnerabilityCoroutine);
                damageInvulnerabilityCoroutine = StartCoroutine(DamageInvulnerabilityRoutine());
            }

            if (currentHealth <= 0) Die();
        }

        SyncCurrentHealthToSO();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLifeStage();

        UpdateTemporaryHealthUI();
    }

    /// <summary>
    /// Rutina que hace parpadear la emisión roja en los materiales del jugador.
    /// </summary>
    private IEnumerator DamageFlashRoutine()
    {
        for (int i = 0; i < damageFlashCount; i++)
        {
            ApplyDamageEmission();
            yield return new WaitForSeconds(damageFlashDuration);

            RestoreOriginalEmission();
            yield return new WaitForSeconds(damageFlashDuration);
        }

        damageFlashCoroutine = null;
    }

    private IEnumerator DamageInvulnerabilityRoutine()
    {
        isDamageInvulnerable = true;
        ReportDebug($"El jugador es invulnerable por dano continuo durante {damageInvulnerabilityTime} segundos.", 1);

        float blinkInterval = 0.1f;
        float timer = 0f;

        // Si no hay SpriteRenderer asignado, solo esperamos el tiempo sin intentar cambiar color.
        if (playerSpriteRenderer == null)
        {
            yield return new WaitForSeconds(damageInvulnerabilityTime);
            isDamageInvulnerable = false;
            damageInvulnerabilityCoroutine = null;
            ReportDebug("La invulnerabilidad por dano ha terminado (no hay SpriteRenderer).", 1);
            yield break;
        }

        while (timer < damageInvulnerabilityTime)
        {
            playerSpriteRenderer.color = new Color(1f, 1f, 1f, 0.5f);
            yield return new WaitForSeconds(blinkInterval);

            playerSpriteRenderer.color = Color.white;
            yield return new WaitForSeconds(blinkInterval);

            timer += blinkInterval * 2;
        }

        isDamageInvulnerable = false;
        damageInvulnerabilityCoroutine = null;
        ReportDebug("La invulnerabilidad por dano ha terminado.", 1);

        playerSpriteRenderer.color = Color.white;
    }

    // Funcion que maneja el cooldown del bloqueo del escudo.
    private IEnumerator ShieldBlockCooldownRoutine()
    {
        ReportDebug($"El escudo bloqueara de nuevo en {shieldBlockCooldown} segundos.", 1);
        yield return new WaitForSeconds(shieldBlockCooldown);

        isShieldBlockReady = true;
        ReportDebug("El escudo esta listo para bloquear de nuevo.", 1);
    }

    /// <summary>
    /// Funcion que cura al jugador.
    /// </summary>
    /// <param name="healAmount"> Cantidad de dano a curar </param>
    public void Heal(float healAmount)
    {
        if (IsKillHealBlocked)
        {
            ReportDebug($"Curación BLOQUEADA por Distorsión.", 2);
            return; 
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        if (HasAmuletOfEndurance)
        {
            AddTemporaryHealth(healAmount, maxHealth);
            ReportDebug($"Curación desviada a vida temporal debido al Amuleto. Cantidad: {healAmount}", 1);
        }
        else
        {
            currentHealth += healAmount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); 
            ReportDebug($"El jugador ha sido curado {healAmount} de vida normal.", 1);
        }

        SyncCurrentHealthToSO();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        UpdateLifeStage();

        UpdateTemporaryHealthUI();
    }

    /// <summary>
    /// Funcion que actualiza la etapa de vida del jugador y notifica si ha cambiado.
    /// </summary>
    /// <param name="forceNotify"> Si es true, fuerza la notificacion del cambio de etapa incluso si no ha cambiado. </param>
    private void UpdateLifeStage(bool forceNotify = false)
    {
        LifeStage oldStage = CurrentLifeStage;
        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
        float healthPercentage = currentHealth / maxHealth;

        if (healthPercentage > 0.666f) CurrentLifeStage = LifeStage.Young;
        else if (healthPercentage > 0.333f) CurrentLifeStage = LifeStage.Adult;
        else CurrentLifeStage = LifeStage.Elder;

        // Solo notificar si la etapa realmente ha cambiado, o si se fuerza al inicio
        if (CurrentLifeStage != oldStage || forceNotify)
        {
            OnLifeStageChanged?.Invoke(CurrentLifeStage);
            UpdateModelForLifeStage(CurrentLifeStage);

            int ageStageValue = 0;
            switch (CurrentLifeStage)
            {
                case PlayerHealth.LifeStage.Young:
                    ageStageValue = 3;
                    break;
                case PlayerHealth.LifeStage.Adult:
                    ageStageValue = 2;
                    break;
                case PlayerHealth.LifeStage.Elder:
                    ageStageValue = 1;
                    break;
            }

            if (playerAnimator != null) playerAnimator.SetFloat("AgeState", ageStageValue);
            ReportDebug($"Etapa de vida cambiada a {CurrentLifeStage}. Animator AgeStage seteado a {ageStageValue}.", 1);

            // Actualizar TextMeshPro si esta asignado
            if (lifeStageText != null)
            {
                // Mostrar solo el nombre de la etapa (sin texto adicional)
                lifeStageText.text = GetLifeStageString(CurrentLifeStage);
            }
        }
    }

    /// <summary>
    /// Ajusta la escala y la posicion vertical del modelo del jugador segun su etapa de vida.
    /// </summary>
    /// <param name="newStage">La nueva etapa de vida a representar.</param>
    private void UpdateModelForLifeStage(LifeStage newStage)
    {
        if (playerModelTransform == null)
        {
            ReportDebug("No se ha asignado 'playerModelTransform'. No se puede actualizar el modelo.", 2);
            return;
        }

        Vector3 targetScale = Vector3.one;
        float targetYOffset = 0f;

        switch (newStage)
        {
            case LifeStage.Young:
                targetScale = scaleYoung;
                targetYOffset = yOffsetYoung;
                break;
            case LifeStage.Adult:
                targetScale = scaleAdult;
                targetYOffset = yOffsetAdult;
                break;
            case LifeStage.Elder:
                targetScale = scaleElder;
                targetYOffset = yOffsetElder;
                break;
        }

        playerModelTransform.localScale = targetScale;
        playerModelTransform.localPosition = new Vector3(0, targetYOffset, 0);
        ReportDebug($"Modelo actualizado para la etapa {newStage}. Escala: {targetScale}, Posicion Y: {targetYOffset}", 1);
    }

    // Traduce el enum de LifeStage a una cadena en espanol para mostrar en TMP.
    private string GetLifeStageString(LifeStage stage)
    {
        switch (stage)
        {
            case LifeStage.Young:
                return "Joven";
            case LifeStage.Adult:
                return "Adulto";
            case LifeStage.Elder:
                return "Anciano";
            default:
                return stage.ToString();
        }
    }

    // Funcion que maneja la muerte del jugador.
    private void Die()
    {
        if (isDying) return;

        if (combatActionManager != null)
        {
            combatActionManager.InterruptCombatActions();
        }

        isDying = true;

        if (playerAudioController != null)
        {
            playerAudioController.PlayDeathSound();
        }

        ReportDebug("El jugador ha muerto. Cargando escena: " + sceneToLoadOnDeath, 1);

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = false;
        if (playerShieldController != null) playerShieldController.enabled = false;

        Collider playerCollider = GetComponent<Collider>();
        if (playerCollider != null) playerCollider.enabled = false;

        var sm = statsManager;
        var im = inventoryManager;

        if (FadeController.Instance != null)
        {
            StartCoroutine(FadeController.Instance.FadeOut(
                fadeColor: deathFadeColor,
                onComplete: () =>
                {
                    if (sm != null)
                    {
                        if (im != null && im.ActiveBehavioralEffects != null)
                        {
                            sm.RemoveAllBehavioralEffects(im.ActiveBehavioralEffects);
                        }

                        sm.ClearAllNamedModifiers();

                        sm.ResetRunStatsToDefaults();
                        sm.ResetStatsOnDeath();
                    }
                    else
                    {
                        ReportDebug("StatsManager es null en Die() durante callback de FadeOut.", 2);
                    }

                    if (im != null)
                    {
                        im.ClearInventory();
                    }
                    else
                    {
                        ReportDebug("InventoryManager es null en Die() durante callback de FadeOut.", 2);
                    }

                    SceneManager.LoadScene(sceneToLoadOnDeath);
                }));
        }
        else
        {
            if (sm != null)
            {
                if (im != null && im.ActiveBehavioralEffects != null)
                {
                    sm.RemoveAllBehavioralEffects(im.ActiveBehavioralEffects);
                }

                sm.ClearAllNamedModifiers();

                sm.ResetRunStatsToDefaults();
                sm.ResetStatsOnDeath();
            }
            else
            {
                ReportDebug("StatsManager es null en Die() (sin FadeController).", 2);
            }

            if (im != null)
            {
                im.ClearInventory();
            }
            else
            {
                ReportDebug("InventoryManager es null en Die() (sin FadeController).", 2);
            }

            SceneManager.LoadScene(sceneToLoadOnDeath);
        }
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return MaxHealth;
    }

    public bool IsDead()
    {
        return isDying;
    }

    #region Stun System

    public void ApplyStun(float stunDuration)
    {
        if (isDying) return;

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
        }

        stunCoroutine = StartCoroutine(StunRoutine(stunDuration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        // Inmovilizar completamente
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        ReportDebug($"Jugador aturdido por {duration}s debido a rotura de escudo.", 2);

        yield return new WaitForSeconds(duration);

        isStunned = false;

        // Restaurar movimiento
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }

        stunCoroutine = null;
        ReportDebug("Stun finalizado. Recarga de durabilidad iniciada.", 1);
    }

    public bool IsStunned()
    {
        return isStunned;
    }

    #endregion

    public void EnableShieldBlockUpgrade()
    {
        HasShieldBlockUpgrade = true;

        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.isShieldBlockUpgradeActive = true;
        }

        ReportDebug("La mejora de bloqueo de escudo ha sido activada.", 1);
    }

    public void DisableShieldBlockUpgrade()
    {
        HasShieldBlockUpgrade = false;

        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.isShieldBlockUpgradeActive = false;
        }

        ReportDebug("La mejora de bloqueo de escudo ha sido desactivada.", 1);
    }
    public void ActivateEnduranceAmulet()
    {
        if (HasAmuletOfEndurance) return;

        HasAmuletOfEndurance = true;

        statsManager.ApplyNamedModifier("EnduranceAmulet_DR", StatType.DamageTaken, 0.15f);

        ReportDebug("¡Amuleto de Endurecimiento ACTIVADO! Reducción de daño +15% aplicada.", 2);
    }
    public void AddTemporaryHealth(float amount, float maxLimit)
    {
        maxTemporaryHealthLimit = Mathf.Max(maxTemporaryHealthLimit, maxLimit);

        currentTemporaryHealth += amount;
        currentTemporaryHealth = Mathf.Min(currentTemporaryHealth, maxTemporaryHealthLimit);

        if (temporaryHealthDecayCoroutine != null)
        {
            StopCoroutine(temporaryHealthDecayCoroutine);
        }
        temporaryHealthDecayCoroutine = StartCoroutine(TemporaryHealthDecayRoutine());

        UpdateTemporaryHealthUI();
        ReportDebug($"Vida temporal añadida: {amount}. Total: {currentTemporaryHealth}/{maxTemporaryHealthLimit}. Tiempo de decaimiento reseteado.", 1);
    }

    private IEnumerator TemporaryHealthDecayRoutine()
    {
        yield return new WaitForSeconds(temporaryHealthDuration);

        ReportDebug("Tiempo de gracia de vida temporal terminado. Iniciando decaimiento rápido.", 1);

        float startHealth = currentTemporaryHealth;
        float timePassed = 0f;

        while (timePassed < temporaryHealthDecaySpeed)
        {
            timePassed += Time.deltaTime;

            currentTemporaryHealth = Mathf.Lerp(startHealth, 0f, timePassed / temporaryHealthDecaySpeed);

            UpdateTemporaryHealthUI();

            yield return null;
        }

        currentTemporaryHealth = 0f;
        UpdateTemporaryHealthUI();
        temporaryHealthDecayCoroutine = null;
        maxTemporaryHealthLimit = 0f;

        ReportDebug("Decaimiento de vida temporal completado.", 1);
    }
    public void GrantTemporaryHealthOnKill(float amount)
    {
        if (!HasAmuletOfEndurance) return;


        float maxLimit = 30f;
        float currentMax = statsManager.GetCurrentStat(StatType.MaxHealth);

        if (currentHealth < currentMax)
        {
            ReportDebug("Vida no está al máximo, no se puede añadir vida temporal.", 1);
            return;
        }

        AddTemporaryHealth(amount, maxLimit);

        if (temporaryHealthDecayCoroutine != null)
        {
            StopCoroutine(temporaryHealthDecayCoroutine);
        }
        temporaryHealthDecayCoroutine = StartCoroutine(TemporaryHealthDecayRoutine());
    }
    public void UpdateTemporaryHealthUI()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.SetTemporaryHealthValues(currentTemporaryHealth, MaxHealth);
        }
    }
    public void AcquireAmuletOfEndurance()
    {
        HasAmuletOfEndurance = true;
        ReportDebug("Amuleto adquirido. La curación normal está deshabilitada.", 1);
    }

    public void BlockKillHeal(bool isBlocked)
    {
        IsKillHealBlocked = isBlocked;
        Debug.Log($"[PlayerHealth] Curación por muerte {(isBlocked ? "BLOQUEADA" : "RESTAURADA")} por Distorsión.");
    }

    #region Debuffs

    /// <summary>
    /// Funcion que aplica el efecto de veneno al jugador cuando es golpeado por un proyectil de Morlock.
    /// </summary>
    public void ApplyMorlockPoisonHit(float duration, float initialDamage, int hitThreshold)
    {
        if (isDying) return;

        if (morlockPoisonHitCoroutine != null)
        {
            ReportDebug("Golpe de Morlock recibido, pero el veneno ya está activo. Omitiendo contador.", 1);
            return;
        }

        morlockHitCounter++;

        if (morlockPoisonResetCoroutine != null)
        {
            StopCoroutine(morlockPoisonResetCoroutine);
        }

        if (morlockHitCounter >= hitThreshold)
        {
            float baseDamagePerTick = initialDamage;
            float damageIncrement = morlockHitCounter - hitThreshold;
            float damagePerSecond = baseDamagePerTick + damageIncrement;

            ApplyMorlockPoison(duration: 5f, damagePerSecond: damagePerSecond, tickInterval: 1f);

            morlockHitCounter = 0;
        }
        else
        {
            morlockPoisonResetCoroutine = StartCoroutine(ResetMorlockPoisonCounter(duration));
        }
    }

    private void ApplyMorlockPoison(float duration, float damagePerSecond, float tickInterval = 1f)
    {
        if (morlockPoisonHitCoroutine != null)
        {
            StopCoroutine(morlockPoisonHitCoroutine);
        }
        morlockPoisonHitCoroutine = StartCoroutine(ApplyMorlockPoisonCoroutine(duration, damagePerSecond, tickInterval));

        ReportDebug($"Veneno de Morlock aplicado: {damagePerSecond} dano por segundo durante {duration} segundos.", 1);
    }

    private IEnumerator ApplyMorlockPoisonCoroutine(float duration, float damagePerSecond, float tickInterval = 1f)
    {
        float timeElapsed = 0f;
        while (timeElapsed < duration)
        {
            if (isDying) yield break;
            TakeDamage(damagePerSecond);
            yield return new WaitForSeconds(1f);
            timeElapsed += tickInterval;
        }
        morlockPoisonHitCoroutine = null;
        ReportDebug("Efecto de veneno de Morlock ha terminado.", 1);
    }

    private IEnumerator ResetMorlockPoisonCounter(float duration)
    {
        yield return new WaitForSeconds(duration);
        morlockHitCounter = 0;
        morlockPoisonResetCoroutine = null;
    }

    #endregion

    // ------------------ METODOS AGREGADOS PARA ACIDO / VENENO / RALENTIZACION ------------------

    private Coroutine rutinaVenenoCoroutine;
    private Coroutine rutinaRalentizacionCoroutine;

    /// <summary>
    /// Aplica un veneno que causa dano por segundo durante 'duracion' segundos.
    /// Internamente llama a TakeDamage() para aplicar cada tick.
    /// </summary>
    /// <param name="duracion">Tiempo total del veneno en segundos.</param>
    /// <param name="danoPorSegundo">Dano por segundo aplicado por el veneno.</param>
    /// <param name="intervaloTick">Intervalo entre ticks (por defecto 1s).</param>
    public void AplicarVeneno(float duracion, float danoPorSegundo, float intervaloTick = 1f)
    {
        // si ya hay una rutina de veneno, reiniciamos con la nueva duracion
        if (rutinaVenenoCoroutine != null) StopCoroutine(rutinaVenenoCoroutine);
        rutinaVenenoCoroutine = StartCoroutine(RutinaVeneno(duracion, danoPorSegundo, intervaloTick));
    }

    private IEnumerator RutinaVeneno(float duracion, float danoPorSegundo, float intervaloTick)
    {
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            if (isDying) yield break;
            float danoTick = danoPorSegundo * intervaloTick;
            // marcado como cost damage false para que respete invulnerabilidades/escudos
            TakeDamage(danoTick);
            yield return new WaitForSeconds(intervaloTick);
            tiempo += intervaloTick;
        }
        rutinaVenenoCoroutine = null;
    }

    /// <summary>
    /// Propone una ralentizacion logica. Esto no cambia automaticamente la velocidad
    /// del movimiento; expone propiedades que PlayerMovement puede leer para aplicar la reduccion.
    /// </summary>
    public bool EstaRalentizado { get; private set; } = false;
    public float MultiplicadorVelocidadPorRalentizacion { get; private set; } = 1f;

    /// <summary>
    /// Aplica una ralentizacion (porcentaje entre 0 y 1: 0.5 = 50% de velocidad) durante 'duracion' segundos.
    /// Deja propiedades publicas que PlayerMovement puede consultar al mover.
    /// </summary>
    /// <param name="porcentajeRalentizacion">Fraccion de velocidad que queda (0..1).</param>
    /// <param name="duracion">Duracion en segundos.</param>
    public void AplicarRalentizacion(float porcentajeRalentizacion, float duracion)
    {
        if (porcentajeRalentizacion < 0.01f) porcentajeRalentizacion = 0.01f;
        if (rutinaRalentizacionCoroutine != null) StopCoroutine(rutinaRalentizacionCoroutine);
        MultiplicadorVelocidadPorRalentizacion = Mathf.Clamp01(porcentajeRalentizacion);
        rutinaRalentizacionCoroutine = StartCoroutine(RutinaRalentizacion(duracion));
    }

    private IEnumerator RutinaRalentizacion(float duracion)
    {
        EstaRalentizado = true;
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            if (isDying) break;
            tiempo += Time.deltaTime;
            yield return null;
        }
        EstaRalentizado = false;
        MultiplicadorVelocidadPorRalentizacion = 1f;
        rutinaRalentizacionCoroutine = null;
    }

    /// <summary>
    /// Remueve la ralentizacion inmediatamente (util por AreaAcido al salir).
    /// </summary>
    public void RemoverRalentizacion()
    {
        if (rutinaRalentizacionCoroutine != null) StopCoroutine(rutinaRalentizacionCoroutine);
        EstaRalentizado = false;
        MultiplicadorVelocidadPorRalentizacion = 1f;
        rutinaRalentizacionCoroutine = null;
    }

    // -------------------------------------------------------------------------------------------

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funcion de depuracion para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <param name="message">Mensaje a reportar.</param>
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerHealth] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerHealth] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerHealth] {message}");
                break;
            default:
                Debug.Log($"[PlayerHealth] {message}");
                break;
        }
    }
}
