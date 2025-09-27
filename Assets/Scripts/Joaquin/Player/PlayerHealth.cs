using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Clase que maneja la salud del jugador, incluyendo daño, curación y etapas de vida.
/// </summary>
public class PlayerHealth : MonoBehaviour
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
    [Tooltip("Vida máxima por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackMaxHealth = 100;
    [SerializeField] private float currentHealth;
    [SerializeField] private float damageInvulnerabilityTime = 0.5f;

    [Header("Configuración de Muerte")]
    [SerializeField] private string sceneToLoadOnDeath = "Tuto";
    [SerializeField] private Color deathFadeColor = Color.red;


    [Header("Mejora de Escudo")]
    [SerializeField] private float shieldBlockCooldown = 18f;
    private bool isShieldBlockReady = true;

    [Header("Veneno de Morlock")]
    [SerializeField] private MorlockStats morlockStats;
    [SerializeField] private int poisonHitThreshold = 3;
    [SerializeField] private float poisonInitialDamage = 2;
    [SerializeField] private float poisonResetTime = 5;
    private int morlockHitCounter = 0;

    public bool HasShieldBlockUpgrade { get; private set; } = false;
    public bool IsInvulnerable { get; set; } = false;
    private bool isDamageInvulnerable = false;
    public LifeStage CurrentLifeStage { get; private set; }

    private bool isDying = false;

    public static event Action<float, float> OnHealthChanged;
    public static event Action<LifeStage> OnLifeStageChanged;

    private PlayerMovement playerMovement;
    private PlayerMeleeAttack playerMeleeAttack;
    private PlayerShieldController playerShieldController;
    private Coroutine damageInvulnerabilityCoroutine;
    private Coroutine poisonResetCoroutine;

    public float MaxHealth => statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerHealth. Usando vida máxima de fallback.", 2);

        playerMovement = GetComponent<PlayerMovement>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();
    }

    private void Start()
    {
        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLifeStage(true);
        InitializedPosionDebuff();
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
            ReportDebug("MorlockStats no está asignado en PlayerHealth. Usando valores de veneno por defecto.", 2);
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
    /// <param name="statType">Tipo de estadística que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estadística.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.MaxHealth)
        {
            float oldMaxHealth = Mathf.Max(1, statsManager.GetStat(StatType.MaxHealth));
            float percentage = currentHealth / oldMaxHealth;

            currentHealth = Mathf.Clamp(newValue * percentage, 0, newValue);
            OnHealthChanged?.Invoke(currentHealth, newValue);

            UpdateLifeStage();

            ReportDebug($"Nueva vida máxima: {newValue}, vida actual ajustada a {currentHealth}", 1);
        }
    }

    /// <summary>
    /// Función que aplica daño al jugador.
    /// </summary>
    /// <param name="damageAmount"> Cantidad de daño a aplicar. </param>
    public void TakeDamage(float damageAmount)
    {
        if (isDying) return;

        if (isDamageInvulnerable || IsInvulnerable)
        {
            ReportDebug("El jugador es invulnerable y no recibe daño.", 1);
            return;
        }

        if (HasShieldBlockUpgrade)
        {
            if (isShieldBlockReady)
            {
                isShieldBlockReady = false; // El bloqueo se consume
                ReportDebug("El escudo ha bloqueado el daño entrante.", 1);

                StartCoroutine(ShieldBlockCooldownRoutine());
                return;
            }
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        isDamageInvulnerable = true;
        if (damageInvulnerabilityCoroutine != null) StopCoroutine(damageInvulnerabilityCoroutine);
        damageInvulnerabilityCoroutine = StartCoroutine(DamageInvulnerabilityRoutine());

        if (currentHealth <= 0) Die();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLifeStage();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El jugador ha recibido {damageAmount} de daño. Vida actual: {currentHealth}/{maxHealth}", 1);
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

    // Función que maneja el cooldown del bloqueo del escudo.
    private IEnumerator ShieldBlockCooldownRoutine()
    {
        ReportDebug($"El escudo bloqueará de nuevo en {shieldBlockCooldown} segundos.", 1);
        yield return new WaitForSeconds(shieldBlockCooldown);

        isShieldBlockReady = true;
        ReportDebug("El escudo está listo para bloquear de nuevo.", 1);
    }

    /// <summary>
    /// Función que cura al jugador.
    /// </summary>
    /// <param name="healAmount"> Cantidad de daño a curar </param>
    public void Heal(float healAmount)
    {
        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        UpdateLifeStage();

        ReportDebug($"El jugador ha sido curado {healAmount}. Vida actual: {currentHealth}/{maxHealth}", 1);
    }

    /// <summary>
    /// Función que actualiza la etapa de vida del jugador y notifica si ha cambiado.
    /// </summary>
    /// <param name="forceNotify"> Si es true, fuerza la notificación del cambio de etapa incluso si no ha cambiado. </param>
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
        }
    }

    // Función que maneja la muerte del jugador.
    private void Die()
    {
        if (isDying) return;
        isDying = true;

        ReportDebug("El jugador ha muerto. Recargando escena: " + sceneToLoadOnDeath, 1);
        statsManager.ResetStats();

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = false;
        if (playerShieldController != null) playerShieldController.enabled = false;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null) playerCollider.enabled = false;


        if (FadeController.Instance != null)
        {
            StartCoroutine(FadeController.Instance.FadeOut(
                fadeColor: deathFadeColor,
                onComplete: () => {
                    SceneManager.LoadScene(sceneToLoadOnDeath);
                }));
        }
        else
        {
            SceneManager.LoadScene(sceneToLoadOnDeath);
        }
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public void EnableShieldBlockUpgrade()
    {
        HasShieldBlockUpgrade = true;
        ReportDebug("La mejora de bloqueo de escudo ha sido activada.", 1);
    }

    public void DisableShieldBlockUpgrade()
    {
        HasShieldBlockUpgrade = false;
        ReportDebug("La mejora de bloqueo de escudo ha sido desactivada.", 1);
    }

    #region Debuffs

    /// <summary>
    /// Función que aplica el efecto de veneno al jugador cuando es golpeado por un proyectil de Morlock.
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
            // Aquí podrías iniciar un efecto de veneno que dañe con el tiempo
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
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
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