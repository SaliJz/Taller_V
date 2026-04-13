using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ChessPieceAttack))]
public class BishopPiece : BoardPiece
{
    #region Enums

    public enum AIState { Patrolling, Alerted, Committed, Moving }

    #endregion

    #region Variables

    [Header("Bishop Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float patrolMoveSpeed = 8f;
    [SerializeField] private float attackMoveSpeed = 16f;
    [SerializeField] private float patrolRotSpeed = 10f;
    [SerializeField] private float attackRotSpeed = 20f;
    [SerializeField] private float farDistThreshold = 5f;
    [SerializeField] private int patrolStepDist = 2;
    [SerializeField] private float attackWindupTime = 1f;

    private int currentPatrolIndex = 0;
    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private Vector2Int committedTargetCoord;
    private Coroutine brainCoroutine;
    private ChessPieceAttack attackComponent;

    private readonly Vector2Int[] moveDirs = new Vector2Int[]
    {
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1, -1), new Vector2Int(-1,  1)
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
            brainCoroutine = StartCoroutine(BishopBrain());
    }

    private void Update()
    {
        if (!isMoving || isStunned) return;
        float rot = (currentState == AIState.Moving) ? attackRotSpeed : patrolRotSpeed;
        LookAtTarget(currentTargetWorldPos, rot);
    }

    #endregion

    #region Brain

    private IEnumerator BishopBrain()
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
                        Vector2Int approach = GetSmartApproach(playerCoord);
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

        currentState = IsPlayerNearby(playerTransform) ? AIState.Alerted : AIState.Patrolling;
    }

    #endregion

    #region Trail / Path Line

    private void ShowPathLine(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) != Mathf.Abs(diff.y) || diff.x == 0) return;

        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        Vector2Int cur = from;
        var points = new List<Vector3>();

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
            currentState = IsPlayerNearby(playerTransform) ? AIState.Alerted : AIState.Patrolling;
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
        float dist = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : 0f;
        float speed = dist > farDistThreshold ? patrolMoveSpeed * 1.3f : patrolMoveSpeed;

        Vector2Int target = currentCoord + (moveDirs[currentPatrolIndex] * patrolStepDist);

        if (!IsDiagonalPathValid(currentCoord, target))
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % moveDirs.Length;
            yield break;
        }

        yield return StartCoroutine(MoveRoutine(target, speed));
        currentPatrolIndex = (currentPatrolIndex + 1) % moveDirs.Length;
    }

    #endregion

    #region Detection

    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        if (Mathf.Abs(diff.x) != Mathf.Abs(diff.y) || diff.x == 0) return false;

        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        Vector2Int check = currentCoord + dir;
        while (check != playerCoord)
        {
            if (!boardManager.TileExists(check) || boardManager.IsTileOccupied(check)) return false;
            check += dir;
        }
        return true;
    }

    private bool IsDiagonalPathValid(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) != Mathf.Abs(diff.y) || diff.x == 0) return false;

        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        Vector2Int cur = from + dir;
        while (cur != to)
        {
            if (!boardManager.TileExists(cur)) return false;
            cur += dir;
        }
        return boardManager.TileExists(to);
    }

    private Vector2Int GetSmartApproach(Vector2Int playerCoord)
    {
        var candidates = new List<Vector2Int>();
        float minScore = float.MaxValue;

        foreach (var dir in moveDirs)
        {
            for (int i = 1; i <= 8; i++)
            {
                Vector2Int test = currentCoord + (dir * i);
                if (!boardManager.TileExists(test) || boardManager.IsTileOccupied(test)) break;
                if (!IsDiagonalPathValid(currentCoord, test)) break;

                float score = Vector2Int.Distance(test, playerCoord);
                Vector2Int d = playerCoord - test;
                if (Mathf.Abs(d.x) == Mathf.Abs(d.y) && d.x != 0) score -= 20f;

                if (score < minScore) { minScore = score; candidates.Clear(); candidates.Add(test); }
                else if (Mathf.Abs(score - minScore) < 0.1f) candidates.Add(test);
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
        brainCoroutine = StartCoroutine(BishopBrain());
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