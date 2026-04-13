using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SergioExperimentalWordLibrary))]
public class SergioExperimentalEnemyShooter : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform target;

    [Header("Disparo")]
    [SerializeField] private bool autoShoot = true;
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float shotInterval = 1.35f;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private Vector3 aimOffset = new Vector3(0f, 0.5f, 0f);

    private SergioExperimentalWordLibrary wordLibrary;
    private SergioProjectileWordTrail trailSystem;
    private Coroutine autoShootRoutine;

    private void Awake()
    {
        wordLibrary = GetComponent<SergioExperimentalWordLibrary>();
        
        // Si no asignaste el firePoint, lo buscamos
        if (firePoint == null) firePoint = transform.Find("FirePoint") ?? transform;

        // Buscamos el sistema de trail que DEBE estar en el FirePoint
        trailSystem = firePoint.GetComponent<SergioProjectileWordTrail>();
    }

    private void Start()
    {
        if (autoShoot) autoShootRoutine = StartCoroutine(AutoShootLoop());
    }

    public void Fire()
    {
        if (projectilePrefab == null || firePoint == null) return;

        Vector3 direction = (GetAimTargetPosition() - firePoint.position).normalized;
        string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "STATIC";

        // 1. Lanzar la bola roja
        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        SergioExperimentalProjectile projectile = projectileObject.GetComponent<SergioExperimentalProjectile>();
        if (projectile != null)
        {
            projectile.Launch(direction, projectileSpeed, projectileLifetime, GetComponent<Collider>());
        }

        // 2. Lanzar la estela de letras desde el FirePoint
        if (trailSystem != null)
        {
            trailSystem.SetupWord(selectedWord, direction, projectileSpeed, projectileLifetime);
        }
        else
        {
            Debug.LogWarning("No se encontró SergioProjectileWordTrail en el FirePoint. Las letras no saldrán.");
        }
    }

    private IEnumerator AutoShootLoop()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        while (enabled)
        {
            Fire();
            yield return new WaitForSeconds(shotInterval);
        }
    }

    private Vector3 GetAimTargetPosition() => (target != null || (target = Camera.main?.transform) != null) ? target.position + aimOffset : firePoint.position + transform.forward * 5f;
}