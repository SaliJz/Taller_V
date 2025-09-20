//using UnityEngine;

public static class CriticalHitSystem
{
    public static float CriticalChance = 0.2f; // 20%
    public static float CriticalMultiplier = 2f; // Da�o x2

    /// <summary>
    /// Calcula si un ataque es cr�tico y devuelve el da�o final.
    /// </summary>
    public static float CalculateDamage(float baseDamage, out bool isCritical)
    {
        isCritical = UnityEngine.Random.value <= CriticalChance;
        return isCritical ? baseDamage * CriticalMultiplier : baseDamage;
    }
}