using UnityEngine;
using System.Collections;

public class StaticEnemyLevel1 : StaticEnemyBase, IAnimEventHandler
{
    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 1;

    private float projectileDamage = 5.5f;

    protected override void Awake()
    {
        //LoadStatsFromSheet();
        base.Awake(); 
    }

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null) return;

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            EnemyToughness toughnessComp = GetComponent<EnemyToughness>();
            if (toughnessComp != null)
            {
                if (row.Superarmor > 0)
                {
                    toughnessComp.SetUseToughness(true);
                    toughnessComp.SetMaxToughness(row.Superarmor);
                }
                else
                {
                    toughnessComp.SetUseToughness(false);
                }
            }

            health = row.Health;
            moveSpeed = row.Movespeed;
            projectileDamage = row.Regulardamage;

            if (row.Attackfrequency > 0)
            {
                fireRate = 1f / row.Attackfrequency;
                minFireRate = fireRate * 0.8f;
                maxFireRate = fireRate * 1.2f;
            }

            Debug.Log($"[StaticLevel1] Cargado ID {ENEMY_ID}. HP: {health}, SA: {row.Superarmor}, FireRate: {fireRate}");
            return;
        }
    }

    protected override void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        var projectile = projectileObj.GetComponent<StaticProjectileBase>();

        if (projectile != null)
        {
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "STATIC";
            projectile.Initialize(projectileSpeed, projectileDamage, selectedWord);
        }
    }

    protected override IEnumerator ShootAfterDelayRoutine()
    {
        if (useRandomFireRate) fireRate = Random.Range(minFireRate, maxFireRate);

        yield return new WaitForSeconds(fireRate);

        if (!isDead && currentState != StaticState.Patrol && currentState != StaticState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                ForceFacePlayer();
                if (visualCtrl != null) visualCtrl.PlayShoot();
                //ExecuteProjectileSpawn();
            }
        }

        shootCoroutine = null;
    }

    public void HandleAnimEvents(string eventName)
    {
        if (eventName == "AnimEvent_Shoot") ExecuteProjectileSpawn();
    }
}