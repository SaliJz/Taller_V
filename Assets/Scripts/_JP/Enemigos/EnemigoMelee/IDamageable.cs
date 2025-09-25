using System;
using UnityEngine;

/// <summary>
/// Define el contrato para cualquier objeto en el juego que pueda recibir da�o,
/// tener vida y morir.
/// </summary>
public interface IDamageable
{
    public float CurrentHealth { get; }
    public float MaxHealth { get; }

    // Evento que se dispara cuando la entidad muere.
    // Env�a el GameObject para que otros sistemas sepan qui�n muri�.
    public event Action<GameObject> OnDeath;

    /// <summary>
    /// M�todo universal para aplicar da�o.
    /// </summary>
    /// <param name="damageAmount">La cantidad de da�o a aplicar.</param>
    /// <param name="isCritical">Indica si el da�o es cr�tico (para efectos visuales/sonoros).</param>
    public void TakeDamage(float damageAmount, bool isCritical = false);
}