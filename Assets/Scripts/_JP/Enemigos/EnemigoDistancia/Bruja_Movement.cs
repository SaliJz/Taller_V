// Bruja_Movement.cs (versi�n mejorada para respetar distancias y evitar jitter)
using System.Collections;
using System.Collections.Generic;
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
    public float navSampleMaxDistance = 2f; // radio para samplear el NavMesh cuando convertimos destinos
    public float arrivalTolerance = 0.18f; // tolerancia para considerar llegada

    [Header("Retries y ajustes de b�squeda")]
    [Tooltip("Cu�ntas muestras alternativas intentar para encontrar un punto navegable cuando el objetivo no est� en el NavMesh")]
    public int fallbackSamples = 8;
    [Tooltip("Distancia m�xima para buscar fallback (se usar� alrededor del punto deseado)")]
    public float fallbackRadius = 2.5f;

    Vector3 firePointOffset = Vector3.zero; // offset local desde transform hasta firePoint (si se usa)

    Coroutine retreatCoroutine;
    float originalSpeed;
    bool isAtDestination = false;

    // pausa / reanudar
    bool isPaused = false;
    bool hadPathBeforePause = false;
    Vector3 lastDestination = Vector3.zero;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        originalSpeed = agent.speed;
        agent.updateRotation = false; // rotamos manualmente para suavizar
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void LateUpdate()
    {
        if (agent == null) return;

        // Si ya est� detenido por nosotros, garantizar velocity = 0
        if (agent.isStopped && isAtDestination)
        {
            agent.velocity = Vector3.zero;
            return;
        }

        // Si el agente est� calculando el path, no forzamos checks que puedan interferir
        if (agent.pathPending) return;

        // Si no hay path, nada que comprobar
        if (!agent.hasPath)
        {
            // marcado como en destino si no tiene path y no est� movi�ndose
            isAtDestination = true;
            return;
        }

        // Solo cuando hay path y est� completo, comprobamos remainingDistance
        if (agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            float remaining = agent.remainingDistance;
            // Considerar arrived si remaining <= arrivalTolerance
            if (remaining <= arrivalTolerance || (agent.velocity.sqrMagnitude < 0.01f && remaining <= navSampleMaxDistance))
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                isAtDestination = true;
            }
        }
    }

    // ----- Helpers p�blicos -----
    public bool IsNavigating()
    {
        if (agent == null) return false;
        return agent.hasPath && !agent.pathPending && !agent.isStopped && agent.pathStatus == NavMeshPathStatus.PathComplete;
    }

    // Comprueba si ya estamos navegando hacia una posici�n pr�xima (evita reasignar dest constantemente)
    public bool IsNavigatingTo(Vector3 worldTarget, float tolerance = 0.6f)
    {
        if (agent == null || !agent.hasPath) return false;
        Vector3 currentDest = agent.destination;
        return Vector3.Distance(currentDest, worldTarget) <= tolerance;
    }

    // Conversor seguro: intenta samplear navmesh y devuelve true si encontr� una posici�n navegable
    bool TryGetNavPosition(Vector3 worldPoint, out Vector3 navPos, float sampleRadius = -1f)
    {
        navPos = worldPoint;
        if (agent == null) return false;
        float radius = (sampleRadius > 0f) ? sampleRadius : navSampleMaxDistance;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(worldPoint, out hit, radius, NavMesh.AllAreas))
        {
            navPos = hit.position;
            return true;
        }
        return false;
    }

    // Intenta setear destino de forma segura: samplea navmesh, prueba fallbacks y solo setea si es distinto al destino actual.
    bool TrySetNavDestination(Vector3 worldTarget, float sampleRadius = -1f)
    {
        if (agent == null) return false;

        Vector3 navPos;
        // 1) intento directo
        if (TryGetNavPosition(worldTarget, out navPos, sampleRadius))
        {
            if (!IsNavigatingTo(navPos, 0.1f))
            {
                agent.isStopped = false;
                agent.speed = moveSpeed;
                agent.SetDestination(navPos);
                isAtDestination = false;
            }
            return true;
        }

        // 2) intentos fallback alrededor del objetivo (c�rculo)
        for (int i = 0; i < fallbackSamples; i++)
        {
            float ang = (360f / fallbackSamples) * i;
            Vector3 offset = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad)) * fallbackRadius;
            Vector3 alt = worldTarget + offset;
            if (TryGetNavPosition(alt, out navPos, sampleRadius))
            {
                if (!IsNavigatingTo(navPos, 0.1f))
                {
                    agent.isStopped = false;
                    agent.speed = moveSpeed;
                    agent.SetDestination(navPos);
                    isAtDestination = false;
                }
                return true;
            }
        }

        // 3) si todo falla, no seteamos destino (evita pedidos a puntos inalcanzables)
        return false;
    }

    // ----- API p�blica -----

    public void SetFirePointOffset(Vector3 offset)
    {
        firePointOffset = offset;
    }

    // Mover hacia la posicion del player usando NavMeshAgent (usar TryGetNavPosition para evitar destinos fuera del NavMesh)
    public void MoveTowardsPlayer()
    {
        if (player == null || agent == null) return;

        // No reasignar destino si ya vamos al player (o a una posici�n equivalente)
        if (IsNavigatingTo(player.position, 0.5f)) return;

        isAtDestination = false;
        agent.isStopped = false;
        agent.speed = moveSpeed;

        // Try set nav dest (si falla, no seteamos nada)
        bool ok = TrySetNavDestination(player.position, navSampleMaxDistance);
        if (!ok)
        {
            // si no conseguimos samplear el punto exacto del player, intentamos samplear cerca del player con mayor radio
            TrySetNavDestination(player.position, Mathf.Max(navSampleMaxDistance, 3f));
        }
    }

    // Moverse a una posicion objetivo (por ejemplo posicion de flanqueo)
    public void MoveToPosition(Vector3 position)
    {
        if (agent == null) return;

        // Evitamos reasignar si ya vamos a esa posici�n
        if (IsNavigatingTo(position, 0.5f)) return;

        isAtDestination = false;
        agent.isStopped = false;
        agent.speed = moveSpeed;

        TrySetNavDestination(position, navSampleMaxDistance);
    }

    // Alejarse del player: calculamos un objetivo detras en el navmesh y nos aseguramos que la nueva posicion aumente la distancia al player
    public void MoveAwayFromPlayer()
    {
        if (player == null || agent == null) return;
        isAtDestination = false;

        Vector3 dir = (transform.position - player.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;

        Vector3 desired = transform.position + dir.normalized * retreatDistance;

        // Intento encontrar posici�n navegable cerca de "desired"
        Vector3 navPos;
        bool found = TryGetNavPosition(desired, out navPos, Mathf.Max(navSampleMaxDistance, retreatDistance * 0.5f));

        if (!found)
        {
            // si el punto directo no es navegable, pruebo a lo largo de la direcci�n en pasos decrecientes
            const int steps = 6;
            for (int s = 1; s <= steps && !found; s++)
            {
                float factor = 1f - (s / (float)(steps + 1));
                Vector3 test = transform.position + dir.normalized * (retreatDistance * factor);
                if (TryGetNavPosition(test, out navPos, Mathf.Max(navSampleMaxDistance, 1f)))
                {
                    found = true;
                    break;
                }
            }
        }

        // Si se encontr� una posici�n navegable, verificamos que (aproximadamente) aumente la distancia al player,
        // si no, intentamos muestrear puntos alrededor para buscar uno que s� aumente la distancia.
        if (found)
        {
            float currentDist = Vector3.Distance(transform.position, player.position);
            float newDist = Vector3.Distance(navPos, player.position);

            if (newDist < currentDist + 0.2f) // no aument� lo suficiente
            {
                // fallback: samplear en un anillo alrededor del player buscando puntos navegables y m�s lejanos
                for (int i = 0; i < fallbackSamples; i++)
                {
                    float ang = (360f / fallbackSamples) * i;
                    Vector3 samplePoint = player.position + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad)) * (retreatDistance + 0.5f);
                    if (TryGetNavPosition(samplePoint, out navPos, fallbackRadius))
                    {
                        float candidateDist = Vector3.Distance(navPos, player.position);
                        if (candidateDist > currentDist + 0.5f)
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }
        }

        if (found)
        {
            // No reasignar si ya estamos navegando hacia ese navPos
            if (!IsNavigatingTo(navPos, 0.1f))
            {
                agent.isStopped = false;
                agent.speed = retreatSpeed;
                agent.SetDestination(navPos);
                isAtDestination = false;
            }
        }
        else
        {
            // Como �ltimo recurso, si no hay punto navegable que aumente distancia, intentamos marcharnos en la direcci�n local (aunque sea parcial)
            Vector3 fallback = transform.position + dir.normalized * (retreatDistance * 0.4f);
            TrySetNavDestination(fallback, navSampleMaxDistance);
            agent.speed = retreatSpeed;
        }
    }

    // Detener movimiento inmediatamente (usar para que no haya jitter)
    public void StopMoving()
    {
        if (agent == null) return;
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        isAtDestination = true;
    }

    // pausa / reanudar
    public void PauseMovement()
    {
        if (agent == null) return;
        if (isPaused) return;
        isPaused = true;
        hadPathBeforePause = agent.hasPath && !agent.isStopped;
        if (hadPathBeforePause) lastDestination = agent.destination;
        StopMoving();
    }

    public void ResumeMovement()
    {
        if (agent == null) return;
        if (!isPaused) return;
        isPaused = false;
        isAtDestination = false;
        agent.isStopped = false;

        if (hadPathBeforePause)
        {
            TrySetNavDestination(lastDestination, navSampleMaxDistance);
        }
        else if (player != null)
        {
            TrySetNavDestination(player.position, navSampleMaxDistance);
        }

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

    // Obtener distancia actual al player (posici�n transform)
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

    // Draw gizmos para debug
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
        Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, navSampleMaxDistance);
    }
}


