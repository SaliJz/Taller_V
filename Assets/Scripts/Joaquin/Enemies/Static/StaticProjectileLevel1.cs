using UnityEngine;

public class StaticProjectileLevel1 : StaticProjectileBase
{
    protected override void OnPlayerHit(GameObject player)
    {
        hasImpacted = true;
        player.GetComponent<IDamageable>()?.TakeDamage(damage);
        Destroy(gameObject);
    }

    protected override void OnEnvironmentHit(GameObject obstacle)
    {
        hasImpacted = true;
        Destroy(gameObject);
    }
}