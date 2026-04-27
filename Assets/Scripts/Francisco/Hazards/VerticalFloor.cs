using UnityEngine;

public class VerticalFloor : FloorBase
{
    #region Inspector Fields

    [Header("Vertical Settings")]
    [SerializeField] private float expandedScaleMultiplier = 3f;

    #endregion

    #region FloorBase Overrides

    protected override void SetTriggeredScale()
    {
        triggeredScale = new Vector3(
            defaultScale.x,
            defaultScale.y * expandedScaleMultiplier,
            defaultScale.z);
    }

    public override string GizmoAxisLabel => "Y (up)";

    public override Vector3 GetTriggeredScale()
    {
        return new Vector3(
            defaultScale.x,
            defaultScale.y * expandedScaleMultiplier,
            defaultScale.z);
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Vector3 top = transform.position + transform.up * (transform.localScale.y * 0.5f);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.DrawLine(top, top + transform.up * 0.5f);
        DrawArrowHead(top + transform.up * 0.5f, transform.up, 0.12f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + transform.up * (transform.localScale.y + 0.3f),
            "Expands UP");
#endif
    }

    private void DrawArrowHead(Vector3 tip, Vector3 dir, float size)
    {
        Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized * size;
        if (right == Vector3.zero) right = Vector3.Cross(dir, Vector3.up).normalized * size;
        Gizmos.DrawLine(tip, tip - dir * size + right);
        Gizmos.DrawLine(tip, tip - dir * size - right);
    }

    #endregion
}