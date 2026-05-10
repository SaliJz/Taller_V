using System.Collections;
using UnityEngine;

/// <summary>
/// Barrera de luz sólida creada por "Falla Divisoria".
/// </summary>
public class SolidLightWall : MonoBehaviour
{
    #region Inspector - Dano

    [Header("Dano de contacto")]
    [SerializeField] private float contactDamagePerSecond = 2.5f;

    [Header("Dano instantaneo al crear encima del jugador")]
    [SerializeField] private float instantDamageOnSpawn = 12.5f;

    #endregion

    #region Inspector - Configuracion

    [Header("Tiempo de vida")]
    [SerializeField] private float wallLifetime = 5f;

    [Header("Deteccion")]
    [SerializeField] private LayerMask playerLayer;

    #endregion

    #region Internal State

    private float contactTimer;

    #endregion

    #region Public Properties & Events

    public float ContactDamagePerSecond 
    { 
        get => contactDamagePerSecond; 
        set => contactDamagePerSecond = value; 
    }
    public float InstantDamageOnSpawn   
    { 
        get => instantDamageOnSpawn;   
        set => instantDamageOnSpawn = value; 
    }
    public float WallLifetime           
    { 
        get => wallLifetime;           
        set => wallLifetime = value; 
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (CheckSpawnOverlapPlayer()) return; // Si se creo encima, se destruye ya
        StartCoroutine(LifetimeRoutine());
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        contactTimer += Time.deltaTime;
        if (contactTimer < 1f) return;

        other.GetComponent<PlayerHealth>()?.TakeDamage(contactDamagePerSecond);
        contactTimer = 0f;
    }

    #endregion

    #region Core Logic

    /// <returns>true si habia jugador encima y la pared se destruyo sin mantenerse.</returns>
    private bool CheckSpawnOverlapPlayer()
    {
        Collider[] hits = Physics.OverlapBox(
            transform.position,
            transform.localScale * 0.5f,
            transform.rotation,
            playerLayer
        );

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            hit.GetComponent<PlayerHealth>()?.TakeDamage(instantDamageOnSpawn);
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(wallLifetime);
        Destroy(gameObject);
    }

    #endregion

    #region Logging

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.7f, 1f, 0.2f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }

    #endregion
}