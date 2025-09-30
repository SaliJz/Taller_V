using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyHealth))]
public class VeynarEnemy : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private SimplePool hivePool;
    [SerializeField] private int maxActiveHives = 5;
    [SerializeField] private float hiveSpawnInterval = 2.5f;
    [SerializeField] private float hiveSpawnRadius = 3f;

    [Header("Behavior")]
    [SerializeField] private float teleportAlertRange = 7f;
    [SerializeField] private float teleportDelayIfPlayerClose = 3f;
    [SerializeField] private float teleportRange = 10f;

    [Header("Visuals & Effects")]
    [Tooltip("El material normal y opaco del enemigo.")]
    [SerializeField] private Material normalMaterial;
    [Tooltip("El material translúcido para cuando se oculta.")]
    [SerializeField] private Material transparentMaterial;

    [Header("Limits & Pools")]
    [SerializeField] private SimplePool larvaPool;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnHiveSFX;
    [SerializeField] private AudioClip teleportSFX;

    private EnemyHealth enemyHealth;
    private Transform playerTransform;
    private bool isHidden = false;
    private Coroutine spawnRoutine;
    private Coroutine teleportWatchRoutine;

    private List<Hive> activeHives = new List<Hive>();
    private Renderer[] allRenderers;
    private NavMeshAgent navAgent;
    private MaterialPropertyBlock mpb;

    public bool IsDead => enemyHealth != null && enemyHealth.IsDead;
    public Transform PlayerTransform => playerTransform;
    public int ActiveHiveCount => activeHives.Count;

    private float lastTeleportTime = -999f;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        allRenderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (hivePool == null || larvaPool == null)
        {
            Debug.LogError($"Pools no asignados en {gameObject.name}. Veynar no funcionará correctamente.", this);
            enabled = false;
            return;
        }

        spawnRoutine = StartCoroutine(HiveSpawnRoutine());
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        StopAllCoroutines();

        foreach (var hive in new List<Hive>(activeHives))
        {
            if (hive != null) hive.StopProducing();
        }
        activeHives.Clear();
    }

    private IEnumerator HiveSpawnRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (!IsDead)
        {
            if (activeHives.Count < maxActiveHives)
            {
                SpawnHive();
                yield return new WaitForSeconds(hiveSpawnInterval);
            }
            else
            {
                if (!isHidden) EnterHiddenState();
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private void SpawnHive()
    {
        Vector3 spawnPosition = transform.position + (Random.insideUnitSphere * hiveSpawnRadius);
        spawnPosition.y = transform.position.y;
        if (NavMesh.SamplePosition(spawnPosition, out var hit, 5f, NavMesh.AllAreas)) spawnPosition = hit.position;

        var gameObject = hivePool.Get();
        gameObject.transform.position = spawnPosition;
        gameObject.transform.rotation = Quaternion.identity;

        var hive = gameObject.GetComponent<Hive>();
        if (hive != null)
        {
            hive.Initialize(this, hivePool, larvaPool);
            activeHives.Add(hive);
        }

        PlaySFX(spawnHiveSFX);
    }

    internal void OnHiveDestroyed(Hive hive)
    {
        if (activeHives.Contains(hive))
        {
            activeHives.Remove(hive);
        }

        if (isHidden && activeHives.Count < maxActiveHives) ExitHiddenState();
    }

    private void EnterHiddenState()
    {
        isHidden = true;
        SetHiddenStateVisuals(true);
        if (teleportWatchRoutine == null) teleportWatchRoutine = StartCoroutine(TeleportWatchRoutine());
    }

    private void ExitHiddenState()
    {
        isHidden = false;
        SetHiddenStateVisuals(false);
        if (teleportWatchRoutine != null)
        {
            StopCoroutine(teleportWatchRoutine);
            teleportWatchRoutine = null;
        }
    }

    private void SetHiddenStateVisuals(bool hidden)
    {
        Material targetMaterial = hidden ? transparentMaterial : normalMaterial;
        if (targetMaterial == null || allRenderers == null) return;

        foreach (var render in allRenderers)
        {
            render.material = targetMaterial;
        }
    }

    private IEnumerator TeleportWatchRoutine()
    {
        while (isHidden && !IsDead)
        {
            if (playerTransform != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                if (distanceToPlayer <= teleportAlertRange)
                {
                    yield return new WaitForSeconds(teleportDelayIfPlayerClose);
                    
                    if (Vector3.Distance(transform.position, playerTransform.position) <= teleportAlertRange)
                    {
                        TeleportToRandomValidPos();
                        PlaySFX(teleportSFX);
                        lastTeleportTime = Time.time;
                    }
                }
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void TeleportToRandomValidPos()
    {
        Vector3 candidate = transform.position + Random.insideUnitSphere * teleportRange;
        candidate.y = transform.position.y;
        if (NavMesh.SamplePosition(candidate, out var hit, teleportRange, NavMesh.AllAreas))
        {
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.Warp(hit.position);
            }
            else
            {
                transform.position = hit.position;
            }
            return;
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}