// EnemyHealth.cs
using UnityEngine;

/// <summary>
/// Ejemplo de componente de vida que delega en IDamageable del mismo GameObject.
/// �til si quieres separar l�gica de salud de controlador.
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
        Debug.Log($"{name} recibi� {amount} de da�o. HP={current}/{maxHealth}");

        if (controller != null) controller.TakeDamage(amount);

        if (current <= 0)
        {
            Destroy(gameObject, 0.1f);
        }
    }
}
