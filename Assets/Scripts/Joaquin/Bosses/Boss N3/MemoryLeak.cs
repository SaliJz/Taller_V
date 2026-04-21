using System.Collections;
using UnityEngine;

/// <summary>
/// Fuga de Memoria – charco de daño continuo dejado por los teletransportes
/// de Desfragmentación Evasiva y Latencia Cero del jefe Baal.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class MemoryLeak : MonoBehaviour
{
    #region Private Fields

    private float duration;
    private float dps;
    private float radius;

    #endregion

    #region Inspector - Effects & Synergy

    [Header("Visual")]
    [SerializeField] private ParticleSystem poolVFX;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip loopSFX;

    [Header("Larva Synergy")]
    [Tooltip("Porcentaje de expansión del radio al recibir una explosión de larva.")]
    [SerializeField] private float synergyExpansionPercent = 0.15f;

    #endregion

    #region Internal State

    private SphereCollider col;
    private bool playerIsInside = false;
    private bool hasExpired = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInside = true;
            StartCoroutine(DamagePlayerRoutine(other.gameObject));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerIsInside = false;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el charco con los parámetros del boss.
    /// </summary>
    public void Initialize(float duration, float dps, float radius)
    {
        this.duration = duration;
        this.dps = dps;
        this.radius = radius;
        col.radius = radius;

        if (poolVFX != null) poolVFX.Play();
        if (audioSource != null && loopSFX != null)
        {
            audioSource.clip = loopSFX;
            audioSource.loop = true;
            audioSource.Play();
        }

        StartCoroutine(LifetimeRoutine());
    }

    /// <summary>
    /// Llamado por la larva kamikaze al explotar dentro del charco.
    /// Expande el radio.
    /// </summary>
    public void ExpandFromLarvaExplosion()
    {
        if (hasExpired) return;

        radius *= (1f + synergyExpansionPercent);
        col.radius = radius;
        transform.localScale *= (1f + synergyExpansionPercent); // reflejo visual

        Debug.Log($"[MemoryLeak] Sinergia de larva: radio expandido a {radius:F2} uds.");
    }

    #endregion

    #region Private Routines

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(duration);

        hasExpired = true;

        if (audioSource != null) audioSource.Stop();
        if (poolVFX != null) poolVFX.Stop();

        Destroy(gameObject);
    }

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

    #endregion
}