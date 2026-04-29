using UnityEngine;

public class GlassShardDamage : MonoBehaviour
{
    [HideInInspector] public float damagePerSecond = 4f;
    [HideInInspector] public LayerMask playerLayer;
    [HideInInspector] public float shardDeathDuration = 4f;
    [SerializeField] private bool effectDeath = false; 

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        if (other.TryGetComponent<PlayerHealth>(out var pHealth))
        {
            pHealth.TakeDamage(damagePerSecond * Time.deltaTime);
            if (effectDeath)       
            {
                Vector3 dir = (other.transform.position - transform.position).normalized;
                dir.y = 0;
                pHealth.ApplyKnockback(dir, 4, 1f);
            }
        }
    }

    private void Start()
    {
        Destroy(gameObject, shardDeathDuration);
    }
}