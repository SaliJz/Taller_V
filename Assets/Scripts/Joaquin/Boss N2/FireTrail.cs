using UnityEngine;

/// <summary>
/// Script para el rastro de fuego verde del ataque Sodoma y Gomorra.
/// </summary>
public class FireTrail : MonoBehaviour
{
    [SerializeField] private float damagePerSecond;
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float groundOffset = 0.01f;

    private float damageTimer = 0f;
    public float DamagePerSecond
    {
        get { return damagePerSecond; }
        set { damagePerSecond = value; }
    }

    public float Lifetime
    {
        get { return lifetime; }
        set { lifetime = value; }
    }

    private ParticleSystem fireParticles;

    private void Start()
    {
        AlignToGround();
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            damageTimer += Time.deltaTime;

            if (damageTimer >= 1f)
            {
                PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(DamagePerSecond);
                }

                damageTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Realiza un Raycast hacia abajo para alinear el objeto con el suelo.
    /// El suelo debe estar en la capa "Ground".
    /// </summary>
    private void AlignToGround()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 5f, Vector3.down); // lanza desde un poco más arriba por seguridad
        RaycastHit hit;

        int groundLayer = LayerMask.GetMask("Ground");

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
        {
            transform.position = hit.point + Vector3.up * groundOffset;
        }
        else
        {
            Debug.LogWarning("[FireTrail] No se encontró suelo debajo del objeto. Revisa la capa 'Ground'.");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 5f, Vector3.down * 10f);
    }
}