using UnityEngine;

public class CurativeLarva : BaseLarva
{
    #region Settings

    [Header("Healing")]
    public float baseHealAmount = 15f;
    public float healingRadius = 0.75f;
    public float stopDistance = 0.5f;

    #endregion

    #region Private

    private PlayerHealth playerHealth;
    private Transform playerTransform;
    private float calculatedHealAmount;
    private bool hasHealed = false;

    #endregion

    #region Initialization

    public void Initialize(float originalEnemyBaseHealth)
    {
        calculatedHealAmount = baseHealAmount;
    }

    #endregion

    #region Overrides

    protected override void Awake()
    {
        base.Awake();

        playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null) playerTransform = playerHealth.transform;

        if (agent != null) agent.stoppingDistance = stopDistance;
        if (calculatedHealAmount == 0) calculatedHealAmount = baseHealAmount;
    }

    protected override bool HasTarget() => playerTransform != null;

    protected override void SearchForTarget() { }

    protected override void OnChaseUpdate()
    {
        if (hasHealed || playerTransform == null) return;

        if (agent != null && agent.isOnNavMesh)
            agent.SetDestination(playerTransform.position);

        if (Vector3.Distance(transform.position, playerTransform.position) <= healingRadius)
            HealAndDie();
    }

    protected override void OnUpdate()
    {
        if (currentState == LarvaState.Chase)
            OnChaseUpdate();
    }

    protected override void OnDieAction() { }

    #endregion

    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (!hasHealed && other.CompareTag("Player"))
            HealAndDie();
    }

    #endregion

    #region Healing

    private void HealAndDie()
    {
        if (hasHealed) return;
        hasHealed = true;

        playerHealth?.Heal(calculatedHealAmount);

        if (agent != null) agent.enabled = false;
        Destroy(gameObject);
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healingRadius);
    }

    #endregion
}