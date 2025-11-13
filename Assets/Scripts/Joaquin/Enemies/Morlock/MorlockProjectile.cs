using UnityEngine;

public class MorlockProjectile : MonoBehaviour
{
    [SerializeField] private MorlockStats morlockStats;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clip;

    [Header("Projectile Stats")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 2f;

    [Header("Poison Effect")]
    [SerializeField] private int poisonHitThreshold = 3;
    [SerializeField] private float poisonInitialDamage = 2f;
    [SerializeField] private float poisonResetTime = 5f;

    private Vector3 direction;
    private bool wasReflected = false;

    [SerializeField] private bool debugMode = false;

    public void Initialize(float projectileSpeed, float projectileDamage)
    {
        if (morlockStats != null)
        {
            poisonHitThreshold = morlockStats.poisonHitThreshold;
            poisonInitialDamage = morlockStats.poisonInitialDamage;
            poisonResetTime = morlockStats.poisonResetTime;
        }
        else
        {
            if (debugMode) Debug.LogWarning("MorlockStats no esta asignado en MorlockProjectile. Usando valores de veneno por defecto.");
        }

        speed = projectileSpeed;
        damage = projectileDamage;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (wasReflected && other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyHealth>()?.TakeDamage(damage);
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Player"))
        {
            bool wasBlocked = ExecuteAttack(other.gameObject, damage);

            if (wasReflected)
            {
                Debug.Log("[Projectile] Bloqueado y Reflejado. No se destruye.");
                if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
                return;
            }

            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);

            Destroy(gameObject);
        }
    }

    private bool ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) &&
            target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(this.transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount, this.gameObject);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                return true;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
            return false;
        }

        return false;
    }

    public void Redirect(Vector3 newDirection)
    {
        direction = newDirection.normalized;
        transform.forward = direction;
        wasReflected = true;

        Debug.Log($"[Projectile] Reflejado hacia {newDirection}");
    }

    public bool WasReflected => wasReflected;
}