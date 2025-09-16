using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats PlayerStats;

    [Header("Configuraci�n de Vida")]
    [SerializeField] private float maxHealth = 100;
    [SerializeField] private float currentHealth;

    public static event Action<float, float> OnHealthChanged;

    void Start()
    {
        if (PlayerStats != null)
        {
            maxHealth = PlayerStats.maxHealth;
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float healAmount)
    {
        currentHealth += healAmount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        Debug.Log("El jugador ha muerto.");
    }
}