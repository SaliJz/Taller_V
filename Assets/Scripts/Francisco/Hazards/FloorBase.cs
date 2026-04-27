using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(NavMeshObstacle))]
public abstract class FloorBase : MonoBehaviour
{
    #region Enums

    public enum FloorState { Default, Transitioning, Triggered }
    public enum MoveMode { Scale, Translate }

    #endregion

    #region Inspector Fields — Shared

    [Header("Movement")]
    [SerializeField] protected MoveMode moveMode = MoveMode.Scale;
    [SerializeField] protected float moveSpeed = 5f;

    [Header("Behavior")]
    [SerializeField] private float initialDelay = 1f;
    [SerializeField][Range(0f, 1f)] private float triggerProbability = 0.5f;
    [SerializeField] private float rerollInterval = 5f;
    [SerializeField] private float triggerDuration = 3f;

    [Header("Translate Mode — Limits")]
    [SerializeField] protected Transform defaultPoint;
    [SerializeField] protected Transform triggeredPoint;

    [Header("NavMesh")]
    [SerializeField] private LayerMask agentLayers;
    [SerializeField] private float navMeshCarveDelay = 0.4f;

    [Header("Emissive")]
    [SerializeField] private Renderer[] emissiveRenderers;
    [ColorUsage(true, true)][SerializeField] private Color colorDefault = Color.green;
    [ColorUsage(true, true)][SerializeField] private Color colorTriggered = Color.red;
    [ColorUsage(true, true)][SerializeField] private Color colorTransitioning = Color.yellow;

    #endregion

    #region State

    protected FloorState currentState = FloorState.Default;
    protected BoxCollider boxCollider;
    protected NavMeshObstacle navObstacle;
    protected Vector3 defaultScale;
    protected Vector3 triggeredScale;

    private List<Material> emissiveMaterials = new List<Material>();
    private Coroutine rerollCoroutine;
    private Coroutine transitionCoroutine;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        navObstacle = GetComponent<NavMeshObstacle>();

        navObstacle.carving = false;
        navObstacle.shape = NavMeshObstacleShape.Box;
        SyncObstacleToCollider();

        defaultScale = transform.localScale;
        SetTriggeredScale();

        foreach (var r in emissiveRenderers)
            if (r) emissiveMaterials.Add(r.material);
    }

    protected virtual void Start()
    {
        ApplyStateInstant(FloorState.Default);
        rerollCoroutine = StartCoroutine(RerollLoop());
    }

    #endregion

    #region Abstract Interface

    protected abstract void SetTriggeredScale();
    public abstract string GizmoAxisLabel { get; }
    public abstract Vector3 GetTriggeredScale();

    #endregion

    #region State Machine

    private IEnumerator RerollLoop()
    {
        yield return new WaitForSeconds(initialDelay);
        while (true)
        {
            RollTrigger();
            yield return new WaitForSeconds(rerollInterval);
        }
    }

    private void RollTrigger()
    {
        if (currentState == FloorState.Transitioning) return;

        bool shouldTrigger = Random.value < triggerProbability;

        if (shouldTrigger && currentState == FloorState.Default)
            StartTransition(FloorState.Triggered);
        else if (!shouldTrigger && currentState == FloorState.Triggered)
            StartTransition(FloorState.Default);
    }

    private void StartTransition(FloorState target)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionRoutine(target));
    }

    private IEnumerator TransitionRoutine(FloorState target)
    {
        currentState = FloorState.Transitioning;
        UpdateEmissive(colorTransitioning);

        yield return new WaitForSeconds(navMeshCarveDelay);

        navObstacle.carving = true;
        SyncObstacleToCollider();

        yield return StartCoroutine(MoveToState(target));

        currentState = target;
        navObstacle.carving = (target == FloorState.Triggered);
        SyncObstacleToCollider();
        UpdateEmissive(target == FloorState.Default ? colorDefault : colorTriggered);

        if (target == FloorState.Triggered)
        {
            yield return new WaitForSeconds(triggerDuration);
            StartTransition(FloorState.Default);
        }
    }

    private IEnumerator MoveToState(FloorState target)
    {
        if (moveMode == MoveMode.Scale)
        {
            Vector3 targetScale = (target == FloorState.Triggered) ? triggeredScale : defaultScale;

            while (Vector3.Distance(transform.localScale, targetScale) > 0.005f)
            {
                transform.localScale = Vector3.MoveTowards(
                    transform.localScale, targetScale, moveSpeed * Time.deltaTime);
                SyncObstacleToCollider();
                yield return null;
            }
            transform.localScale = targetScale;
        }
        else
        {
            if (defaultPoint == null || triggeredPoint == null) yield break;

            Transform targetTransform = (target == FloorState.Triggered) ? triggeredPoint : defaultPoint;

            while (Vector3.Distance(transform.position, targetTransform.position) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, targetTransform.position, moveSpeed * Time.deltaTime);
                SyncObstacleToCollider();
                yield return null;
            }
            transform.position = targetTransform.position;
        }
    }

    #endregion

    #region NavMesh

    private void SyncObstacleToCollider()
    {
        if (navObstacle == null || boxCollider == null) return;
        navObstacle.center = boxCollider.center;
        navObstacle.size = Vector3.Scale(boxCollider.size, transform.localScale);
    }

    #endregion

    #region Emissive

    private void UpdateEmissive(Color color)
    {
        foreach (var mat in emissiveMaterials)
            if (mat) mat.SetColor("_EmissionColor", color);
    }

    private void ApplyStateInstant(FloorState state)
    {
        Color c = state switch
        {
            FloorState.Triggered => colorTriggered,
            FloorState.Transitioning => colorTransitioning,
            _ => colorDefault
        };
        UpdateEmissive(c);

        if (moveMode == MoveMode.Scale)
        {
            transform.localScale = (state == FloorState.Triggered) ? triggeredScale : defaultScale;
            SyncObstacleToCollider();
        }
    }

    #endregion

    #region Public API

    public void ForceTrigger()
    {
        if (currentState != FloorState.Triggered)
            StartTransition(FloorState.Triggered);
    }

    public void ForceReset()
    {
        if (currentState != FloorState.Default)
            StartTransition(FloorState.Default);
    }

    public FloorState CurrentState => currentState;

    #endregion

    #region Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);
        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

        Vector3 ts = GetTriggeredScale();
        Vector3 ratio = new Vector3(
            defaultScale.x > 0 ? ts.x / defaultScale.x : 1f,
            defaultScale.y > 0 ? ts.y / defaultScale.y : 1f,
            defaultScale.z > 0 ? ts.z / defaultScale.z : 1f);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.18f);
        Gizmos.DrawCube(boxCollider.center, Vector3.Scale(boxCollider.size, ratio));
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(boxCollider.center, Vector3.Scale(boxCollider.size, ratio));

        Gizmos.matrix = Matrix4x4.identity;

        if (moveMode == MoveMode.Translate && defaultPoint != null && triggeredPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(defaultPoint.position, triggeredPoint.position);
            Gizmos.DrawSphere(defaultPoint.position, 0.08f);
            Gizmos.DrawSphere(triggeredPoint.position, 0.08f);
        }
    }

    #endregion
}