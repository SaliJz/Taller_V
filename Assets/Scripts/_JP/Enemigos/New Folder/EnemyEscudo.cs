// VidaEnemigoEscudo.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VidaEnemigoEscudo: maneja vida, curacion, UI (slider), reducción local de daño y muerte.
/// Implementa la interfaz IDamageable ya existente en el proyecto.
/// </summary>
[DisallowMultipleComponent]
public class VidaEnemigoEscudo : MonoBehaviour, IDamageable
{
    [Header("Health / UI")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = -1f; // si es <=0 se inicializa a maxHealth en Awake
    [SerializeField] private float deathCooldown = 2f;
    [SerializeField] private float healthSteal = 5f;

    [Header("UI - Slider (opcional)")]
    [SerializeField] private Slider lifeSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float offsetAboveEnemy = 2f;
    [SerializeField] private float glowDelayAfterCritical = 2f;

    // Propiedades requeridas por la interfaz IDamageable
    public float CurrentHealth
    {
        get => currentHealth;
        private set
        {
            if (!Mathf.Approximately(currentHealth, value))
            {
                currentHealth = value;
                OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }
    public float MaxHealth => maxHealth;

    public static event Action<float, float> OnEnemyHealthChanged;
    public event Action<GameObject> OnDeath;

    private bool isDead = false;
    private Coroutine currentCriticalDamageCoroutine;
    private EnemyVisualEffects enemyVisualEffects;

    // Reduccion local que aplica al TakeDamage (cuando la propia IA activa su armadura)
    private float _reduccionLocal = 0f;

    private void Awake()
    {
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        currentHealth = Mathf.Clamp(currentHealth > 0f ? currentHealth : maxHealth, 0f, maxHealth);
        UpdateSlidersSafely();
    }

    #region IDamageable / Salud
    // Implementa TakeDamage para ser usado por otros componentes
    public void TakeDamage(float damageAmount, bool isCritical = false)
    {
        if (isDead) return;
        if (currentHealth <= 0f) return;

        float cantidad = damageAmount;

        // aplicar reducción local (si existe)
        if (_reduccionLocal > 0f)
        {
            cantidad = cantidad * (1f - _reduccionLocal);
        }

        currentHealth -= cantidad;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // actualizar propiedad y UI
        CurrentHealth = currentHealth;
        UpdateSlidersSafely();

        // feedback visual / sonido
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayDamageFeedback(transform.position + Vector3.up * offsetAboveEnemy, cantidad, isCritical);

            if (isCritical)
            {
                enemyVisualEffects.StartArmorGlow();

                if (currentCriticalDamageCoroutine != null) StopCoroutine(currentCriticalDamageCoroutine);
                currentCriticalDamageCoroutine = StartCoroutine(StopGlowAfterDelay(glowDelayAfterCritical));
            }
        }

        if (currentHealth <= 0f)
        {
            CurrentHealth = 0f;
            Die();
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
        if (isDead) return;
        if (currentHealth <= 0f) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        CurrentHealth = currentHealth;
        UpdateSlidersSafely();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        OnDeath?.Invoke(gameObject);

        // intentar curar al jugador si existe PlayerHealth
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent<PlayerHealth>(out var ph))
        {
            ph.Heal(healthSteal);
        }

        Destroy(gameObject, deathCooldown);
    }
    #endregion

    #region UI y util
    private void UpdateSlidersSafely()
    {
        if (lifeSlider != null)
        {
            lifeSlider.maxValue = Mathf.Max(1f, maxHealth);
            lifeSlider.value = Mathf.Clamp(currentHealth, 0f, maxHealth);
            if (!lifeSlider.gameObject.activeSelf) lifeSlider.gameObject.SetActive(true);
        }
        if (fillImage != null)
        {
            if (!fillImage.gameObject.activeSelf) fillImage.gameObject.SetActive(true);
            fillImage.color = Color.red;
        }
    }
    #endregion

    #region Reduccion local (expuesto para que la IA active la armadura)
    /// <summary>
    /// Aplica una reduccion de danio local durante 'duracion' segundos.
    /// La IA debe llamar a este metodo en la propia unidad cuando activa su armadura.
    /// </summary>
    public void ApplyDamageReduction(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f) return;
        StartCoroutine(RutinaReduccionLocal(percent, duration));
    }

    private IEnumerator RutinaReduccionLocal(float percent, float duration)
    {
        _reduccionLocal = percent;
        yield return new WaitForSeconds(duration);
        _reduccionLocal = 0f;
    }
    #endregion
}







