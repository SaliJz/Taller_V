using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ChessPieceAttack))]
public class KnightPiece : BoardPiece
{
    #region Enums

    public enum AIState { Patrolling, Alerted, Committed, Moving }

    #endregion

    #region Variables

    [Header("Knight Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float patrolMoveSpeed = 10f;
    [SerializeField] private float attackMoveSpeed = 16f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float attackWindupTime = 1f;

    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private Vector3 currentMoveStepTarget;
    private Vector2Int committedTargetCoord;
    private Coroutine brainCoroutine;
    private ChessPieceAttack attackComponent;

    private readonly Vector2Int[] knightMoves = new Vector2Int[]
    {
        new Vector2Int( 2,  1), new Vector2Int( 2, -1),
        new Vector2Int(-2,  1), new Vector2Int(-2, -1),
        new Vector2Int( 1,  2), new Vector2Int( 1, -2),
        new Vector2Int(-1,  2), new Vector2Int(-1, -2)
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
            brainCoroutine = StartCoroutine(KnightBrain());
    }

    private void Update()
    {
        if (isMoving && !isStunned)
            LookAtTarget(currentMoveStepTarget, rotationSpeed);
    }

    #endregion

    #region Brain

    private IEnumerator KnightBrain()
    {
        yield return new WaitUntil(() => boardManager != null);

        while (true)
        {
            if (isStunned) { yield return new WaitForSeconds(0.2f); continue; }
            if (isMoving) { yield return new WaitForSeconds(0.1f); continue; }

            Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
            bool canAttack = CanSeePlayer(playerCoord);
            bool isNearby = IsPlayerNearby(playerTransform);

            switch (currentState)
            {
                case AIState.Patrolling:
                    if (canAttack)
                    {
                        committedTargetCoord = playerCoord;
                        currentState = AIState.Committed;
                        yield return StartCoroutine(AttackWindup());
                    }
                    else
                    {
                        if (isNearby) currentState = AIState.Alerted;
                        else yield return StartCoroutine(PatrolRoutine());
                    }
                    break;

                case AIState.Alerted:
                    if (canAttack)
                    {
                        committedTargetCoord = playerCoord;
                        currentState = AIState.Committed;
                        yield return StartCoroutine(AttackWindup());
                    }
                    else if (!isNearby)
                    {
                        currentState = AIState.Patrolling;
                    }
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

                case AIState.Moving:
                    break;
            }

            yield return new WaitForSeconds(detectionInterval * 0.5f);
        }
    }

    #endregion

    #region Attack Flow

    private IEnumerator AttackWindup()
    {
        ShowPathLine(committedTargetCoord);

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

        currentState = IsPlayerNearby(playerTransform) ? AIState.Alerted : AIState.Patrolling;
    }

    #endregion

    #region Trail / Path Line

    private void ShowPathLine(Vector2Int targetCoord)
    {
        if (!TryGetValidPivot(currentCoord, targetCoord, out Vector2Int pivotCoord)) return;

        Vector3 startPos = boardManager.GetWorldPosFromCoord(currentCoord) + Vector3.up * trailHeight;
        Vector3 pivotPos = boardManager.GetWorldPosFromCoord(pivotCoord) + Vector3.up * trailHeight;
        Vector3 endPos = boardManager.GetWorldPosFromCoord(targetCoord) + Vector3.up * trailHeight;

        SetTrailPoints(new Vector3[] { startPos, pivotPos, endPos });
    }

    #endregion

    #region Movement

    private IEnumerator MoveRoutine(Vector2Int targetCoord, float speed)
    {
        if (!boardManager.TileExists(targetCoord) || boardManager.IsTileOccupied(targetCoord))
        {
            currentState = IsPlayerNearby(playerTransform) ? AIState.Alerted : AIState.Patrolling;
            yield break;
        }

        Vector2Int originCoord = currentCoord;
        Vector3 startPos = transform.position;

        if (!TryGetValidPivot(originCoord, targetCoord, out Vector2Int pivotCoord))
        {
            currentState = IsPlayerNearby(playerTransform) ? AIState.Alerted : AIState.Patrolling;
            yield break;
        }

        Vector3 finalPos = boardManager.GetWorldPosFromCoord(targetCoord);
        finalPos.y = startPos.y;

        Vector3 pivotPos = boardManager.GetWorldPosFromCoord(pivotCoord);
        pivotPos.y = startPos.y;

        currentTargetWorldPos = finalPos;
        UpdateBoardPosition(targetCoord);

        isMoving = true;

        currentMoveStepTarget = pivotPos;
        yield return StartCoroutine(MoveToPoint(pivotPos, speed));

        currentMoveStepTarget = finalPos;
        yield return StartCoroutine(MoveToPoint(finalPos, speed));

        isMoving = false;
    }

    private IEnumerator MoveToPoint(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (isStunned) yield break;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    private IEnumerator PatrolRoutine()
    {
        var possible = GetValidMoves();
        if (possible.Count > 0)
            yield return StartCoroutine(MoveRoutine(possible[Random.Range(0, possible.Count)], patrolMoveSpeed));
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

        pivot = ScorePath(origin, pivotA, target) >= ScorePath(origin, pivotB, target)
                ? pivotA : pivotB;
        return true;
    }

    private int ScorePath(Vector2Int o, Vector2Int p, Vector2Int t)
        => CountSegTiles(o, p) + CountSegTiles(p, t);

    private int CountSegTiles(Vector2Int from, Vector2Int to)
    {
        int count = 0;
        Vector2Int dir = new Vector2Int(
            to.x == from.x ? 0 : (int)Mathf.Sign(to.x - from.x),
            to.y == from.y ? 0 : (int)Mathf.Sign(to.y - from.y));
        Vector2Int cur = from;
        int maxSteps = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y) + 1;

        for (int i = 0; i < maxSteps; i++)
        {
            if (boardManager.TileExists(cur)) count++;
            if (cur == to) break;
            cur += dir;
        }
        return count;
    }

    #endregion

    #region Detection

    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        foreach (Vector2Int move in knightMoves)
            if (diff == move) return true;
        return false;
    }

    private List<Vector2Int> GetValidMoves()
    {
        var valid = new List<Vector2Int>();
        foreach (Vector2Int move in knightMoves)
        {
            Vector2Int dest = currentCoord + move;
            if (boardManager.TileExists(dest) && !boardManager.IsTileOccupied(dest))
                if (TryGetValidPivot(currentCoord, dest, out _))
                    valid.Add(dest);
        }
        return valid;
    }

    private Vector2Int GetBestMoveTowards(Vector2Int target)
    {
        var best = new List<Vector2Int>();
        float minDist = float.MaxValue;

        foreach (Vector2Int coord in GetValidMoves())
        {
            float dist = Vector2Int.Distance(coord, target);
            if (dist < minDist) { minDist = dist; best.Clear(); best.Add(coord); }
            else if (Mathf.Abs(dist - minDist) < 0.1f) best.Add(coord);
        }
        return best.Count > 0 ? best[Random.Range(0, best.Count)] : currentCoord;
    }

    #endregion

    #region Overrides

    public override void OnStunEnd()
    {
        isStunned = false;
        isMoving = false;
        HideTrail();

        if (brainCoroutine != null) StopCoroutine(brainCoroutine);
        brainCoroutine = StartCoroutine(KnightBrain()); 
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (!showGizmos || boardManager == null) return;

        Gizmos.color = new Color(1, 1, 1, 0.1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Color stateColor =
            isStunned ? new Color(1f, 0.9f, 0f, 0.4f) :
            currentState == AIState.Moving ? new Color(1f, 0f, 0f, 0.6f) :
            currentState == AIState.Committed ? new Color(1f, 0.4f, 0f, 0.5f) :
            currentState == AIState.Alerted ? new Color(1f, 0.8f, 0f, 0.4f) :
                                                new Color(0f, 1f, 0f, 0.3f);

        Gizmos.color = stateColor;
        Gizmos.DrawCube(transform.position + Vector3.up * gizmoHeight,
                        new Vector3(gizmosAnchor, gizmosLenght, gizmosAnchor));

        if (isMoving)
        {
            Gizmos.color = Color.cyan;
            DrawPathArrow(transform.position + Vector3.up * 0.3f,
                          currentTargetWorldPos + Vector3.up * 0.3f);
        }
    }

    #endregion
}