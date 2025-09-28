using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public const int MaxInventorySize = 10;
    private readonly List<ShopItem> purchasedItems = new();

    [Header("UI y Mensajes de Advertencia")]
    public GameObject inventoryFullPanel;
    public TextMeshProUGUI inventoryFullText;

    [TextArea]
    public string[] inventoryFullMessages = new string[]
    {
        "Inventario lleno.",
        "No puedes llevar más."
    };

    public float messageDisplayTime = 2.0f;

    private int messageIndex = 0;
    private Coroutine hideMessageCoroutine;

    [Header("Visualización del Inventario")]
    public TextMeshProUGUI[] itemDisplayTexts = new TextMeshProUGUI[MaxInventorySize];

    private void Start()
    {
        if (inventoryFullPanel != null)
        {
            inventoryFullPanel.SetActive(false);
        }
        else if (inventoryFullText != null)
        {
            inventoryFullText.gameObject.SetActive(false);
        }

        UpdateInventoryUI();
    }

    public bool TryAddItem(ShopItem item)
    {
        if (purchasedItems.Count < MaxInventorySize)
        {
            purchasedItems.Add(item);
            UpdateInventoryUI();
            return true;
        }
        else
        {
            ShowInventoryFullMessage();
            return false;
        }
    }

    private void UpdateInventoryUI()
    {
        for (int i = 0; i < MaxInventorySize; i++)
        {
            if (i < purchasedItems.Count)
            {
                if (itemDisplayTexts[i] != null)
                {
                    itemDisplayTexts[i].text = purchasedItems[i].itemName;
                }
            }
            else
            {
                if (itemDisplayTexts[i] != null)
                {
                    itemDisplayTexts[i].text = string.Empty;
                }
            }
        }
    }

    public void ShowInventoryFullMessage()
    {
        if (inventoryFullText == null) return;

        string message = "Inventario lleno.";
        if (inventoryFullMessages.Length > 0)
        {
            message = inventoryFullMessages[messageIndex];
            messageIndex = (messageIndex + 1) % inventoryFullMessages.Length;
        }

        inventoryFullText.text = message;
        inventoryFullText.color = Color.red;

        if (inventoryFullPanel != null)
        {
            inventoryFullPanel.SetActive(true);
        }
        else
        {
            inventoryFullText.gameObject.SetActive(true);
        }

        if (hideMessageCoroutine != null)
        {
            StopCoroutine(hideMessageCoroutine);
        }
        hideMessageCoroutine = StartCoroutine(HideMessageAfterDelay());
    }

    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayTime);

        if (inventoryFullPanel != null)
        {
            inventoryFullPanel.SetActive(false);
        }
        else if (inventoryFullText != null)
        {
            inventoryFullText.gameObject.SetActive(false);
        }
    }

    public int GetCurrentItemCount() => purchasedItems.Count;
}