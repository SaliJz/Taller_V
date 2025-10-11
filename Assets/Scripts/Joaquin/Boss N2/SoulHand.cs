using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script para la mano de almas del ataque Necio Pecador.
/// </summary>
public class SoulHand : MonoBehaviour
{
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private float damage;
    [SerializeField] private float radius;
    [SerializeField] private bool hasExploded = false;

    private List<GameObject> instantiatedEffects = new List<GameObject>();

    private void OnDestroy()
    {
        StopAllCoroutines();
        DestroyAllInstantiatedEffects();
    }

    private void DestroyAllInstantiatedEffects()
    {
        foreach (GameObject effect in instantiatedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        instantiatedEffects.Clear();
    }

    public void Initialize(float damageAmount, float grabRadius)
    {
        damage = damageAmount;
        radius = grabRadius;

        StartCoroutine(EmergenceRoutine());
    }

    private IEnumerator EmergenceRoutine()
    {
        // Animación de emergencia (0.5 segundos)
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * radius;

        while (elapsed < 0.5f)
        {
            transform.localScale = Vector3.Lerp(startScale, endScale, elapsed / 0.5f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = endScale;

        // Intentar agarrar al jugador
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                GrabPlayer(hit.gameObject);
                yield break;
            }
        }

        // Si no agarró a nadie, desaparecer
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }

    private void GrabPlayer(GameObject player)
    {
        if (hasExploded) return;
        hasExploded = true;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        // Explosión de fuego verde
        SpawnExplosion();

        Destroy(gameObject, 0.5f);
    }

    private void SpawnExplosion()
    {
        if (explosionVFXPrefab != null)
        {
            GameObject explosion = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            instantiatedEffects.Add(explosion);

            explosion.transform.position = transform.position;
            explosion.transform.localScale = Vector3.one * radius * 2f;

            Destroy(explosion, 0.5f);
        }
    }
}