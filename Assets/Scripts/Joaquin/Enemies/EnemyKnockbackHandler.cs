using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyKnockbackHandler : MonoBehaviour
{
    private NavMeshAgent agent;
    private Coroutine knockbackCoroutine;

    private bool isMovementPausedByStun = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void StopMovement(bool stop)
    {
        isMovementPausedByStun = stop;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = stop;
        }

        if (!stop && knockbackCoroutine == null && agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }
    }

    /// <summary>
    /// Método público que inicia el proceso de empuje.
    /// </summary>
    public void TriggerKnockback(Vector3 direction, float force, float duration)
    {
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        Vector3 start = transform.position;
        Vector3 target = start + direction * force;

        float timer = 0;

        while (timer < duration)
        {
            transform.position = Vector3.Lerp(start, target, timer / duration);

            timer += Time.deltaTime;
            yield return null;
        }

        if (agent != null && !agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
                agent.enabled = true;

                if (agent.isOnNavMesh)
                {
                    agent.isStopped = isMovementPausedByStun;
                }
            }
            else
            {
                Debug.LogWarning($"[EnemyKnockbackHandler] {gameObject.name} no pudo reactivarse - no está en NavMesh");
            }
        }

        knockbackCoroutine = null;
    }
}