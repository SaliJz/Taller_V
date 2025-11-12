using UnityEngine;

public class ProjectileDummy : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string shieldTag = "Escudo";
    [SerializeField] private string[] ignoreTags;
    [SerializeField] private float damage = 5f;
    [SerializeField] private float lifetime = 5f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void HandleCollision(GameObject collidedObject)
    {
        if (collidedObject.CompareTag("Player"))
        {
            collidedObject.GetComponent<PlayerHealth>()?.TakeDamage(damage);

            Destroy(gameObject);
        }
        else if (collidedObject.CompareTag(shieldTag))
        {
            ShieldHitManager.Instance?.RegisterShieldHit();

            Destroy(gameObject);
        }

        foreach (string tag in ignoreTags)
        {
            if (collidedObject.CompareTag(tag))
            {
                return;
            }
        }

        Destroy(gameObject);
    }
}
