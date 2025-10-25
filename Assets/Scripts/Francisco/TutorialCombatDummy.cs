using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public enum DamageType { Melee, Shield, Other }

public class TutorialCombatDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 1f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    [Header("Vulnerability")]
    [SerializeField] private DamageType requiredDamageType = DamageType.Melee;

    [Header("Hit Count Logic")]
    [SerializeField] private int requiredHits = 3;
    private int currentHits = 0;

    [Header("Visuals & Cooldown")]
    [SerializeField] private float hitCooldownTime = 0.5f;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f; 
    private Color originalColor;
    private bool canBeHit = true;
    private Coroutine colorChangeCoroutine;

    [Header("Delays")]
    [SerializeField] private float deathDelay = 0.5f;

    [Header("Dialogues")]
    public DialogLine[] FirstHitDialog;
    public DialogLine[] DeathDialog;
    public DialogLine[] DefeatedSequenceDialog;

    public AudioClip infernalSound;
    private AudioSource audioSource;

    private bool isFirstHit = true;
    private bool isDefeated = false;

    private Collider[] colliders;
    private Renderer[] renderers;

    public UnityEvent OnDummyDefeated;

    [Header("Evento de Secuencia")]
    public UnityEvent OnDeathSequenceStart;

    private void Awake()
    {
        currentHealth = maxHealth;
        colliders = GetComponents<Collider>().Where(c => c.enabled).ToArray();
        renderers = GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        if (renderers.Length > 0 && renderers[0].material != null)
        {
            originalColor = renderers[0].material.color;
        }
    }

    public void TakeDamage(float damageAmount, bool isCritical = false)
    {
        TakeDamage(damageAmount, isCritical, DamageType.Melee);
    }

    public void TakeDamage(float damageAmount, bool isCritical, DamageType type)
    {
        if (isDefeated || !canBeHit) return;

        if (requiredDamageType != type)
        {
            Debug.Log($"Dummy ignoró daño de {type}. Requiere: {requiredDamageType}");
            return;
        }

        StartCoroutine(HitCooldown());
        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        colorChangeCoroutine = StartCoroutine(FlashColor());

        if (isFirstHit)
        {
            isFirstHit = false;
            StartCoroutine(ExecuteFirstHitDialogueFlow());
        }

        bool isHitCountDummy = requiredDamageType == DamageType.Melee || requiredDamageType == DamageType.Shield;

        if (isHitCountDummy)
        {
            HandleHitCount(type);
        }
        else
        {
            currentHealth -= damageAmount;

            if (currentHealth <= 0)
            {
                Die();
            }
        }
    }

    private void HandleHitCount(DamageType type)
    {
        currentHits++;
        Debug.Log($"Dummy de Contador golpeado! Tipo: {type}. Hits: {currentHits}/{requiredHits}.");

        if (currentHits >= requiredHits)
        {
            currentHealth = 0;
            Die();
        }
    }

    private IEnumerator HitCooldown()
    {
        canBeHit = false;
        yield return new WaitForSeconds(hitCooldownTime);
        canBeHit = true;
    }

    private IEnumerator FlashColor()
    {
        foreach (var rend in renderers)
        {
            if (rend.material.HasProperty("_Color"))
            {
                rend.material.color = flashColor;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (var rend in renderers)
        {
            if (rend.material.HasProperty("_Color"))
            {
                rend.material.color = originalColor;
            }
        }
        colorChangeCoroutine = null;
    }

    private IEnumerator ExecuteFirstHitDialogueFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no está en la escena.");
            yield break;
        }

        if (FirstHitDialog != null && FirstHitDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(FirstHitDialog);

            while (DialogManager.Instance.IsActive)
            {
                yield return null;
            }
        }
    }

    private void Die()
    {
        if (isDefeated) return;
        isDefeated = true;

        StopAllCoroutines();
        DisableDummyVisualsAndPhysics();

        StartCoroutine(ExecuteDeathDialogueFlowWithDelay());
    }

    private void DisableDummyVisualsAndPhysics()
    {
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

        foreach (var c in colliders)
        {
            c.enabled = false;
        }
    }

    private IEnumerator ExecuteDeathDialogueFlowWithDelay()
    {
        OnDeathSequenceStart?.Invoke();

        if (deathDelay > 0)
        {
            yield return new WaitForSeconds(deathDelay);
        }

        yield return StartCoroutine(ExecuteDeathDialogueFlow());
    }

    private IEnumerator ExecuteDeathDialogueFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no está en la escena.");
            yield break;
        }

        if (DeathDialog != null && DeathDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(DeathDialog);

            while (DialogManager.Instance.IsActive)
            {
                yield return null;
            }
        }

        if (audioSource != null && infernalSound != null)
        {
            audioSource.PlayOneShot(infernalSound);
        }

        if (DefeatedSequenceDialog != null && DefeatedSequenceDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(DefeatedSequenceDialog);

            while (DialogManager.Instance.IsActive)
            {
                yield return null;
            }
        }

        OnDummyDefeated?.Invoke();

        gameObject.SetActive(false);
    }
}