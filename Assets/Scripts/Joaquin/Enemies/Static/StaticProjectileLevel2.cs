using UnityEngine;

public class StaticProjectileLevel2 : StaticProjectileBase
{
    [Header("Mine Settings")]
    [SerializeField] private GameObject minePrefab;
    private float storedMineDamage;

    public void InitializeLevel2(float pSpeed, float pDamage, string pWord, float pMineDmg)
    {
        base.Initialize(speed, pDamage, pWord);
        this.storedMineDamage = pMineDmg;
    }


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
                trapScript.InitializeTrap(currentWord, storedMineDamage);
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
                trapScript.InitializeTrap(currentWord, storedMineDamage);
            }
        }

        Destroy(gameObject);
    }
}