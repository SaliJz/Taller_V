// Bruja_Movement.cs
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

    Vector3 firePointOffset = Vector3.zero; // offset local desde transform hasta firePoint (si se usa)

    Coroutine retreatCoroutine;
    float originalSpeed;

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

    // Setear offset para simulaciones de line of sight cuando se prueban posiciones futuras
    public void SetFirePointOffset(Vector3 offset)
    {
        firePointOffset = offset;
    }

    // Mover hacia la posicion del player usando NavMeshAgent
    public void MoveTowardsPlayer()
    {
        if (player == null || agent == null) return;
        agent.isStopped = false;
        agent.speed = moveSpeed;
        agent.SetDestination(player.position);
    }

    // Moverse a una posicion objetivo (por ejemplo posicion de flanqueo)
    public void MoveToPosition(Vector3 position)
    {
        if (agent == null) return;
        agent.isStopped = false;
        agent.speed = moveSpeed;
        agent.SetDestination(position);
    }

    // Alejarse del player: calculamos un objetivo detras en el navmesh
    public void MoveAwayFromPlayer()
    {
        if (player == null || agent == null) return;
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
            // si no encuentra muestra, intenta moverse en la direccion directa con la distancia pedida
            agent.isStopped = false;
            agent.speed = retreatSpeed;
            agent.SetDestination(desired);
        }
    }

    // Detener movimiento inmediatamente
    public void StopMoving()
    {
        if (agent == null) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
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


//using System.Collections;
//using UnityEngine;

//[RequireComponent(typeof(Rigidbody))]
//public class Bruja_Movement : MonoBehaviour
//{
//    public Transform player;
//    Rigidbody rb;

//    [Header("Velocidades")]
//    public float moveSpeed = 4f;
//    public float retreatSpeed = 6f;

//    [Header("Distancias")]
//    public float minSafeDistance = 5f;   // si el player esta por debajo de esto, la bruja se aleja
//    public float attackDistance = 10f;   // distancia target para poder disparar / orientarse

//    Coroutine retreatCoroutine;

//    void Awake()
//    {
//        rb = GetComponent<Rigidbody>();
//        if (player == null)
//        {
//            GameObject p = GameObject.FindGameObjectWithTag("Player");
//            if (p) player = p.transform;
//        }
//    }

//    void FixedUpdate()
//    {
//        // el movimiento real generalmente es manejado por las llamadas a MoveTowards/MoveAway/Stop
//        // dejamos el Update en el controlador para decidir que hacer.
//    }

//    public void MoveTowardsPlayer(float speedMultiplier = 1f)
//    {
//        if (player == null) return;
//        Vector3 dir = (player.position - transform.position);
//        dir.y = 0f;
//        dir.Normalize();
//        Vector3 newPos = transform.position + dir * moveSpeed * speedMultiplier * Time.fixedDeltaTime;
//        rb.MovePosition(newPos);
//    }

//    public void MoveAwayFromPlayer(float speedMultiplier = 1f)
//    {
//        if (player == null) return;
//        Vector3 dir = (transform.position - player.position);
//        dir.y = 0f;
//        dir.Normalize();
//        Vector3 newPos = transform.position + dir * retreatSpeed * speedMultiplier * Time.fixedDeltaTime;
//        rb.MovePosition(newPos);
//    }

//    public void StopMoving()
//    {
//        rb.velocity = Vector3.zero;
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
//        while (t < seconds)
//        {
//            MoveAwayFromPlayer(extraMultiplier);
//            t += Time.fixedDeltaTime;
//            yield return new WaitForFixedUpdate();
//        }
//    }

//    // opcion util: mantener orientacion hacia player (para disparo)
//    public void FacePlayer()
//    {
//        if (player == null) return;
//        Vector3 dir = player.position - transform.position;
//        dir.y = 0f;
//        if (dir.sqrMagnitude < 0.0001f) return;
//        Quaternion targetRot = Quaternion.LookRotation(dir);
//        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
//    }

//    // distancia actual al player
//    public float DistanceToPlayer()
//    {
//        if (player == null) return float.MaxValue;
//        return Vector3.Distance(transform.position, player.position);
//    }
//}
