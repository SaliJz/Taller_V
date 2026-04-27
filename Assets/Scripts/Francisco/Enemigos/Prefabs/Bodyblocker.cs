using UnityEngine;

[DisallowMultipleComponent]
public class Bodyblocker : MonoBehaviour
{
    #region Inspector Fields

    [Header("Block Zone")]
    [SerializeField] private float checkRadius = 0.6f;
    [SerializeField] private float checkHeightOffset = 0.8f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Push Force")]
    [SerializeField] private float pushForce = 8f;
    [SerializeField] private float upwardBias = 2f;
    [SerializeField] private float pushDuration = 0.15f;

    #endregion

    #region Private State

    private Collider enemyCollider;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        enemyCollider = GetComponent<Collider>();
        if (enemyCollider == null)
            Log("No Collider found on enemy. checkHeightOffset will be used as absolute Y offset.", 2);
    }

    private void FixedUpdate()
    {
        if (playerLayer.value == 0) return;

        Vector3 checkCenter = GetCheckCenter();
        Collider[] hits = Physics.OverlapSphere(checkCenter, checkRadius, playerLayer, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) continue;

            Vector3 pushDir = hit.transform.position - transform.position;
            pushDir.y = 0f;

            if (pushDir.sqrMagnitude < 0.001f)
                pushDir = transform.right;

            pushDir.Normalize();
            pushDir.y = upwardBias;

            playerHealth.ApplyKnockback(pushDir, pushForce, pushDuration);

            Log($"Pushed player. Direction: {pushDir}, Force: {pushForce}, Duration: {pushDuration}", 1);
        }
    }

    #endregion

    #region Helpers

    private Vector3 GetCheckCenter()
    {
        if (enemyCollider != null)
            return new Vector3(transform.position.x, enemyCollider.bounds.max.y, transform.position.z);

        return transform.position + Vector3.up * checkHeightOffset;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(GetCheckCenter(), checkRadius);
    }

    #endregion

    #region Debug

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string message, int level)
    {
        switch (level)
        {
            case 1: Debug.Log($"[Bodyblocker] {message}"); break;
            case 2: Debug.LogWarning($"[Bodyblocker] {message}"); break;
            case 3: Debug.LogError($"[Bodyblocker] {message}"); break;
        }
    }

    #endregion
}