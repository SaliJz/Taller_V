using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Clase que maneja la salud de los enemigos y su comportamiento al recibir daño.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health statistics")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float deathCooldown = 2f;

    [Header("Health Stealing Mechanics")]
    [SerializeField] private float healthSteal = 5;

    [Header("UI - Sliders (optional)")]
    [SerializeField] private Slider firstLifeSlider;
    [SerializeField] private Image firstFillImage;
    [SerializeField] private float offsetAboveEnemy = 2f;
    [SerializeField] private float glowDelayAfterCritical = 2f;

    private bool isDead = false;
    private Coroutine currentCriticalDamageCoroutine;

    public float CurrentHealth
    {
        get => currentHealth;
        private set
        {
            if (currentHealth != value)
            {
                currentHealth = value;
                OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }

    public float MaxHealth => maxHealth;
    public static event Action<float, float> OnEnemyHealthChanged;
    public event Action<GameObject> OnDeath;
    private EnemyVisualEffects enemyVisualEffects;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();

        if (enemyVisualEffects == null)
        {
            ReportDebug("Componente EnemyVisualEffects no encontrado en el enemigo.", 2);
        }
    }

    private void Start()
    {
        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        else playerTransform.TryGetComponent(out playerHealth);
    }

    private void OnDestroy()
    {
        if (isDead) StopAllCoroutines();
    }

    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = maxHealth;

        UpdateSlidersSafely();
    }

    public void TakeDamage(float damageAmount, bool isCritical = false)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Aquí puedes invocar otros eventos si los necesitas, como OnDamaged
        // OnDamaged?.Invoke(damageAmount, isCritical);

        if (currentHealth <= 0)
        {
            CurrentHealth = 0;
            Die();
        }

        UpdateSlidersSafely();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El enemigo 1 ha recibido {damageAmount} de daño. Vida actual: {currentHealth}/{maxHealth}", 1);

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayDamageFeedback(transform.position + Vector3.up * offsetAboveEnemy, damageAmount, isCritical);

            if (isCritical)
            {
                ReportDebug("El Blood Knight ha recibido daño crítico.", 1);

                enemyVisualEffects.StartArmorGlow();

                if (currentCriticalDamageCoroutine != null) StopCoroutine(currentCriticalDamageCoroutine);
                currentCriticalDamageCoroutine = StartCoroutine(StopGlowAfterDelay(glowDelayAfterCritical));
            }
        }
    }

    private IEnumerator StopGlowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StopArmorGlow();
        }
    }

    public void Heal(float healAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);

        ReportDebug($"{gameObject.name} ha sido curado por {healAmount}. Vida actual: {currentHealth}/{maxHealth}", 1);
    }

    private void Die()
    {
        isDead = true;

        ReportDebug($"{gameObject.name} ha muerto.", 1);
        OnDeath?.Invoke(gameObject);
        
        if (playerHealth != null)
        {
            playerHealth.Heal(healthSteal);
            ReportDebug($"El jugador ha robado {healthSteal} de vida al matar a {gameObject.name}.", 1);
        }

        Destroy(gameObject, deathCooldown);
    }

    private void UpdateSlidersSafely()
    {
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, maxHealth);
            firstLifeSlider.value = Mathf.Clamp(currentHealth, 0, maxHealth);
            if (!firstLifeSlider.gameObject.activeSelf) firstLifeSlider.gameObject.SetActive(true);
        }
        if (firstFillImage != null)
        {
            if (!firstFillImage.gameObject.activeSelf) firstFillImage.gameObject.SetActive(true);
            firstFillImage.color = Color.red;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.up * offsetAboveEnemy);
    }

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
                Debug.Log($"[EnemyHealth] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyHealth] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyHealth] {message}");
                break;
            default:
                Debug.Log($"[EnemyHealth] {message}");
                break;
        }
    }
}