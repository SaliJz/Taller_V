// AttackHitbox.cs
using UnityEngine;

/// <summary>
/// Hitbox simple que aplica daño cuando está activa.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    public int damage = 1;
    public float life = 0.5f; // tiempo máximo antes de autodestruirse
    bool activeDamage = false;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }

    public void SetActiveDamage(bool on)
    {
        activeDamage = on;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!activeDamage) return;
        if (other == null || other.gameObject == null) return;

        var dmg = other.GetComponent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damage);
            return;
        }

        // fallback por nombre común
        var ph = other.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(damage);
            return;
        }

        if (other.CompareTag("Player"))
        {
            other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
    }
}