//// Bruja_Movement.cs (versi�n mejorada para evitar "jitter" al llegar)
//using System.Collections;
//using UnityEngine;
//using UnityEngine.AI;

//[RequireComponent(typeof(NavMeshAgent))]
//public class Bruja_Movement : MonoBehaviour
//{
//    public Transform player;
//    NavMeshAgent agent;

//    [Header("Velocidades")]
//    public float moveSpeed = 4f;
//    public float retreatSpeed = 6f;

//    [Header("Distancias y ajustes")]
//    public float retreatDistance = 6f; // distancia objetivo al alejarse
//    public float navSampleMaxDistance = 2f;

//    [Header("Ajustes de llegada")]
//    [Tooltip("Tolerancia (m) para considerar que el agente lleg� y dejar de moverse (evita jitter).")]
//    public float arrivalTolerance = 0.18f;

//    Vector3 firePointOffset = Vector3.zero; // offset local desde transform hasta firePoint (si se usa)

//    Coroutine retreatCoroutine;
//    float originalSpeed;
//    bool isAtDestination = false;

//    // ----- Nuevas variables para pausar / reanudar movimiento -----
//    bool isPaused = false;
//    bool hadPathBeforePause = false;
//    Vector3 lastDestination = Vector3.zero;

//    void Awake()
//    {
//        agent = GetComponent<NavMeshAgent>();
//        originalSpeed = agent.speed;
//        agent.updateRotation = false; // controlamos rotacion manualmente con FacePlayer para suavizar
//        if (player == null)
//        {
//            GameObject p = GameObject.FindGameObjectWithTag("Player");
//            if (p) player = p.transform;
//        }
//    }

