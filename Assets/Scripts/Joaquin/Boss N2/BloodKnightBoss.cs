using UnityEngine;
using System.Collections;

public class BloodKnightBoss : MonoBehaviour
{
    #region Statistics and Configuration

    [Header("Boss Configuration")]
    [SerializeField] private float maxHealth = 300f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Attack 1 - Sodoma y Gomorra")]
    [SerializeField] private float sodomaDamage = 35f;
    [SerializeField] private float fireTrailDamage = 5f;
    [SerializeField] private float sodomaCooldown = 15f;
    [SerializeField] private float sodomaRange = 8f;
    [SerializeField] private float stunDuration = 4f;

    [Header("Attack 2 - Apocalipsis")]
    [SerializeField] private float apocalipsisDamage = 7f;
    [SerializeField] private float apocalipsisSpeed = 8f;
    [SerializeField] private int apocalipsisAttacks = 10;
    [SerializeField] private float attackInterval = 1f;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem fireTrailEffect;
    [SerializeField] private ParticleSystem armorGlowEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sodomaChargeSound;
    [SerializeField] private AudioClip sodomaAttackSound;
    [SerializeField] private AudioClip apocalipsisSound;

    #endregion

    #region Internal Variables

    private enum BossState
    {
        Idle,
        Moving,
        ChargingSodoma,
        ExecutingSodoma,
        ExecutingApocalipsis,
        Stunned,
        Recovery
    }

    private BossState currentState = BossState.Idle;
    private float stateTimer;
    private float lastAttackTime;
    private bool isVulnerableToCounter = false;
    private int consecutiveApocalipsisAttacks;

    private Vector3 targetPosition;
    private bool isMoving = false;

    private Vector3[] diagonalDirections = new Vector3[8];
    private int currentDiagonalIndex = 0;

    private PlayerHealth playerHealth;
    private Coroutine currentAttackCoroutine;

    #endregion

    #region Properties

    private void Start()
    {
        InitializeBoss();
    }

    private void InitializeBoss()
    {
        currentHealth = maxHealth;

        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;

        playerHealth = player?.GetComponent<PlayerHealth>();

        InitializeDiagonalDirections();

        StartCoroutine(BossAI());
    }

