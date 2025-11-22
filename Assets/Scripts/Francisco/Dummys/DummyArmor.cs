using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System;

public class DummyArmor : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Super Armor Settings")]
    [SerializeField] private float maxArmorHealth = 50f;
    private float currentArmorHealth;
    [SerializeField, Range(0f, 1f)] private float reductionPercentage = 0.5f;

    [Header("Damage Cooldown")]
    [SerializeField] private float damageCooldownTime = 0.2f;
    private float nextDamageTime = 0f;

    [Header("References")]
    [SerializeField] private DummyUIController uiController;
    [SerializeField] private float maxArmorValueForUI = 1.0f;
    [SerializeField] private Animator animator;
    [SerializeField] private string hitTriggerName = "OnHit";

    [Header("Rotation Logic")]
    [SerializeField] private Transform transformToRotate;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float rotationYOffset = 0f;
    private Transform playerTransform;
    private Coroutine rotationCoroutine;

    [Header("Events")]
    public UnityEvent OnDummyDefeated;

    public event Action<DamageType, bool> OnHitByPlayer;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentArmorHealth => currentArmorHealth;
    public float CurrentReduction => currentArmorHealth > 0 ? reductionPercentage : 0f;

    void Awake()
    {
        currentHealth = maxHealth;
        currentArmorHealth = maxArmorHealth;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (transformToRotate == null)
        {
            transformToRotate = transform;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        UpdateHealthUI();
        UpdateArmorUI();
    }

    public void TakeDamage(float damageAmount, bool isCritical, AttackDamageType damageType)
    {
        if (Time.time < nextDamageTime) return;
        if (currentHealth <= 0) return;

        nextDamageTime = Time.time + damageCooldownTime;

        if (playerTransform != null)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateTowardsPlayer());
        }

        float finalDamage = damageAmount;
        DamageType hitType = DamageType.Melee;

        if (currentArmorHealth > 0f)
        {
            finalDamage = 0f;

            if (damageType == AttackDamageType.Melee)
            {
                float damageToArmor = damageAmount;
                currentArmorHealth -= damageToArmor;
                currentArmorHealth = Mathf.Max(0, currentArmorHealth);
                hitType = DamageType.Shield;
                Debug.Log($"[DUMMY H&A] Daño absorbido (Melee). Armor restante: {currentArmorHealth:F2}. Armor rota por: {damageToArmor:F2}");
            }
            else if (damageType == AttackDamageType.Ranged)
            {
                float reductionFactor = (1f - reductionPercentage);
                float damageToArmor = damageAmount * reductionFactor;

                currentArmorHealth -= damageToArmor;
                currentArmorHealth = Mathf.Max(0, currentArmorHealth);
                hitType = DamageType.Shield;
                Debug.Log($"[DUMMY H&A] Daño absorbido (Ranged, {reductionPercentage * 100:F0}% reducción). Daño aplicado a Armadura: {damageToArmor:F2}. Armor restante: {currentArmorHealth:F2}");
            }
        }
        else
        {
            finalDamage = damageAmount;
            hitType = DamageType.Melee;
            Debug.Log("[DUMMY H&A] Super Armor roto. Daño completo.");
        }

        if (animator != null)
        {
            animator.SetTrigger(hitTriggerName);
        }

        currentHealth -= finalDamage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"[DUMMY H&A] Vida actual: {currentHealth}/{maxHealth}");

        bool isFatal = currentHealth <= 0;
        OnHitByPlayer?.Invoke(hitType, isFatal);

        UpdateHealthUI();
        UpdateArmorUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator RotateTowardsPlayer()
    {
        if (transformToRotate == null || playerTransform == null) yield break;

        Vector3 direction = (playerTransform.position - transformToRotate.position).normalized;
        direction.y = 0;

        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation *= Quaternion.Euler(0, rotationYOffset, 0);

        float elapsedTime = 0f;
        float maxRotationTime = 2f;

        while (Quaternion.Angle(transformToRotate.rotation, targetRotation) > 0.1f && elapsedTime < maxRotationTime)
        {
            transformToRotate.rotation = Quaternion.Slerp(
                transformToRotate.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transformToRotate.rotation = targetRotation;
        rotationCoroutine = null;
    }

    private void Die()
    {
        Debug.Log("[DUMMY H&A] Dummy Defeated!");

        OnDummyDefeated.Invoke();

        if (uiController != null) uiController.SetUIActive(DummyLogicType.HealthState, false);
        Destroy(gameObject, 0.1f);
    }

    private void UpdateHealthUI()
    {
        if (uiController != null)
        {
            float healthRatio = currentHealth / maxHealth;
            uiController.UpdateHealthBar(healthRatio, null);
        }
    }

    private void UpdateArmorUI()
    {
        if (uiController != null)
        {
            float armorRatio = maxArmorHealth > 0 ? currentArmorHealth / maxArmorHealth : 0f;

            float uiValue = armorRatio * maxArmorValueForUI;

            uiController.UpdateArmorBar(uiValue);
        }
    }

    public void ResetArmorState()
    {
        currentHealth = maxHealth;
        currentArmorHealth = maxArmorHealth;
        UpdateHealthUI();
        UpdateArmorUI();
        Debug.Log("[DUMMY H&A] Dummy revivido!");
    }
}