//    void LateUpdate()
//    {
//        // Comprueba llegada al destino para evitar que el agente siga corrigiendo su posici�n (jitter)
//        if (agent == null) return;

//        // Si ya est� detenido por nosotros, garantizar velocity = 0
//        if (agent.isStopped && isAtDestination)
//        {
//            agent.velocity = Vector3.zero;
//            return;
//        }

//        if (agent.pathPending) return;

//        // Si no hay path y no estamos movi�ndonos, nada que comprobar
//        if (!agent.hasPath) return;

//        float remaining = agent.remainingDistance;
//        // remainingDistance puede ser 0 cuando llega; considerar arrivalTolerance
//        if (remaining <= arrivalTolerance || (agent.velocity.sqrMagnitude < 0.01f && remaining <= navSampleMaxDistance))
//        {
//            // Lleg�: parar y limpiar path para evitar microajustes
//            agent.isStopped = true;
//            agent.ResetPath();
//            agent.velocity = Vector3.zero;
//            isAtDestination = true;
//        }
//    }

//    // Setear offset para simulaciones de line of sight cuando se prueban posiciones futuras
//    public void SetFirePointOffset(Vector3 offset)
//    {
//        firePointOffset = offset;
//    }

//    // Mover hacia la posicion del player usando NavMeshAgent
//    public void MoveTowardsPlayer()
//    {
//        if (player == null || agent == null) return;
//        isAtDestination = false;
//        agent.isStopped = false;
//        agent.speed = moveSpeed;
//        agent.SetDestination(player.position);
//    }

//    // Moverse a una posicion objetivo (por ejemplo posicion de flanqueo)
//    public void MoveToPosition(Vector3 position)
//    {
//        if (agent == null) return;
//        isAtDestination = false;
//        agent.isStopped = false;
//        agent.speed = moveSpeed;
//        agent.SetDestination(position);
//    }

