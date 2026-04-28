using UnityEngine;

public class StaticProjectileLevel2 : StaticProjectileBase
{
    [Header("Mine Settings")]
    [SerializeField] private GameObject minePrefab;

    protected override void OnPlayerHit(GameObject player)
    {
        hasImpacted = true;
        player.GetComponent<IDamageable>()?.TakeDamage(damage);

        if (minePrefab != null)
        {
            GameObject mineInstance = Instantiate(minePrefab, transform.position, Quaternion.identity);

            if (mineInstance.TryGetComponent<StaticTrapMine>(out var trapScript))
            {
                string currentWord = wordTrail != null ? wordTrail.GetWord() : "STATIC";
                trapScript.InitializeTrap(currentWord);
            }
        }

        Destroy(gameObject);
    }

    protected override void OnEnvironmentHit(GameObject obstacle)
    {
        hasImpacted = true;

        if (minePrefab != null)
        {
            GameObject mineInstance = Instantiate(minePrefab, transform.position, Quaternion.identity);

            if (mineInstance.TryGetComponent<StaticTrapMine>(out var trapScript))
            {
                string currentWord = wordTrail != null ? wordTrail.GetWord() : "STATIC";
                trapScript.InitializeTrap(currentWord);
            }
        }

        Destroy(gameObject);
    }
}