using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class MorlockEnemy : MonoBehaviour
{
    private enum MorlockState { Cautious, Combat, Repositioning }
    private MorlockState currentState;

    [Header("Referencias")]
    [SerializeField] private MorlockStats stats;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    #region Variables

    [Header("Statistics (fallback si no hay MorlockStats)")]
    [Header("Health")]
    public float health = 15f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float optimalAttackDistance = 5f;
    [SerializeField] private float teleportRange = 5f;

    [Header("Combat")]
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private int maxDamageIncrease = 2;
    [SerializeField] private float maxRangeForDamageIncrease = 6f;
    [SerializeField] private float maxDistanceForDamageStart = 20f;

    [Header("Perception")]
    [Tooltip("Radio dentro del cual Morlock detecta al jugador y empezará a perseguir/atacar.")]
    [SerializeField] private float detectionRadius = 15f;

    [Header("Patrol")]
    [Tooltip("Si se asignan waypoints, Morlock los recorrerá en bucle. Si no, hará roaming aleatorio en patrolRadius.")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float patrolRadius = 8f; // usado si no hay waypoints
    [SerializeField] private float patrolIdleTime = 1.2f; // espera entre puntos

    [Header("Teleport Pursuit (Perseguir 1)")]
    [Tooltip("Distancia de avance por teletransporte al perseguir al jugador")]
    [SerializeField] private float pursuitAdvanceDistance = 4f;
    [Tooltip("Variación lateral del teletransporte (3-5 unidades)")]
    [SerializeField] private float pursuitLateralVariationMin = 3f;
    [SerializeField] private float pursuitLateralVariationMax = 5f;
    [SerializeField] private float pursuitTeleportCooldown = 2.5f;

    [Header("Defensive Teleport (Perseguir 2)")]
    [Tooltip("Radio de activación para teletransporte defensivo")]
    [SerializeField] private float defensiveTeleportActivationRadius = 5f;
    [Tooltip("Rango de ataque después del teletransporte defensivo")]
    [SerializeField] private float defensiveTeleportRange = 5f;
    [SerializeField] private float defensiveTeleportCooldown = 2.5f;

    [Header("Evasive Teleport (Perseguir 3)")]
    [Tooltip("Cooldown del teletransporte evasivo")]
    [SerializeField] private float evasiveTeleportCooldown = 1.5f;
    [Tooltip("Número de posiciones a generar alrededor del jugador")]
    [SerializeField] private int evasiveTeleportPositions = 4;
    [Tooltip("Radio para generar posiciones de teletransporte evasivo (5-10 unidades)")]
    [SerializeField] private float evasiveTeleportRadiusMin = 5f;
    [SerializeField] private float evasiveTeleportRadiusMax = 10f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip shootSFX;

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;

    #endregion

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private Animator animator;
    private CharacterController playerCharacterController;

    private float fireTimer;
    private float pursuitTeleportTimer;
    private float defensiveTeleportTimer;
    private float evasiveTeleportTimer;
    private float patrolIdleTimer;
    private int currentWaypointIndex = 0;
    private bool isDead = false;

    private Coroutine teleportCoroutine = null;
    
    private int currentPursuitPhase = 0;
    
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle worldLabelStyle;

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
        if (playerGameObject != null)
        {
            playerTransform = playerGameObject.transform;
            playerCharacterController = playerGameObject.GetComponent<CharacterController>();
        }

        InitializedEnemy();

        if (agent != null)
        {
            agent.speed = moveSpeed;
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
            moveSpeed = stats.moveSpeed;
            optimalAttackDistance = stats.optimalAttackDistance;
            teleportRange = stats.teleportRange;
            fireRate = stats.fireRate;
            projectileDamage = stats.projectileDamage;
            projectileSpeed = stats.projectileSpeed;
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
        enemyHealth.OnDamaged += HandleDamageTaken;
        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
            teleportCoroutine = null;
        }
    }

    private void OnDisable()
    {
        enemyHealth.OnDeath -= HandleEnemyDeath;
        enemyHealth.OnDamaged -= HandleDamageTaken;
        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
            teleportCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        enemyHealth.OnDeath -= HandleEnemyDeath;
        enemyHealth.OnDamaged -= HandleDamageTaken;
    }

    private void HandleDamageTaken()
    {
        // Si recibe daño y el teletransporte evasivo está listo, lo prioriza.
        if (currentState == MorlockState.Combat && evasiveTeleportTimer >= evasiveTeleportCooldown)
        {
            PerformEvasiveTeleport();
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

        pursuitTeleportTimer += Time.deltaTime;
        defensiveTeleportTimer += Time.deltaTime;
        evasiveTeleportTimer += Time.deltaTime;
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
        if (isDead || playerTransform == null || currentState == MorlockState.Repositioning) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > detectionRadius)
        {
            ChangeState(MorlockState.Cautious);
            return;
        }

        LookAtPlayer();

        // SISTEMA DE TELETRANSPORTE POR FASES
        // 1. Prioridad defensiva: si el jugador está muy cerca, teletransporte defensivo.
        if (distanceToPlayer < defensiveTeleportActivationRadius && defensiveTeleportTimer >= defensiveTeleportCooldown)
        {
            PerformDefensiveTeleport();
            return;
        }

        // 2. Comportamiento de ataque: Disparar si está en la distancia óptima.
        if (distanceToPlayer <= optimalAttackDistance)
        {
            fireTimer += Time.deltaTime;
            if (fireTimer >= 1f / fireRate)
            {
                Shoot();
                fireTimer = 0;
            }
        }

        // 3. Reposicionamiento ofensivo: si está fuera de rango o necesita presionar.
        else if (pursuitTeleportTimer >= pursuitTeleportCooldown)
        {
            PerformPursuitTeleport();
            return;
        }
    }

    #region Sistemas de Teletransporte

    /// <summary>
    /// Perseguir 1: Teletransporte de persecución con avance y variación lateral
    /// </summary>
    private void PerformPursuitTeleport()
    {
        pursuitTeleportTimer = 0f;
        currentPursuitPhase = 1;

        // Calcular línea de distancia mínima entre Morlock y jugador
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;

        // Avanzar 4 unidades hacia el jugador
        Vector3 advancePosition = transform.position + directionToPlayer * pursuitAdvanceDistance;

        // Añadir variación lateral (3-5 unidades perpendicular a la línea)
        Vector3 lateralDirection = Vector3.Cross(directionToPlayer, Vector3.up);
        float lateralOffset = Random.Range(pursuitLateralVariationMin, pursuitLateralVariationMax);
        if (Random.value > 0.5f) lateralOffset = -lateralOffset; // Aleatorio izquierda o derecha

        Vector3 targetPosition = advancePosition + lateralDirection * lateralOffset;

        ReportDebug($"Perseguir 1: Teletransporte de avance. Fase {currentPursuitPhase}/3", 1);

        if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
        teleportCoroutine = StartCoroutine(TeleportRoutine(targetPosition, MorlockState.Combat));
    }

    /// <summary>
    /// Perseguir 2: Teletransporte defensivo cuando el jugador está muy cerca
    /// </summary>
    private void PerformDefensiveTeleport()
    {
        defensiveTeleportTimer = 0f;
        currentPursuitPhase = 2;
        optimalAttackDistance = defensiveTeleportRange;

        // Generar 4 posiciones equidistantes alrededor del jugador
        Vector3[] teleportPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f; // 0°, 90°, 180°, 270°
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * defensiveTeleportRange;
            teleportPositions[i] = playerTransform.position + offset;
        }

        // Elegir la posición más lejana al jugador
        Vector3 targetPosition = teleportPositions[0];
        float maxDistance = 0f;

        foreach (Vector3 pos in teleportPositions)
        {
            float dist = Vector3.Distance(transform.position, pos);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                targetPosition = pos;
            }
        }

        ReportDebug("Perseguir 2: Teletransporte defensivo activado", 1);

        if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
        teleportCoroutine = StartCoroutine(TeleportRoutine(targetPosition, MorlockState.Combat));
    }

    /// <summary>
    /// Perseguir 3: Teletransporte evasivo (más frecuente y dinámico)
    /// </summary>
    private void PerformEvasiveTeleport()
    {
        evasiveTeleportTimer = 0f;
        currentPursuitPhase = 3;
        optimalAttackDistance = evasiveTeleportRadiusMax;

        // Generar 4 posiciones aleatorias alrededor del jugador (5-10 unidades)
        Vector3[] teleportPositions = new Vector3[evasiveTeleportPositions];

        for (int i = 0; i < evasiveTeleportPositions; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(evasiveTeleportRadiusMin, evasiveTeleportRadiusMax);
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
            teleportPositions[i] = playerTransform.position + offset;
        }

        // Elegir una posición aleatoria de las generadas
        Vector3 targetPosition = teleportPositions[Random.Range(0, teleportPositions.Length)];

        ReportDebug("Perseguir 3: Teletransporte evasivo activado", 1);

        if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
        teleportCoroutine = StartCoroutine(TeleportRoutine(targetPosition, MorlockState.Combat));
    }

    #endregion

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

        pursuitTeleportTimer = 0;

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
        
        Vector3 aimPoint = playerTransform.position;
        if (playerCharacterController != null)
        {
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);
        }

        Vector3 directionToAim = (aimPoint - firePoint.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(directionToAim);

        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);

            projectile.Initialize(projectileSpeed, calculatedDamage);
            ReportDebug($"Disparando proyectil. Daño: {calculatedDamage} (dist: {distanceToPlayer:F2})", 1);
        }
    }

    /// <summary>
    /// Calcula el punto de intercepción para un disparo, prediciendo el movimiento del objetivo.
    /// </summary>
    private Vector3 CalculateInterceptPoint(Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 directionToTarget = targetPosition - firePoint.position;
        float distance = directionToTarget.magnitude;

        if (projectileSpeed <= 0) return targetPosition;

        float timeToIntercept = distance / projectileSpeed;
        Vector3 futurePosition = targetPosition + targetVelocity * timeToIntercept;
        futurePosition += Random.insideUnitSphere * 0.5f;

        return futurePosition;
    }

    /// <summary>
    /// Calcula el daño del proyectil basado en la distancia al jugador.
    /// Escalado lineal: 6 unidades = 2 daño, 20+ unidades = 1 daño
    /// </summary>
    private float CalculateDamageByDistance(float distance)
    {
        if (distance <= maxRangeForDamageIncrease)
        {
            return maxDamageIncrease; // Daño máximo = 2
        }
        else if (distance >= maxDistanceForDamageStart)
        {
            return projectileDamage; // Daño mínimo = 1
        }
        else
        {
            // Interpolación lineal entre 6 y 20 unidades
            float t = (distance - maxRangeForDamageIncrease) / (maxDistanceForDamageStart - maxRangeForDamageIncrease);
            return Mathf.Lerp(maxDamageIncrease, projectileDamage, t);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, optimalAttackDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.forward * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        // Visualizar rangos de teletransporte
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, defensiveTeleportActivationRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, defensiveTeleportRange);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, evasiveTeleportRadiusMin);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, evasiveTeleportRadiusMax);
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

    private void DrawWorldLabel(Vector3 worldPos, string text, GUIStyle style)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0) return;

        Vector2 guiPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);
        Vector2 size = style.CalcSize(new GUIContent(text));
        Rect rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y - 8f, size.x + 8f, size.y + 6f);

        GUI.Box(rect, GUIContent.none, style);
        GUI.Label(rect, text, style);
    }

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
        if (!showDetailsOptions) return;
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild) return;
#endif

        EnsureGuiStyles();

        Rect area = new Rect(10, 10, 380, 400);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("MORLOCK - DEBUG", titleStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Estado:", labelStyle, GUILayout.Width(140));
        GUILayout.Label(currentState.ToString(), labelStyle);
        GUILayout.EndHorizontal();

        if (enemyHealth != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("HP:", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{enemyHealth.CurrentHealth:F1} / {enemyHealth.MaxHealth:F1}", labelStyle);
            GUILayout.EndHorizontal();
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dist. a Jugador (m):", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{dist:F2}", labelStyle);
            GUILayout.EndHorizontal();

            // Mostrar daño calculado en tiempo real
            float calculatedDamage = CalculateDamageByDistance(dist);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Daño actual (por dist):", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{calculatedDamage:F2}", labelStyle);
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Fase de persecución:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{currentPursuitPhase}/3", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Pursuit Timer:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{pursuitTeleportTimer:F2} / {pursuitTeleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Defensive Timer:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{defensiveTeleportTimer:F2} / {defensiveTeleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Evasive Timer:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{evasiveTeleportTimer:F2} / {evasiveTeleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        float timeToNextShot = Mathf.Max(0f, (1f / Mathf.Max(0.0001f, fireRate)) - fireTimer);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Siguiente disparo (s):", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{timeToNextShot:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Proj. daño / vel:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{projectileDamage:F1}-{maxDamageIncrease:F1} / {projectileSpeed:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Teleport Now", GUILayout.Height(26)))
        {
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
            pursuitTeleportTimer = 0f;
            defensiveTeleportTimer = 0f;
            evasiveTeleportTimer = 0f;
            patrolIdleTimer = 0f;
        }
        if (GUILayout.Button("Reset Phase", GUILayout.Height(22)))
        {
            currentPursuitPhase = 0;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Pursuit TP", GUILayout.Height(22)))
        {
            PerformPursuitTeleport();
        }
        if (GUILayout.Button("Force Defensive TP", GUILayout.Height(22)))
        {
            PerformDefensiveTeleport();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Force Evasive TP", GUILayout.Height(22)))
        {
            PerformEvasiveTeleport();
        }

        if (GUILayout.Button("Kill (debug)", GUILayout.Height(22)))
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(9999f);
        }

        GUILayout.EndArea();

        // etiqueta flotante en el mundo
        string worldText = $"Morlock\nState: {currentState}\nHP: {(enemyHealth != null ? enemyHealth.CurrentHealth.ToString("F0") : "N/A")}\nPhase: {currentPursuitPhase}";
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