using UnityEngine;
using System.Collections;
using TMPro;

public class DummyTarget : MonoBehaviour, IDamageable
{
    #region IDAMAGEABLE IMPLEMENTATION
    public float CurrentHealth { get { return 100f; } }
    public float MaxHealth { get { return 100f; } }
    #endregion

    #region REFERENCES AND CONFIGURATION

    private Renderer dummyRenderer;
    private Color baseColor;
    private Coroutine colorChangeCoroutine;

    [Header("Dialogue Configuration")]
    public string[] dialogueLines; 
    private int dialogueIndex = 0; 

    [Header("Invulnerability Settings")]
    public float invulnerabilityDuration = 0.5f; 
    private bool canBeHit = true; 

    [Header("Visual Effects Settings")]
    public Color hitColor = Color.red;
    public float hitColorDuration = 0.2f;

    [Header("UI Configuration")]
    public GameObject logPanel;
    public TextMeshProUGUI logText;
    public float logPanelDisplayTime = 4f;

    private Coroutine panelToggleCoroutine;

    #endregion

    #region INITIALIZATION
    void Awake()
    {
        dummyRenderer = GetComponent<Renderer>();

        if (dummyRenderer != null)
        {
            baseColor = dummyRenderer.material.GetColor("_Color");
        }
        else
        {
            Debug.LogError("Renderer component not found on DummyTarget.");
        }

        if (logPanel != null) logPanel.SetActive(false);
    }
    #endregion


    #region CORE FUNCTIONALITY

    public void TakeDamage(float damageAmount, bool isCritical = false)
    {
        if (!canBeHit)
        {
            return;
        }

        canBeHit = false;
        StartCoroutine(HitCooldown());

        if (colorChangeCoroutine != null)
        {
            StopCoroutine(colorChangeCoroutine);
        }
        colorChangeCoroutine = StartCoroutine(FlashColor());

        ShowNextLineCyclic();

        Debug.Log($"Maniquí golpeado. Daño: {damageAmount} (Crítico: {isCritical}). Nueva línea de diálogo.");
    }

    private IEnumerator HitCooldown()
    {
        yield return new WaitForSeconds(invulnerabilityDuration);
        canBeHit = true;
    }

    public void ShowNextLineCyclic()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            if (logText != null) logText.text = "No hay líneas de diálogo configuradas.";
            return;
        }

        dialogueIndex++;

        if (dialogueIndex >= dialogueLines.Length)
        {
            dialogueIndex = 0; 
        }

        ShowCurrentLine();
    }

    public void ShowCurrentLine()
    {
        UpdateLogUI();

        if (panelToggleCoroutine != null)
        {
            StopCoroutine(panelToggleCoroutine);
        }
        panelToggleCoroutine = StartCoroutine(TogglePanelAfterDelay());
    }

    #endregion

    #region LOGIC AND UI

    private void UpdateLogUI()
    {
        if (logText == null || dialogueLines == null || dialogueLines.Length == 0) return;

        logText.text = dialogueLines[dialogueIndex];
    }

    private IEnumerator TogglePanelAfterDelay()
    {
        if (logPanel != null)
        {
            logPanel.SetActive(true);
        }

        yield return new WaitForSeconds(logPanelDisplayTime);

        if (logPanel != null)
        {
            logPanel.SetActive(false);
        }
    }

    #endregion

    #region VISUAL EFFECTS
    private IEnumerator FlashColor()
    {
        if (dummyRenderer != null)
        {
            dummyRenderer.material.color = hitColor;

            yield return new WaitForSeconds(hitColorDuration);

            dummyRenderer.material.color = baseColor;
        }
    }
    #endregion

    #region CLEANUP
    void OnDestroy()
    {
        if (dummyRenderer != null && dummyRenderer.material != null)
        {
            Destroy(dummyRenderer.material);
        }
    }
    #endregion
}