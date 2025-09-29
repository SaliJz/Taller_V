using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public const int MaxInventorySize = 10;
    private static readonly List<ShopItem> currentRunItems = new List<ShopItem>();

    public static List<ShopItem> CurrentRunItems
    {
        get { return currentRunItems; }
    }

    public void ResetRunItems()
    {
        currentRunItems.Clear();
    }


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

    [Header("Visualización del Inventario (UI)")]
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
        if (CurrentRunItems.Count < MaxInventorySize) 
        {
            CurrentRunItems.Add(item); 
            UpdateInventoryUI();
            return true;
        }

        ShowInventoryFullMessage();
        return false;
    }

    public int GetCurrentItemCount()
    {
        return CurrentRunItems.Count; 
    }

    public void ClearInventory()
    {
        ResetRunItems(); 
        UpdateInventoryUI();
    }

    private void UpdateInventoryUI()
    {
        List<ShopItem> currentItems = CurrentRunItems; 

        for (int i = 0; i < MaxInventorySize; i++)
        {
            if (itemDisplayTexts[i] != null)
            {
                if (i < currentItems.Count)
                {
                    itemDisplayTexts[i].text = currentItems[i].itemName;
                }
                else
                {
                    itemDisplayTexts[i].text = "";
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

        ShowWarningMessage(message);
    }

    public void ShowWarningMessage(string message)
    {
        if (inventoryFullText == null) return;

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
}