using System.Collections;
using UnityEngine;

public class StaticStressResidue : MonoBehaviour
{
    #region Private State

    private float duration;
    private float dps;
    private float radius;
    private float tickInterval = 0.1f;

    #endregion

    public void Initialize(float inDuration, float inDPS, float inRadius)
    {
        duration = inDuration;
        dps = inDPS;
        radius = inRadius;
        StartCoroutine(LifetimeRoutine());
    }

    #region Lifetime

    private IEnumerator LifetimeRoutine()
    {
        float elapsed = 0f;
        float damagePerTick = dps * tickInterval;

        while (elapsed < duration)
        {
            DamagePlayersInRadius(damagePerTick);
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }

        Destroy(gameObject);
    }

    private void DamagePlayersInRadius(float amount)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].CompareTag("Player")) continue;
            IDamageable damageable = hits[i].GetComponent<IDamageable>();
            damageable?.TakeDamage(amount, false);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    #endregion
}