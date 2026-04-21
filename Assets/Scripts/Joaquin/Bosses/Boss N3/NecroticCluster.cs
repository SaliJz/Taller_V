using System.Collections;
using UnityEngine;

/// <summary>
/// Clúster Necrótico – creado por el "Buffer Overrun" del jefe Baal.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class NecroticCluster : MonoBehaviour
{
    #region Private Fields 

    private float duration;
    private float dps;
    private float radius;
    private GameObject larvaPrefab;
    private int larvaCount;

    #endregion

    #region Inspector - Effects & Synergy

    [Header("Visual")]
    [SerializeField] private ParticleSystem idleVFX;
    [SerializeField] private ParticleSystem collapseVFX;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip collapseSFX;

    [Header("Slow")]
    [Tooltip("Fracción de velocidad que se elimina mientras el jugador está dentro.")]
    [SerializeField] private float slowFraction = 0.2f;

    [Header("Larva Synergy")]
    [Tooltip("Porcentaje de expansión del radio cuando una larva explota dentro del charco.")]
    [SerializeField] private float synergyExpansionPercent = 0.15f;

    #endregion

    #region Internal State

    private SphereCollider col;
    private bool playerIsInside = false;
    private bool slowApplied = false;
    private float savedPlayerSpeed = 0f;
    private PlayerMovement playerMovement;
    private bool hasExpired = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    private void OnDestroy()
    {
        // Restaura la velocidad del jugador si este seguía dentro al destruirse
        RestorePlayerSpeed();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInside = true;
            playerMovement = other.GetComponent<PlayerMovement>();
            ApplySlowToPlayer();
            StartCoroutine(DamagePlayerRoutine(other.gameObject));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInside = false;
            RestorePlayerSpeed();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el clúster con los parámetros del boss.
    /// </summary>
    public void Initialize(float duration, float dps, float radius,
                           GameObject larvaPrefab, int larvaCount)
    {
        this.duration = duration;
        this.dps = dps;
        this.radius = radius;
        this.larvaPrefab = larvaPrefab;
        this.larvaCount = larvaCount;

        col.radius = radius;

        if (idleVFX != null) idleVFX.Play();

        StartCoroutine(LifetimeRoutine());
    }

    /// <summary>
    /// Llamado externamente cuando una larva explota dentro del charco.
    /// Expande el radio.
    /// </summary>
    public void ExpandFromLarvaExplosion()
    {
        if (hasExpired) return;

        radius *= (1f + synergyExpansionPercent);
        col.radius = radius;

        // Escala el transform visualmente para reflejar la expansión
        transform.localScale = Vector3.one * (radius / (transform.localScale.x > 0f ? transform.localScale.x : 1f))
                             * transform.localScale.x;

        Debug.Log($"[NecroticCluster] Sinergia: radio expandido a {radius:F2} uds.");
    }

    #endregion

    #region Private Routines

    /// <summary>
    /// Ciclo de vida
    /// Espera <paramref name="duration"/> segundos y colapsa.
    /// </summary>
    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(duration);
        Collapse();
    }

    /// <summary>
    /// Aplica DPS continuamente mientras el jugador permanece dentro.
    /// Se cancela si el jugador sale o el clúster colapsa.
    /// </summary>
    private IEnumerator DamagePlayerRoutine(GameObject playerObj)
    {
        PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
        if (health == null) yield break;

        while (playerIsInside && !hasExpired)
        {
            health.TakeDamage(dps * Time.deltaTime, false, AttackDamageType.Melee);
            yield return null;
        }
    }

    private void Collapse()
    {
        hasExpired = true;

        // Restaura la velocidad del jugador si este seguía dentro
        RestorePlayerSpeed();

        // VFX y audio de colapso
        if (idleVFX != null) idleVFX.Stop();
        if (collapseVFX != null)
        {
            collapseVFX.transform.SetParent(null); // desvincula para que no desaparezca con el objeto
            collapseVFX.Play();
            Destroy(collapseVFX.gameObject, collapseVFX.main.duration + 0.5f);
        }
        if (audioSource != null && collapseSFX != null) audioSource.PlayOneShot(collapseSFX);

        // Suelta larvas
        SpawnLarvas();

        Destroy(gameObject);
    }

    private void SpawnLarvas()
    {
        if (larvaPrefab == null) return;

        Transform playerTarget = null;
        if (playerMovement != null)
        {
            playerTarget = playerMovement.transform;
        }
        else
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) playerTarget = pObj.transform;
        }

        for (int i = 0; i < larvaCount; i++)
        {
            // Distribuye las larvas en círculo alrededor del clúster
            float angle = i * (360f / larvaCount);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (radius * 0.5f);

            // Posición candidata sin offset en Y
            Vector3 candidatePos = transform.position + offset;
            candidatePos.y = transform.position.y;

            Vector3 finalSpawnPos = candidatePos;

            // Validación en NavMesh para asegurar que el agente se ancle correctamente al suelo
            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                finalSpawnPos = hit.position;
            }

            GameObject larvaGO = Instantiate(larvaPrefab, finalSpawnPos, Quaternion.identity);

            // Inicialización directa
            if (larvaGO.TryGetComponent(out Larva larvaScript))
            {
                larvaScript.Initialize(playerTarget);
            }
        }
    }

    #endregion

    #region Player Slow Logic

    private void ApplySlowToPlayer()
    {
        if (playerMovement == null || slowApplied) return;
        savedPlayerSpeed = playerMovement.MoveSpeed;
        playerMovement.MoveSpeed = savedPlayerSpeed * (1f - slowFraction);
        slowApplied = true;
    }

    private void RestorePlayerSpeed()
    {
        if (!slowApplied || playerMovement == null) return;
        playerMovement.MoveSpeed = savedPlayerSpeed;
        slowApplied = false;
    }

    #endregion
}