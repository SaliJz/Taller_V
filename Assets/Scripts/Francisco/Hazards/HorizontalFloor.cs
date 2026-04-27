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
    [SerializeField] private ContractionDirection contractionDirection = ContractionDirection.Random;

    #endregion

    #region State

    private Renderer[] childRenderers;
    private bool directionResolved;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        if (visualChild != null)
            childRenderers = visualChild.GetComponentsInChildren<Renderer>();

        ResolveRandomDirection();
    }

    #endregion

    #region FloorBase Overrides

    protected override void SetChildTriggeredScale()
    {
        childTriggeredScale = contractionAxis == ContractionAxis.X
            ? new Vector3(0f, childDefaultScale.y, childDefaultScale.z)
            : new Vector3(childDefaultScale.x, childDefaultScale.y, 0f);
    }

    public override string GizmoAxisLabel =>
        contractionAxis == ContractionAxis.X ? "X (left/right)" : "Z (fwd/back)";

    public override Vector3 GetTriggeredChildScale()
    {
        Vector3 baseScale = (visualChild != null)
            ? visualChild.transform.localScale
            : Vector3.one;

        return contractionAxis == ContractionAxis.X
            ? new Vector3(0f, baseScale.y, baseScale.z)
            : new Vector3(baseScale.x, baseScale.y, 0f);
    }

    protected override void OnTransitionBegin(FloorState target)
    {
        if (target == FloorState.Default)
            SetChildMeshVisible(true);
    }

    protected override void OnTransitionEnd(FloorState target)
    {
        if (target == FloorState.Triggered)
            SetChildMeshVisible(false);
    }

    protected override void OnChildScaleUpdated()
    {
        if (visualChild == null) return;

        bool nearZero = contractionAxis == ContractionAxis.X
            ? visualChild.transform.localScale.x < 0.01f
            : visualChild.transform.localScale.z < 0.01f;

        if (nearZero) SetChildMeshVisible(false);
    }

    #endregion

    #region Mesh Visibility

    private void SetChildMeshVisible(bool visible)
    {
        if (childRenderers == null) return;
        foreach (var r in childRenderers)
            if (r) r.enabled = visible;
    }

    #endregion

    #region Helpers

    private void ResolveRandomDirection()
    {
        if (directionResolved) return;
        if (contractionDirection == ContractionDirection.Random)
            contractionDirection = Random.value < 0.5f
                ? ContractionDirection.Left
                : ContractionDirection.Right;
        directionResolved = true;
    }

    #endregion

    #region Gizmos

    protected override void DrawChildGizmos(BoxCollider col, bool selected, float alpha)
    {
        Vector3 dir = contractionAxis == ContractionAxis.X
            ? (contractionDirection == ContractionDirection.Left ? -transform.right : transform.right)
            : (contractionDirection == ContractionDirection.Left ? -transform.forward : transform.forward);

        float halfExtent = contractionAxis == ContractionAxis.X
            ? col.size.x * 0.5f
            : col.size.z * 0.5f;

        Vector3 side = transform.position
                     + transform.rotation * (col.center + dir * (halfExtent + 0.1f));

        Gizmos.color = new Color(1f, 0.6f, 0f, alpha);
        Gizmos.DrawLine(transform.position, side);
        DrawArrowHead(side, transform.rotation * dir, 0.12f);

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