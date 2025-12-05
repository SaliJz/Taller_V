using UnityEngine;

public class BlockCounterProjectile : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private GameObject impactVFX;

    private float damageToDeal;
    private bool isInitialized = false;
    private Vector3 direction;

    public void Initialize(float damage, Vector3 fireDirection)
    {
        this.damageToDeal = damage;
        this.direction = fireDirection.normalized;
        this.isInitialized = true;

        transform.forward = this.direction;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!isInitialized) return;

        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;

        if (other.CompareTag("Player") || other.isTrigger) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("Wall")) Destroy(gameObject);

        var damageable = other.GetComponent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damageAmount: damageToDeal, attackDamageType: AttackDamageType.Ranged);
        }

        if (impactVFX != null)
        {
            Instantiate(impactVFX, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}