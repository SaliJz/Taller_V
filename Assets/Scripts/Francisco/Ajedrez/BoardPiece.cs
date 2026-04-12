using UnityEngine;
using System.Collections;

public abstract class BoardPiece : MonoBehaviour
{
    #region Variables
    [Header("Board References")]
    public BoardManager boardManager;
    public Vector2Int currentCoord;
    public float cellSize = 1f;

    [Header("Status")]
    public float stunDuration = 1.5f;
    protected bool isStunned = false;
    protected bool isMoving = false;

    [Header("Detection Settings")]
    public float detectionRadius = 5f;
    public float detectionInterval = 0.5f;
    public float chaseGracePeriod = 2f;

    [Header("Gizmos Face Settings")]
    [SerializeField] protected bool showGizmos = true;
    [SerializeField] protected Color faceColor = Color.blue;
    [SerializeField] protected Vector3 faceOffset = new Vector3(0, 0.1f, 0);
    [SerializeField] protected Vector3 faceSize = new Vector3(0.8f, 0.1f, 0.8f);
    [SerializeField] protected float faceForwardLength = 0.6f;
    #endregion

    #region Properties
    public bool IsStunned => isStunned;
    public bool IsMoving => isMoving;
    #endregion

    #region Board Resolution

    protected void AutoConnectToNearestBoard()
    {
        if (boardManager != null)
        {
            Vector2Int spawnCoord = boardManager.WorldPosToCoord(transform.position);
            boardManager.ConnectPiece(this, spawnCoord);
            return;
        }

        BoardManager[] allBoards = FindObjectsByType<BoardManager>(FindObjectsSortMode.None);

        if (allBoards.Length == 0)
        {
            Debug.LogWarning($"[BoardPiece] {name}: No BoardManager found in scene.");
            return;
        }

        BoardManager nearest = null;
        float bestDist = float.MaxValue;
        foreach (var bm in allBoards)
        {
            float dist = Vector3.Distance(transform.position, bm.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = bm;
            }
        }

        boardManager = nearest;
        Vector2Int coord = boardManager.WorldPosToCoord(transform.position);
        boardManager.ConnectPiece(this, coord);
    }
    #endregion

    #region Logic & Helpers
    public Vector2Int WorldPosToCoord(Vector3 worldPos)
    {
        if (boardManager == null) return default;
        return boardManager.WorldPosToCoord(worldPos);
    }

    protected void UpdateBoardPosition(Vector2Int newCoord)
    {
        boardManager.SetOccupant(currentCoord, null);
        currentCoord = newCoord;
        boardManager.SetOccupant(currentCoord, this.gameObject);
    }

    public abstract bool CanSeePlayer(Vector2Int playerCoord);

    public bool IsPlayerNearby(Transform playerTransform)
    {
        if (playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= detectionRadius;
    }

    protected void LookAtTarget(Vector3 target, float rotationSpeed)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    protected void DrawPathArrow(Vector3 start, Vector3 end)
    {
        Gizmos.DrawLine(start, end);
        Vector3 dir = (end - start).normalized;
        if (dir == Vector3.zero) return;
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 135, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -135, 0) * Vector3.forward;
        Gizmos.DrawRay(end, right * 0.3f);
        Gizmos.DrawRay(end, left * 0.3f);
    }
    #endregion

    #region Status & Physics
    public void ApplyStun()
    {
        if (!isStunned)
        {
            StopAllCoroutines();
            StartCoroutine(StunSequence());
        }
    }

    private IEnumerator StunSequence()
    {
        isStunned = true;
        isMoving = false;
        OnStunStart();
        yield return StartCoroutine(KnockbackRoutine());
        yield return new WaitForSeconds(stunDuration);
        isStunned = false;
        OnStunEnd();
    }

    private IEnumerator KnockbackRoutine()
    {
        Vector2Int[] escapeDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int backCoord = currentCoord;
        foreach (var dir in escapeDirs)
        {
            Vector2Int testCoord = currentCoord + dir;
            if (boardManager.TileExists(testCoord) && !boardManager.IsTileOccupied(testCoord))
            {
                backCoord = testCoord;
                break;
            }
        }
        if (backCoord != currentCoord)
        {
            UpdateBoardPosition(backCoord);
            Vector3 targetPos = boardManager.GetWorldPosFromCoord(backCoord);
            targetPos.y = transform.position.y;
            float elapsed = 0;
            Vector3 startPos = transform.position;
            while (elapsed < 0.2f)
            {
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.2f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = targetPos;
        }
    }
    #endregion

    #region Virtual Methods
    public virtual void OnStunStart() { }
    public virtual void OnStunEnd() { }
    #endregion

    #region Base Gizmos
    protected virtual void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = faceColor;
        Gizmos.DrawWireCube(faceOffset, faceSize);

        Vector3 lineEnd = faceOffset + Vector3.forward * faceForwardLength;
        Gizmos.DrawLine(faceOffset, lineEnd);
        Gizmos.DrawLine(lineEnd, lineEnd + (Vector3.back + Vector3.right) * 0.15f);
        Gizmos.DrawLine(lineEnd, lineEnd + (Vector3.back + Vector3.left) * 0.15f);

        Gizmos.matrix = Matrix4x4.identity;
    }
    #endregion
}