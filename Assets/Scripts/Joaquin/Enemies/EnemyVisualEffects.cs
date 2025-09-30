using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja los efectos visuales del enemigo, incluyendo el efecto de brillo del armadura y la visualizaci�n de n�mmeros de da�o.
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    [Header("Damage Flash Settings")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color normalFlashColor = new Color(1f, 0f, 0f, 0.6f);
    [SerializeField] private Color criticalFlashColor = new Color(1f, 0.5f, 0f, 0.9f);
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private int flashCount = 2;

    [Header("Materials")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material glowMaterial;

    [Header("Armor Glow Effect")]
    [SerializeField] private float glowIntensity = 2f;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalHitSFX;
    [SerializeField] private AudioClip criticalHitSFX;

    private Coroutine flashRoutine;

    private void Awake()
    {
        if (renderers != null && defaultMaterial != null)
        {
            foreach (var r in renderers)
            {
                r.material = defaultMaterial;
            }
        }
    }

    private void OnEnable()
    {
        ResetVisuals();
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    #region Damage Feedback

    /// <summary>
    /// Llama este m�todo cuando el enemigo recibe da�o. 
    /// Incluye feedback visual, sonoro y num�rico.
    /// </summary>
    public void PlayDamageFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        // N�meros de da�o
        ShowDamageNumber(damagePosition, damage, isCritical);

        // Parpadeo visual
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(isCritical));

        // Sonido
        if (audioSource != null)
        {
            AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    private IEnumerator FlashRoutine(bool isCritical)
    {
        Color flashColor = isCritical ? criticalFlashColor : normalFlashColor;

        for (int i = 0; i < flashCount; i++)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.material.SetColor("_EmissionColor", flashColor);
                }
            }
            yield return new WaitForSeconds(flashDuration / (flashCount * 2f));

            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.material.SetColor("_EmissionColor", Color.black);
                }
            }
            yield return new WaitForSeconds(flashDuration / (flashCount * 2f));
        }
    }

    #endregion

    #region Armor Glow

    public void StartArmorGlow()
    {
        foreach (Renderer renderer in renderers)
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
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && defaultMaterial != null)
            {
                renderer.material = defaultMaterial;
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

    #endregion

    #region Damage Numbers

    /// <summary>
    /// Metodo para mostrar n�meros de da�o en la posici�n especificada.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="damage"></param>
    /// <param name="isCritical"></param>
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

    private void ResetVisuals()
    {
        if (renderers != null && defaultMaterial != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.material = defaultMaterial;
            }
        }

        if (damageNumberParent != null)
        {
            for (int i = 0; i < damageNumberParent.childCount; i++)
            {
                Destroy(damageNumberParent.GetChild(i).gameObject);
            }
        }
    }

    #endregion
}