using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BishopPiece : BoardPiece
{
    #region Enums
    public enum AIState { Patrolling, Chasing, Stunned }
    #endregion

    #region Variables
    [Header("Bishop Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed = 8f;
    [SerializeField] private float fastMoveSpeed = 16f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float farDistanceThreshold = 5f;
    [SerializeField] private int patrolStepDistance = 2;

    private int currentPatrolIndex = 0;
    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private float lastSeenTime;
    private Coroutine brainCoroutine;

    private readonly Vector2Int[] moveDirections = new Vector2Int[]
    {
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, -1), new Vector2Int(-1, 1)
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
            brainCoroutine = StartCoroutine(BishopBrain());
        }
    }

    private void Update()
    {
        if (isMoving && !isStunned)
        {
            LookAtTarget(currentTargetWorldPos, rotationSpeed);
        }
    }
    #endregion

    #region Brain Logic
    private IEnumerator BishopBrain()
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
                if (canSee) yield return StartCoroutine(MoveRoutine(playerCoord));
                else
                {
                    Vector2Int searchTile = GetSmartChaseTile(playerCoord);
                    if (searchTile != currentCoord) yield return StartCoroutine(MoveRoutine(searchTile));
                }
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

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool canAttack = CanSeePlayer(boardManager.WorldPosToCoord(playerTransform.position));
        float speed = (currentState == AIState.Chasing && canAttack && dist > farDistanceThreshold) ? fastMoveSpeed : baseMoveSpeed;

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
        Vector2Int targetCoord = currentCoord + (moveDirections[currentPatrolIndex] * patrolStepDistance);
        if (!boardManager.TileExists(targetCoord) || boardManager.IsTileOccupied(targetCoord))
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % moveDirections.Length;
            yield break;
        }
        yield return StartCoroutine(MoveRoutine(targetCoord));
        currentPatrolIndex = (currentPatrolIndex + 1) % moveDirections.Length;
    }
    #endregion

    #region Overrides & Detection
    public override void OnStunEnd()
    {
        isStunned = false;
        isMoving = false;
        if (brainCoroutine != null) StopCoroutine(brainCoroutine);
        brainCoroutine = StartCoroutine(BishopBrain());
    }

    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        if (Mathf.Abs(diff.x) == Mathf.Abs(diff.y) && diff.x != 0)
        {
            Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
            Vector2Int check = currentCoord + dir;
            while (check != playerCoord)
            {
                if (boardManager.IsTileOccupied(check)) return false;
                check += dir;
            }
            return true;
        }
        return false;
    }

    private Vector2Int GetSmartChaseTile(Vector2Int playerCoord)
    {
        List<Vector2Int> goodTiles = new List<Vector2Int>();
        float minScore = float.MaxValue;
        foreach (var dir in moveDirections)
        {
            for (int i = 1; i <= 3; i++)
            {
                Vector2Int testCoord = currentCoord + (dir * i);
                if (!boardManager.TileExists(testCoord) || boardManager.IsTileOccupied(testCoord)) break;
                float score = Vector2Int.Distance(testCoord, playerCoord);
                if (Mathf.Abs((playerCoord - testCoord).x) == Mathf.Abs((playerCoord - testCoord).y)) score -= 12f;
                if (score < minScore) { minScore = score; goodTiles.Clear(); goodTiles.Add(testCoord); }
                else if (Mathf.Abs(score - minScore) < 0.1f) goodTiles.Add(testCoord);
            }
        }
        return goodTiles.Count > 0 ? goodTiles[Random.Range(0, goodTiles.Count)] : currentCoord;
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