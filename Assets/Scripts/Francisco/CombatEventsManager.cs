using UnityEngine;
using System;

public static class CombatEventsManager
{
    public static event Action<GameObject, bool> OnPlayerHitEnemy;
    public static event Action<GameObject, float> OnEnemyKilled;


    public static void TriggerPlayerHitEnemy(GameObject enemy, bool isMelee)
    {
        OnPlayerHitEnemy?.Invoke(enemy, isMelee);
    }

    public static void TriggerEnemyKilled(GameObject killedEnemy, float enemyMaxHealth)
    {
        OnEnemyKilled?.Invoke(killedEnemy, enemyMaxHealth);
    }
}