    private void InitializeDiagonalDirections()
    {
        float angle = 0f;
        for (int i = 0; i < 8; i++)
        {
            diagonalDirections[i] = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad));
            angle += 45f;
        }
    }

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        stateTimer += Time.deltaTime;
        HandleMovement();
        HandleCounterAttackWindow();
    }

    #endregion

    #region AI System

    private IEnumerator BossAI()
    {
        while (currentHealth > 0)
        {
            yield return new WaitForSeconds(0.5f);

            if (currentState == BossState.Idle || currentState == BossState.Recovery)
            {
                DecideNextAction();
            }
        }
    }

    private void DecideNextAction()
    {
        if (Time.time - lastAttackTime < 2f) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float randomValue = Random.Range(0f, 1f);

        if (ShouldUseSodoma(distanceToPlayer, randomValue))
        {
            StartSodomaYGomorra();
        }
        else if (ShouldUseApocalipsis(distanceToPlayer, randomValue))
        {
            StartApocalipsis();
        }
        else
        {
            MoveTowardsPlayer();
        }
    }

    private bool ShouldUseSodoma(float distance, float randomValue) => distance > 6f && randomValue < 0.7f;
    private bool ShouldUseApocalipsis(float distance, float randomValue) => distance <= 6f && randomValue < 0.6f;

    #endregion

    #region Movement System

    private void HandleMovement()
    {
        if (isMoving && currentState == BossState.Moving)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
            {
                isMoving = false;
                currentState = BossState.Idle;
            }
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        targetPosition = player.position - directionToPlayer * 4f;

        isMoving = true;
        currentState = BossState.Moving;
        animator.SetBool("IsMoving", true);
    }

    #endregion

    #region Attack 1 - Sodoma y Gomorra

    private void StartSodomaYGomorra()
    {
        if (Time.time - lastAttackTime < sodomaCooldown) return;

        if (currentAttackCoroutine != null) StopCoroutine(currentAttackCoroutine);

        currentAttackCoroutine = StartCoroutine(ExecuteSodomaYGomorra());
    }

    private IEnumerator ExecuteSodomaYGomorra()
    {
        currentState = BossState.ChargingSodoma;
        animator.SetBool("IsMoving", false);

        // Paso 1: Deslíza hacia atrás y agacha
        Vector3 backwardDirection = -transform.forward;
        Vector3 startPosition = transform.position;
        Vector3 backwardPosition = startPosition + backwardDirection * 2f;

        float slideTime = 0.8f;
        float elapsedTime = 0f;

        while (elapsedTime < slideTime)
        {
            transform.position = Vector3.Lerp(startPosition, backwardPosition, elapsedTime / slideTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Paso 2: Animación de carga y brillo de la armadura
        animator.SetTrigger("ChargeSodoma");
        armorGlowEffect.Play();
        audioSource.PlayOneShot(sodomaChargeSound);

        isVulnerableToCounter = true;

        yield return new WaitForSeconds(1.5f);

        if (currentState != BossState.Stunned)
        {
            // Paso 3: Ejecuta el ataque
            currentState = BossState.ExecutingSodoma;
            isVulnerableToCounter = false;

            // Mira hacia el jugador
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(directionToPlayer);

            // Dash hacia el jugador
            Vector3 dashTarget = player.position;
            float dashTime = 0.5f;
            elapsedTime = 0f;
            startPosition = transform.position;

            animator.SetTrigger("ExecuteSodoma");
            audioSource.PlayOneShot(sodomaAttackSound);
            fireTrailEffect.Play();

            while (elapsedTime < dashTime)
            {
                transform.position = Vector3.Lerp(startPosition, dashTarget, elapsedTime / dashTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ejecuta el ataque de arco de 180°
            DealSodomaDamage();

            // Mantiene activa la ruta de fuego
            StartCoroutine(FireTrailDamage());

            yield return new WaitForSeconds(1f);
        }

        // Recovery
        currentState = BossState.Recovery;
        armorGlowEffect.Stop();
        lastAttackTime = Time.time;

        yield return new WaitForSeconds(2f);

        currentState = BossState.Idle;
    }

    private void DealSodomaDamage()
    {
        // Compruebe si el jugador está delante del ataque en arco de 180 ° del jefe
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.forward, directionToPlayer);
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (dotProduct > 0 && distanceToPlayer <= sodomaRange) // El jugador está delante del arco frontal y en alcance.
        {
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(sodomaDamage);
            }
        }
    }

    private IEnumerator FireTrailDamage()
    {
        float trailDuration = 3f;
        float damageInterval = 1f;
        float elapsed = 0f;

        while (elapsed < trailDuration)
        {
            // Check if player is near the fire trail
            if (Vector3.Distance(transform.position, player.position) <= 3f)
            {
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(fireTrailDamage);
                }
            }

            yield return new WaitForSeconds(damageInterval);
            elapsed += damageInterval;
        }

        fireTrailEffect.Stop();
    }

    #endregion

    #region Attack 2 - Apocalipsis

    private void StartApocalipsis()
    {
        if (currentAttackCoroutine != null) StopCoroutine(currentAttackCoroutine);

        currentAttackCoroutine = StartCoroutine(ExecuteApocalipsis());
    }

    private IEnumerator ExecuteApocalipsis()
    {
        currentState = BossState.ExecutingApocalipsis;
        animator.SetBool("IsMoving", false);
        animator.SetTrigger("StartApocalipsis");

        audioSource.PlayOneShot(apocalipsisSound);

        consecutiveApocalipsisAttacks = 0;

        for (int i = 0; i < apocalipsisAttacks; i++)
        {
            yield return StartCoroutine(ExecuteDiagonalAttack());
            consecutiveApocalipsisAttacks++;
        }

        // Recovery
        currentState = BossState.Recovery;
        lastAttackTime = Time.time;

        yield return new WaitForSeconds(1.5f);

        currentState = BossState.Idle;
    }

    private IEnumerator ExecuteDiagonalAttack()
    {
        // Choose diagonal direction around player
        Vector3 playerPosition = player.position;
        Vector3 diagonalDirection = diagonalDirections[currentDiagonalIndex];
        currentDiagonalIndex = (currentDiagonalIndex + 1) % 8;

        // Calculate slide target position
        Vector3 slideTarget = playerPosition + diagonalDirection * 4f;
        Vector3 startPosition = transform.position;

        // Slide diagonally
        float slideTime = 0.8f;
        float elapsedTime = 0f;

        // Look towards player
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(directionToPlayer);

        while (elapsedTime < slideTime)
        {
            transform.position = Vector3.Lerp(startPosition, slideTarget, elapsedTime / slideTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Execute attack (slash or thrust based on distance)
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= 2.5f) // Close range - slash
        {
            animator.SetTrigger("Slash");
            yield return new WaitForSeconds(0.2f);
            DealApocalipsisDamage(2.5f); // Slash range
        }
        else if (distanceToPlayer <= 4f) // Medium range - thrust
        {
            animator.SetTrigger("Thrust");
            yield return new WaitForSeconds(0.3f);
            DealApocalipsisDamage(4f); // Thrust range
        }

        yield return new WaitForSeconds(attackInterval - 0.5f); // Remaining time to complete 1 second interval
    }

    private void DealApocalipsisDamage(float range)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= range)
        {
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(apocalipsisDamage);
            }
        }
    }

    #endregion

    #region Counter Attack System

    private void HandleCounterAttackWindow()
    {
        // This method should be called when player attacks during vulnerable window
        if (isVulnerableToCounter && currentState == BossState.ChargingSodoma)
        {
            // Check if player is attacking (this would be triggered by player's attack system)
            // For now, we'll use a placeholder - in your game, you'd check for player input/attack
            if (Input.GetKeyDown(KeyCode.Space)) // Placeholder for player attack
            {
                StartCoroutine(GetStunned());
            }
        }
    }

    public void OnPlayerCounterAttack()
    {
        if (isVulnerableToCounter && currentState == BossState.ChargingSodoma)
        {
            StartCoroutine(GetStunned());
        }
    }

    private IEnumerator GetStunned()
    {
        StopAllCoroutines(); // Stop current attack

        currentState = BossState.Stunned;
        isVulnerableToCounter = false;

        animator.SetTrigger("Stunned");
        armorGlowEffect.Stop();
        fireTrailEffect.Stop();

        // Visual feedback for stun
        // Add screen shake, particle effects, etc.

        yield return new WaitForSeconds(stunDuration);

        currentState = BossState.Idle;
        StartCoroutine(BossAI()); // Restart AI
    }

    #endregion

    #region Health System

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        StopAllCoroutines();
        animator.SetTrigger("Death");

        // Add death effects, drop loot, etc.

        Destroy(gameObject, 3f);
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        // Draw attack ranges
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sodomaRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 4f); // Apocalipsis thrust range

        Gizmos.color = new Color(1f, 0.5f, 0f); // Orange color (RGB: 255, 128, 0)
        Gizmos.DrawWireSphere(transform.position, 2.5f); // Apocalipsis slash range
    }

    #endregion
}