using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DummyUIController : MonoBehaviour, ICombatDummyUI
{
    [Header("Health State UI")]
    [SerializeField] private GameObject healthBarGroup;
    [SerializeField] private Slider healthSlider;

    [Header("Hit Count UI")]
    [SerializeField] private GameObject hitCountGroup;
    [SerializeField] private TMP_Text hitCountText;

    public void UpdateHealthBar(float currentHealthRatio, Color? stateColor)
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealthRatio;
        }
    }

    public void UpdateHitCounter(int currentHits, int requiredHits)
    {
        float ratio = (float)currentHits / requiredHits;

        if (healthSlider != null)
        {
            healthSlider.value = ratio;
        }

        if (hitCountText != null)
        {
            hitCountText.text = $"Golpes: {currentHits}/{requiredHits}";
        }
    }

    public void SetUIActive(DummyLogicType logicType, bool active)
    {
        if (healthBarGroup != null)
        {
            healthBarGroup.SetActive(active && logicType == DummyLogicType.HealthState);
        }

        if (hitCountGroup != null)
        {
            hitCountGroup.SetActive(active && logicType == DummyLogicType.RequiredHitCount);
        }
    }
}