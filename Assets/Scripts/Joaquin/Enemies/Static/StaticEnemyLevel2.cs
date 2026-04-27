using UnityEngine;

public class StaticEnemyLevel2 : StaticEnemyBase
{
    [Header("Static Level 2 - Fixed Damage")]
    [SerializeField] private float fixedDamage = 15f;

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