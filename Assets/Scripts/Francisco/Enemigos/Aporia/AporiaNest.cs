using UnityEngine;
using System.Collections;

public class AporiaNest : MonoBehaviour
{
    #region Settings
    [SerializeField] private GameObject larvaPrefab;
    [SerializeField] private float duration = 3f;
    [SerializeField] private float dps = 1f;
    #endregion

    #region Unity Events
    private void OnEnable()
    {
        StartCoroutine(LifeCycle());
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<PlayerHealth>(out var health))
            {
                health.TakeDamage(dps * Time.deltaTime);
            }
        }
    }
    #endregion

    #region Logic
    private IEnumerator LifeCycle()
    {
        yield return new WaitForSeconds(duration);

        for (int i = 0; i < 2; i++)
        {
            if (larvaPrefab != null)
            {
                Vector3 spawnPos = transform.position + (Random.insideUnitSphere * 0.5f);
                spawnPos.y = transform.position.y;
                Instantiate(larvaPrefab, spawnPos, Quaternion.identity);
            }
        }

        gameObject.SetActive(false);
    }
    #endregion
}