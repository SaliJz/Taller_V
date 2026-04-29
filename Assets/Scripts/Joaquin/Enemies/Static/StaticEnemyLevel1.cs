using UnityEngine;
using System.Collections;

public class StaticEnemyLevel1 : StaticEnemyBase
{
    protected override IEnumerator ShootAfterDelayRoutine()
    {
        if (useRandomFireRate) fireRate = Random.Range(minFireRate, maxFireRate);

        yield return new WaitForSeconds(fireRate);

        if (!isDead && currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                ForceFacePlayer();
                if (visualCtrl != null) visualCtrl.PlayShoot();
                ExecuteProjectileSpawn();
            }
        }

        shootCoroutine = null;
    }

    //public void HandleAnimEvents(string eventName)
    //{
    //    if (eventName == "AnimEvent_Shoot") ExecuteProjectileSpawn();
    //}
}