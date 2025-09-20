using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Clase que maneja la salud del jugador, incluyendo da�o, curaci�n y etapas de vida.
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

    [Header("Configuraci�n de Vida")]
    [Tooltip("Vida m�xima por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackMaxHealth = 100;
    [SerializeField] private float currentHealth;

    [Header("Mejora de Escudo")]
    [SerializeField] private float shieldBlockCooldown = 18f;
    private bool isShieldBlockReady = true;

    public bool HasShieldBlockUpgrade { get; private set; } = false;
    public bool IsInvulnerable { get; set; } = false;
    public LifeStage CurrentLifeStage { get; private set; }

    public static event Action<float, float> OnHealthChanged;
    public static event Action<LifeStage> OnLifeStageChanged;

    private PlayerMovement playerMovement;
    private PlayerMeleeAttack playerMeleeAttack;
    private PlayerShieldController playerShieldController;

    private void Awake()
    {
        // Se asegura de que la referencia se obtenga antes de que se ejecute cualquier Start()
        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no est� asignado en PlayerHealth. Usando vida m�xima de fallback.", 2);

        playerMovement = GetComponent<PlayerMovement>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();

        // Suscripci�n al evento en Awake para que siempre est� listo
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void Start()
    {
        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLifeStage(true);
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
            float oldMaxHealth = Mathf.Max(1, statsManager.GetStat(StatType.MaxHealth));
            float percentage = currentHealth / oldMaxHealth;

            currentHealth = Mathf.Clamp(newValue * percentage, 0, newValue);
            OnHealthChanged?.Invoke(currentHealth, newValue);

            UpdateLifeStage();

            ReportDebug($"Nueva vida m�xima: {newValue}, vida actual ajustada a {currentHealth}", 1);
        }
    }

    /// <summary>
    /// Funci�n que aplica da�o al jugador.
    /// </summary>
    /// <param name="damageAmount"> Cantidad de da�o a aplicar. </param>
    public void TakeDamage(float damageAmount)
    {
        if (IsInvulnerable)
        {
            ReportDebug("El jugador es invulnerable y no recibe da�o.", 1);
            return;
        }

        if (HasShieldBlockUpgrade)
        {
            if (isShieldBlockReady)
            {
                isShieldBlockReady = false; // El bloqueo se consume
                ReportDebug("El escudo ha bloqueado el da�o entrante.", 1);

                StartCoroutine(ShieldBlockCooldownRoutine());
                return;
            }
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0) Die();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        UpdateLifeStage();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El jugador ha recibido {damageAmount} de da�o. Vida actual: {currentHealth}/{maxHealth}", 1);
    }

    // Funci�n que maneja el cooldown del bloqueo del escudo.
    private IEnumerator ShieldBlockCooldownRoutine()
    {
        ReportDebug($"El escudo bloquear� de nuevo en {shieldBlockCooldown} segundos.", 1);
        yield return new WaitForSeconds(shieldBlockCooldown);

        isShieldBlockReady = true;
        ReportDebug("El escudo est� listo para bloquear de nuevo.", 1);
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
        }
    }

    // Funci�n que maneja la muerte del jugador.
    private void Die()
    {
        ReportDebug("El jugador ha muerto.", 1);
        statsManager.ResetStats();

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = false;
        if (playerShieldController != null) playerShieldController.enabled = false;

        if (FadeController.Instance != null)
        {
            StartCoroutine(FadeController.Instance.FadeOut(onComplete: () => {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }));
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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


    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funci�n de depuraci�n para reportar mensajes en la consola de Unity. 
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