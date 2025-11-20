using UnityEngine;
using System.Collections;

public class ExplosionDelayHandler : MonoBehaviour
{
    private float _explosionRadius;
    private float _explosionDamagePercentage;
    private GameObject _explosionVisualizerPrefab;
    private float _enemyBaseHealth;

    public void StartExplosion(float damagePercent, float radius, GameObject visualizerPrefab, float baseHealth, float delay)
    {
        _explosionDamagePercentage = damagePercent;
        _explosionRadius = radius;
        _explosionVisualizerPrefab = visualizerPrefab;
        _enemyBaseHealth = baseHealth;

        StartCoroutine(ExplosionRoutine(delay));
    }

    private IEnumerator ExplosionRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        //float explosionDamage = _enemyBaseHealth * _explosionDamagePercentage;
        Vector3 position = transform.position;

        //Collider[] hitColliders = Physics.OverlapSphere(position, _explosionRadius);
        //foreach (var hitCollider in hitColliders)
        //{
        //    if (hitCollider.gameObject == gameObject) continue;

        //    IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
        //    if (damageable != null)
        //    {
        //        damageable.TakeDamage(explosionDamage, false);
        //        Debug.Log($"[ExplosionDelayHandler] Daño por explosión de {explosionDamage:F2} aplicado a {hitCollider.gameObject.name}.");
        //    }
        //}

        if (_explosionVisualizerPrefab != null)
        {
            GameObject visualzerExplosion = Instantiate(_explosionVisualizerPrefab, position, Quaternion.identity);
        }

        Destroy(this);
    }

    private void OnDrawGizmos()
    {
        if (_explosionRadius > 0)
        {
            Gizmos.color = Color.red; 
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }
    }
}