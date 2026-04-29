using UnityEngine;
using System.Collections;

public class StaticEnemyLevel2 : StaticEnemyBase
{
    [Header("Static Level 2 - Fixed Damage")]
    [SerializeField] private float fixedDamage = 15f;

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

    protected override void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        StaticProjectileBase projectile = projectileObj.GetComponent<StaticProjectileBase>();

        if (projectile != null)
        {
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "TRAP";
            projectile.Initialize(projectileSpeed, fixedDamage, selectedWord);
        }
    }
}