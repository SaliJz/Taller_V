using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TextMeshProUGUI))]
public class HoverElectricEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Color de Energía HDR (Usa Mostaza Intenso)")]
    [ColorUsage(true, true)]
    public Color electricColor = new Color(2.8f, 1.6f, 0.0f, 1.0f);

    [Header("Parámetros de Caos del Rayo")]
    public float flickerSpeed = 12f;
    public float maxFlickerPower = 0.8f;
    public float flickerFadeSpeed = 15f;

    [Header("Movimiento (Offsets)")]
    public float verticalTravel = 0.5f;
    public float verticalSpeed = 4.0f;
    public float chaosAmount = 0.2f;

    private TextMeshProUGUI textMesh;
    private Material instanceMaterial;
    private float currentIntensity = 0f;
    private bool isHovering = false;

    private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowPowerID = Shader.PropertyToID("_GlowPower");
    private static readonly int GlowOffsetID = Shader.PropertyToID("_GlowOffset");
    private static readonly int GlowOuterID = Shader.PropertyToID("_GlowOuter");
    private static readonly int GlowSoftnessID = Shader.PropertyToID("_GlowSoftness");

    void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();

        instanceMaterial = new Material(textMesh.fontSharedMaterial);
        textMesh.fontMaterial = instanceMaterial;

        instanceMaterial.EnableKeyword("GLOW_ON");
    }

    void Update()
    {
        float target = isHovering ? maxFlickerPower : 0f;
        currentIntensity = Mathf.Lerp(currentIntensity, target, Time.deltaTime * flickerFadeSpeed);

        instanceMaterial.SetColor(GlowColorID, electricColor);
        instanceMaterial.SetFloat(GlowPowerID, currentIntensity);

        if (currentIntensity > 0.01f)
        {
            float t = Time.time;

            float x = (Mathf.PerlinNoise(t * flickerSpeed, 0f) - 0.5f) * chaosAmount;
            float y = (Mathf.PerlinNoise(0f, t * flickerSpeed) - 0.5f) * verticalTravel;
            float yFlow = Mathf.Repeat(t * verticalSpeed, verticalTravel);

            Vector4 finalOffset = new Vector4(x, y + yFlow, 0, 0);
            instanceMaterial.SetVector(GlowOffsetID, finalOffset);

            float softness = Mathf.Lerp(0.1f, 0.25f, Mathf.Sin(t * 30f) * 0.5f + 0.5f);
            instanceMaterial.SetFloat(GlowSoftnessID, softness);

            textMesh.UpdateMeshPadding();
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovering = true;
    public void OnPointerExit(PointerEventData eventData) => isHovering = false;

    void OnDestroy()
    {
        if (instanceMaterial != null)
            Destroy(instanceMaterial);
    }
}