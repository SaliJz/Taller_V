using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class MorlockEnemy : MonoBehaviour
{
    private enum MorlockState { Cautious, Combat, Repositioning }
    private MorlockState currentState;

    [Header("Referencias")]
    [SerializeField] private MorlockStats stats;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Statistics (fallback si no hay MorlockStats)")]
    [Header("Health")]
    public float health = 8f;

    [Header("Movement")]
    [SerializeField] private float optimalAttackDistance = 10f;
    [SerializeField] private float teleportMinDistance = 5f;
    [SerializeField] private float teleportRange = 5f;
    [SerializeField] private float teleportCooldown = 2.5f;

    [Header("Combat")]
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float projectileDamage = 5f;
    [SerializeField] private float projectileSpeed = 15f;

    [Header("Perception")]
    [Tooltip("Radio dentro del cual Morlock detecta al jugador y empezará a perseguir/atacar.")]
    [SerializeField] private float detectionRadius = 15f;

    [Header("Patrol")]
    [Tooltip("Si se asignan waypoints, Morlock los recorrerá en bucle. Si no, hará roaming aleatorio en patrolRadius.")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float patrolRadius = 8f; // usado si no hay waypoints
    [SerializeField] private float patrolIdleTime = 1.2f; // espera entre puntos

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip shootSFX;

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private Animator animator;

    private float fireTimer;
    private float teleportTimer;
    private float patrolIdleTimer;
    private int currentWaypointIndex = 0;
    private bool isDead = false;

    private Coroutine teleportCoroutine = null;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle worldLabelStyle;
    private Rect debugArea = new Rect(10, 10, 340, 260);

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (enemyHealth == null) ReportDebug("Componente EnemyHealth no encontrado en el enemigo.", 3);
        if (agent == null) ReportDebug("Componente NavMeshAgent no encontrado en el enemigo.", 3);
    }

    private void Start()
    {
        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);

        InitializedEnemy();

        if (agent != null)
        {
            agent.stoppingDistance = optimalAttackDistance;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        ChangeState(MorlockState.Cautious);
    }

    
    private void InitializedEnemy()
    {
        if (stats != null)
        {
            optimalAttackDistance = stats.optimalAttackDistance;
            teleportMinDistance = stats.teleportMinDistance;
            teleportRange = stats.teleportRange;
            teleportCooldown = stats.teleportCooldown;
            fireRate = stats.fireRate;
            projectileDamage = stats.projectileDamage;
            enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
            enemyHealth.SetMaxHealth(health);
        }
    }

    private void OnEnable()
    {
        enemyHealth.OnDeath += HandleEnemyDeath;
        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
            teleportCoroutine = null;
        }
    }

    private void OnDisable()
    {
        enemyHealth.OnDeath -= HandleEnemyDeath;
        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
            teleportCoroutine = null;
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;
        isDead = true;
        StopAllCoroutines();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        if (animator != null) animator.SetTrigger("Die");
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void Update()
    {
        if (isDead || playerTransform == null) return;

        switch (currentState)
        {
            case MorlockState.Cautious:
                UpdateCautiousState();
                break;
            case MorlockState.Combat:
                UpdateCombatState();
                break;
            case MorlockState.Repositioning:
                break;
        }

        teleportTimer += Time.deltaTime;
    }

    /// <summary>
    /// Metodo que maneja el cambio de estados
    /// </summary>
    /// <param name="newState"> El estado para actualiza. Si es el mismo, no pasara nada </param>
    private void ChangeState(MorlockState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    private void UpdateCautiousState()
    {
        if (Vector3.Distance(transform.position, playerTransform.position) < detectionRadius)
        {
            ChangeState(MorlockState.Combat);
            return;
        }

        patrolIdleTimer += Time.deltaTime;
        if (patrolIdleTimer >= patrolIdleTime)
        {
            StartCoroutine(PatrolTeleportRoutine());
        }
    }

    private void UpdateCombatState()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > detectionRadius)
        {
            ChangeState(MorlockState.Cautious);
            return;
        }

        LookAtPlayer();

        // Comportamiento defensivo: Teletransportarse si el jugador está muy cerca
        if (distanceToPlayer < teleportMinDistance && teleportTimer >= teleportCooldown)
        {
            Vector3 randomDirection = Random.insideUnitSphere * teleportRange;
            Vector3 targetPosition = transform.position + randomDirection;

            if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
            teleportCoroutine = StartCoroutine(TeleportRoutine(targetPosition, MorlockState.Combat));
            return;
        }

        // Comportamiento de ataque: Disparar si está en la distancia óptima
        if (distanceToPlayer <= optimalAttackDistance)
        {
            fireTimer += Time.deltaTime;
            if (fireTimer >= 1f / fireRate) // Una flecha por segundo
            {
                Shoot();
                fireTimer = 0;
            }
        }
    }

    private IEnumerator PatrolTeleportRoutine()
    {
        patrolIdleTimer = 0;
        Vector3 targetPosition;

        if (!loopWaypoints && currentWaypointIndex >= patrolWaypoints.Length - 1)
        {
            // Si no hay bucle y ya llegó al final, no hace nada más.
            // Se quedará en estado Vigilante en el último punto.
            yield break;
        }

        // Determinar el siguiente punto de "patrulla"
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            if (loopWaypoints)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            }
            else
            {
                currentWaypointIndex++;
            }
            targetPosition = patrolWaypoints[currentWaypointIndex].position;
        }
        else
        {
            Vector3 randomPoint;
            TryGetRandomPoint(transform.position, patrolRadius, out randomPoint);
            targetPosition = randomPoint;
        }

        if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
        teleportCoroutine = StartCoroutine(TeleportRoutine(targetPosition, MorlockState.Cautious));
        yield return teleportCoroutine;
    }

    /// <summary>
    /// Intenta obtener un punto válido sobre el NavMesh dentro de radio.
    /// </summary>
    /// <param name="center">Centro del círculo de búsqueda.</param>
    /// <param name="radius">Radio de búsqueda.</param>
    /// <param name="result">Punto encontrado (si retorna true).</param>
    /// <returns>True si se encontró un punto válido.</returns>
    private bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 rand = center + Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    private IEnumerator TeleportRoutine(Vector3 targetPosition, MorlockState stateAfterTeleport)
    {
        ChangeState(MorlockState.Repositioning);

        teleportTimer = 0;

        if (animator != null) animator.SetTrigger("TeleportOut");
        if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);

        yield return new WaitForSeconds(0.5f);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, teleportRange, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
        }

        if (animator != null) animator.SetTrigger("TeleportIn");

        yield return new WaitForSeconds(0.5f);

        ChangeState(stateAfterTeleport);
        teleportCoroutine = null;
    }

    private void LookAtPlayer()
    {
        Vector3 direction = playerTransform.position - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
        }
    }

    private void Shoot()
    {
        if (animator != null) animator.SetTrigger("Shoot");
        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();

        if (projectile != null)
        {
            projectile.Initialize(projectileSpeed, projectileDamage);
            ReportDebug("Disparando proyectil.", 1);
        }
    }

    private void OnDrawGizmos()
    {
        if (firePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(firePoint.position, firePoint.position + firePoint.forward * optimalAttackDistance);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        worldLabelStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white, background = Texture2D.blackTexture },
            padding = new RectOffset(6, 6, 3, 3)
        };
    }

    /// <summary>
    /// Dibuja una etiqueta en pantalla en la posición del mundo provista.
    /// </summary>
    private void DrawWorldLabel(Vector3 worldPos, string text, GUIStyle style)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0) return; // detrás de la cámara

        Vector2 guiPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);
        Vector2 size = style.CalcSize(new GUIContent(text));
        Rect rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y - 8f, size.x + 8f, size.y + 6f);

        GUI.Box(rect, GUIContent.none, style);
        GUI.Label(rect, text, style);
    }

    /// <summary>
    /// Intenta obtener un target de teletransporte para el botón de debug:
    /// - si hay waypoints, usa el siguiente waypoint
    /// - sino, genera un punto aleatorio válido (TryGetRandomPoint)
    /// </summary>
    private Vector3 GetDebugTeleportTarget()
    {
        Vector3 target = transform.position;
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            int nextIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            if (patrolWaypoints[nextIndex] != null) target = patrolWaypoints[nextIndex].position;
        }
        else
        {
            Vector3 randomPoint;
            if (TryGetRandomPoint(transform.position, patrolRadius, out randomPoint)) target = randomPoint;
        }
        return target;
    }

    private void OnGUI()
    {
        // mostrar solo si se habilitó el toggle y solo en editor o builds de desarrollo
        if (!showDetailsOptions) return;
#if !UNITY_EDITOR
    if (!Debug.isDebugBuild) return;
#endif

        EnsureGuiStyles();

        // área de debug en pantalla
        Rect area = new Rect(10, 10, 360, 320);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("MORLOCK - DEBUG", titleStyle);

        // Estado actual
        GUILayout.BeginHorizontal();
        GUILayout.Label("Estado:", labelStyle, GUILayout.Width(120));
        GUILayout.Label(currentState.ToString(), labelStyle);
        GUILayout.EndHorizontal();

        // Salud
        if (enemyHealth != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("HP:", labelStyle, GUILayout.Width(120));
            GUILayout.Label($"{enemyHealth.CurrentHealth:F1} / {enemyHealth.MaxHealth:F1}", labelStyle);
            GUILayout.EndHorizontal();
        }

        // Distancias / player
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dist. a Jugador (m):", labelStyle, GUILayout.Width(120));
            GUILayout.Label($"{dist:F2}", labelStyle);
            GUILayout.EndHorizontal();
        }

        // Detección / teleport timers
        GUILayout.BeginHorizontal();
        GUILayout.Label("DetectionRadius:", labelStyle, GUILayout.Width(120));
        GUILayout.Label($"{detectionRadius:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("TeleportTimer:", labelStyle, GUILayout.Width(120));
        GUILayout.Label($"{teleportTimer:F2} / {teleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        // Disparo
        float timeToNextShot = Mathf.Max(0f, (1f / Mathf.Max(0.0001f, fireRate)) - fireTimer);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Cadencia (s):", labelStyle, GUILayout.Width(120));
        GUILayout.Label($"{1f / Mathf.Max(0.0001f, fireRate):F2} (next in {timeToNextShot:F2}s)", labelStyle);
        GUILayout.EndHorizontal();

        // Proyectil
        GUILayout.BeginHorizontal();
        GUILayout.Label("Proj. daño / vel:", labelStyle, GUILayout.Width(120));
        GUILayout.Label($"{projectileDamage:F1} / {projectileSpeed:F1}", labelStyle);
        GUILayout.EndHorizontal();

        // Patrol info
        GUILayout.BeginHorizontal();
        GUILayout.Label("Patrol waypoints:", labelStyle, GUILayout.Width(120));
        GUILayout.Label($"{(patrolWaypoints != null ? patrolWaypoints.Length.ToString() : "0")}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Patrol index:", labelStyle, GUILayout.Width(120));
        GUILayout.Label($"{currentWaypointIndex}", labelStyle);
        GUILayout.EndHorizontal();

        // NavMeshAgent info
        if (agent != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Agent stopped:", labelStyle, GUILayout.Width(120));
            GUILayout.Label(agent.isStopped ? "True" : "False", labelStyle);
            GUILayout.EndHorizontal();

            string remain = agent.pathPending ? "pending" : $"{agent.remainingDistance:F2}";
            GUILayout.BeginHorizontal();
            GUILayout.Label("RemainDist:", labelStyle, GUILayout.Width(120));
            GUILayout.Label(remain, labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("StopDist:", labelStyle, GUILayout.Width(120));
            GUILayout.Label($"{agent.stoppingDistance:F2}", labelStyle);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        // Botones útiles para QA (teleport, shoot, reset)
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Teleport Now", GUILayout.Height(26)))
        {
            // obtener target y lanzar teleport
            Vector3 target = GetDebugTeleportTarget();
            if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
            teleportCoroutine = StartCoroutine(TeleportRoutine(target, currentState));
        }

        if (GUILayout.Button("Shoot Now", GUILayout.Height(26)))
        {
            Shoot();
            fireTimer = 0f;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Timers", GUILayout.Height(22)))
        {
            fireTimer = 0f;
            teleportTimer = 0f;
            patrolIdleTimer = 0f;
        }
        if (GUILayout.Button("Kill (debug)", GUILayout.Height(22)))
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(9999f);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        // Etiqueta en mundo (sobre la cabeza)
        string worldText = $"Morlock\nState: {currentState}\nHP: {(enemyHealth != null ? enemyHealth.CurrentHealth.ToString("F0") : "N/A")}";
        DrawWorldLabel(transform.position + Vector3.up * 2.0f, worldText, worldLabelStyle);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[MorlockEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[MorlockEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[MorlockEnemy] {message}");
                break;
            default:
                Debug.Log($"[MorlockEnemy] {message}");
                break;
        }
    }
}