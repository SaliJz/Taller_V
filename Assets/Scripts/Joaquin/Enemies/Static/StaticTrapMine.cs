using UnityEngine;

public class StaticTrapMine : MonoBehaviour
{
    [Header("Mine Stats")]
    [SerializeField] private float duration = 5f;
    [SerializeField] private float explosionRadius = 1.8f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float slowDuration = 0.5f;
    [SerializeField] private float slowFraction = 0.1f;

    [Header("Collision Layers")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask environmentLayer;

    [Header("VFX")]
    [SerializeField] private ParticleSystem explosionVFXPrefab;
    [SerializeField] private GameObject explosionSpherePrefab;

    private bool hasExploded = false;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void InitializeTrap(string wordToDisplay)
    {
        rb.useGravity = true;
        rb.isKinematic = false;

        Vector3 randomBounce = new Vector3(Random.Range(-2f, 2f), 3f, Random.Range(-2f, 2f));
        rb.AddForce(randomBounce, ForceMode.Impulse);

        Invoke(nameof(Explode), duration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if ((environmentLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            rb.linearDamping = 3f;
            rb.angularDamping = 3f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;

        if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        hasExploded = true;
        CancelInvoke(nameof(Explode));

        if (explosionVFXPrefab != null) Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
        if (explosionSpherePrefab != null) Instantiate(explosionSpherePrefab, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, playerLayer);
        foreach (var hit in hits)
        {
            hit.GetComponent<IDamageable>()?.TakeDamage(damage);

            PlayerStatsManager statsManager = hit.GetComponent<PlayerStatsManager>();
            if (statsManager != null)
            {
                string slowKey = "MineSlow_" + GetInstanceID();
                statsManager.ApplyTimedModifier(slowKey, StatType.MoveSpeed, -slowFraction, slowDuration, isPercentage: true);
            }
        }

        Destroy(gameObject);
    }
}