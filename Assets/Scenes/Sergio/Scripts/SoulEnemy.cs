using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class SoulEnemy : MonoBehaviour
{
    [Header("Patrolling")]
    public List<Transform> patrolPoints; 
    private int currentPointIndex = 0;
    public float baseSpeed = 4f;

    [Header("Detection & Chase")]
    public Transform target;
    public float detectionRange = 10f;
    public float chaseSpeed = 8f;

    [Header("Attack Logic")]
    public float attackDistance = 1.8f;
    public float paralyzeDuration = 2f;
    public float attackCooldown = 2f;
    private bool isAttacking = false;

    [Header("Stats")]
    public float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    [Header("Death Effects")]
    public float explosionForce = 5f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private Color originalColor;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        originalColor = meshRenderer.material.color;
        currentHealth = maxHealth;

        rb.isKinematic = true; 
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (target == null) 
            target = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (patrolPoints.Count > 0) GoToNextPatrolPoint();
    }

    void Update()
    {
        if (isDead || target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= detectionRange)
        {
            ChasePlayer();
            if (distance <= attackDistance && !isAttacking) 
            {
                StartCoroutine(ExecuteBite());
            }
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        if (patrolPoints.Count == 0 || isAttacking) return;
        agent.isStopped = false;
        agent.speed = baseSpeed;
        
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToNextPatrolPoint();
        }
    }

    void GoToNextPatrolPoint()
    {
        agent.destination = patrolPoints[currentPointIndex].position;
        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
    }

    void ChasePlayer()
    {
        if (isAttacking) return;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(target.position);
    }

    IEnumerator ExecuteBite()
    {
        isAttacking = true;
        agent.isStopped = true;
        agent.updateRotation = false;
        
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        Debug.Log("Te mordió ,no TE PUEDES MOVER");

        BlueWill playerScript = target.GetComponent<BlueWill>();
        if (playerScript != null)
        {
            playerScript.canMove = false;
            yield return new WaitForSeconds(paralyzeDuration);
            playerScript.canMove = true;
        }

        yield return new WaitForSeconds(attackCooldown);

        agent.updateRotation = true; 
        agent.isStopped = false;
        isAttacking = false;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        currentHealth -= amount;
        StartCoroutine(HitFlash());
        if (currentHealth <= 0) Die();
    }

    IEnumerator HitFlash()
    {
        meshRenderer.material.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        meshRenderer.material.color = originalColor;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        agent.isStopped = true;
        agent.enabled = false;

        Transform[] pieces = GetComponentsInChildren<Transform>();

        foreach (Transform piece in pieces)
        {
            if (piece == transform) continue;

            piece.SetParent(null);

            Rigidbody pRb = piece.gameObject.GetComponent<Rigidbody>();
            if (pRb == null) pRb = piece.gameObject.AddComponent<Rigidbody>();

            Collider pCol = piece.gameObject.GetComponent<Collider>();
            if (pCol == null) pCol = piece.gameObject.AddComponent<MeshCollider>();
            
            pRb.isKinematic = false;
            pRb.useGravity = true;

            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1f), Random.Range(-1f, 1f));
            pRb.AddForce(randomDir * explosionForce, ForceMode.Impulse);

            Destroy(piece.gameObject, 5f);
        }

        Destroy(gameObject, 0.1f);
    }
}