//    // Alejarse del player: calculamos un objetivo detras en el navmesh
//    public void MoveAwayFromPlayer()
//    {
//        if (player == null || agent == null) return;
//        isAtDestination = false;
//        Vector3 dir = (transform.position - player.position);
//        dir.y = 0f;
//        if (dir.sqrMagnitude < 0.01f) dir = transform.forward; // fallback

//        Vector3 desired = transform.position + dir.normalized * retreatDistance;
//        NavMeshHit hit;
//        if (NavMesh.SamplePosition(desired, out hit, navSampleMaxDistance, NavMesh.AllAreas))
//        {
//            agent.isStopped = false;
//            agent.speed = retreatSpeed;
//            agent.SetDestination(hit.position);
//        }
//        else
//        {
//            agent.isStopped = false;
//            agent.speed = retreatSpeed;
//            agent.SetDestination(desired);
//        }
//    }

//    // Detener movimiento inmediatamente (usar para que no haya jitter)
//    public void StopMoving()
//    {
//        if (agent == null) return;
//        agent.isStopped = true;
//        agent.ResetPath();          // limpia path para evitar correcciones constantes
//        agent.velocity = Vector3.zero;
//        isAtDestination = true;
//    }

//    // ----- Nuevos m�todos: pausar y reanudar movimiento -----
//    // Pausa el agente guardando su destino actual (si lo ten�a)
//    public void PauseMovement()
//    {
//        if (agent == null) return;
//        if (isPaused) return; // ya pausado, ignorar
//        isPaused = true;

//        // Guardar estado previo si exist�a un path activo
//        hadPathBeforePause = agent.hasPath && !agent.isStopped;
//        if (hadPathBeforePause)
//        {
//            lastDestination = agent.destination;
//        }

//        // Detener inmediatamente
//        StopMoving();
//    }

//    // Reanuda el movimiento restaurando el destino previo si exist�a,
//    // o dirigi�ndose al player si no hab�a destino.
//    public void ResumeMovement()
//    {
//        if (agent == null) return;
//        if (!isPaused) return; // no estaba pausado

//        isPaused = false;
//        isAtDestination = false;
//        agent.isStopped = false;

//        if (hadPathBeforePause)
//        {
//            agent.SetDestination(lastDestination);
//        }
//        else if (player != null)
//        {
//            // comportamiento por defecto: volver a perseguir al player
//            agent.SetDestination(player.position);
//        }
//        // limpiar flags temporales
//        hadPathBeforePause = false;
//        lastDestination = Vector3.zero;
//    }

//    // Retirada rapida por interrupcion: alejarse un tiempo determinado
//    public void RetreatForSeconds(float seconds, float extraMultiplier = 1f)
//    {
//        if (retreatCoroutine != null) StopCoroutine(retreatCoroutine);
//        retreatCoroutine = StartCoroutine(RetreatRoutine(seconds, extraMultiplier));
//    }

//    IEnumerator RetreatRoutine(float seconds, float extraMultiplier)
//    {
//        float t = 0f;
//        float savedSpeed = agent.speed;
//        agent.speed = retreatSpeed * extraMultiplier;
//        while (t < seconds)
//        {
//            MoveAwayFromPlayer();
//            t += Time.deltaTime;
//            yield return null;
//        }
//        agent.speed = savedSpeed;
//    }

//    // Mantener orientacion hacia player (para disparo)
//    public void FacePlayer(float rotationSpeed = 10f)
//    {
//        if (player == null) return;
//        Vector3 dir = player.position - transform.position;
//        dir.y = 0f;
//        if (dir.sqrMagnitude < 0.0001f) return;
//        Quaternion targetRot = Quaternion.LookRotation(dir);
//        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
//    }

//    // Obtener distancia actual al player
//    public float DistanceToPlayer()
//    {
//        if (player == null) return float.MaxValue;
//        return Vector3.Distance(transform.position, player.position);
//    }

//    // Devuelve true si una posicion dada es navegable (samplea el navmesh)
//    public bool IsNavPositionReachable(Vector3 position, out Vector3 navPos)
//    {
//        navPos = transform.position;
//        NavMeshHit hit;
//        if (NavMesh.SamplePosition(position, out hit, navSampleMaxDistance, NavMesh.AllAreas))
//        {
//            navPos = hit.position;
//            return true;
//        }
//        return false;
//    }
//}


