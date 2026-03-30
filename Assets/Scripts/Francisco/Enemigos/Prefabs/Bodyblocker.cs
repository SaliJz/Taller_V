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
    [SerializeField] private float pushForce = 12f;
    [SerializeField] private float upwardBias = 2f;

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

            Rigidbody playerRb = hit.GetComponentInParent<Rigidbody>();
            if (playerRb == null) continue;

            Vector3 pushDirection = hit.transform.position - transform.position;
            pushDirection.y = 0f;

            if (pushDirection.sqrMagnitude < 0.001f)
                pushDirection = transform.right;

            pushDirection.Normalize();
            pushDirection.y = upwardBias;

            playerRb.AddForce(pushDirection * pushForce, ForceMode.VelocityChange);

            Log($"Pushed player away from top. Direction: {pushDirection}", 1);
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
            case 1: Debug.Log($"[OveruseBodyBlocker] {message}"); break;
            case 2: Debug.LogWarning($"[OveruseBodyBlocker] {message}"); break;
            case 3: Debug.LogError($"[OveruseBodyBlocker] {message}"); break;
        }
    }

    #endregion
}
