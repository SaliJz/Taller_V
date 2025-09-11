using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el comportamiento del escudo lanzado por el jugador.
/// </summary>
public class Shield : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private bool canRebound = false;

    private enum ShieldState { Thrown, Returning, Inactive }
    private ShieldState currentState = ShieldState.Inactive;

    private Vector3 startPosition;
    private Transform returnTarget;
    private PlayerShieldController owner;

    private int reboundCount = 0;
    private int maxRebounds = 1;

    /// <summary>
    /// Función que es llamada por el PlayerShieldController para lanzar el escudo.
    /// </summary>
    /// <param name="owner"> Referencia al controlador del jugador </param>
    /// <param name="direction"> Orientación del escudo en la dirección del lanzamiento </param>
    public void Throw(PlayerShieldController owner, Vector3 direction)
    {
        this.owner = owner;
        this.returnTarget = owner.transform;
        transform.forward = direction;
        startPosition = transform.position;
        currentState = ShieldState.Thrown;
        gameObject.SetActive(true);
        reboundCount = 0;
    }

    private void Update()
    {
        if (currentState == ShieldState.Inactive) return;

        if (currentState == ShieldState.Thrown)
        {
            transform.position += transform.forward * speed * Time.deltaTime;

            if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
            {
                StartReturning();
            }
        }
        else if (currentState == ShieldState.Returning)
        {
            Vector3 directionToTarget = (returnTarget.position - transform.position).normalized;
            transform.position += directionToTarget * speed * 1.5f * Time.deltaTime;

            if (Vector3.Distance(transform.position, returnTarget.position) < 1.0f)
            {
                owner.CatchShield();
                currentState = ShieldState.Inactive;
                gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState != ShieldState.Thrown) return;

        // Si golpea un enemigo
        if (other.CompareTag("Enemy"))
        {
            // Lógica de daño al enemigo
            // other.GetComponent<EnemyHealth>().TakeDamage(damageAmount);

            if (canRebound && reboundCount < maxRebounds)
            {
                reboundCount++;
                StartReturning();
            }
            else
            {
                StartReturning();
            }
        }
        // Si golpea un obstáculo
        else if (other.CompareTag("Wall"))
        {
            StartReturning();
        }
    }

    // Inicia el retorno del escudo al jugador
    private void StartReturning()
    {
        currentState = ShieldState.Returning;
    }
}