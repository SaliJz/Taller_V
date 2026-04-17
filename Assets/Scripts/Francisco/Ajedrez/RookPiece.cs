using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ChessPieceAttack))]
public class RookPiece : BoardPiece
{
    #region Enums
    public enum AIState { Patrolling, Alerted, Committed, Moving }
    #endregion

    #region Variables
    [Header("Rook Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float patrolMoveSpeed = 7f;
    [SerializeField] private float attackMoveSpeed = 18f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private int patrolStepDist = 3;
    [SerializeField] private float attackWindupTime = 0.8f;

    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private Vector2Int committedTargetCoord;
    private Coroutine brainCoroutine;
    private ChessPieceAttack attackComponent;

    private readonly Vector2Int[] moveDirs = new Vector2Int[]
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };
    #endregion

    #region Unity Events
    private void Start()
    {
        attackComponent = GetComponent<ChessPieceAttack>();
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        AutoConnectToNearestBoard();

        if (boardManager != null)
            brainCoroutine = StartCoroutine(RookBrain());
    }

    private void Update()
    {
        if (!isMoving || isStunned) return;
        LookAtTarget(currentTargetWorldPos, rotationSpeed);
    }
    #endregion

    #region Brain
    private IEnumerator RookBrain()
    {
        yield return new WaitUntil(() => boardManager != null);

        while (true)
        {
            if (isStunned) { yield return new WaitForSeconds(0.2f); continue; }
            if (isMoving) { yield return new WaitForSeconds(0.1f); continue; }

            Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
            bool playerIsOnBoard = boardManager.TileExists(playerCoord);

            if (!playerIsOnBoard)
            {
                currentState = AIState.Patrolling;
                yield return StartCoroutine(PatrolRoutine());
                continue;
            }

            bool canAttack = playerIsOnBoard && CanSeePlayer(playerCoord);
            bool isNearby = playerIsOnBoard && IsPlayerNearby(playerTransform);

            switch (currentState)
            {
                case AIState.Patrolling:
                    if (canAttack)
                    {
                        committedTargetCoord = playerCoord;
                        currentState = AIState.Committed;
                        yield return StartCoroutine(AttackWindup());
                    }
                    else if (isNearby) currentState = AIState.Alerted;
                    else yield return StartCoroutine(PatrolRoutine());
                    break;

                case AIState.Alerted:
                    if (canAttack)
                    {
                        committedTargetCoord = playerCoord;
                        currentState = AIState.Committed;
                        yield return StartCoroutine(AttackWindup());
                    }
                    else if (!isNearby) currentState = AIState.Patrolling;
                    else
                    {
                        Vector2Int approach = GetBestMoveTowards(playerCoord);
                        if (approach != currentCoord)
                            yield return StartCoroutine(MoveRoutine(approach, patrolMoveSpeed));
                        else
                            yield return new WaitForSeconds(detectionInterval);
                    }
                    break;

                case AIState.Committed:
                    currentState = isNearby ? AIState.Alerted : AIState.Patrolling;
                    break;
            }

            yield return new WaitForSeconds(detectionInterval * 0.5f);
        }
    }
    #endregion

    #region Attack Flow
    private IEnumerator AttackWindup()
    {
        ShowPathLine(currentCoord, committedTargetCoord);
        float elapsed = 0f;
        while (elapsed < attackWindupTime)
        {
            if (isStunned) { HideTrail(); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentState = AIState.Moving;
        yield return StartCoroutine(MoveRoutine(committedTargetCoord, attackMoveSpeed));
        HideTrail();
        attackComponent.ForceHitCheck();
        currentState = AIState.Patrolling;
    }
    #endregion

    #region Trail
    private void ShowPathLine(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (diff.x != 0 && diff.y != 0) return;
        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        if (diff.x == 0) dir.x = 0;
        if (diff.y == 0) dir.y = 0;

        var points = new List<Vector3>();
        Vector2Int cur = from;
        while (cur != to)
        {
            points.Add(boardManager.GetWorldPosFromCoord(cur) + Vector3.up * trailHeight);
            cur += dir;
        }
        points.Add(boardManager.GetWorldPosFromCoord(to) + Vector3.up * trailHeight);
        SetTrailPoints(points.ToArray());
    }
    #endregion

    #region Movement
    private IEnumerator MoveRoutine(Vector2Int targetCoord, float speed)
    {
        if (!boardManager.TileExists(targetCoord) || boardManager.IsTileOccupied(targetCoord))
        {
            yield break;
        }

        isMoving = true;
        currentTargetWorldPos = boardManager.GetWorldPosFromCoord(targetCoord);
        currentTargetWorldPos.y = transform.position.y;
        UpdateBoardPosition(targetCoord);

        while (Vector3.Distance(transform.position, currentTargetWorldPos) > 0.05f)
        {
            if (isStunned) { isMoving = false; yield break; }
            transform.position = Vector3.MoveTowards(transform.position, currentTargetWorldPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = currentTargetWorldPos;
        isMoving = false;
    }

    private IEnumerator PatrolRoutine()
    {
        Vector2Int dir = moveDirs[Random.Range(0, moveDirs.Length)];
        Vector2Int target = currentCoord;
        for (int i = 1; i <= patrolStepDist; i++)
        {
            Vector2Int next = currentCoord + (dir * i);
            if (boardManager.TileExists(next) && !boardManager.IsTileOccupied(next)) target = next;
            else break;
        }
        if (target != currentCoord) yield return StartCoroutine(MoveRoutine(target, patrolMoveSpeed));
    }
    #endregion

    #region Detection
    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        if (diff.x != 0 && diff.y != 0) return false;
        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        if (diff.x == 0) dir.x = 0;
        if (diff.y == 0) dir.y = 0;

        Vector2Int check = currentCoord + dir;
        while (check != playerCoord)
        {
            if (!boardManager.TileExists(check) || boardManager.IsTileOccupied(check)) return false;
            check += dir;
        }
        return true;
    }

    private Vector2Int GetBestMoveTowards(Vector2Int target)
    {
        var candidates = new List<Vector2Int>();
        float minDist = float.MaxValue;
        foreach (var dir in moveDirs)
        {
            for (int i = 1; i <= 5; i++)
            {
                Vector2Int test = currentCoord + (dir * i);
                if (!boardManager.TileExists(test) || boardManager.IsTileOccupied(test)) break;
                float dist = Vector2Int.Distance(test, target);
                if (test.x == target.x || test.y == target.y) dist -= 10f;
                if (dist < minDist) { minDist = dist; candidates.Clear(); candidates.Add(test); }
                else if (Mathf.Abs(dist - minDist) < 0.1f) candidates.Add(test);
            }
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : currentCoord;
    }
    #endregion

    #region Overrides
    public override void OnStunEnd()
    {
        isStunned = false;
        isMoving = false;
        HideTrail();
        if (brainCoroutine != null) StopCoroutine(brainCoroutine);
        brainCoroutine = StartCoroutine(RookBrain());
    }
    #endregion

    #region Gizmos
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (!showGizmos || boardManager == null) return;
        Gizmos.color = new Color(1, 1, 1, 0.1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Color stateColor = isStunned ? new Color(1f, 0.9f, 0f, 0.4f) :
            currentState == AIState.Moving ? new Color(1f, 0f, 0f, 0.6f) :
            currentState == AIState.Committed ? new Color(1f, 0.4f, 0f, 0.5f) :
            currentState == AIState.Alerted ? new Color(1f, 0.8f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.3f);
        Gizmos.color = stateColor;
        Gizmos.DrawCube(transform.position + Vector3.up * gizmoHeight, new Vector3(gizmosAnchor, gizmosLenght, gizmosAnchor));
    }
    #endregion
}