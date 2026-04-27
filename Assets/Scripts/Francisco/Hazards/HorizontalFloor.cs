using UnityEngine;

public class HorizontalFloor : FloorBase
{
    #region Enums

    public enum ContractionAxis { X, Z }
    public enum ContractionDirection { Random, Left, Right }

    #endregion

    #region Inspector Fields

    [Header("Horizontal Settings")]
    [SerializeField] private ContractionAxis contractionAxis = ContractionAxis.X;
    [SerializeField][Range(0.01f, 0.99f)] private float contractedScaleMultiplier = 0.1f;
    [SerializeField] private ContractionDirection contractionDirection = ContractionDirection.Random;

    #endregion

    #region FloorBase Overrides

    protected override void Awake()
    {
        base.Awake();
        ResolveRandomDirection();
    }

    protected override void SetTriggeredScale()
    {
        triggeredScale = contractionAxis == ContractionAxis.X
            ? new Vector3(defaultScale.x * contractedScaleMultiplier, defaultScale.y, defaultScale.z)
            : new Vector3(defaultScale.x, defaultScale.y, defaultScale.z * contractedScaleMultiplier);
    }

    public override string GizmoAxisLabel =>
        contractionAxis == ContractionAxis.X ? "X (left/right)" : "Z (fwd/back)";

    public override Vector3 GetTriggeredScale()
    {
        return contractionAxis == ContractionAxis.X
            ? new Vector3(defaultScale.x * contractedScaleMultiplier, defaultScale.y, defaultScale.z)
            : new Vector3(defaultScale.x, defaultScale.y, defaultScale.z * contractedScaleMultiplier);
    }

    #endregion

    #region Helpers

    private void ResolveRandomDirection()
    {
        if (contractionDirection == ContractionDirection.Random)
            contractionDirection = Random.value < 0.5f
                ? ContractionDirection.Left
                : ContractionDirection.Right;
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Vector3 dir = contractionAxis == ContractionAxis.X
            ? (contractionDirection == ContractionDirection.Left ? -transform.right : transform.right)
            : (contractionDirection == ContractionDirection.Left ? -transform.forward : transform.forward);

        Vector3 side = transform.position + dir * (transform.localScale.x * 0.5f + 0.1f);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.DrawLine(transform.position, side);
        DrawArrowHead(side, dir, 0.12f);

#if UNITY_EDITOR
        string label = contractionAxis == ContractionAxis.X
            ? (contractionDirection == ContractionDirection.Left ? "Contracts LEFT" : "Contracts RIGHT")
            : (contractionDirection == ContractionDirection.Left ? "Contracts BACK" : "Contracts FWD");

        UnityEditor.Handles.Label(side + Vector3.up * 0.2f, label);
#endif
    }

    private void DrawArrowHead(Vector3 tip, Vector3 dir, float size)
    {
        Vector3 right = Vector3.Cross(dir, Vector3.up).normalized * size;
        if (right == Vector3.zero) right = Vector3.Cross(dir, Vector3.forward).normalized * size;
        Gizmos.DrawLine(tip, tip - dir * size + right);
        Gizmos.DrawLine(tip, tip - dir * size - right);
    }

    #endregion
}