using UnityEngine;
using TMPro;
using System.Text;

public class PlayerStatsPanel : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("El contenedor principal del panel (RectTransform).")]
    [SerializeField] private RectTransform panelRect;
    [Tooltip("El componente de texto donde se imprimirán las estadísticas.")]
    [SerializeField] private TextMeshProUGUI statsTextDisplay;

    [Header("Posicionamiento")]
    [Tooltip("Desplazamiento en el mundo 3D respecto a la posición del jugador.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(2f, 0f, 0f);

    private PlayerStatsManager statsManager;
    private Canvas parentCanvas;
    private bool wasInventoryOpen = false;

    private void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += OnStatChangedHandler;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= OnStatChangedHandler;
    }

    private void OnStatChangedHandler(StatType type, float newValue)
    {
        if (panelRect != null && panelRect.gameObject.activeSelf)
        {
            UpdateStatsPanel();
        }
    }

    private void Update()
    {
        bool isInventoryOpen = InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen;

        if (isInventoryOpen != wasInventoryOpen)
        {
            wasInventoryOpen = isInventoryOpen;

            if (isInventoryOpen)
            {
                if (statsManager == null) statsManager = FindAnyObjectByType<PlayerStatsManager>();
                UpdateStatsPanel();
                if (panelRect != null) panelRect.gameObject.SetActive(true);
            }
            else
            {
                if (panelRect != null) panelRect.gameObject.SetActive(false);
            }
        }

        if (isInventoryOpen && panelRect != null && statsManager != null)
        {
            FollowPlayerPosition();
        }
    }

    /// <summary>
    /// Convierte la posición 3D del jugador a coordenadas 2D del Canvas para posicionar el panel.
    /// </summary>
    private void FollowPlayerPosition()
    {
        if (Camera.main == null || parentCanvas == null) return;

        Vector3 targetWorldPosition = statsManager.transform.position + worldOffset;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, targetWorldPosition);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            screenPoint,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 localPoint);

        panelRect.localPosition = localPoint;
    }

    public void UpdateStatsPanel()
    {
        if (statsManager == null || statsTextDisplay == null) return;
        StringBuilder sb = new StringBuilder();

        float baseRes = (1f - statsManager.GetBaseStat(StatType.DamageTaken)) * 100f;
        float currRes = (1f - statsManager.GetCurrentStat(StatType.DamageTaken)) * 100f;
        //sb.AppendLine(FormatCustomLine("Res. Daño", baseRes, currRes, isPercentage: true));

        sb.AppendLine(FormatStat("Vel. Mov.", StatType.MoveSpeed));
        sb.AppendLine(FormatStat("Alc. Dash", StatType.DashRangeMultiplier));

        sb.AppendLine(FormatStat("Atq. Melé", StatType.MeleeAttackDamage));
        sb.AppendLine(FormatStat("Atq. Dist.", StatType.ShieldAttackDamage));
        sb.AppendLine(FormatStat("Vel. Melé", StatType.MeleeAttackSpeed));
        sb.AppendLine(FormatStat("Vel. Dist.", StatType.ShieldSpeed));

        //sb.AppendLine(FormatStat("Emp. Melé", StatType.MeleePushForce)); 
        sb.AppendLine(FormatStat("Emp. Dist.", StatType.ShieldPushForce));

        //sb.AppendLine(FormatStat("Daño Ext. vs Superresistencia", StatType.ToughnessDamageMultiplier)); 

        float realBaseRebounds = statsManager.GetBaseStat(StatType.ShieldMaxRebounds);
        float currentRebounds = statsManager.GetCurrentStat(StatType.ShieldMaxRebounds);
        sb.AppendLine(FormatCustomLine("Reb. Escudo", 0, (currentRebounds - realBaseRebounds)));

        sb.AppendLine(FormatStat("Coste Bers.", StatType.HealthDrainAmount, inverseColors: true));
        //sb.AppendLine(FormatStat("Bonus Bers.", StatType.BerserkerBonus));

        sb.AppendLine(FormatStat("Robo Vida", StatType.LifestealOnKill));

        statsTextDisplay.text = sb.ToString();
    }

    /// <summary>
    /// Extrae automáticamente del Gestor y formatea.
    /// </summary>
    private string FormatStat(string statName, StatType type, bool inverseColors = false)
    {
        float baseValue = statsManager.GetBaseStat(type);
        float currentValue = statsManager.GetCurrentStat(type);
        return FormatCustomLine(statName, baseValue, currentValue, false, inverseColors);
    }

    /// <summary>
    /// Función base de formateo. Permite inyectar matemáticas manuales (como el % de Resistencia o Rebotes).
    /// </summary>
    private string FormatCustomLine(string statName, float baseValue, float currentValue, bool isPercentage = false, bool inverseColors = false)
    {
        float difference = currentValue - baseValue;
        string diffString = "";
        string symbol = isPercentage ? "%" : "";

        if (Mathf.Abs(difference) > 0.01f)
        {
            if (difference > 0)
            {
                string color = inverseColors ? "red" : "green";
                diffString = $"<color={color}>+{difference:0.##}{symbol}</color>";
            }
            else
            {
                string color = inverseColors ? "green" : "red";
                diffString = $"<color={color}>{difference:0.##}{symbol}</color>";
            }
        }
        return $"{statName} <pos=65%>{baseValue:0.##}{symbol} <pos=85%>{diffString}";
    }
}