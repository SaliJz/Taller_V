using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class KronusEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KronusStats stats;
    [SerializeField] private Transform hitPoint;
    [SerializeField] private GameObject visualHit;
    [SerializeField] private GameObject groundIndicator;

    [Header("Statistics (fallback si no hay KronusStats)")]
    [Header("Health")]
    [SerializeField] private float health = 20f;

    [Header("Movement")]
    [Tooltip("Velocidad de movimiento por defecto si no se encuentra KronusStats.")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float dashSpeedMultiplier = 2f;
    [SerializeField] private float dashDuration = 1f;
    [SerializeField] private float dashMaxDistance = 6f;

    [Header("Grounding Configuration")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Attack")]
    [SerializeField] private bool isPercentageDmg = false;
    [SerializeField] private float attackCycleCooldown = 2.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackDamagePercentage = 0.2f;
    [SerializeField] private float attackRadius = 1.5f;
    [SerializeField] private float preparationTime = 1f;
    [SerializeField] private float knockbackForce = 1f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Perception")]
    [Tooltip("Radio dentro del cual Kronus detecta al jugador y empezar� a perseguir/atacar.")]
    [SerializeField] private float detectionRadius = 12f;

    [Header("Patrol")]
    [Tooltip("Si se asignan waypoints, Kronus los recorrer� en bucle. Si no, har� roaming aleatorio en patrolRadius.")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolIdleTime = 1.2f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private AudioClip hammerSmashSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip hitSFX;

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;
    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.25f;

    private List<GameObject> activeInstantiatedEffects = new List<GameObject>();

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private Animator animator;

    private bool isAttacking = false;
    private float attackTimer;

    private bool hasHitPlayerThisDash = false;
    private Coroutine dashCoroutine;

    private bool isPatrolWaiting = false;
    private int currentWaypointIndex = 0;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle worldLabelStyle;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (enemyHealth == null) ReportDebug("Falta EnemyHealth componente.", 3);
        if (agent == null) ReportDebug("Falta NavMeshAgent componente.", 3);
    }

    private void Start()
    {
        if (visualHit != null) visualHit.SetActive(false);
        if (groundIndicator != null) groundIndicator.SetActive(false);

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        else playerTransform.TryGetComponent(out playerHealth);

        InitializedEnemy();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRadius;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        SetNextPatrolDestination();
    }

    private void InitializedEnemy()
    {
        if (stats != null)
        {
            moveSpeed = stats.moveSpeed;
            dashSpeedMultiplier = stats.dashSpeedMultiplier;
            dashDuration = stats.dashDuration;
            dashMaxDistance = stats.dashMaxDistance;
            attackCycleCooldown = stats.attackCycleCooldown;
            attackDamagePercentage = stats.attackDamagePercentage;
            attackDamage = stats.attackDamage;
            attackRadius = stats.attackRadius;
            preparationTime = stats.preparationTime;
            knockbackForce = stats.knockbackForce;
            if (enemyHealth != null) enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
            if (enemyHealth != null) enemyHealth.SetMaxHealth(health);
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
        if (groundIndicator != null) groundIndicator.SetActive(false);
        if (visualHit != null) visualHit.SetActive(false);
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (groundIndicator != null) groundIndicator.SetActive(false);
        if (visualHit != null) visualHit.SetActive(false);
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (groundIndicator != null) Destroy(groundIndicator);
        if (visualHit != null) Destroy(visualHit);
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        isAttacking = false;
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            else
            {
                agent.enabled = false;
            }
        }

        if (animator != null) animator.SetTrigger("Die");
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void Update()
    {
        if (!enabled) return;

        if (playerTransform == null)
        {
            if (agent != null) agent.isStopped = true;
            PatrolUpdate();
            return;
        }

        float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
        float detectionSqr = detectionRadius * detectionRadius;

        if (sqrDistToPlayer <= detectionSqr)
        {
            if (!isAttacking)
            {
                attackTimer += Time.deltaTime;

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(playerTransform.position);
                }

                if (attackTimer >= attackCycleCooldown)
                {
                    if (!isAttacking)
                    {
                        dashCoroutine = StartCoroutine(DashAttackRoutine());
                    }
                }
            }
        }
        else
        {
            PatrolUpdate();
        }
    }

    #region Patrulla / Roaming

    /// <summary>
    /// Actualiza el comportamiento de patrulla o roaming.
    /// </summary>
    private void PatrolUpdate()
    {
        if (isAttacking) return;

        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            if (agent != null && !agent.pathPending && !isPatrolWaiting)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    StartCoroutine(WaitThenMoveToNextWaypoint(patrolIdleTime));
                }
            }
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance))
            {
                Vector3 roamPoint;
                if (TryGetRandomPoint(transform.position, patrolRadius, out roamPoint))
                {
                    agent.SetDestination(roamPoint);
                }
            }
        }
    }

    /// <summary>
    /// Espera un tiempo antes de avanzar al siguiente waypoint.
    /// </summary>
    /// <param name="wait">Tiempo a esperar en segundos.</param>
    private IEnumerator WaitThenMoveToNextWaypoint(float wait)
    {
        isPatrolWaiting = true;
        if (agent != null) agent.isStopped = true;
        yield return new WaitForSeconds(wait);

        if (agent != null) agent.isStopped = false;

        // avanzar �ndice
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= patrolWaypoints.Length)
            {
                if (loopWaypoints) currentWaypointIndex = 0;
                else
                {
                    // detener patrulla si no loopea
                    currentWaypointIndex = patrolWaypoints.Length - 1;
                    agent.isStopped = true;
                    isPatrolWaiting = false;
                    yield break;
                }
            }
            SetNextPatrolDestination();
        }

        isPatrolWaiting = false;
    }

    /// <summary>
    /// Define el siguiente destino de patrulla, ya sea un waypoint o un punto aleatorio.
    /// </summary>
    private void SetNextPatrolDestination()
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && patrolWaypoints[currentWaypointIndex] != null)
            {
                agent.SetDestination(patrolWaypoints[currentWaypointIndex].position);
            }
        }
        else
        {
            Vector3 roamPoint;
            if (TryGetRandomPoint(transform.position, patrolRadius, out roamPoint) && agent != null)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.SetDestination(roamPoint);
            }
        }
    }

    /// <summary>
    /// Intenta obtener un punto v�lido sobre el NavMesh dentro de radio.
    /// </summary>
    /// <param name="center">Centro del c�rculo de b�squeda.</param>
    /// <param name="radius">Radio de b�squeda.</param>
    /// <param name="result">Punto encontrado (si retorna true).</param>
    /// <returns>True si se encontr� un punto v�lido.</returns>
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

    #endregion

    #region Dash / Attack

    private IEnumerator DashAttackRoutine()
    {
        isAttacking = true;
        attackTimer = 0f;
        hasHitPlayerThisDash = false;

        ReportDebug("Kronus inicia ataque dash.", 1);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updatePosition = false;
            agent.updateRotation = true;
        }

        if (animator != null) animator.SetTrigger("StartDash");
        if (audioSource != null && dashSFX != null) audioSource.PlayOneShot(dashSFX);

        Vector3 startPosition = transform.position;
        Vector3 dashTarget = playerTransform != null ? playerTransform.position : transform.position;

        // Mantener la Y actual del enemigo
        dashTarget.y = startPosition.y;

        Vector3 direction = (dashTarget - startPosition);
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        direction.Normalize();

        float distanceToPlayer = Vector3.Distance(new Vector3(startPosition.x, 0, startPosition.z), new Vector3(dashTarget.x, 0, dashTarget.z));
        float dashDistance = Mathf.Min(distanceToPlayer, dashMaxDistance);
        Vector3 finalDashTarget = startPosition + direction * dashDistance;
        finalDashTarget.y = startPosition.y;

        float elapsed = 0f;
        float interruptDistance = attackRadius * 1.5f;

        // Dash por duraci�n
        while (elapsed < dashDuration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;

            Vector3 currentPos = transform.position;
            Vector3 toTarget = finalDashTarget - currentPos;
            toTarget.y = 0f;
            float remaining = toTarget.magnitude;

            if (remaining <= 0.05f) break;

            Vector3 dashDir = toTarget.normalized;
            float step = moveSpeed * dashSpeedMultiplier * delta;
            float moveAmount = Mathf.Min(step, remaining);

            // Calcular nueva posici�n manteniendo Y
            Vector3 newPosition = currentPos + dashDir * moveAmount;

            // Muestrear NavMesh para obtener la Y correcta
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(newPosition, out navHit, 2f, NavMesh.AllAreas))
            {
                newPosition.y = navHit.position.y;
            }

            // Actualizar posici�n directamente
            transform.position = newPosition;

            // Rotaci�n suave
            if (dashDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dashDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * delta);
            }

            // Interrupci�n por proximidad al jugador
            if (playerTransform != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                if (distToPlayer <= interruptDistance)
                {
                    ReportDebug("Dash interrumpido: jugador detectado en la trayectoria.", 1);
                    break;
                }
            }

            yield return null;
        }

        if (agent != null && agent.enabled)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
            }

            agent.updatePosition = true;
        }

        ReportDebug("Kronus preparando martillazo.", 1);

        if (groundIndicator != null)
        {
            groundIndicator.transform.SetParent(null, true);

            Vector3 sampleBase = (hitPoint != null) ? hitPoint.position : transform.position;
            Vector3 indicatorPos = sampleBase;
            NavMeshHit navHit;

            if (NavMesh.SamplePosition(sampleBase, out navHit, 3f, NavMesh.AllAreas))
            {
                indicatorPos = navHit.position;
                indicatorPos.y += 0.01f;
            }
            else
            {
                RaycastHit hit;
                if (Physics.Raycast(sampleBase + Vector3.up * 3f, Vector3.down, out hit, 6f, groundLayer))
                {
                    indicatorPos.y = hit.point.y + 0.01f;
                }
            }

            Collider indCol = groundIndicator.GetComponent<Collider>();
            if (indCol != null) indCol.enabled = false;

            groundIndicator.transform.position = indicatorPos;
            groundIndicator.transform.localScale = new Vector3(attackRadius * 2f, 0.025f, attackRadius * 2f);
            groundIndicator.SetActive(true);
        }

        if (animator != null) animator.SetTrigger("PrepareAttack");

        yield return new WaitForSeconds(preparationTime);

        if (groundIndicator != null) groundIndicator.SetActive(false);

        if (animator != null) animator.SetTrigger("Attack");
        if (audioSource != null && hammerSmashSFX != null) audioSource.PlayOneShot(hammerSmashSFX);

        PerformHammerSmash();

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;

            if (playerTransform != null && agent.isOnNavMesh)
            {
                agent.SetDestination(playerTransform.position);
            }
        }

        yield return new WaitForSeconds(1f);

        ReportDebug("Kronus finaliza ataque dash.", 1);
        isAttacking = false;
    }

    public void PerformHammerSmash()
    {
        if (hasHitPlayerThisDash) return;

        if (hitPoint == null || playerHealth == null)
        {
            ReportDebug("PerformHammerSmash: falta hitPoint o playerHealth.", 2);
            return;
        }

        if (visualHit != null) visualHit.SetActive(true);

        Collider[] hitPlayer = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);

        foreach (Collider collider in hitPlayer)
        {
            if (playerHealth != null)
            {
                var hitTransform = collider.transform;
                bool isCritical;
                float damage;

                if (isPercentageDmg) damage = playerHealth.MaxHealth * attackDamagePercentage;
                else damage = attackDamage;

                // Calcular da�o con sistema de cr�ticos
                float damageToApply = CriticalHitSystem.CalculateDamage(damage, transform, hitTransform, out isCritical);
                
                if (audioSource != null && hitSFX != null) audioSource.PlayOneShot(hitSFX);

                playerHealth.TakeDamage(damageToApply);

                // Aplicar empuje
                ApplyKnockback(hitTransform);

                ReportDebug($"Kronus atac� al jugador por {damage} de da�o.", 1);

                hasHitPlayerThisDash = true;

                break;
            }
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private void ApplyKnockback(Transform target)
    {
        // Calcula la direcci�n del empuje (desde Kronus hacia el jugador)
        Vector3 knockbackDirection = (target.position - transform.position).normalized;
        knockbackDirection.y = 0f; // Mantener en el plano horizontal

        // Aplica el empuje
        CharacterController cc = target.GetComponent<CharacterController>();
        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (cc != null)
        {
            // Si el jugador usa CharacterController
            StartCoroutine(ApplyKnockbackOverTime(cc, knockbackDirection * knockbackForce));
        }
        else if (rb != null)
        {
            // Si el jugador usa Rigidbody
            rb.AddForce(knockbackDirection * knockbackForce * 10f, ForceMode.Impulse);
        }

        ReportDebug($"Empuje aplicado al jugador en direcci�n {knockbackDirection}", 1);
    }

    private IEnumerator ApplyKnockbackOverTime(CharacterController cc, Vector3 knockbackVelocity)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cc != null && cc.enabled)
            {
                cc.Move(knockbackVelocity * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        Vector3 originalScale = visualHit.transform.localScale;

        showGizmo = true;
        if (visualHit != null && hitPoint != null)
        {
            visualHit.transform.localScale = Vector3.one * attackRadius * 2f;
            visualHit.SetActive(true);
        }
        yield return new WaitForSeconds(gizmoDuration);

        showGizmo = false;
        if (visualHit != null && hitPoint != null)
        {
            visualHit.SetActive(false);
            visualHit.transform.localScale = originalScale;
        }
    }

    /// <summary>
    /// Valida y corrige la posici�n de Kronus si est� debajo del terreno o fuera del NavMesh.
    /// </summary>
    private void ValidatePositionOnNavMesh()
    {
        if (agent == null) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            // Posici�n v�lida, mantener
            transform.position = hit.position;
        }
        else
        {
            // Fuera del NavMesh, intentar encontrar posici�n cercana
            ReportDebug("Kronus fuera del NavMesh, buscando posici�n v�lida...", 2);

            for (int i = 0; i < 8; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 2f;
                if (NavMesh.SamplePosition(transform.position + randomOffset, out hit, 2f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    ReportDebug("Kronus reposicionado en NavMesh v�lido.", 1);
                    return;
                }
            }

            ReportDebug("No se encontr� posici�n v�lida en NavMesh. Kronus puede estar atrapado.", 3);
        }
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        if (hitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitPoint.position, attackRadius);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dashMaxDistance);
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

    private void OnGUI()
    {
        if (!showDetailsOptions) return;
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild) return;
#endif
        EnsureGuiStyles();

        Rect area = new Rect(10, 10, 360, 340);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("KRONUS - DEBUG", titleStyle);

        if (enemyHealth != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("HP:", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{enemyHealth.CurrentHealth:F1}/{enemyHealth.MaxHealth:F1}", labelStyle);
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Move Speed:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{moveSpeed:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Dash Speed Mult:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{dashSpeedMultiplier:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Dash Duration:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{dashDuration:F1}s", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Dash Max Distance:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{dashMaxDistance:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Attack Damage:", labelStyle, GUILayout.Width(140));
        GUILayout.Label(isPercentageDmg ? $"{attackDamagePercentage * 100f:F0}% HP" : $"{attackDamage:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Attack Radius:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{attackRadius:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Knockback Force:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{knockbackForce:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Next Attack (s):", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{Mathf.Max(0, attackCycleCooldown - attackTimer):F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        if (GUILayout.Button("Force Attack", GUILayout.Height(24)))
        {
            PerformHammerSmash();
        }

        if (GUILayout.Button("Force Dash", GUILayout.Height(24)))
        {
            if (dashCoroutine == null) dashCoroutine = StartCoroutine(DashAttackRoutine());
        }

        if (GUILayout.Button("Kill (debug)", GUILayout.Height(24)))
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(9999f);
        }

        GUILayout.EndArea();

        // etiqueta flotante en el mundo
        string worldText = $"Kronus\nHP: {(enemyHealth != null ? enemyHealth.CurrentHealth.ToString("F0") : "N/A")}\nAttacking: {isAttacking}";
        DrawWorldLabel(transform.position + Vector3.up * 2f, worldText, worldLabelStyle);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funci�n de depuraci�n para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[KronusEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[KronusEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[KronusEnemy] {message}");
                break;
            default:
                Debug.Log($"[KronusEnemy] {message}");
                break;
        }
    }
}