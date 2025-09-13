// EnemyHealth.cs
using UnityEngine;

/// <summary>
/// Ejemplo de componente de vida que delega en IDamageable del mismo GameObject.
/// Útil si quieres separar lógica de salud de controlador.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 10;
    int current;
    public MeleeEnemyController controller;

    void Awake()
    {
        current = maxHealth;
        if (controller == null) controller = GetComponent<MeleeEnemyController>();
    }

    public void TakeDamage(int amount)
    {
        current -= amount;
        Debug.Log($"{name} recibió {amount} de daño. HP={current}/{maxHealth}");

        if (controller != null) controller.TakeDamage(amount);

        if (current <= 0)
        {
            Destroy(gameObject, 0.1f);
        }
    }
}
