//using UnityEngine;

using UnityEngine;

public static class CriticalHitSystem
{
    [Tooltip("Probabilidad de crítico al atacar por la espalda.")]
    public static float BackCriticalChance = 0.25f; // Probabilidad alta de 25 %

    [Tooltip("Probabilidad de crítico al atacar de frente. Puede ser 0.")]
    public static float FrontalCriticalChance = 0.05f; // Probabilidad baja de 5 %

    public static float CriticalMultiplier = 2f; // Daño crítico x2

    /// <summary>
    /// Calcula el daño basado en la posición relativa del atacante y el objetivo.
    /// </summary>
    /// <param name="baseDamage">Daño base del ataque.</param>
    /// <param name="attacker">El Transform de quien ataca.</param>
    /// <param name="target">El Transform de quien recibe el ataque.</param>
    /// <param name="isCritical">Devuelve true si el golpe fue crítico.</param>
    /// <returns>El daño final calculado.</returns>
    public static float CalculateDamage(float baseDamage, Transform attacker, Transform target, out bool isCritical)
    {
        Vector3 attackDirection = (attacker.position - target.position).normalized;

        attackDirection.y = 0;
        Vector3 targetForward = target.forward;
        targetForward.y = 0;

        float dotProduct = Vector3.Dot(targetForward, attackDirection);

        float currentCritChance;
        if (dotProduct < 0) // Si el resultado es negativo, el ataque es por la espalda
        {
            currentCritChance = BackCriticalChance;
            ReportDebug("Ataque por la espalda detectado.", 1);
        }
        else // Si es positivo o cero, es frontal o lateral
        {
            currentCritChance = FrontalCriticalChance;
            ReportDebug("Ataque frontal detectado.", 1);
        }

        isCritical = UnityEngine.Random.value <= currentCritChance;
        return isCritical ? baseDamage * CriticalMultiplier : baseDamage;
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
                Debug.Log($"[CriticalHitSystem] {message}");
                break;
            case 2:
                Debug.LogWarning($"[CriticalHitSystem] {message}");
                break;
            case 3:
                Debug.LogError($"[CriticalHitSystem] {message}");
                break;
            default:
                Debug.Log($"[CriticalHitSystem] {message}");
                break;
        }
    }
}