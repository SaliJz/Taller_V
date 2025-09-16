using UnityEngine;
using System.Collections;

public class BloodKnightVisualEffects : MonoBehaviour
{
    [Header("Armor Glow Effect")]
    [SerializeField] private Renderer[] armorPieces;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private float glowIntensity = 2f;

    [Header("Screen Shake")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float shakeIntensity = 1f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;

    private Vector3 originalCameraPosition;
    private bool isShaking = false;

    private void Start()
    {
        if (mainCamera != null)
        {
            originalCameraPosition = mainCamera.transform.localPosition;
        }
    }

    public void StartArmorGlow()
    {
        foreach (Renderer renderer in armorPieces)
        {
            if (renderer != null && glowMaterial != null)
            {
                renderer.material = glowMaterial;

                // Animate glow intensity
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

    public void TriggerScreenShake()
    {
        if (!isShaking && mainCamera != null)
        {
            StartCoroutine(ScreenShake());
        }
    }

    private IEnumerator ScreenShake()
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;

            mainCamera.transform.localPosition = originalCameraPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = originalCameraPosition;
        isShaking = false;
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