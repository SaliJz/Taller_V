using UnityEngine;
using System.Collections;

public class StaticEnemyLevel3 : StaticEnemyBase
{
    [Header("Static Level 3 - Reactive Evasion")]
    [SerializeField] private float evasionCooldown = 6f;
    [SerializeField] private float evasionTeleportDistance = 8f;

    [Header("Static Level 3 - Stress Residue")]
    [SerializeField] private GameObject stressResiduePrefab;
    [SerializeField] private float residueDuration = 4f;
    [SerializeField] private float residueDPS = 2f;
    [SerializeField] private float residueRadius = 2f;

    [Header("Static Level 3 - VFX Screen")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Color evasionReadyBorderColor = Color.red;
    [SerializeField] private Color evasionCooldownBorderColor = Color.gray;

    [Header("Static Level 3 - SFX")]
    [SerializeField] private AudioClip evasionSFX;
    [SerializeField] private AudioClip retaliatoryShootSFX;

    private static readonly int ShaderBorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int ShaderShowIconId = Shader.PropertyToID("_ShowIcon");
    private static readonly int ShaderIconTypeId = Shader.PropertyToID("_IconType");

    private const int ICON_EYE = 0;
    private const int ICON_NO_SIGNAL = 1;

    private bool evasionReady = true;
    private bool evasionOnCooldown = false;
    private Coroutine evasionCooldownRoutine;
    private Coroutine screenFeedbackRoutine;

    protected override void InitializedEnemy()
    {
        base.InitializedEnemy();
        UpdateEvasionFeedback();
    }

    protected override void OnBeforeTeleport(Vector3 fromPosition)
    {
        if (stressResiduePrefab == null) return;

        GameObject residue = Instantiate(stressResiduePrefab, fromPosition, Quaternion.identity);
        residue.GetComponent<StaticStressResidue>()?.Initialize(residueDuration, residueDPS, residueRadius);
    }

    protected override void HandleDamageTaken()
    {
        base.HandleDamageTaken();

        if (isDead || !evasionReady || evasionOnCooldown) return;

        if (enemyHealth != null && enemyHealth.LastDamageType == AttackDamageType.Melee)
        {
            StartCoroutine(ReactiveEvasionRoutine());
        }
    }

    private IEnumerator ReactiveEvasionRoutine()
    {
        evasionReady = false;
        evasionOnCooldown = true;

        if (audioSource != null && evasionSFX != null)
        {
            audioSource.PlayOneShot(evasionSFX);
        }

        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        Vector3 escapeDir = (transform.position - playerTransform.position).normalized;
        Vector3 evasionTarget = transform.position + escapeDir * evasionTeleportDistance;

        yield return StartCoroutine(TeleportToPositionRoutine(evasionTarget));

        yield return new WaitForSeconds(0.1f);
        if (!isDead)
        {
            ForceFacePlayer();
            if (audioSource != null && retaliatoryShootSFX != null)
            {
                audioSource.PlayOneShot(retaliatoryShootSFX);
            }
            ExecuteProjectileSpawn();
        }

        ChangeState(currentState);

        if (evasionCooldownRoutine != null) StopCoroutine(evasionCooldownRoutine);
        evasionCooldownRoutine = StartCoroutine(EvasionCooldownRoutine());
    }

    private IEnumerator EvasionCooldownRoutine()
    {
        UpdateEvasionFeedback();
        yield return new WaitForSeconds(evasionCooldown);
        evasionOnCooldown = false;
        evasionReady = true;
        UpdateEvasionFeedback();
        evasionCooldownRoutine = null;
    }

    private void UpdateEvasionFeedback()
    {
        if (screenFeedbackRoutine != null) StopCoroutine(screenFeedbackRoutine);
        screenFeedbackRoutine = StartCoroutine(EvasionFeedbackRoutine());
    }

    private IEnumerator EvasionFeedbackRoutine()
    {
        if (screenRenderer == null) yield break;

        Material mat = screenRenderer.material;

        if (evasionReady && !evasionOnCooldown)
        {
            mat.SetColor(ShaderBorderColorId, evasionReadyBorderColor);
            mat.SetFloat(ShaderIconTypeId, ICON_EYE);
        }
        else
        {
            mat.SetColor(ShaderBorderColorId, evasionCooldownBorderColor);
            mat.SetFloat(ShaderIconTypeId, ICON_NO_SIGNAL);
        }

        mat.SetFloat(ShaderShowIconId, 1f);
        yield return null;
        screenFeedbackRoutine = null;
    }

    protected override void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        StaticProjectileLevel3 projectile = projectileObj.GetComponent<StaticProjectileLevel3>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "STATIC";

            projectile.Initialize(projectileSpeed, calculatedDamage, selectedWord);
        }
    }
}