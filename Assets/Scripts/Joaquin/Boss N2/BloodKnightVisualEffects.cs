using UnityEngine;
using System.Collections;

public class BloodKnightVisualEffects : MonoBehaviour
{
    [Header("Armor Glow Effect")]
    [SerializeField] private Renderer[] armorPieces;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private float glowIntensity = 2f;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;

    public void StartArmorGlow()
    {
        foreach (Renderer renderer in armorPieces)
        {
            if (renderer != null && glowMaterial != null)
            {
                renderer.material = glowMaterial;

                StartCoroutine(AnimateGlow(renderer));
            }
        }
    }

    public void StopArmorGlow()
    {
        foreach (Renderer renderer in armorPieces)
        {
            if (renderer != null && normalMaterial != null)
            {
                renderer.material = normalMaterial;
            }
        }
    }

    private IEnumerator AnimateGlow(Renderer renderer)
    {
        Material material = renderer.material;
        float baseIntensity = 1f;

        while (material == glowMaterial)
        {
            float intensity = baseIntensity + Mathf.Sin(Time.time * 3f) * glowIntensity;
            material.SetFloat("_EmissionIntensity", intensity);
            yield return null;
        }
    }

    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false)
    {
        if (damageNumberPrefab != null)
        {
            GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);
            DamageNumber damageNumberScript = damageNumber.GetComponent<DamageNumber>();

            if (damageNumberScript != null)
            {
                damageNumberScript.Initialize(damage, isCritical);
            }
        }
    }
}