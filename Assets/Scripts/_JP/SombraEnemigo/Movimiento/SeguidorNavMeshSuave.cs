using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SeguidorNavMeshSuave : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Si se deja vacio buscara el GameObject con el tag especificado")]
    public Transform objetivo;
    [Tooltip("Tag usado si no se asigna objetivo manualmente")]
    public string tagObjetivo = "Player";

    [Header("Movimiento")]
    [Tooltip("Distancia a la que el agente se detiene")]
    public float distanciaParada = 2f;
    [Tooltip("Velocidad con la que gira hacia la direccion de movimiento")]
    public float velocidadRotacion = 8f;
    [Tooltip("Tiempo (s) entre actualizaciones de destino para NavMesh (optimizacion)")]
    public float tasaActualizacionDestino = 0.15f;
    [Tooltip("Lerp para suavizar cambios de velocidad (0..1)")]
    [Range(0f, 1f)]
    public float velocidadLerp = 0.2f;

    // NUEVO: multiplicador que puede cambiarse desde otros scripts
    [Header("Multiplicador externo")]
    [Tooltip("Multiplicador que afecta la velocidad base del agente (1 = normal)")]
    public float multiplicadorVelocidad = 1f;

    NavMeshAgent agente;
    float ultimaActualizacion = -999f;
    float velocidadInicial;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        if (agente == null)
        {
            Debug.LogError("NavMeshAgent requerido.");
            enabled = false;
            return;
        }

        // No dejar que el agente rote automaticamente: lo hacemos manualmente para suavizar.
        agente.updateRotation = false;
        agente.stoppingDistance = distanciaParada;
        velocidadInicial = agente.speed;

        if (objetivo == null)
        {
            var go = GameObject.FindGameObjectWithTag(tagObjetivo);
            if (go != null) objetivo = go.transform;
        }
    }

    void Update()
    {
        if (objetivo == null) return;

        // Actualizar destino periodicamente (evita llamadas costosas cada frame)
        if (Time.time - ultimaActualizacion >= tasaActualizacionDestino)
        {
            agente.SetDestination(objetivo.position);
            ultimaActualizacion = Time.time;
        }

        // Comportamiento de parada
        float distanciaRestante = agente.hasPath ? agente.remainingDistance : Mathf.Infinity;
        bool cerca = distanciaRestante <= agente.stoppingDistance && (!agente.pathPending);

        agente.isStopped = cerca;

        // Suavizar velocidad (reduce antes de detenerse)
        float velocidadDeseada = cerca ? 0f : velocidadInicial * Mathf.Max(0.01f, multiplicadorVelocidad);
        agente.speed = Mathf.Lerp(agente.speed, velocidadDeseada, velocidadLerp);

        // Rotacion suave: preferimos la direccion de desiredVelocity si hay movimiento,
        // si esta detenido, mirar directamente al objetivo.
        Vector3 dirMirada = Vector3.zero;
        Vector3 velDeseadaAgente = agente.desiredVelocity;
        if (velDeseadaAgente.sqrMagnitude > 0.01f)
        {
            dirMirada = velDeseadaAgente.normalized;
        }
        else
        {
            Vector3 haciaObjetivo = objetivo.position - transform.position;
            haciaObjetivo.y = 0f;
            if (haciaObjetivo.sqrMagnitude > 0.0001f) dirMirada = haciaObjetivo.normalized;
        }

        if (dirMirada.sqrMagnitude > 0.0001f)
        {
            Quaternion rotObjetivo = Quaternion.LookRotation(dirMirada);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotObjetivo, velocidadRotacion * Time.deltaTime);
        }
    }

    // Metodo publico para que otros scripts ajusten el multiplicador
    public void SetMultiplicadorVelocidad(float nuevoMultiplicador)
    {
        multiplicadorVelocidad = nuevoMultiplicador;
    }

    // Para depuracion: dibuja la distancia de parada en la escena
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaParada);
    }
}
