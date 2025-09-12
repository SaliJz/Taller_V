

using UnityEngine;

public class Bruja_Projectile : MonoBehaviour
{
    public float speed = 12f;
    public int damage = 1;
    public float lifeTime = 5f;
    Vector3 direction;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void SetDirection(Vector3 dir)
    {
        direction = dir.normalized;
    }

    void FixedUpdate()
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.position += direction * speed * Time.fixedDeltaTime;
            transform.forward = direction;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // suponer que el player tiene tag "Player" y un script que maneja recibo de danio
        if (other.CompareTag("Player"))
        {
            //// intentar mandar mensaje al player
            //other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            Destroy(gameObject);
        }
        //else
        //{
        //    // si golpea algo solido, destruir
        //    if (!other.isTrigger)
        //    {
        //        Destroy(gameObject);
        //    }
        //}
    }
}
