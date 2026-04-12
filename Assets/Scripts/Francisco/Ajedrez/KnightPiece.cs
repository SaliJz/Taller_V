using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KnightPiece : BoardPiece
{
    #region Enums
    public enum AIState { Patrolling, Chasing, Stunned }
    #endregion

    #region Variables
    [Header("Knight Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotationSpeed = 15f;

    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private Vector3 currentMoveStepTarget;
    private float lastSeenTime;
    private Coroutine brainCoroutine;

    private readonly Vector2Int[] knightMoves = new Vector2Int[]
    {
        new Vector2Int(2, 1), new Vector2Int(2, -1),
        new Vector2Int(-2, 1), new Vector2Int(-2, -1),
        new Vector2Int(1, 2), new Vector2Int(1, -2),
        new Vector2Int(-1, 2), new Vector2Int(-1, -2)
    };
    #endregion

    #region Unity Events
    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        AutoConnectToNearestBoard();

        if (boardManager != null)
            brainCoroutine = StartCoroutine(KnightBrain());
    }

    private void Update()
    {
        if (isMoving && !isStunned)
            LookAtTarget(currentMoveStepTarget, rotationSpeed);
    }
    #endregion

    #region Brain Logic
    private IEnumerator KnightBrain()
    {
        yield return new WaitUntil(() => boardManager != null);
        while (true)
        {
            if (isStunned)
            {
                currentState = AIState.Stunned;
                yield return new WaitForSeconds(0.2f);
                continue;
            }
            if (isMoving)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
            bool canSee = CanSeePlayer(playerCoord);
            bool isNearby = IsPlayerNearby(playerTransform);

            if (canSee || isNearby)
            {
                lastSeenTime = Time.time;
                currentState = AIState.Chasing;
            }
            else if (Time.time - lastSeenTime > chaseGracePeriod)
            {
                currentState = AIState.Patrolling;
            }

            if (currentState == AIState.Chasing)
            {
                Vector2Int targetTile = GetBestMoveTowards(playerCoord);
                if (targetTile != currentCoord)
                    yield return StartCoroutine(MoveRoutine(targetTile));
            }
            else
            {
                yield return StartCoroutine(PatrolRoutine());
            }

            yield return new WaitForSeconds(detectionInterval);
        }
    }
    #endregion

    #region Pivot Logic

    private bool TryGetValidPivot(Vector2Int origin, Vector2Int target, out Vector2Int pivot)
    {
        pivot = origin;
        Vector2Int diff = target - origin;

        Vector2Int pivotA = new Vector2Int(origin.x + diff.x, origin.y);
        Vector2Int pivotB = new Vector2Int(origin.x, origin.y + diff.y);

        bool aOk = boardManager.TileExists(pivotA);
        bool bOk = boardManager.TileExists(pivotB);

        if (!aOk && !bOk) return false;
        if (aOk && !bOk) { pivot = pivotA; return true; }
        if (!aOk && bOk) { pivot = pivotB; return true; }

        pivot = (ScorePath(origin, pivotA, target) >= ScorePath(origin, pivotB, target))
                ? pivotA : pivotB;
        return true;
    }

    private int ScorePath(Vector2Int origin, Vector2Int pivot, Vector2Int target)
        => CountSegmentTiles(origin, pivot) + CountSegmentTiles(pivot, target);

    private int CountSegmentTiles(Vector2Int from, Vector2Int to)
    {
        int count = 0;
        Vector2Int dir = new Vector2Int(
            to.x == from.x ? 0 : (int)Mathf.Sign(to.x - from.x),
            to.y == from.y ? 0 : (int)Mathf.Sign(to.y - from.y));

        Vector2Int cur = from;
        for (int i = 0; i <= Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y); i++)
        {
            if (boardManager.TileExists(cur)) count++;
            if (cur == to) break;
            cur += dir;
        }
        return count;
    }

    #endregion

    #region Movement

    private IEnumerator MoveRoutine(Vector2Int targetCoord)
    {
        if (!boardManager.TileExists(targetCoord) || boardManager.IsTileOccupied(targetCoord))
            yield break;

        Vector2Int originCoord = currentCoord;
        Vector3 startPos = transform.position;

        if (!TryGetValidPivot(originCoord, targetCoord, out Vector2Int pivotCoord))
            yield break;

        Vector3 finalPos = boardManager.GetWorldPosFromCoord(targetCoord);
        finalPos.y = startPos.y;

        Vector3 pivotPos = boardManager.GetWorldPosFromCoord(pivotCoord);
        pivotPos.y = startPos.y;

        currentTargetWorldPos = finalPos;

        UpdateBoardPosition(targetCoord);

        isMoving = true;

        currentMoveStepTarget = pivotPos;
        yield return StartCoroutine(MoveToPoint(pivotPos));

        currentMoveStepTarget = finalPos;
        yield return StartCoroutine(MoveToPoint(finalPos));

        isMoving = false;
    }

    private IEnumerator MoveToPoint(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (isStunned) yield break;
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    private List<Vector2Int> GetValidMoves()
    {
        var valid = new List<Vector2Int>();
        foreach (Vector2Int move in knightMoves)
        {
            Vector2Int dest = currentCoord + move;
            if (!boardManager.TileExists(dest) || boardManager.IsTileOccupied(dest))
                continue;
            if (TryGetValidPivot(currentCoord, dest, out _))
                valid.Add(dest);
        }
        return valid;
    }

    private Vector2Int GetBestMoveTowards(Vector2Int targetCoord)
    {
        var best = new List<Vector2Int>();
        float minDist = float.MaxValue;

        foreach (Vector2Int coord in GetValidMoves())
        {
            float dist = Vector2Int.Distance(coord, targetCoord);
            if (dist < minDist) { minDist = dist; best.Clear(); best.Add(coord); }
            else if (Mathf.Abs(dist - minDist) < 0.1f) best.Add(coord);
        }

        return best.Count > 0 ? best[Random.Range(0, best.Count)] : currentCoord;
    }

    private IEnumerator PatrolRoutine()
    {
        List<Vector2Int> possible = GetValidMoves();
        if (possible.Count > 0)
            yield return StartCoroutine(MoveRoutine(possible[Random.Range(0, possible.Count)]));
    }
    #endregion

    #region Overrides & Detection
    public override void OnStunEnd()
    {
        isStunned = false;
        isMoving = false;
        if (brainCoroutine != null) StopCoroutine(brainCoroutine);
        brainCoroutine = StartCoroutine(KnightBrain());
    }

    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        foreach (Vector2Int move in knightMoves)
            if (diff == move) return true;
        return false;
    }
    #endregion

    #region Gizmos Override
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (!showGizmos || boardManager == null) return;

        Gizmos.color = new Color(1, 1, 1, 0.1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Color stateColor = isStunned ? new Color(1, 0.9f, 0, 0.4f) :
                          (currentState == AIState.Chasing ? new Color(1, 0, 0, 0.3f) : new Color(0, 1, 0, 0.3f));

        Vector3 gizmoPos = transform.position + Vector3.up * gizmoHeight;
        Gizmos.color = stateColor;
        Gizmos.DrawCube(gizmoPos, new Vector3(gizmosAnchor, gizmosLenght, gizmosAnchor));

        if (isMoving)
        {
            Gizmos.color = Color.cyan;
            DrawPathArrow(transform.position + Vector3.up * 0.3f, currentTargetWorldPos + Vector3.up * 0.3f);
        }
    }
    #endregion
}