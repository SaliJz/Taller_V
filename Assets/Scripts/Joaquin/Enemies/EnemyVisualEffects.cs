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

    #endregion

    #region Inspector - Material & Flash Settings

    [Header("Configuracion de Materiales")]
    [Tooltip("Si es true, se ignora 'defaultMaterial' y se usa el material que tenga el renderer al inicio.")]
    [SerializeField] private bool useOriginalMaterials = true;
    [Tooltip("Solo se usa si useOriginalMaterials es false.")]
    [SerializeField] private Material defaultMaterial;

    [Header("Flash / Blink Effect")]
    [SerializeField] private Material blinkFlashMaterial;
    [SerializeField] private float blinkInterval = 0.06f;
    [SerializeField] private int blinkCount = 6;

    [Header("Flash / Blink - Amount Override")]
    [Tooltip("Si es true, en lugar de cambiar el material durante el flash, se anima el valor '_Amount' en el renderer referenciado. Ese renderer queda excluido del swap de material normal.")]
    [SerializeField] private bool useAmountFlash = false;
    [Tooltip("Renderer cuyo material recibira el cambio de _Amount en lugar del swap de material.")]
    [SerializeField] private Renderer amountFlashRenderer;
    [Tooltip("Nombre de la propiedad float del shader que se animara.")]
    [SerializeField] private string amountFlashProperty = "_Amount";
    [Tooltip("Valor del _Amount durante el pico del flash.")]
    [SerializeField] private float amountFlashPeakValue = 1f;
    [Tooltip("Valor del _Amount en reposo (antes y despues del flash).")]
    [SerializeField] private float amountFlashRestValue = 0f;

    #endregion

    #region Inspector - Effect Settings

    [Header("Stun Effect")]
    [SerializeField] private Material stunMaterial;
    [SerializeField] private Color stunGlowColor = Color.yellow;
    [SerializeField] private float stunBlinkSpeed = 0.1f;
    [SerializeField] private GameObject stunVFXPrefab;
    [SerializeField] private float stunVFXHeight = 2f;
    [SerializeField] private AudioClip stunSFX;

    [Header("Armor Glow Effect")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private float glowIntensity = 2f;

    [Header("Damage Visual Settings")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color criticalColor = new Color(0.5f, 0f, 0f);
    [SerializeField] private AudioClip normalHitSFX;
    [SerializeField] private AudioClip criticalHitSFX;

    #endregion

    #region Internal State

    private bool isStunned = false;
    private Coroutine stunEffectCoroutine = null;
    private GameObject activeStunVFX;
    private Coroutine glowCoroutine;

    private Material amountFlashMatInstance = null;
    private Coroutine amountBlinkCoroutine = null;

    private Dictionary<Component, Coroutine> activeBlinkRoutines = new Dictionary<Component, Coroutine>();

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
        RestoreAllOriginalMaterials();
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
                if (r != null)
                {
                    originalMeshMats[r] = useOriginalMaterials ? r.sharedMaterial : defaultMaterial;
                    r.material = originalMeshMats[r];
                }
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null)
                {
                    originalSpriteMats[s] = useOriginalMaterials ? s.sharedMaterial : defaultMaterial;
                    s.material = originalSpriteMats[s];
                }
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
        glowCoroutine = null;
        amountBlinkCoroutine = null;
        activeBlinkRoutines.Clear();

        ResetAmountFlashValue();

        if (activeStunVFX != null)
        {
            if (forceImmediate)
            {
                Destroy(activeStunVFX);
            }
            else
            {
                DetachAndStopStunVFX(activeStunVFX);
            }
            activeStunVFX = null;
        }

        for (int i = activePersistentEffects.Count - 1; i >= 0; i--)
        {
            GameObject fx = activePersistentEffects[i];
            if (fx != null) Destroy(fx);
        }
        activePersistentEffects.Clear();

        RestoreAllOriginalMaterials();
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

        SetRenderersActive(true);
    }

    #endregion

    #region Damage & Hit Feedback

    public void PlayToughnessHitFeedback(Vector3 position)
    {
        ShowDamageNumber(position, 0, false);
    }

    public void PlayDamageFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        ShowDamageNumber(damagePosition, damage, isCritical);
        PlayHitSound(isCritical);

        if (isStunned) return;

        if (useAmountFlash)
        {
            if (amountFlashRenderer != null && amountFlashMatInstance != null)
            {
                if (amountBlinkCoroutine != null)
                {
                    StopCoroutine(amountBlinkCoroutine);
                    amountBlinkCoroutine = null;
                }
                amountBlinkCoroutine = StartCoroutine(BlinkAmountCoroutine());
            }
        }
        else
        {
            if (blinkFlashMaterial != null)
            {
                if (meshRenderers != null)
                {
                    foreach (var r in meshRenderers)
                    {
                        if (r == null) continue;
                        StartBlinkRoutine(r, blinkFlashMaterial, blinkInterval, blinkCount);
                    }
                }

                if (spriteRenderers != null)
                {
                    foreach (var s in spriteRenderers)
                    {
                        if (s == null) continue;
                        if (s != null) StartBlinkRoutine(s, blinkFlashMaterial, blinkInterval, blinkCount);
                    }
                }
            }
        }
    }

    private void PlayHitSound(bool isCritical)
    {
        if (audioSource == null) return;
        AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false)
    {
        if (damageNumberPrefab != null)
        {
            GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);
            DamageNumber script = damageNumber.GetComponent<DamageNumber>();
            if (script != null)
            {
                script.SetColor(normalColor, criticalColor);
                script.Initialize(damage, isCritical);
            }
        }
    }

    #endregion

    #region Blink System (Material Swap & Amount)

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

    private void StartBlinkRoutine(Component target, Material flashMat, float interval, int count)
    {
        if (target == null) return;

        if (activeBlinkRoutines.TryGetValue(target, out Coroutine existing))
        {
            if (existing != null) StopCoroutine(existing);
            activeBlinkRoutines.Remove(target);
            RestoreMaterialSingle(target);
        }

        Coroutine routine = StartCoroutine(BlinkCoroutine(target, flashMat, interval, count));
        activeBlinkRoutines[target] = routine;
    }

    private IEnumerator BlinkCoroutine(Component target, Material flashMat, float interval, int toggles)
    {
        Material originalMat = GetOriginalMaterial(target);
        if (originalMat == null)
        {
            activeBlinkRoutines.Remove(target);
            yield break;
        }

        bool isFlash = false;
        Renderer meshR = target as Renderer;
        SpriteRenderer spriteR = target as SpriteRenderer;

        for (int i = 0; i < toggles; i++)
        {
            if (target == null)
            {
                activeBlinkRoutines.Remove(target);
                yield break;
            }

            isFlash = !isFlash;
            Material matToUse = isFlash ? flashMat : originalMat;

            if (meshR != null)
            {
                meshR.material = matToUse;
            }
            else if (spriteR != null)
            {
                spriteR.material = matToUse;
            }

            yield return new WaitForSeconds(interval);
        }

        if (target != null)
        {
            RestoreMaterialSingle(target);
        }

        activeBlinkRoutines.Remove(target);
    }

    #endregion

    #region Stun & Armor Effects

    public void StartStunEffect(float duration)
    {
        activeBlinkRoutines.Clear();

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

        if (stunMaterial != null) ApplyMaterialToAll(stunMaterial);
        else ApplyColorToAll(stunGlowColor);

        if (audioSource != null && stunSFX != null) audioSource.PlayOneShot(stunSFX);

        if (stunVFXPrefab != null)
        {
            if (activeStunVFX == null)
            {
                activeStunVFX = Instantiate(stunVFXPrefab, transform.position + Vector3.up * stunVFXHeight, Quaternion.identity, transform);
            }
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
        RestoreAllOriginalMaterials();

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

        var ps = vfxToStop.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        else
        {
            Destroy(vfxToStop);
        }
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

        if (!isStunned && glowCoroutine == null)
        {
            if (!activeBlinkRoutines.ContainsKey(targetRenderer))
            {
                targetRenderer.material = newMaterial;
            }
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

    private void ApplyColorToAll(Color col)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null && r.material.HasProperty("_Color")) r.material.color = col;
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) s.color = col;
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

    private Material GetOriginalMaterial(Component target)
    {
        if (target is Renderer r && originalMeshMats.ContainsKey(r)) return originalMeshMats[r];
        if (target is SpriteRenderer s && originalSpriteMats.ContainsKey(s)) return originalSpriteMats[s];
        return null;
    }

    private void RestoreMaterialSingle(Component target)
    {
        if (target == null) return;
        Material original = GetOriginalMaterial(target);
        if (original == null) return;

        if (target is Renderer r) r.material = original;
        else if (target is SpriteRenderer s) s.material = original;
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

    #endregion

    #region Debugging

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.up * stunVFXHeight));
    }

    #endregion
}