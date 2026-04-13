using UnityEngine;

[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class SergioExperimentalProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private bool destroyOnAnyCollision = true;
    [SerializeField] private LayerMask collisionLayers = -1;

    private Vector3 direction = Vector3.forward;
    private float elapsedLifetime;
    private Collider ownerCollider;

    private void Awake()
    {
        if (TryGetComponent(out Rigidbody body))
        {
            body.useGravity = false;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    public void Launch(Vector3 projectileDirection, float projectileSpeed, float projectileLifetime, Collider ignoredOwner = null)
    {
        direction = projectileDirection.sqrMagnitude > 0.0001f ? projectileDirection.normalized : transform.forward;
        speed = projectileSpeed;
        lifetime = projectileLifetime;
        elapsedLifetime = 0f;
        ownerCollider = ignoredOwner;

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (ownerCollider != null && TryGetComponent(out Collider projectileCollider))
        {
            Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
        }
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
        elapsedLifetime += Time.deltaTime;
        if (elapsedLifetime >= lifetime) Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!ShouldDestroy(other)) return;
        Destroy(gameObject);
    }

    private bool ShouldDestroy(Collider other)
    {
        if (other == null) return false;
        if (ownerCollider != null)
        {
            if (other == ownerCollider || other.transform.IsChildOf(ownerCollider.transform.root)) return false;
        }
        if (other.isTrigger) return false;
        if (destroyOnAnyCollision) return true;
        return (collisionLayers.value & (1 << other.gameObject.layer)) != 0;
    }
}