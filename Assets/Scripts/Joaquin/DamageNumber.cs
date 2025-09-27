using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageNumber : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = Color.red;

    private CanvasGroup canvasGroup;
    private Vector3 velocity;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(float damage, bool isCritical)
    {
        if (damageText != null)
        {
            damageText.text = ((int)damage).ToString();
            damageText.color = isCritical ? criticalColor : normalColor;
            transform.localScale = isCritical ? Vector3.one * 1.5f : Vector3.one;
        }

        velocity = new Vector3(Random.Range(-1f, 1f), floatSpeed, 0); 
        StartCoroutine(AnimateAndDeactivate());
    }

    public void Deactivate()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private IEnumerator AnimateAndDeactivate()
    {
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            transform.position += velocity * Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / (lifetime / fadeSpeed));

            elapsed += Time.deltaTime;
            yield return null;
        }

        Deactivate();
    }
}