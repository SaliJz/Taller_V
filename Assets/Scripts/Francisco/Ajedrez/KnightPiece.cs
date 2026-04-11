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
        if (boardManager == null) boardManager = FindAnyObjectByType<BoardManager>();
        if (playerTransform == null) playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (boardManager != null)
        {
            Vector2Int spawnCoord = boardManager.WorldPosToCoord(transform.position);
            boardManager.ConnectPiece(this, spawnCoord);
            brainCoroutine = StartCoroutine(KnightBrain());
        }
    }

    private void Update()
    {
        if (isMoving && !isStunned)
        {
            LookAtTarget(currentMoveStepTarget, rotationSpeed);
        }
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
                if (targetTile != currentCoord) yield return StartCoroutine(MoveRoutine(targetTile));
            }
            else yield return StartCoroutine(PatrolRoutine());

            yield return new WaitForSeconds(detectionInterval);
        }
    }
    #endregion

    #region Movement Skills
    private IEnumerator MoveRoutine(Vector2Int targetCoord)
    {
        if (!boardManager.TileExists(targetCoord) || boardManager.IsTileOccupied(targetCoord)) yield break;
        isMoving = true;

        Vector3 finalPos = boardManager.GetWorldPosFromCoord(targetCoord);
        finalPos.y = transform.position.y;

        Vector2Int diff = targetCoord - currentCoord;
        Vector2Int pivotCoord = (Mathf.Abs(diff.x) > Mathf.Abs(diff.y)) ?
            new Vector2Int(currentCoord.x + diff.x, currentCoord.y) :
            new Vector2Int(currentCoord.x, currentCoord.y + diff.y);

        Vector3 pivotPos = boardManager.GetWorldPosFromCoord(pivotCoord);
        pivotPos.y = transform.position.y;

        currentTargetWorldPos = finalPos;
        UpdateBoardPosition(targetCoord);

        currentMoveStepTarget = pivotPos;
        yield return StartCoroutine(MoveToPoint(pivotPos));

        currentMoveStepTarget = finalPos;
        yield return StartCoroutine(MoveToPoint(finalPos));

        isMoving = false;
    }

    private IEnumerator MoveToPoint(Vector3 targetPoint)
    {
        while (Vector3.Distance(transform.position, targetPoint) > 0.05f)
        {
            if (isStunned) yield break;
            transform.position = Vector3.MoveTowards(transform.position, targetPoint, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPoint;
    }

    private Vector2Int GetBestMoveTowards(Vector2Int targetCoord)
    {
        List<Vector2Int> validMoves = new List<Vector2Int>();
        float minDistance = float.MaxValue;
        foreach (Vector2Int move in knightMoves)
        {
            Vector2Int testCoord = currentCoord + move;
            if (boardManager.TileExists(testCoord) && !boardManager.IsTileOccupied(testCoord))
            {
                float dist = Vector2Int.Distance(testCoord, targetCoord);
                if (dist < minDistance) { minDistance = dist; validMoves.Clear(); validMoves.Add(testCoord); }
                else if (Mathf.Abs(dist - minDistance) < 0.1f) validMoves.Add(testCoord);
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : currentCoord;
    }

    private IEnumerator PatrolRoutine()
    {
        List<Vector2Int> possibleMoves = new List<Vector2Int>();
        foreach (Vector2Int move in knightMoves)
        {
            Vector2Int testCoord = currentCoord + move;
            if (boardManager.TileExists(testCoord) && !boardManager.IsTileOccupied(testCoord))
                possibleMoves.Add(testCoord);
        }
        if (possibleMoves.Count > 0)
            yield return StartCoroutine(MoveRoutine(possibleMoves[Random.Range(0, possibleMoves.Count)]));
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
        foreach (Vector2Int move in knightMoves) if (diff == move) return true;
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