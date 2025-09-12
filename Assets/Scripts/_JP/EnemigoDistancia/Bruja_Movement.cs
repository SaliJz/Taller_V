// Bruja_Movement.cs (versión mejorada para evitar "jitter" al llegar)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Bruja_Movement : MonoBehaviour
{
    public Transform player;
    NavMeshAgent agent;

    [Header("Velocidades")]
    public float moveSpeed = 4f;
    public float retreatSpeed = 6f;

    [Header("Distancias y ajustes")]
    public float retreatDistance = 6f; // distancia objetivo al alejarse
    public float navSampleMaxDistance = 2f;

    [Header("Ajustes de llegada")]
    [Tooltip("Tolerancia (m) para considerar que el agente llegó y dejar de moverse (evita jitter).")]
    public float arrivalTolerance = 0.18f;

    Vector3 firePointOffset = Vector3.zero; // offset local desde transform hasta firePoint (si se usa)

    Coroutine retreatCoroutine;
    float originalSpeed;
    bool isAtDestination = false;

    // ----- Nuevas variables para pausar / reanudar movimiento -----
    bool isPaused = false;
    bool hadPathBeforePause = false;
    Vector3 lastDestination = Vector3.zero;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        originalSpeed = agent.speed;
        agent.updateRotation = false; // controlamos rotacion manualmente con FacePlayer para suavizar
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void LateUpdate()
    {
        // Comprueba llegada al destino para evitar que el agente siga corrigiendo su posición (jitter)
        if (agent == null) return;

        // Si ya está detenido por nosotros, garantizar velocity = 0
        if (agent.isStopped && isAtDestination)
        {
            agent.velocity = Vector3.zero;
            return;
        }

        if (agent.pathPending) return;

        // Si no hay path y no estamos moviéndonos, nada que comprobar
        if (!agent.hasPath) return;

        float remaining = agent.remainingDistance;
        // remainingDistance puede ser 0 cuando llega; considerar arrivalTolerance
        if (remaining <= arrivalTolerance || (agent.velocity.sqrMagnitude < 0.01f && remaining <= navSampleMaxDistance))
        {
            // Llegó: parar y limpiar path para evitar microajustes
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            isAtDestination = true;
        }
    }

    // Setear offset para simulaciones de line of sight cuando se prueban posiciones futuras
    public void SetFirePointOffset(Vector3 offset)
    {
        firePointOffset = offset;
    }

    // Mover hacia la posicion del player usando NavMeshAgent
    public void MoveTowardsPlayer()
    {
        if (player == null || agent == null) return;
        isAtDestination = false;
        agent.isStopped = false;
        agent.speed = moveSpeed;
        agent.SetDestination(player.position);
    }

    // Moverse a una posicion objetivo (por ejemplo posicion de flanqueo)
    public void MoveToPosition(Vector3 position)
    {
        if (agent == null) return;
        isAtDestination = false;
        agent.isStopped = false;
        agent.speed = moveSpeed;
        agent.SetDestination(position);
    }

    // Alejarse del player: calculamos un objetivo detras en el navmesh
    public void MoveAwayFromPlayer()
    {
        if (player == null || agent == null) return;
        isAtDestination = false;
        Vector3 dir = (transform.position - player.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward; // fallback

        Vector3 desired = transform.position + dir.normalized * retreatDistance;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(desired, out hit, navSampleMaxDistance, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.speed = retreatSpeed;
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.isStopped = false;
            agent.speed = retreatSpeed;
            agent.SetDestination(desired);
        }
    }

    // Detener movimiento inmediatamente (usar para que no haya jitter)
    public void StopMoving()
    {
        if (agent == null) return;
        agent.isStopped = true;
        agent.ResetPath();          // limpia path para evitar correcciones constantes
        agent.velocity = Vector3.zero;
        isAtDestination = true;
    }

    // ----- Nuevos métodos: pausar y reanudar movimiento -----
    // Pausa el agente guardando su destino actual (si lo tenía)
    public void PauseMovement()
    {
        if (agent == null) return;
        if (isPaused) return; // ya pausado, ignorar
        isPaused = true;

        // Guardar estado previo si existía un path activo
        hadPathBeforePause = agent.hasPath && !agent.isStopped;
        if (hadPathBeforePause)
        {
            lastDestination = agent.destination;
        }

        // Detener inmediatamente
        StopMoving();
    }

    // Reanuda el movimiento restaurando el destino previo si existía,
    // o dirigiéndose al player si no había destino.
    public void ResumeMovement()
    {
        if (agent == null) return;
        if (!isPaused) return; // no estaba pausado

        isPaused = false;
        isAtDestination = false;
        agent.isStopped = false;

        if (hadPathBeforePause)
        {
            agent.SetDestination(lastDestination);
        }
        else if (player != null)
        {
            // comportamiento por defecto: volver a perseguir al player
            agent.SetDestination(player.position);
        }
        // limpiar flags temporales
        hadPathBeforePause = false;
        lastDestination = Vector3.zero;
    }

    // Retirada rapida por interrupcion: alejarse un tiempo determinado
    public void RetreatForSeconds(float seconds, float extraMultiplier = 1f)
    {
        if (retreatCoroutine != null) StopCoroutine(retreatCoroutine);
        retreatCoroutine = StartCoroutine(RetreatRoutine(seconds, extraMultiplier));
    }

    IEnumerator RetreatRoutine(float seconds, float extraMultiplier)
    {
        float t = 0f;
        float savedSpeed = agent.speed;
        agent.speed = retreatSpeed * extraMultiplier;
        while (t < seconds)
        {
            MoveAwayFromPlayer();
            t += Time.deltaTime;
            yield return null;
        }
        agent.speed = savedSpeed;
    }

    // Mantener orientacion hacia player (para disparo)
    public void FacePlayer(float rotationSpeed = 10f)
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    // Obtener distancia actual al player
    public float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }

    // Devuelve true si una posicion dada es navegable (samplea el navmesh)
    public bool IsNavPositionReachable(Vector3 position, out Vector3 navPos)
    {
        navPos = transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, navSampleMaxDistance, NavMesh.AllAreas))
        {
            navPos = hit.position;
            return true;
        }
        return false;
    }
}


