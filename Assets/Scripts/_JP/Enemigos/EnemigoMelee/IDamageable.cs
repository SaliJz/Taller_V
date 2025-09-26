using System;
using UnityEngine;

/// <summary>
/// Define el contrato para cualquier objeto en el juego que pueda recibir daño,
/// tener vida y morir.
/// </summary>
public interface IDamageable
{
    public float CurrentHealth { get; }
    public float MaxHealth { get; }

    /// <summary>
    /// Método universal para aplicar daño.
    /// </summary>
    /// <param name="damageAmount">La cantidad de daño a aplicar.</param>
    /// <param name="isCritical">Indica si el daño es crítico (para efectos visuales/sonoros).</param>
    public void TakeDamage(float damageAmount, bool isCritical = false);
}