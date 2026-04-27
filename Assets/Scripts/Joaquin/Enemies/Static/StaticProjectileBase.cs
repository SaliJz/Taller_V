using UnityEngine;

public abstract class StaticProjectileBase : MonoBehaviour
{
    [Header("Base Projectile Stats")]
    [SerializeField] protected float speed = 20f;
    [SerializeField] protected float damage = 1f;
    [SerializeField] protected float lifetime = 5f;

    [Header("Collision Layers")]
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask environmentLayer;

    protected MorlockProjectileWordTrail wordTrail;
    protected Rigidbody rb;
    protected Vector3 originPosition;
    protected bool hasImpacted = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wordTrail = GetComponent<MorlockProjectileWordTrail>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        originPosition = transform.position;
    }

    public virtual void Initialize(float pSpeed, float pDamage, string pWord)
    {
        speed = pSpeed;
        damage = pDamage;
        if (wordTrail != null) wordTrail.InitializeWord(pWord);
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (hasImpacted) return;

        if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            OnPlayerHit(other.gameObject);
        }
        else if ((environmentLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            OnEnvironmentHit(other.gameObject);
        }
    }

    protected abstract void OnPlayerHit(GameObject player);
    protected abstract void OnEnvironmentHit(GameObject obstacle);
}