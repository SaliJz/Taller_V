//using UnityEngine;

public static class CriticalHitSystem
{
    public static float CriticalChance = 0.2f; // 20%
    public static float CriticalMultiplier = 2f; // Daño x2

    /// <summary>
    /// Calcula si un ataque es crítico y devuelve el daño final.
    /// </summary>
    public static float CalculateDamage(float baseDamage, out bool isCritical)
    {
        isCritical = UnityEngine.Random.value <= CriticalChance;
        return isCritical ? baseDamage * CriticalMultiplier : baseDamage;
    }
}