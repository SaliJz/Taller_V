using UnityEngine;

public class HazardTest : MonoBehaviour
{
    [SerializeField] private float damagePerSecond = 10f;

    private void OnTriggerStay(Collider other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damagePerSecond * Time.deltaTime);
        }
    }
}