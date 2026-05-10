using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mano que emerge del suelo para el ataque "Manos de los Ahogados".
/// </summary>
public class SoulHand : MonoBehaviour
{
    #region Inspector - References

    [Header("VFX")]
    [SerializeField] private GameObject explosionVFXPrefab;

    #endregion

    #region Inspector - Dano

    [Header("Dano")]
    [SerializeField] private float damage = 15f;
    [SerializeField] private float radius = 1.5f;

    #endregion

    #region Inspector - Tiempos de Animacion

    [Header("Tiempos de animacion")]
    [SerializeField] private float emergeDuration = 0.5f; // Sube del suelo
    [SerializeField] private float activeWindow = 0.6f; // Ventana de impacto
    [SerializeField] private float retreatDuration = 0.3f; // Baja si no conecta

    #endregion

    #region Internal State

    private bool hasExploded;

    private readonly List<GameObject> vfxInstances = new();

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        StopAllCoroutines();
        foreach (var vfx in vfxInstances)
        {
            if (vfx != null) Destroy(vfx);
        }
        vfxInstances.Clear();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Inicializa la mano con dano y radio, luego inicia la secuencia de emergencia.
    /// </summary>
    public void Initialize(float damageAmount, float grabRadius)
    {
        damage = damageAmount;
        radius = grabRadius;
        StartCoroutine(EmergenceRoutine());
    }

    #endregion

    #region Core Logic

    private IEnumerator EmergenceRoutine()
    {
        // 1. Emerge desde el suelo
        float elapsed = 0f;
        while (elapsed < emergeDuration)
        {
            transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * radius, elapsed / emergeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = Vector3.one * radius;

        // 2. Ventana de impacto activa
        float activeTimer = 0f;
        while (activeTimer < activeWindow)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;
                TriggerExplosion(hit.gameObject);
                yield break;
            }
            activeTimer += Time.deltaTime;
            yield return null;
        }

        // 3. Retrocede si no conecto
        elapsed = 0f;
        Vector3 currentScale = transform.localScale;
        while (elapsed < retreatDuration)
        {
            transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, elapsed / retreatDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private void TriggerExplosion(GameObject player)
    {
        if (hasExploded) return;
        hasExploded = true;

        player.GetComponent<PlayerHealth>()?.TakeDamage(damage);

        SpawnExplosionVFX();
        Destroy(gameObject, 0.5f);
    }

    #endregion

    #region Visual Effects

    private void SpawnExplosionVFX()
    {
        if (explosionVFXPrefab == null) return;

        GameObject vfx = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * radius * 2f;
        vfxInstances.Add(vfx);
        Destroy(vfx, 1f);
    }

    #endregion
}