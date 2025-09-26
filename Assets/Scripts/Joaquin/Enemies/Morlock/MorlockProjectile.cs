using UnityEngine;

public class MorlockProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage;

    public void Initialize(float projectileSpeed, float projectileDamage)
    {
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
            other.GetComponent<PlayerHealth>()?.ApplyMorlockPoisonHit();
            other.GetComponent<PlayerHealth>()?.TakeDamage(damage);

            Destroy(gameObject);
        }
    }
}