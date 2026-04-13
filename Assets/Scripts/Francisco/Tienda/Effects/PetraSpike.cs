using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetraSpike : MonoBehaviour
{
    #region Private Fields

    private float damage;
    private LayerMask enemyLayer;
    private bool isLargeSpike;
    private float tickCooldown = 0.5f;
    private float tickTimer = 0f;
    private HashSet<Collider> overlappingEnemies = new HashSet<Collider>();

    #endregion

    #region Public Methods

    public void Initialize(float damage, float lifetime, LayerMask enemyLayer, bool isLargeSpike = false)
    {
        this.damage = damage;
        this.enemyLayer = enemyLayer;
        this.isLargeSpike = isLargeSpike;
        StartCoroutine(LifetimeRoutine(lifetime));
    }

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        if (isLargeSpike || overlappingEnemies.Count == 0) return;

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickCooldown;
            foreach (var col in overlappingEnemies)
            {
                if (col != null) DealDamageTo(col.gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsEnemy(other)) return;
        DealDamageTo(other.gameObject);
        if (!isLargeSpike)
        {
            overlappingEnemies.Add(other);
            tickTimer = tickCooldown;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isLargeSpike) overlappingEnemies.Remove(other);
    }

    #endregion

    #region Private Methods

    private bool IsEnemy(Collider col)
        => (enemyLayer.value & (1 << col.gameObject.layer)) > 0;

    private void DealDamageTo(GameObject enemy)
    {
        var health = enemy.GetComponent<EnemyHealth>();
        if (health != null) health.TakeDamage(Mathf.RoundToInt(damage));
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }

    #endregion
}