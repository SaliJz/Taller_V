using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI manipulationIndicatorText;
    [SerializeField] private TextMeshProUGUI roomStatusText;

    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        manipulationIndicatorText.enabled = false; 
    }

    private void Update()
    {
        if (DevilManipulationManager.Instance != null && roomStatusText != null)
        {
            roomStatusText.text = DevilManipulationManager.Instance.GetCurrentManipulationStatus();
        }
    }


    public void ShowManipulationText(string message)
    {
        manipulationIndicatorText.text = message;
        manipulationIndicatorText.enabled = true;
    }

    public void ClearManipulationText()
    {
        manipulationIndicatorText.enabled = false;
        manipulationIndicatorText.text = string.Empty;
    }
}