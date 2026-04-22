using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Componente para cada slot de ítem en el inventario
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Datos externos

    [Header("Referencias UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private GameObject temporalEffectObject;
    [SerializeField] private Image pulseGlowImage;

    [Header("Efectos de Pulso")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    //[SerializeField] private bool enableDebugLogs = false;

    #endregion

    #region Datos internos

    private ShopItem itemData;
    private InventoryUIManager inventoryManager;
    private Color originalBackgroundColor;
    private Color originalBorderColor;
    private bool isClicked;
    private bool hasItem;
    private bool isGoldenSlot;
    private bool isCol1Slot; // Se detecta automáticamente por el padre

    private Coroutine pulseCoroutine;

    #endregion

    #region Inicialización

    public void Initialize(InventoryUIManager manager)
    {
        inventoryManager = manager;

        if (backgroundImage != null) originalBackgroundColor = backgroundImage.color;

        if (temporalEffectObject != null) temporalEffectObject.SetActive(false);

        // Determina si pertenece a col1 comprobando el padre
        isCol1Slot = transform.parent != null && transform.parent.name.Contains("1");

        ClearSlot();
    }

    public void SetGolden(bool golden, Color color)
    {
        isGoldenSlot = golden;
        if (backgroundImage != null && golden)
        {
            backgroundImage.color = new Color(color.r, color.g, color.b, 0.15f);
            originalBackgroundColor = backgroundImage.color;
        }
        if (rarityBorder != null && golden)
        {
            rarityBorder.color = color;
            rarityBorder.enabled = true;
            originalBorderColor = color;
        }
    }

    public void SetItem(ShopItem item)
    {
        itemData = item;
        hasItem = item != null;

        if (!hasItem) { ClearSlot(); return; }

        if (iconImage != null)
        {
            iconImage.sprite = item.itemIcon;
            iconImage.enabled = item.itemIcon != null;
        }

        if (rarityBorder != null)
        {
            rarityBorder.color = isGoldenSlot ? originalBorderColor : item.GetRarityColor();
            rarityBorder.enabled = true;
            if (!isGoldenSlot) originalBorderColor = rarityBorder.color;
        }

        if (item.isTemporary) ShowTemporalEffect();
        else HideTemporalEffect();
    }

    public void ClearSlot()
    {
        itemData = null;
        hasItem = false;

        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }

        if (!isGoldenSlot && rarityBorder != null)
            rarityBorder.enabled = false;

        HideTemporalEffect();
    }

    #endregion

    #region Efectos

    private void ShowTemporalEffect()
    {
        if (temporalEffectObject != null) temporalEffectObject.SetActive(true);
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        if (gameObject.activeInHierarchy) pulseCoroutine = StartCoroutine(PulseEffect());
    }

    private void HideTemporalEffect()
    {
        if (temporalEffectObject != null) temporalEffectObject.SetActive(false);
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        if (rarityBorder != null && hasItem) rarityBorder.color = originalBorderColor;
    }

    private IEnumerator PulseEffect()
    {
        Color baseColor = originalBorderColor;
        while (hasItem && itemData != null && itemData.isTemporary)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(1f - pulseIntensity, 1f, pulse);

            if (rarityBorder != null)
            {
                rarityBorder.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            if (pulseGlowImage != null)
            {
                pulseGlowImage.color = new Color(1f, 0.27f, 0f, Mathf.Lerp(0.2f, 0.6f, pulse));
            }
            yield return null;
        }
        pulseCoroutine = null;
    }

    #endregion

    #region Interacción

    private void OnEnable()
    {
        if (hasItem && itemData != null && itemData.isTemporary)
        {
            ShowTemporalEffect();
        }
    }

    private void OnDisable()
    {
        if (pulseCoroutine != null) 
        { 
            StopCoroutine(pulseCoroutine); pulseCoroutine = null; 
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasItem) return;

        // Highlight background
        if (backgroundImage != null && inventoryManager != null)
        {
            backgroundImage.color =
                Color.Lerp(originalBackgroundColor, inventoryManager.GetHighlightColor(), 0.5f);
        }
        transform.localScale = Vector3.one * 1.05f;

        InventoryTooltip.Instance?.Show(itemData);
        InventoryAudioManager.Instance?.PlayHoverSound();
        
        if (isCol1Slot) inventoryManager?.SetInteractiveOpacity(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hasItem) return;

        if (backgroundImage != null) backgroundImage.color = originalBackgroundColor;

        transform.localScale = Vector3.one;

        InventoryTooltip.Instance?.Hide();

        if (isCol1Slot) inventoryManager?.SetInteractiveOpacity(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hasItem) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (!isClicked)
        {
            isClicked = true;
            inventoryManager?.ShowItemDetails(itemData);
        }
        else
        {
            isClicked = false;
            inventoryManager?.HideItemDetails();
        }
        InventoryAudioManager.Instance?.PlayClickSound();
        InventoryAudioManager.Instance?.PlayRaritySound(itemData.rarity);
    }

    #endregion
}