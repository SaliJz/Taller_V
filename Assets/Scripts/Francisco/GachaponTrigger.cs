using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct RarityColorMapping
{
    public EffectRarity rarity;
    [ColorUsage(true, true)]
    public Color color;
}

[RequireComponent(typeof(Collider))]
public class GachaponTrigger : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GachaponSystem gachaponSystem;
    [SerializeField] private MeshRenderer cubeRenderer;
    [SerializeField] private Material activatedMaterial;

    [Header("Configuración")]
    public KeyCode activationKey = KeyCode.E;
    private bool playerIsNear = false;
    private bool isActivated = false;

    [Header("Animation Configuration")]
    public float animationDuration = 1.5f;
    [Range(0.05f, 1f)]
    public float cycleSpeed = 0.15f;
    public AnimationCurve colorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float intensityLerpDuration = 0.5f;
    [Range(1.0f, 10.0f)]
    public float finalIntensityMultiplier = 3.0f;

    [Header("Rarity Colors")]
    public List<RarityColorMapping> rarityColorMappings = new List<RarityColorMapping>
    {
        new RarityColorMapping { rarity = EffectRarity.Comun, color = new Color(0.5f, 0.5f, 0.5f, 1f) },
        new RarityColorMapping { rarity = EffectRarity.Raro, color = Color.blue },
        new RarityColorMapping { rarity = EffectRarity.Epico, color = new Color(1f, 0f, 1f, 1f) },
        new RarityColorMapping { rarity = EffectRarity.Legendario, color = Color.yellow }
    };
    private Dictionary<object, Color> rarityColorsMap = new Dictionary<object, Color>();

    private Material originalMaterialInstance;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        if (gachaponSystem == null)
            gachaponSystem = FindAnyObjectByType<GachaponSystem>();

        if (cubeRenderer == null)
            cubeRenderer = GetComponent<MeshRenderer>();

        if (gachaponSystem == null || cubeRenderer == null)
        {
            Debug.LogError("GachaponSystem o MeshRenderer no encontrado. Desactivando GachaponTrigger.");
            enabled = false;
            return;
        }

        originalMaterialInstance = cubeRenderer.material;

        foreach (var mapping in rarityColorMappings)
        {
            rarityColorsMap[mapping.rarity] = mapping.color;
        }
    }

    private void Update()
    {
        if (playerIsNear && !isActivated && Input.GetKeyDown(activationKey))
        {
            StartCoroutine(AnimateAndPull());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = false;
        }
    }

    private IEnumerator AnimateAndPull()
    {
        isActivated = true;

        Coroutine animationCoroutine = StartCoroutine(AnimateGachaponLight(animationDuration));
        yield return animationCoroutine;

        GachaponResult result = gachaponSystem.PullGachapon();

        if (rarityColorsMap.ContainsKey(result.rarity))
        {
            if (result.effectPair != null)
            {
                gachaponSystem.ApplyEffect(result);
            }
            else
            {
                Debug.LogWarning("La gachapon falló o no devolvió un par de efectos. El color de la rareza ganada será visible permanentemente.");
            }

            yield return StartCoroutine(SetFinalRarityColor(result.rarity));

            if (rarityColorsMap.TryGetValue(result.rarity, out Color baseColor))
            {
                StartCoroutine(PulseFinalRarityColor(baseColor));
            }
        }
        else
        {
            cubeRenderer.material.SetColor(EmissionColor, Color.black);
            Debug.LogWarning("La gachapon no devolvió una rareza válida. Sistema bloqueado y apagado.");
        }
    }

    private IEnumerator AnimateGachaponLight(float duration)
    {
        float startTime = Time.time;
        Material animMaterial = cubeRenderer.material;

        List<Color> cycleColors = rarityColorMappings.Select(m => m.color).ToList();

        if (!animMaterial.HasProperty(EmissionColor) || cycleColors.Count < 2)
        {
            yield break;
        }

        int colorIndex = 0;
        float segmentTime = 0f;

        while (Time.time < startTime + duration)
        {
            Color startColor = cycleColors[colorIndex];
            Color endColor = cycleColors[(colorIndex + 1) % cycleColors.Count];

            segmentTime += Time.deltaTime;
            float t = segmentTime / cycleSpeed;
            t = colorCurve.Evaluate(Mathf.Clamp01(t));

            Color currentColor = Color.Lerp(startColor, endColor, t);
            animMaterial.SetColor(EmissionColor, currentColor);

            if (t >= 1.0f)
            {
                colorIndex = (colorIndex + 1) % cycleColors.Count;
                segmentTime = 0f;
            }

            yield return null;
        }
    }

    private IEnumerator SetFinalRarityColor(EffectRarity rarity)
    {
        if (rarityColorsMap.TryGetValue(rarity, out Color baseColor))
        {
            if (activatedMaterial != null)
            {
                if (cubeRenderer.material != activatedMaterial)
                {
                    cubeRenderer.material = activatedMaterial;
                }
            }

            Material finalMaterial = cubeRenderer.material;

            Color startColor = finalMaterial.GetColor(EmissionColor);
            Color targetColor = baseColor * finalIntensityMultiplier;

            float elapsedTime = 0f;

            while (elapsedTime < intensityLerpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / intensityLerpDuration;
                t = Mathf.SmoothStep(0f, 1f, t);

                Color currentColor = Color.Lerp(startColor, targetColor, t);
                finalMaterial.SetColor(EmissionColor, currentColor);

                yield return null;
            }

            finalMaterial.SetColor(EmissionColor, targetColor);
        }
    }

    private IEnumerator PulseFinalRarityColor(Color baseColor)
    {
        Material finalMaterial = cubeRenderer.material;
        Color highIntensityColor = baseColor * finalIntensityMultiplier;
        Color lowIntensityColor = baseColor;

        while (true)
        {
            float t = Mathf.PingPong(Time.time / intensityLerpDuration, 1f);
            t = colorCurve.Evaluate(t);

            Color currentColor = Color.Lerp(lowIntensityColor, highIntensityColor, t);
            finalMaterial.SetColor(EmissionColor, currentColor);

            yield return null;
        }
    }
}