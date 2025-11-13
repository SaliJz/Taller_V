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
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerHealth>()?.ApplyMorlockPoisonHit(poisonResetTime, poisonInitialDamage, poisonHitThreshold);
            ExecuteAttack(other.gameObject, damage);

            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
            Destroy(gameObject);
        }
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(this.transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }
}