using UnityEngine;

public class StaticProjectileLevel1 : StaticProjectileBase
{
    protected override void OnPlayerHit(GameObject player)
    {
        hasImpacted = true;
        player.GetComponent<IDamageable>()?.TakeDamage(damage);
        if (proyectileImpactVFX != null) Instantiate(proyectileImpactVFX, transform.position, proyectileImpactVFX.transform.rotation);
        Destroy(gameObject);
    }

    protected override void OnEnvironmentHit(GameObject obstacle)
    {
        hasImpacted = true;
        if (proyectileImpactVFX != null) Instantiate(proyectileImpactVFX, transform.position,  proyectileImpactVFX.transform.rotation);
        Destroy(gameObject);
    }
}