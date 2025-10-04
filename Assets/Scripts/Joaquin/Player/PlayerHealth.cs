using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Clase que maneja la salud del jugador, incluyendo da�o, curaci�n y etapas de vida.
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
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Configuración de Vida")]
    [Tooltip("Vida m�xima por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackMaxHealth = 100;
    private float currentHealth;
    [SerializeField] private float damageInvulnerabilityTime = 0.5f;

    [Header("Configuración de Muerte")]
    [SerializeField] private string sceneToLoadOnDeath = "Tuto";
    [SerializeField] private Color deathFadeColor = Color.red;

    [Header("Configuración de Modelo por Etapa")]
    [Tooltip("El objeto hijo que contiene el modelo visual del jugador.")]
    [SerializeField] private Transform playerModelTransform;
    [SerializeField] private Vector3 scaleYoung = new Vector3(0.75f, 0.75f, 0.75f);
    [SerializeField] private Vector3 scaleAdult = new Vector3(1f, 1f, 1f);
    [SerializeField] private Vector3 scaleElder = new Vector3(1.25f, 1.25f, 1.25f);

    [Tooltip("El desplazamiento vertical para mantener los pies en el suelo. Depende de la altura base del modelo.")]
    [SerializeField] private float yOffsetYoung = 0.375f;
    [SerializeField] private float yOffsetAdult = 0.5f;
    [SerializeField] private float yOffsetElder = 0.625f;

    [Header("Mejora de Escudo")]
    [SerializeField] private float shieldBlockCooldown = 18f;
    private bool isShieldBlockReady = true;

    [Header("Veneno de Morlock")]
    [SerializeField] private MorlockStats morlockStats;
    [SerializeField] private int poisonHitThreshold = 3;
    [SerializeField] private float poisonInitialDamage = 2;
    [SerializeField] private float poisonResetTime = 5;
    private int morlockHitCounter = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text lifeStageText;

    public bool HasShieldBlockUpgrade { get; private set; } = false;
    public bool IsInvulnerable { get; set; } = false;
    private bool isDamageInvulnerable = false;
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
    private Coroutine poisonResetCoroutine;

    public float MaxHealth => statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
    public float CurrentHealth => currentHealth;

    public Transform PlayerModelTransform => playerModelTransform;
    public Vector3 CurrentModelLocalScale => playerModelTransform != null ? playerModelTransform.localScale : Vector3.one;
    public Vector3 CurrentModelWorldScale => playerModelTransform != null ? playerModelTransform.lossyScale : Vector3.one;
    public float CurrentModelYOffset => playerModelTransform != null ? playerModelTransform.localPosition.y : 0f;

    private void Awake()
    {
        statsManager = GetComponent<PlayerStatsManager>();
        inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (statsManager == null) ReportDebug("StatsManager no est� asignado en PlayerHealth. Usando vida m�xima de fallback.", 2);

        playerMovement = GetComponent<PlayerMovement>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();
    }

    private void Start()
    {
        bool isTutoScene = SceneManager.GetActiveScene().name == "Tuto";
        if (isTutoScene && statsManager != null)
        {
            statsManager.ResetRunStatsToDefaults();
            inventoryManager.ClearInventory();

            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
            statsManager._currentStatSO.currentHealth = maxHealth;
            ReportDebug($"Vida del SO forzada a MaxHealth ({maxHealth}) para el reinicio en la escena {sceneToLoadOnDeath}.", 1);
        }

        InitializeCurrentHealthFromSO();
        InitializeShieldUpgradeFromSO();

        float startMaxHealth = MaxHealth;
        currentHealth = startMaxHealth;
        SyncCurrentHealthToSO();
        ReportDebug($"Vida forzada a MaxHealth al iniciar: {currentHealth}/{startMaxHealth}", 1);

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        UpdateLifeStage(true);
        InitializedPosionDebuff();

        isInitialized = true;
    }

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

    private void SyncCurrentHealthToSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.currentHealth = currentHealth;
        }
    }

    private void InitializedPosionDebuff()
    {
        if (morlockStats != null)
        {
            poisonHitThreshold = morlockStats.poisonHitThreshold;
            poisonInitialDamage = morlockStats.poisonInitialDamage;
            poisonResetTime = morlockStats.poisonResetTime;
        }
        else
        {
            ReportDebug("MorlockStats no esta asignado en PlayerHealth. Usando valores de veneno por defecto.", 2);
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
    /// <param name="statType">Tipo de estad�stica que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estad�stica.</param>
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

    /// <summary>
    /// Funci�n que aplica da�o al jugador.
    /// </summary>
    /// <param name="damageAmount"> Cantidad de da�o a aplicar. </param>
    public void TakeDamage(float damageAmount, bool isCostDamage = false)
    {
        if (isDying) return;

        if (!isCostDamage && (isDamageInvulnerable || IsInvulnerable))
        {
            ReportDebug("El jugador es invulnerable y no recibe da�o.", 1);
            return;
        }

        if (!isCostDamage && HasShieldBlockUpgrade)
        {
            if (isShieldBlockReady)
            {
                isShieldBlockReady = false;
                ReportDebug("El escudo ha bloqueado el da�o entrante.", 1);

                StartCoroutine(ShieldBlockCooldownRoutine());
                return;
            }
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        SyncCurrentHealthToSO();

        if (!isCostDamage)
        {
            isDamageInvulnerable = true;
            if (damageInvulnerabilityCoroutine != null) StopCoroutine(damageInvulnerabilityCoroutine);
            damageInvulnerabilityCoroutine = StartCoroutine(DamageInvulnerabilityRoutine());
        }

        if (currentHealth <= 0) Die();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLifeStage();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El jugador ha recibido {damageAmount} de da�o. Vida actual: {currentHealth}/{maxHealth}", 1);
    }

    private IEnumerator DamageInvulnerabilityRoutine()
    {
        isDamageInvulnerable = true;
        ReportDebug($"El jugador es invulnerable por daño continuo durante {damageInvulnerabilityTime} segundos.", 1);

        float blinkInterval = 0.1f;
        float timer = 0f;

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
        ReportDebug("La invulnerabilidad por daño ha terminado.", 1);

        playerSpriteRenderer.color = Color.white;
    }

    // Funci�n que maneja el cooldown del bloqueo del escudo.
    private IEnumerator ShieldBlockCooldownRoutine()
    {
        ReportDebug($"El escudo bloqueara de nuevo en {shieldBlockCooldown} segundos.", 1);
        yield return new WaitForSeconds(shieldBlockCooldown);

        isShieldBlockReady = true;
        ReportDebug("El escudo esta listo para bloquear de nuevo.", 1);
    }

    /// <summary>
    /// Funci�n que cura al jugador.
    /// </summary>
    /// <param name="healAmount"> Cantidad de da�o a curar </param>
    public void Heal(float healAmount)
    {
        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        SyncCurrentHealthToSO();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        UpdateLifeStage();

        ReportDebug($"El jugador ha sido curado {healAmount}. Vida actual: {currentHealth}/{maxHealth}", 1);
    }

    /// <summary>
    /// Funci�n que actualiza la etapa de vida del jugador y notifica si ha cambiado.
    /// </summary>
    /// <param name="forceNotify"> Si es true, fuerza la notificaci�n del cambio de etapa incluso si no ha cambiado. </param>
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

            // Actualizar TextMeshPro si est� asignado
            if (lifeStageText != null)
            {
                // Mostrar solo el nombre de la etapa (sin texto adicional)
                lifeStageText.text = GetLifeStageString(CurrentLifeStage);
            }
        }
    }

    /// <summary>
    /// Ajusta la escala y la posición vertical del modelo del jugador según su etapa de vida.
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
        ReportDebug($"Modelo actualizado para la etapa {newStage}. Escala: {targetScale}, Posición Y: {targetYOffset}", 1);
    }

    // Traduce el enum de LifeStage a una cadena en español para mostrar en TMP.
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

    // Función que maneja la muerte del jugador.
    private void Die()
    {
        if (isDying) return;
        isDying = true;

        ReportDebug("El jugador ha muerto. Cargando escena: " + sceneToLoadOnDeath, 1);

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = false;
        if (playerShieldController != null) playerShieldController.enabled = false;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null) playerCollider.enabled = false;


        if (FadeController.Instance != null)
        {
            StartCoroutine(FadeController.Instance.FadeOut(
              fadeColor: deathFadeColor,
              onComplete: () =>
              {
                  statsManager.ResetRunStatsToDefaults();
                  statsManager.ResetStatsOnDeath();
                  inventoryManager.ClearInventory();
                  SceneManager.LoadScene(sceneToLoadOnDeath);
              }));
        }
        else
        {
            statsManager.ResetRunStatsToDefaults();
            statsManager.ResetStatsOnDeath();
            inventoryManager.ClearInventory();
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

    #region Debuffs

    /// <summary>
    /// Funci�n que aplica el efecto de veneno al jugador cuando es golpeado por un proyectil de Morlock.
    /// </summary>
    public void ApplyMorlockPoisonHit()
    {
        if (isDying) return;

        morlockHitCounter++;

        if (poisonResetCoroutine != null)
        {
            StopCoroutine(poisonResetCoroutine);
        }

        if (morlockHitCounter >= poisonHitThreshold)
        {
            float poisonDamage = poisonInitialDamage + (morlockHitCounter - poisonHitThreshold);
            TakeDamage(poisonDamage);
            // Aqu� podr�as iniciar un efecto de veneno que da�e con el tiempo
        }

        poisonResetCoroutine = StartCoroutine(ResetPoisonCounter());
    }

    private IEnumerator ResetPoisonCounter()
    {
        yield return new WaitForSeconds(poisonResetTime);
        morlockHitCounter = 0;
        poisonResetCoroutine = null;
    }

    #endregion

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funci�n de depuraci�n para reportar mensajes en la consola de Unity. 
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