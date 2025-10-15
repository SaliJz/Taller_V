using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Clase que maneja los efectos visuales del enemigo, incluyendo:
/// - Visualización de números de daño
/// - Parpadeo por material (material swap) mediante BlinkMaterial(...)
/// - Efecto de brillo (armor glow)
/// - Feedback de sonido
/// - Efecto de stun
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    [Header("Renderers (opcional)")]
    [SerializeField] private Renderer[] renderers;

    [Header("Materials / Material Blink (material swap)")]
    [Tooltip("Material temporal que se usará para el parpadeo por swap (por ejemplo: blanco).")]
    [SerializeField] private Material blinkFlashMaterial;
    [Tooltip("Intervalo entre toggles (segundos). Ej: 0.06 -> parpadeo rápido.")]
    [SerializeField] private float blinkInterval = 0.06f;
    [Tooltip("Número total de toggles (on/off). Ej: 6 = 3 parpadeos completos).")]
    [SerializeField] private int blinkCount = 6;

    [Header("Stun Effect")]
    [SerializeField] private Material stunMaterial;
    [SerializeField] private Color stunGlowColor = Color.yellow;
    [SerializeField] private float stunBlinkSpeed = 0.1f;
    [SerializeField] private GameObject stunVFXPrefab;
    [SerializeField] private float stunVFXHeight = 2f;
    [SerializeField] private AudioClip stunSFX;

    [Header("Materials (default / glow)")]
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

    private bool isStunned = false;
    private Coroutine stunEffectCoroutine = null;
    private GameObject activeStunVFX;
    private Coroutine glowCoroutine;

    private Dictionary<Renderer, Coroutine> materialBlinkRoutines = new Dictionary<Renderer, Coroutine>();
    private Dictionary<Renderer, Material[]> originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();

    private List<GameObject> activeInstantiatedEffects = new List<GameObject>();

    #region Unity Lifecycle

    private void Awake()
    {
        ValidateRenderers();
        InitializeMaterials();
    }

    private void OnEnable()
    {
        ResetVisuals();
    }

    private void OnDisable()
    {
        CleanupAllEffects();
    }

    private void OnDestroy()
    {
        CleanupAllEffects();
    }

    #endregion

    #region Initialization & Validation

    /// <summary>
    /// Valida que existan renderers asignados.
    /// </summary>
    private void ValidateRenderers()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[EnemyVisualEffects] No se encontraron Renderers en {gameObject.name}. Los efectos visuales no funcionarán correctamente.");
            }
        }
    }

    /// <summary>
    /// Inicializa los materiales por defecto.
    /// </summary>
    private void InitializeMaterials()
    {
        if (renderers == null || defaultMaterial == null) return;

        foreach (var r in renderers)
        {
            if (r != null)
            {
                r.material = defaultMaterial;
            }
        }
    }

    /// <summary>
    /// Limpia todos los efectos activos.
    /// </summary>
    private void CleanupAllEffects()
    {
        // Detener stun
        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
            stunEffectCoroutine = null;
        }

        // Detener glow
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }

        // Detener y restaurar todos los parpadeos por material activos
        foreach (var kv in new List<KeyValuePair<Renderer, Coroutine>>(materialBlinkRoutines))
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
            RestoreOriginalMaterialsForRenderer(kv.Key);
        }
        materialBlinkRoutines.Clear();
        originalMaterialsByRenderer.Clear();

        // Destruir efectos instanciados
        DestroyAllInstantiatedEffects();

        // Detener todas las corrutinas
        StopAllCoroutines();
    }

    /// <summary>
    /// Destruye todos los efectos instanciados.
    /// </summary>
    private void DestroyAllInstantiatedEffects()
    {
        foreach (GameObject effect in activeInstantiatedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
        activeInstantiatedEffects.Clear();
    }

    #endregion

    #region Damage Feedback (material blink, numbers, sonido)

    /// <summary>
    /// Llama este método cuando el enemigo recibe daño. 
    /// Incluye:
    /// - Números de daño
    /// - Material swap blink (BlinkMaterial) usando los parámetros serializados (si blinkFlashMaterial asignado)
    /// - Sonido
    /// </summary>
    public void PlayDamageFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        if (stunEffectCoroutine != null && isStunned)
        {
            // Solo mostrar números y sonido
            ShowDamageNumber(damagePosition, damage, isCritical);
            if (audioSource != null)
            {
                PlayHitSound(isCritical);
            }
            return;
        }

        // Números de daño
        ShowDamageNumber(damagePosition, damage, isCritical);

        // Material swap (parpadeo por swap)
        if (blinkFlashMaterial != null && renderers != null)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    BlinkMaterial(renderer, blinkFlashMaterial, blinkInterval, blinkCount);
                }
            }
        }

        PlayHitSound(isCritical);
    }

    /// <summary>
    /// Reproduce el sonido de impacto correspondiente.
    /// </summary>
    private void PlayHitSound(bool isCritical)
    {
        if (audioSource == null) return;

        AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Material Blink (swap materials)

    /// <summary>
    /// Parpadea (swap de materiales) en el renderer objetivo usando 'flashMaterial'.
    /// Si ya existe un parpadeo en ese renderer, lo reinicia y restaura los materiales originales antes de empezar de nuevo.
    /// </summary>
    /// <param name="targetRenderer">Renderer a parpadear (no null)</param>
    /// <param name="flashMaterial">Material temporal de parpadeo</param>
    /// <param name="blinkInterval">intervalo entre toggles</param>
    /// <param name="blinkCount">número total de toggles</param>
    public void BlinkMaterial(Renderer targetRenderer, Material flashMaterial, float blinkInterval, int blinkCount)
    {
        if (targetRenderer == null || flashMaterial == null) return;

        if (materialBlinkRoutines.TryGetValue(targetRenderer, out Coroutine existing))
        {
            if (existing != null) StopCoroutine(existing);
            RestoreOriginalMaterialsForRenderer(targetRenderer);
            materialBlinkRoutines.Remove(targetRenderer);
        }

        Coroutine routine = StartCoroutine(MaterialBlinkCoroutine(targetRenderer, flashMaterial, blinkInterval, Mathf.Max(1, blinkCount)));
        materialBlinkRoutines[targetRenderer] = routine;
    }

    private IEnumerator MaterialBlinkCoroutine(Renderer targetRenderer, Material flashMaterial, float blinkInterval, int toggles)
    {
        if (targetRenderer == null || flashMaterial == null)
        {
            if (materialBlinkRoutines.ContainsKey(targetRenderer)) materialBlinkRoutines.Remove(targetRenderer);
            yield break;
        }

        Material[] originalMaterials = targetRenderer.materials;
        originalMaterialsByRenderer[targetRenderer] = originalMaterials;

        bool showingFlash = false;

        try
        {
            for (int i = 0; i < toggles; i++)
            {
                if (!showingFlash)
                {
                    Material[] flashArray = new Material[originalMaterials.Length];
                    for (int j = 0; j < flashArray.Length; j++) flashArray[j] = flashMaterial;
                    try { targetRenderer.materials = flashArray; }
                    catch { /* Ignorar errores de material assignment */ }
                    
                    showingFlash = true;
                }
                else
                {
                    // restaurar originales
                    try { targetRenderer.materials = originalMaterials; }
                    catch { /* Ignorar errores de material assignment */ }

                    showingFlash = false;
                }

                yield return new WaitForSeconds(blinkInterval);
            }
        }
        finally
        {
            // restaurar por seguridad
            RestoreOriginalMaterialsForRenderer(targetRenderer);

            // limpiar registros
            if (materialBlinkRoutines.ContainsKey(targetRenderer)) materialBlinkRoutines.Remove(targetRenderer);
            if (originalMaterialsByRenderer.ContainsKey(targetRenderer)) originalMaterialsByRenderer.Remove(targetRenderer);
        }
    }

    private void RestoreOriginalMaterialsForRenderer(Renderer r)
    {
        if (r == null) return;

        if (originalMaterialsByRenderer.TryGetValue(r, out Material[] originals))
        {
            if (originals != null && originals.Length > 0)
            {
                try
                {
                    r.materials = originals;
                }
                catch { /* ignorar */ }
            }
        }
    }

    #endregion

    #region Armor Glow

    public void StartArmorGlow()
    {
        if (renderers == null || glowMaterial == null) return;

        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material = glowMaterial;
            }
        }

        glowCoroutine = StartCoroutine(AnimateGlowCoroutine());
    }

    public void StopArmorGlow()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }

        if (renderers == null || defaultMaterial == null) return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material = defaultMaterial;
            }
        }
    }

    /// <summary>
    /// Corrutina que anima el brillo de la armadura.
    /// </summary>
    private IEnumerator AnimateGlowCoroutine()
    {
        if (renderers == null) yield break;

        float baseIntensity = 1f;

        while (true)
        {
            float intensity = baseIntensity + Mathf.Sin(Time.time * 3f) * glowIntensity;

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.material == glowMaterial)
                {
                    try
                    {
                        renderer.material.SetFloat("_EmissionIntensity", intensity);
                    }
                    catch { /* Ignorar si el shader no tiene esta propiedad */ }
                }
            }

            yield return null;
        }
    }

    #endregion

    #region Damage Numbers

    /// <summary>
    /// Metodo para mostrar números de daño en la posición especificada.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="damage"></param>
    /// <param name="isCritical"></param>
    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false)
    {
        if (damageNumberPrefab != null)
        {
            GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);
            activeInstantiatedEffects.Add(damageNumber);

            DamageNumber damageNumberScript = damageNumber.GetComponent<DamageNumber>();

            if (damageNumberScript != null)
            {
                damageNumberScript.Initialize(damage, isCritical);
            }
        }
    }

    private void ResetVisuals()
    {
        // Restaurar materiales por defecto
        if (renderers != null && defaultMaterial != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.material = defaultMaterial;
                    r.enabled = true;
                }
            }
        }

        // Limpiar números de daño existentes
        if (damageNumberParent != null)
        {
            for (int i = 0; i < damageNumberParent.childCount; i++)
            {
                Destroy(damageNumberParent.GetChild(i).gameObject);
            }
        }
    }

    #endregion

    #region Stun Effect Management

    /// <summary>
    /// Inicia el efecto visual de aturdimiento. Se llama desde EnemyHealth.ApplyStun()
    /// </summary>
    public void StartStunEffect(float duration)
    {
        // Detener todos los efectos de parpadeo activos primero
        foreach (var kv in new List<KeyValuePair<Renderer, Coroutine>>(materialBlinkRoutines))
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
            RestoreOriginalMaterialsForRenderer(kv.Key);
        }

        materialBlinkRoutines.Clear();
        originalMaterialsByRenderer.Clear();

        // Detener stun anterior si existía
        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
        }

        Debug.Log($"[EnemyVisualEffects] StartStunEffect llamado en {gameObject.name} por {duration}s");
        stunEffectCoroutine = StartCoroutine(StunEffectCoroutine(duration));
    }

    /// <summary>
    /// Detiene el efecto visual de aturdimiento. Se llama desde EnemyHealth cuando termina el stun.
    /// </summary>
    public void StopStunEffect()
    {
        Debug.Log($"[EnemyVisualEffects] StopStunEffect llamado en {gameObject.name}");

        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
            stunEffectCoroutine = null;
        }

        CleanupStunEffect();
    }

    /// <summary>
    /// Limpia los efectos del stun (materiales, VFX, visibilidad).
    /// </summary>
    private void CleanupStunEffect()
    {
        // Asegurar que los renderers estén visibles
        SetRenderersActive(true);

        // Restaurar materiales originales
        if (renderers != null && defaultMaterial != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.material = defaultMaterial;
                }
            }
        }

        // Destruir VFX de aturdimiento
        if (activeStunVFX != null)
        {
            Destroy(activeStunVFX);
            activeStunVFX = null;
        }
    }

    private IEnumerator StunEffectCoroutine(float duration)
    {
        isStunned = true;

        // 1) Cambiar materiales a stunMaterial (o color de fallback)
        ApplyStunMaterial();

        // 2) Reproducir sonido de stun
        PlayStunSound();

        // 3) Instanciar VFX de aturdimiento
        SpawnStunVFX();

        // 4) Parpadeo rápido durante el stun
        yield return StartCoroutine(StunBlinkEffect(duration));

        // 5) Limpieza final
        CleanupStunEffect();

        stunEffectCoroutine = null;
        isStunned = false;
    }

    /// <summary>
    /// Aplica el material o color de stun a todos los renderers.
    /// </summary>
    private void ApplyStunMaterial()
    {
        if (renderers == null) return;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            if (stunMaterial != null)
            {
                r.material = stunMaterial;
                Debug.Log($"[EnemyVisualEffects] Material de stun aplicado a {r.name}");
            }
            else if (r.material.HasProperty("_Color"))
            {
                r.material.color = stunGlowColor;
                Debug.Log($"[EnemyVisualEffects] Color de stun aplicado a {r.name}");
            }
        }
    }

    /// <summary>
    /// Reproduce el sonido de aturdimiento.
    /// </summary>
    private void PlayStunSound()
    {
        if (audioSource != null && stunSFX != null)
        {
            audioSource.PlayOneShot(stunSFX);
        }
    }

    /// <summary>
    /// Instancia el VFX de aturdimiento.
    /// </summary>
    private void SpawnStunVFX()
    {
        if (stunVFXPrefab == null) return;

        activeStunVFX = Instantiate(
            stunVFXPrefab,
            transform.position + Vector3.up * stunVFXHeight,
            Quaternion.identity,
            transform
        );

        activeInstantiatedEffects.Add(activeStunVFX);
    }

    /// <summary>
    /// Ejecuta el efecto de parpadeo durante el stun.
    /// </summary>
    private IEnumerator StunBlinkEffect(float duration)
    {
        float elapsedTime = 0f;
        float nextBlinkTime = 0f;
        bool isVisible = true;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            // Toggle visibilidad basado en tiempo
            if (elapsedTime >= nextBlinkTime)
            {
                isVisible = !isVisible;
                SetRenderersActive(isVisible);
                nextBlinkTime = elapsedTime + stunBlinkSpeed;
            }

            yield return null;
        }

        SetRenderersActive(true);
    }

    private void SetRenderersActive(bool active)
    {
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.enabled = active;
                }
            }
        }
    }

    #endregion
}