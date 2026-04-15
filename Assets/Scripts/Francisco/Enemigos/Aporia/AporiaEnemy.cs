using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class AporiaEnemy : MonoBehaviour
{
    #region Headers
    [Header("Referencias")]
    [SerializeField] private AporiaAnimCtrl animCtrl;
    [SerializeField] private Transform hitPoint;
    [SerializeField] private GameObject groundIndicatorPrefab;
    [SerializeField] private GameObject tongueVFXPrefab;
    [SerializeField] private GameObject nestPrefab;

    [Header("Patrullaje (Wander)")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float wanderWaitTime = 3f;
    private float wanderTimer;

    [Header("Estadisticas Base")]
    [SerializeField] private float health = 35f;
    [SerializeField] private float moveSpeed = 6.5f;

    [Header("Dash Erratico")]
    [SerializeField] private float dashDuration = 0.5f;
    [SerializeField] private float dashMaxDistance = 12f;
    [SerializeField] private float preparationTime = 0.20f;

    [Header("Ataque Lengua Putra")]
    [SerializeField] private float attackDamage = 30f;
    [SerializeField] private float attackRadius = 2.5f;
    [SerializeField] private float hitDelay = 0.35f;
    [SerializeField] private float recoveryTime = 1.5f;
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Percepcion")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float dashActivationDistance = 10f;

    [Header("Cooldowns")]
    [SerializeField] private float cooldownShort = 1.2f;
    [SerializeField] private float cooldownMedium = 1.8f;
    [SerializeField] private float cooldownLong = 2.5f;

    [Header("Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private AudioClip damageSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("Capas")]
    [SerializeField] private LayerMask groundLayer = ~0;
    #endregion

    #region Private Variables
    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private bool isAttacking = false;
    private float attackTimer;
    private float currentCooldown;

    private GameObject pooledIndicator;
    private GameObject pooledTongue;
    private GameObject pooledNest;
    #endregion

    #region Unity Events
    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        if (animCtrl == null) animCtrl = GetComponent<AporiaAnimCtrl>();
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            playerTransform = player.transform;
        }

        InitializeEnemy();
        SetupPool();
        currentCooldown = cooldownMedium;
        wanderTimer = wanderWaitTime; 
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeath;
            enemyHealth.OnDamaged += PlayDamageSFX;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= PlayDamageSFX;
        }
    }

    private void Update()
    {
        if (!enabled || isAttacking || (enemyHealth != null && (enemyHealth.IsStunned || enemyHealth.IsDead))) return;

        HandleLocomotion();

        float dist = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : float.MaxValue;

        if (dist <= detectionRadius)
        {
            agent.SetDestination(playerTransform.position);
            attackTimer += Time.deltaTime;

            if (attackTimer >= currentCooldown && dist <= dashActivationDistance)
            {
                StartCoroutine(ErraticDashRoutine());
            }
        }
        else
        {
            HandlePatrol();
        }
    }
    #endregion

    #region Core Logic
    private void HandlePatrol()
    {
        wanderTimer += Time.deltaTime;

        if (wanderTimer >= wanderWaitTime)
        {
            Vector3 newPos = GetRandomNavPos(transform.position, wanderRadius);
            agent.SetDestination(newPos);
            wanderTimer = 0f;
        }
    }

    private Vector3 GetRandomNavPos(Vector3 origin, float distance)
    {
        Vector3 randomDirection = Random.insideUnitSphere * distance;
        randomDirection += origin;
        NavMeshHit navHit;
        NavMesh.SamplePosition(randomDirection, out navHit, distance, -1);
        return navHit.position;
    }

    private void InitializeEnemy()
    {
        if (enemyHealth != null) enemyHealth.SetMaxHealth(health);
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRadius;
        }
    }

    private void SetupPool()
    {
        if (groundIndicatorPrefab)
        {
            pooledIndicator = Instantiate(groundIndicatorPrefab);
            pooledIndicator.SetActive(false);
        }
        if (tongueVFXPrefab)
        {
            pooledTongue = Instantiate(tongueVFXPrefab);
            pooledTongue.SetActive(false);
        }
        if (nestPrefab)
        {
            pooledNest = Instantiate(nestPrefab);
            pooledNest.SetActive(false);
        }
    }

    private void HandleLocomotion()
    {
        if (animCtrl != null && agent != null && agent.enabled)
        {
            Vector3 velocity = agent.velocity;
            animCtrl.h = velocity.x;
            animCtrl.v = velocity.z;
        }
    }

    private void PlayDamageSFX()
    {
        if (audioSource && damageSFX) audioSource.PlayOneShot(damageSFX);
    }

    private void HandleDeath(GameObject e)
    {
        if (e != gameObject) return;
        if (audioSource && deathSFX) audioSource.PlayOneShot(deathSFX);
        if (animCtrl) animCtrl.PlayDeath();

        agent.enabled = false;
        this.enabled = false;
    }
    #endregion

    #region Combat Routines
    private IEnumerator ErraticDashRoutine()
    {
        isAttacking = true;
        attackTimer = 0f;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
        }

        Vector3 targetDir = (playerTransform.position - transform.position).normalized;
        targetDir.y = 0;
        transform.rotation = Quaternion.LookRotation(targetDir);

        yield return new WaitForSeconds(preparationTime);

        if (animCtrl) animCtrl.PlayDash();
        if (audioSource && dashSFX) audioSource.PlayOneShot(dashSFX);

        Vector3 startPos = transform.position;
        Vector3 dashEnd = startPos + targetDir * dashMaxDistance;

        NavMeshHit hit;
        if (NavMesh.Raycast(startPos, dashEnd, out hit, NavMesh.AllAreas))
        {
            dashEnd = hit.position;
        }

        float elapsed = 0;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 nextPos = Vector3.Lerp(startPos, dashEnd, elapsed / dashDuration);

            if (Physics.Raycast(nextPos + Vector3.up, Vector3.down, 2f, groundLayer))
            {
                transform.position = nextPos;
            }

            if (Vector3.Distance(transform.position, playerTransform.position) < attackRadius * 0.8f) break;

            yield return null;
        }

        yield return PerformTongueAttack();

        if (agent.enabled)
        {
            agent.Warp(transform.position);
            agent.updatePosition = true;
            agent.isStopped = false;
        }

        isAttacking = false;
        float[] options = { cooldownShort, cooldownMedium, cooldownLong };
        currentCooldown = options[Random.Range(0, options.Length)];
    }

    private IEnumerator PerformTongueAttack()
    {
        if (animCtrl) animCtrl.Invoke("PlayAttack", 0);

        if (pooledIndicator)
        {
            pooledIndicator.transform.position = hitPoint.position;
            pooledIndicator.SetActive(true);
            StartCoroutine(DeactivateAfterDelay(pooledIndicator, hitDelay));
        }

        yield return new WaitForSeconds(hitDelay);

        if (audioSource && attackSFX) audioSource.PlayOneShot(attackSFX);

        if (pooledTongue)
        {
            pooledTongue.transform.position = hitPoint.position;
            pooledTongue.transform.rotation = transform.rotation;
            pooledTongue.SetActive(true);
            StartCoroutine(DeactivateAfterDelay(pooledTongue, 0.2f));
        }

        Collider[] targets = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(attackDamage);
                ApplyKnockback(t.transform);
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (pooledNest)
        {
            pooledNest.transform.position = hitPoint.position;
            pooledNest.SetActive(true);
        }

        yield return new WaitForSeconds(recoveryTime);
    }

    private IEnumerator DeactivateAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        obj.SetActive(false);
    }

    private void ApplyKnockback(Transform target)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (target.TryGetComponent<CharacterController>(out var cc))
        {
            StartCoroutine(KnockbackTick(cc, dir * knockbackForce));
        }
    }

    private IEnumerator KnockbackTick(CharacterController cc, Vector3 force)
    {
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            if (cc) cc.Move(force * Time.deltaTime);
            yield return null;
        }
    }
    #endregion
}