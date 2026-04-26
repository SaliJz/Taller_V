using UnityEngine;

public class GlassShardDamage : MonoBehaviour
{
    [HideInInspector] public float damagePerSecond = 4f;
    [HideInInspector] public LayerMask playerLayer;

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        if (other.TryGetComponent<PlayerHealth>(out var pHealth))
            pHealth.TakeDamage(damagePerSecond * Time.deltaTime);
    }
}