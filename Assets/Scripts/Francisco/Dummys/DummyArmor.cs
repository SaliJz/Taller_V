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

    private AttackDamageType lastAttackDamageType;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentArmorHealth => currentArmorHealth;
    public float CurrentReduction => currentArmorHealth > 0 ? reductionPercentage : 0f;
    public AttackDamageType LastAttackDamageType => lastAttackDamageType;

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

        lastAttackDamageType = damageType;

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
            float damageToArmor = 0f;

            if (damageType == AttackDamageType.Ranged)
            {
                float reductionFactor = (1f - reductionPercentage);
                damageToArmor = damageAmount * reductionFactor;
                hitType = DamageType.Shield;
            }
            else
            {
                damageToArmor = damageAmount;
                hitType = DamageType.Shield;
            }

            currentArmorHealth -= damageToArmor;
            currentArmorHealth = Mathf.Max(0, currentArmorHealth);

            Debug.Log($"[DUMMY H&A] Daño absorbido ({damageType}). Armor restante: {currentArmorHealth:F2}. Armor rota por: {damageToArmor:F2}");
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
        gameObject.SetActive(false);
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
        lastAttackDamageType = AttackDamageType.Ranged;

        gameObject.SetActive(true);
        if (uiController != null) uiController.SetUIActive(DummyLogicType.HealthState, true);

        UpdateHealthUI();
        UpdateArmorUI();
        Debug.Log("[DUMMY H&A] Dummy revivido!");
    }
}