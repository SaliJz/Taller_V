using UnityEngine;

public class DummyShooter : MonoBehaviour
{
    [Header("Shoot Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float launchSpeed = 10f;
    [SerializeField] private float fireRate = 2.0f;

    [Header("Player Health Condition")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float stopShootingThreshold = 0.5f;

    private float nextFireTime;
    private PlayerHealth playerHealth;

    private void Start()
    {
        if (firePoint == null)
        {
            firePoint = transform;
        }
        nextFireTime = Time.time + fireRate;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError($"[DummyShooter] No se encontró el componente PlayerHealth en el objeto con tag '{playerTag}'.");
            }
        }
        else
        {
            Debug.LogError($"[DummyShooter] No se encontró ningún objeto con el tag '{playerTag}'.");
        }
    }

    private void Update()
    {
        if (ShouldStopShooting())
        {
            return;
        }

        if (Time.time > nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    private bool ShouldStopShooting()
    {
        if (playerHealth == null)
        {
            return false;
        }

        float healthRatio = playerHealth.CurrentHealth / playerHealth.MaxHealth;

        if (healthRatio <= stopShootingThreshold)
        {
            return true;
        }

        return false;
    }

    private void Shoot()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("[DummyShooter] ¡Falta asignar el Prefab del Proyectil!");
            return;
        }

        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        Rigidbody rb = projectileGO.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = -firePoint.forward * launchSpeed;
        }
    }
}