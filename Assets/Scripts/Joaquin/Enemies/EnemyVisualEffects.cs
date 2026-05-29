using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Clase que maneja los efectos visuales del enemigo
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    #region Inspector - References

    [Header("Renderers Configuration")]
    [Tooltip("Si se deja vacio, buscara MeshRenderers en los hijos.")]
    [SerializeField] private Renderer[] meshRenderers;
    [Tooltip("Si se deja vacio, buscara SpriteRenderers en los hijos.")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitStunSFX;
    [SerializeField] private AudioClip toughnessBlockSFX;
    [SerializeField] private AudioClip normalHitSFX;
    [SerializeField] private AudioClip criticalHitSFX;

    #endregion

    #region Inspector - Flash / Blink Settings

    [Header("Flash / Blink - Amount Override")]
    [Tooltip("Si es true, se anima el valor '_Amount' en el renderer referenciado.")]
    [SerializeField] private bool useAmountFlash = false;
    [Tooltip("Renderer cuyo material recibira el cambio de _Amount.")]
    [SerializeField] private Renderer amountFlashRenderer;
    [Tooltip("Nombre de la propiedad float del shader que se animara.")]
    [SerializeField] private string amountFlashProperty = "_Amount";
    [Tooltip("Valor del _Amount durante el pico del flash.")]
    [SerializeField] private float amountFlashPeakValue = 1f;
    [Tooltip("Valor del _Amount en reposo (antes y despues del flash).")]
    [SerializeField] private float amountFlashRestValue = 0f;
    [SerializeField] private float blinkInterval = 0.06f;
    [SerializeField] private int blinkCount = 6;

    #endregion

    #region Inspector - Effect Settings

    [Header("Stun Effect")]
    [SerializeField] private GameObject stunVFXPrefab;
    [SerializeField] private Transform stunVFXSpawnPoint;
    [SerializeField] private float stunVFXHeightFallback = 2f;
    [SerializeField] private float stunBlinkSpeed = 0.1f;

    [Header("Armor Glow Effect")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private float glowIntensity = 2f;

    [Header("Toughness Block Effect")]
    [SerializeField] private Color toughnessColor = Color.cyan;

    [Header("Damage Visual Settings")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color criticalColor = new Color(0.5f, 0f, 0f);

    #endregion

    #region Internal State

    private bool isStunned = false;
    private Coroutine stunEffectCoroutine = null;
    private GameObject activeStunVFX;

    private Material amountFlashMatInstance = null;
    private Coroutine amountBlinkCoroutine = null;
    private Coroutine glowCoroutine = null;

    private Dictionary<Renderer, Material> originalMeshMats = new Dictionary<Renderer, Material>();
    private Dictionary<SpriteRenderer, Material> originalSpriteMats = new Dictionary<SpriteRenderer, Material>();
    private List<GameObject> activePersistentEffects = new List<GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ValidateRenderers();
        CacheOriginalMaterials();
        CacheAmountFlashMaterial();
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    private void OnDisable()
    {
        CleanupAllEffects(true);
    }

    private void OnDestroy()
    {
        CleanupAllEffects(true);

        if (amountFlashMatInstance != null)
        {
            Destroy(amountFlashMatInstance);
            amountFlashMatInstance = null;
        }
    }

    #endregion

    #region Initialization & Data Sync

    private void ValidateRenderers()
    {
        if (meshRenderers == null || meshRenderers.Length == 0)
            meshRenderers = GetComponentsInChildren<Renderer>();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        if ((meshRenderers == null || meshRenderers.Length == 0) && (spriteRenderers == null || spriteRenderers.Length == 0))
        {
            Debug.LogWarning($"[EnemyVisualEffects] No se encontraron Renderers ni Sprites en {gameObject.name}.");
        }
    }

    private void CacheOriginalMaterials()
    {
        originalMeshMats.Clear();
        originalSpriteMats.Clear();

        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null) originalMeshMats[r] = r.sharedMaterial;
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) originalSpriteMats[s] = s.sharedMaterial;
            }
        }
    }

    private void CacheAmountFlashMaterial()
    {
        if (!useAmountFlash || amountFlashRenderer == null) return;

        if (amountFlashRenderer.sharedMaterial != null)
        {
            amountFlashMatInstance = new Material(amountFlashRenderer.sharedMaterial);
            amountFlashRenderer.material = amountFlashMatInstance;
            SetAmountFlashValue(amountFlashRestValue);
        }
        else
        {
            Debug.LogWarning($"[EnemyVisualEffects] amountFlashRenderer '{amountFlashRenderer.name}' no tiene sharedMaterial. Amount flash desactivado.");
            useAmountFlash = false;
        }
    }

    #endregion

    #region Core Effect Management

    private void CleanupAllEffects(bool forceImmediate = false)
    {
        StopAllCoroutines();

        stunEffectCoroutine = null;
        amountBlinkCoroutine = null;
        glowCoroutine = null;

        ResetAmountFlashValue();

        ParticleSystem psAS = activeStunVFX != null ? activeStunVFX.GetComponent<ParticleSystem>() : null;

        if (activeStunVFX != null)
        {
            if (forceImmediate) VFXHelper.StopAndDestroy(psAS);
            else DetachAndStopStunVFX(activeStunVFX);

            activeStunVFX = null;
        }

        for (int i = activePersistentEffects.Count - 1; i >= 0; i--)
        {
            GameObject fx = activePersistentEffects[i];
            if (fx == null) continue;

            ParticleSystem psFX = fx.GetComponent<ParticleSystem>();
            if (psFX != null) VFXHelper.StopAndDestroy(psFX);
            else Destroy(fx);
        }
        activePersistentEffects.Clear();

        ResetVisualState();
    }

    private void ResetVisualState()
    {
        RestoreAllOriginalMaterials();
        SetRenderersActive(true);
    }

    private void RestoreAllOriginalMaterials()
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null && originalMeshMats.ContainsKey(r))
                    r.material = originalMeshMats[r];
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null && originalSpriteMats.ContainsKey(s))
                    s.material = originalSpriteMats[s];
            }
        }

        if (useAmountFlash && amountFlashRenderer != null && amountFlashMatInstance != null)
        {
            amountFlashRenderer.material = amountFlashMatInstance;
            ResetAmountFlashValue();
        }
    }

    #endregion

    #region Damage & Hit Feedback

    public void PlayToughnessHitFeedback(Vector3 position, float damageAmount = 0f)
    {
        ShowDamageNumber(position, damageAmount, isCritical: false, isToughness: true);
        PlayToughnessBlockSound();
    }

    public void PlayHealthHitFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        ShowDamageNumber(damagePosition, damage, isCritical);
        PlayHealthHitSound(isCritical);

        if (isStunned) return;

        if (useAmountFlash && amountFlashRenderer != null && amountFlashMatInstance != null)
        {
            if (amountBlinkCoroutine != null)
            {
                StopCoroutine(amountBlinkCoroutine);
                amountBlinkCoroutine = null;
            }
            amountBlinkCoroutine = StartCoroutine(BlinkAmountCoroutine());
        }
    }

    private void PlayHealthHitSound(bool isCritical)
    {
        if (audioSource == null) return;
        AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    private void PlayToughnessBlockSound()
    {
        if (audioSource == null || toughnessBlockSFX == null) return;
        audioSource.PlayOneShot(toughnessBlockSFX);
    }

    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false, bool isToughness = false)
    {
        if (damageNumberPrefab == null) return;

        GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);
        DamageNumber dnScript = damageNumber.GetComponent<DamageNumber>();
        if (dnScript != null)
        {
            dnScript.SetHealthColor(normalColor, criticalColor);
            dnScript.SetToughnessColor(toughnessColor);
            dnScript.Initialize(damage, isCritical, isToughness);
        }
    }

    #endregion

    #region Blink System

    private IEnumerator BlinkAmountCoroutine()
    {
        bool isPeak = false;

        for (int i = 0; i < blinkCount; i++)
        {
            isPeak = !isPeak;
            SetAmountFlashValue(isPeak ? amountFlashPeakValue : amountFlashRestValue);
            yield return new WaitForSeconds(blinkInterval);
        }

        ResetAmountFlashValue();
        amountBlinkCoroutine = null;
    }

    private void SetAmountFlashValue(float value)
    {
        if (amountFlashMatInstance == null) return;
        amountFlashMatInstance.SetFloat(amountFlashProperty, value);
    }

    private void ResetAmountFlashValue()
    {
        SetAmountFlashValue(amountFlashRestValue);
    }

    #endregion

    #region Stun & Armor Effects

    public void StartStunEffect(float duration)
    {
        ResetVisualState();

        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
        }

        stunEffectCoroutine = StartCoroutine(StunEffectCoroutine(duration));
    }

    public void StopStunEffect()
    {
        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
            stunEffectCoroutine = null;
        }
        CleanupStunEffect();
    }

    private IEnumerator StunEffectCoroutine(float duration)
    {
        isStunned = true;

        if (audioSource != null && hitStunSFX != null) audioSource.PlayOneShot(hitStunSFX);

        if (stunVFXPrefab != null && activeStunVFX == null)
        {
            Vector3 stunSpawnPos = stunVFXSpawnPoint != null
                ? stunVFXSpawnPoint.position
                : transform.position + Vector3.up * stunVFXHeightFallback;

            activeStunVFX = Instantiate(stunVFXPrefab, stunSpawnPos, Quaternion.identity, transform);
        }

        float elapsed = 0f;
        float nextBlink = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextBlink)
            {
                visible = !visible;
                SetRenderersActive(visible);
                nextBlink = elapsed + stunBlinkSpeed;
            }
            yield return null;
        }

        CleanupStunEffect();
    }

    private void CleanupStunEffect()
    {
        isStunned = false;
        SetRenderersActive(true);

        if (activeStunVFX != null)
        {
            DetachAndStopStunVFX(activeStunVFX);
            activeStunVFX = null;
        }
    }

    private void DetachAndStopStunVFX(GameObject vfxToStop)
    {
        if (vfxToStop == null) return;

        if (vfxToStop.transform.parent == transform)
        {
            vfxToStop.transform.SetParent(null);
        }

        ParticleSystem vfxPS = vfxToStop.GetComponent<ParticleSystem>();
        VFXHelper.StopAndDestroy(vfxPS);
    }

    public void StartArmorGlow()
    {
        if (glowMaterial == null) return;
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);

        ApplyMaterialToAll(glowMaterial);

        glowCoroutine = StartCoroutine(AnimateGlowCoroutine());
    }

    public void StopArmorGlow()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }
        RestoreAllOriginalMaterials();
    }

    private IEnumerator AnimateGlowCoroutine()
    {
        float baseIntensity = 1f;
        while (true)
        {
            float intensity = baseIntensity + Mathf.Sin(Time.time * 3f) * glowIntensity;

            UpdateGlowIntensity(meshRenderers, intensity);
            UpdateGlowIntensity(spriteRenderers, intensity);

            yield return null;
        }
    }

    #endregion

    #region External Material Control

    public void ReapplyAmountFlashMaterial()
    {
        if (!useAmountFlash || amountFlashRenderer == null || amountFlashMatInstance == null) return;
        amountFlashRenderer.material = amountFlashMatInstance;
    }

    public void UpdateBaseMaterial(Renderer targetRenderer, Material newMaterial)
    {
        if (targetRenderer == null || newMaterial == null) return;

        if (originalMeshMats.ContainsKey(targetRenderer))
        {
            originalMeshMats[targetRenderer] = newMaterial;
        }
        else
        {
            originalMeshMats.Add(targetRenderer, newMaterial);
        }

        if (glowCoroutine == null)
        {
            targetRenderer.material = newMaterial;
        }
    }

    #endregion

    #region Helper Methods

    private void ApplyMaterialToAll(Material mat)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null) r.material = mat;
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) s.material = mat;
            }
        }
    }

    private void UpdateGlowIntensity<T>(T[] renderers, float intensity) where T : Renderer
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null && r.material.HasProperty("_EmissionIntensity"))
            {
                r.material.SetFloat("_EmissionIntensity", intensity);
            }
        }
    }

    private void SetRenderersActive(bool active)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null) r.enabled = active;
            }
        }
        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) s.enabled = active;
            }
        }
    }

    #endregion

    #region Debugging

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.up * stunVFXHeightFallback));
    }

    #endregion
}