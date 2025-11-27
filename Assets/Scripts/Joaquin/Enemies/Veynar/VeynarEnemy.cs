using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyHealth))]
public class VeynarEnemy : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject hivePrefab;
    [SerializeField] private GameObject larvaPrefab;
    [Tooltip("Máximo de colmenas activas simultáneamente.")]
    [SerializeField] private int maxActiveHives = 3;
    [Tooltip("Tiempo entre intentos de invocación de colmena.")]
    [SerializeField] private float hiveSpawnInterval = 5f;
    [Tooltip("Distancia mínima para invocar colmenas.")]
    [SerializeField] private float minHiveSpawnRadius = 5f;
    [Tooltip("Distancia máxima para invocar colmenas.")]
    [SerializeField] private float maxHiveSpawnRadius = 15f;

    [Header("Behavior")]
    [Tooltip("Rango del teletransporte al re-ocultarse.")]
    [SerializeField] private float teleportRange = 20f;
    [Tooltip("¿Las colmenas se teletransportan con Veynar?")]
    [SerializeField] private bool teleportHivesWithVeynar = false;
    [Tooltip("Nombre de la capa a la que Veynar cambia al ser invulnerable (ej. 'Default' o 'Ignore Raycast').")]
    [SerializeField] private string invulnerableLayerName = "Default"; // Para que el escudo no rebote

    [Header("Visuals & Effects")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material transparentMaterial;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnHiveSFX;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathCriesSFX; // Audio de muerte

    private EnemyHealth enemyHealth;
    private Transform playerTransform;
    private List<Hive> activeHives = new List<Hive>();
    private Renderer[] allRenderers;
    private NavMeshAgent navAgent;

    private int previousHiveCount = 0;
    private Vector3 initialPosition; // Posición inicial para el teletransporte
    private Material normalMaterialInstance;
    private Material transparentMaterialInstance;
    private bool hasLostFirstHive = false;

    public bool IsDead => enemyHealth != null && enemyHealth.IsDead;
    public Transform PlayerTransform => playerTransform;
    public int ActiveHiveCount => activeHives.Count;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        allRenderers = GetComponentsInChildren<Renderer>();
        navAgent = GetComponent<NavMeshAgent>();

        // Crear instancias de los materiales
        if (normalMaterial != null) normalMaterialInstance = new Material(normalMaterial);
        if (transparentMaterial != null) transparentMaterialInstance = new Material(transparentMaterial);

        // Asignar la capa de invulnerabilidad a EnemyHealth
        if (enemyHealth != null)
        {
            enemyHealth.invulnerableLayerIndex = LayerMask.NameToLayer(invulnerableLayerName);
            if (enemyHealth.invulnerableLayerIndex == -1) // Si la capa no existe
            {
                Debug.LogWarning($"[VeynarEnemy] La capa '{invulnerableLayerName}' no existe. Veynar podría ser 'targeteable' por el escudo.");
                enemyHealth.invulnerableLayerIndex = gameObject.layer; // Usar capa actual como fallback
            }
        }
    }

    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        initialPosition = transform.position; // Guardar posición inicial

        if (hivePrefab == null || larvaPrefab == null)
        {
            Debug.LogError($"[VeynarEnemy] Prefabs no asignados en {gameObject.name}. Veynar no funcionará correctamente.", this);
            enabled = false;
            return;
        }

        // Empezar invisible y en la capa transparente
        foreach (var r in allRenderers)
        {
            if (transparentMaterialInstance != null) r.material = transparentMaterialInstance;
        }

        StartCoroutine(ApplyInitialInvulnerabilityState());
        StartCoroutine(HiveSpawnRoutine());
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
        if (normalMaterialInstance != null) Destroy(normalMaterialInstance);
        if (transparentMaterialInstance != null) Destroy(transparentMaterialInstance);
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        StopAllCoroutines();
        PlaySFX(deathCriesSFX);

        foreach (var hive in new List<Hive>(activeHives))
        {
            if (hive != null)
            {
                Destroy(hive.gameObject);
            }
        }
        activeHives.Clear();
    }

    private IEnumerator HiveSpawnRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (!IsDead)
        {
            while (enemyHealth != null && enemyHealth.IsStunned)
            {
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                }

                yield return null;
            }

            if (activeHives.Count < maxActiveHives)
            {
                SpawnHive();
                yield return new WaitForSeconds(hiveSpawnInterval);
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }
        }
    }

    private void SpawnHive()
    {
        // Lógica de "dona"
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minHiveSpawnRadius, maxHiveSpawnRadius);
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        spawnPosition.y = -0.4f;

        if (NavMesh.SamplePosition(spawnPosition, out var hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }

        var go = Instantiate(hivePrefab, spawnPosition, Quaternion.identity);
        var hive = go.GetComponent<Hive>();
        if (hive != null)
        {
            hive.Initialize(this, larvaPrefab);
            activeHives.Add(hive);
        }

        PlaySFX(spawnHiveSFX);
        UpdateVulnerabilityState();
    }

    internal void OnHiveDestroyed(Hive hive)
    {
        if (activeHives.Contains(hive))
        {
            activeHives.Remove(hive);
        }

        if (!hasLostFirstHive)
        {
            hasLostFirstHive = true;
        }

        UpdateVulnerabilityState();
    }

    /// <summary>
    /// Aplica el estado inicial de invulnerabilidad completa 100% y visibilidad 0%.
    /// Solo se ejecuta al inicio, antes de que se destruya la primera colmena.
    /// </summary>
    private IEnumerator ApplyInitialInvulnerabilityState()
    {
        // Delay de 1 frame para asegurar que EnemyHealth haya guardado su capa vulnerable
        yield return null;

        // Aplicar invulnerabilidad completa
        if (enemyHealth != null)
        {
            enemyHealth.SetDynamicVulnerability(1.0f); // 100% reducción
        }

        // Aplicar visibilidad 0%
        UpdateVisuals(0.0f);

        Debug.Log($"[VeynarEnemy] Estado inicial aplicado: Invulnerable (100%) e invisible (0%)");
    }

    /// <summary>
    /// Actualiza la vulnerabilidad, visibilidad y capa de Veynar
    /// basado en el número de colmenas activas.
    /// </summary>
    private void UpdateVulnerabilityState()
    {
        if (!hasLostFirstHive)
        {
            return;
        }

        int currentHives = activeHives.Count;

        float statePercent = (maxActiveHives - currentHives) / (float)maxActiveHives;

        // Reducción: 1.0 (100%), 0.67 (67%), 0.33 (33%), 0.0 (0%)
        float damageReduction = 1.0f - statePercent;
        // Visibilidad: 0.0 (0%), 0.33 (33%), 0.67 (67%), 1.0 (100%)
        float visibility = 1.0f - damageReduction;

        // Aplicar reducción de daño Y cambiar la capa
        if (enemyHealth != null)
        {
            enemyHealth.SetDynamicVulnerability(damageReduction);
        }

        // Aplicar visibilidad progresiva
        UpdateVisuals(visibility);

        // Lógica de Teletransporte: Si acaba de alcanzar el maximo de colmenas
        if (currentHives == maxActiveHives && previousHiveCount < maxActiveHives)
        {
            TeleportToRandomValidPos(initialPosition, teleportRange);
            PlaySFX(teleportSFX);
        }

        previousHiveCount = currentHives;
    }

    /// <summary>
    /// Actualiza los materiales de Veynar para reflejar la visibilidad.
    /// </summary>
    /// <param name="visibilityPercent">0.0 (invisible) a 1.0 (totalmente visible)</param>
    private void UpdateVisuals(float visibilityPercent)
    {
        if (allRenderers == null) return;

        if (Mathf.Approximately(visibilityPercent, 1.0f) && normalMaterialInstance != null)
        {
            foreach (var r in allRenderers)
            {
                r.material = normalMaterialInstance;
            }
        }
        // Si no, usar material transparente y ajustar el alfa
        else if (transparentMaterialInstance != null)
        {
            foreach (var r in allRenderers)
            {
                r.material = transparentMaterialInstance;
            }

            Color newColor = transparentMaterialInstance.color;
            newColor.a = Mathf.Lerp(0.1f, 1.0f, visibilityPercent);
            transparentMaterialInstance.color = newColor;
        }
    }

    private void TeleportToRandomValidPos(Vector3 center, float range)
    {
        // Teletransporta a 20 unidades de la posición inicial en una dirección aleatoria válida del NavMesh
        Vector3 candidate = center + Random.insideUnitSphere * range;
        candidate.y = transform.position.y;

        Vector3 oldPosition = transform.position;
        Vector3 newPosition = oldPosition;

        if (NavMesh.SamplePosition(candidate, out var hit, range, NavMesh.AllAreas))
        {
            newPosition = hit.position;
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.Warp(newPosition);
            }
            else
            {
                transform.position = newPosition;
            }
        }
        else
        {
            // Fallback si no encuentra posición
            newPosition = candidate;
            transform.position = newPosition;
        }

        // Teletransportar colmenas si está activado
        if (teleportHivesWithVeynar)
        {
            Vector3 offset = newPosition - oldPosition;
            foreach (Hive hive in activeHives)
            {
                if (hive != null) hive.Teleport(hive.transform.position + offset);
            }
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void OnDrawGizmos()
    {
        // Mostrar rango de spawn de colmenas
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxHiveSpawnRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, minHiveSpawnRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, teleportRange);
    }
}