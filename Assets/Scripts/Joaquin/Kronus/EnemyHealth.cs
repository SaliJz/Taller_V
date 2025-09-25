using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Clase que maneja la salud de los enemigos y su comportamiento al recibir daño.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float deathCooldown = 2f;

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

    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = maxHealth;
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

        ReportDebug($"{gameObject.name} ha recibido {damageAmount} de daño. Vida actual: {currentHealth}/{maxHealth}", 1);
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
        ReportDebug($"{gameObject.name} ha muerto.", 1);
        OnDeath?.Invoke(gameObject);

        Destroy(gameObject, deathCooldown);
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