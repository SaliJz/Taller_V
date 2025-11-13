using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class ExplosionDamage : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damage = 8;
    [SerializeField] private float knockbackForce = 3f;
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool damageOnStart = true;

    private SphereCollider sphereCollider;
    private bool hasDealtDamage = false;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
    }

    private void Start()
    {
        if (damageOnStart)
        {
            DealDamage();
        }
    }

    public void DealDamage()
    {
        if (hasDealtDamage) return;
        hasDealtDamage = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, sphereCollider.radius, enemyLayer);

        foreach (Collider col in hits)
        {
            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, AttackDamageType.Melee, transform.position);
            }

            Vector3 knockbackDir = (col.transform.position - transform.position).normalized;
            knockbackDir.y = 0;

            EnemyKnockbackHandler knockback = col.GetComponent<EnemyKnockbackHandler>();
            if (knockback != null)
            {
                knockback.TriggerKnockback(knockbackDir, knockbackForce, knockbackDuration);
            }

            if (col.CompareTag("EnemyProjectile"))
            {
                Destroy(col.gameObject);
            }
        }
    }

    public void SetDamage(int newDamage) => damage = newDamage;
    public void SetKnockback(float force, float duration)
    {
        knockbackForce = force;
        knockbackDuration = duration;
    }
}