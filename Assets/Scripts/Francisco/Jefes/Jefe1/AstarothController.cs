using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[System.Serializable]
public struct WhipKeyframe
{
    public Vector3 Position;
    public float Time;
    public bool IsTargetable;
}

[System.Serializable]
public struct SmashKeyframe
{
    public Vector3 Position;
    public Vector3 Scale;
    public float Time;
    public bool IsTargetable;
}

public class AstarothController : MonoBehaviour
{
    #region Public Enums and State
    public enum BossState
    {
        Moving,
        Attacking
    }

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;
    #endregion

    #region General Settings
    [Header("Player Settings")]
    [SerializeField] private Transform _player;
    [Header("Movement Settings")]
    [SerializeField] private float _stoppingDistance = 15f;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    #endregion

    #region Attack 1: Whip Attack
    [Header("Attack 1: Latigazo Desgarrador")]
    [SerializeField] private Transform _whipVisualTransform;
    [SerializeField] private WhipKeyframe[] _whipAnimationKeyframes;
    [SerializeField] private float _attack1Cooldown = 7f;
    [SerializeField] private float _whipRange = 25f;
    [SerializeField] private int _whipAttackCount = 3;
    private float _attack1Timer;
    private bool _isAttackingWithWhip;
    private Vector3 _whipTargetPoint;
    private Vector3 _lastWhipRaycastOrigin;
    private Vector3 _lastWhipRaycastDirection;
    private bool _showWhipRaycastGizmo;
    private bool _showWhipImpactGizmo;
    private Vector3 _whipImpactPoint;
    #endregion

    #region Attack 2: Smash Attack
    [Header("Attack 2: Latigazo Demoledor")]
    [SerializeField] private Transform _smashVisualTransform;
    [SerializeField] private SmashKeyframe[] _smashAnimationKeyframes;
    [SerializeField] private float _attack2Cooldown = 12f;
    [SerializeField] private float _smashRadius = 5f;
    [SerializeField] private float _smashDetectionRadius = 10f;
    [SerializeField] private GameObject _smashRadiusPrefab;
    [SerializeField] private float _smashDelay = 1.5f;
    private float _attack2Timer;
    private bool _isSmashing;
    private Vector3 _smashTargetPoint;
    private Vector3 _lastSmashOverlapCenter;
    private float _lastSmashOverlapRadius;
    private bool _showSmashOverlapGizmo;
    #endregion

    #region Unity Lifecycle Methods
    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _navMeshAgent.updateRotation = false;
        _attack1Timer = _attack1Cooldown;
        _attack2Timer = _attack2Cooldown;
        if (_player == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                _player = playerGO.transform;
            }
        }
    }

    void Update()
    {
        if (_player == null)
        {
            return;
        }

        switch (_currentState)
        {
            case BossState.Moving:
                HandleMovement();
                break;
            case BossState.Attacking:
                HandleAttacks();
                break;
        }
    }

    void OnDrawGizmos()
    {
        if (_showWhipRaycastGizmo)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_lastWhipRaycastOrigin, _lastWhipRaycastDirection * _whipRange);
            Gizmos.DrawWireSphere(_lastWhipRaycastOrigin, 0.5f);
        }
        if (_showWhipImpactGizmo)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_whipImpactPoint, 0.5f);
        }
        if (_showSmashOverlapGizmo)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lastSmashOverlapCenter, _lastSmashOverlapRadius);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, _smashDetectionRadius);
        }
    }
    #endregion

    #region State Logic
    private void HandleMovement()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        _navMeshAgent.updateRotation = true;
        if (distanceToPlayer > _stoppingDistance)
        {
            _navMeshAgent.SetDestination(_player.position);
        }
        else
        {
            _navMeshAgent.SetDestination(transform.position);
            _currentState = BossState.Attacking;
        }
    }

    private void HandleAttacks()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer > _stoppingDistance)
        {
            _currentState = BossState.Moving;
            return;
        }
        _navMeshAgent.updateRotation = false;

        _attack1Timer -= Time.deltaTime;
        _attack2Timer -= Time.deltaTime;

        if (!_isAttackingWithWhip && !_isSmashing)
        {
            if (distanceToPlayer <= _smashDetectionRadius && _attack2Timer <= 0)
            {
                StartCoroutine(SmashAttackSequence());
                _attack2Timer = _attack2Cooldown;
                return;
            }
            if (_attack1Timer <= 0)
            {
                StartCoroutine(WhipAttackSequence());
                _attack1Timer = _attack1Cooldown;
                return;
            }
        }
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _navMeshAgent.angularSpeed);
    }
    #endregion

    #region Attack 1 Logic
    private IEnumerator WhipAttackSequence()
    {
        _isAttackingWithWhip = true;
        _showWhipImpactGizmo = false;
        for (int i = 0; i < _whipAttackCount; i++)
        {
            LookAtPlayer();

            Vector3 rayOrigin = _whipVisualTransform.position;
            Vector3 directionToPlayer = (_player.position - rayOrigin).normalized;
            _lastWhipRaycastOrigin = rayOrigin;
            _lastWhipRaycastDirection = directionToPlayer;
            _showWhipRaycastGizmo = true;

            int layerMask = LayerMask.GetMask("Obstacle", "Player", "Wall", "Ground");
            RaycastHit hit;
            if (Physics.SphereCast(rayOrigin, 0.5f, directionToPlayer, out hit, _whipRange, layerMask))
            {
                _whipTargetPoint = hit.point;
            }
            else
            {
                _whipTargetPoint = rayOrigin + directionToPlayer * _whipRange;
            }

            _whipImpactPoint = _whipTargetPoint;
            _showWhipImpactGizmo = true;

            for (int k = 0; k < _whipAnimationKeyframes.Length - 1; k++)
            {
                WhipKeyframe startKeyframe = _whipAnimationKeyframes[k];
                WhipKeyframe endKeyframe = _whipAnimationKeyframes[k + 1];

                if (endKeyframe.IsTargetable)
                {
                    endKeyframe.Position = transform.InverseTransformPoint(_whipTargetPoint);
                }

                float segmentDuration = endKeyframe.Time - startKeyframe.Time;
                float startTime = Time.time;
                while (Time.time < startTime + segmentDuration)
                {
                    float t = (Time.time - startTime) / segmentDuration;
                    _whipVisualTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, t);
                    yield return null;
                }
                _whipVisualTransform.localPosition = endKeyframe.Position;

                if (endKeyframe.IsTargetable)
                {
                    Collider[] hitColliders = Physics.OverlapSphere(_whipTargetPoint, 0.5f);
                    foreach (var hitCollider in hitColliders)
                    {
                        if (hitCollider.CompareTag("Player"))
                        {
                            Debug.Log("Player was hit by Whip Attack!");
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
        _isAttackingWithWhip = false;
        _showWhipRaycastGizmo = false;
        _showWhipImpactGizmo = false;
        _whipVisualTransform.localPosition = _whipAnimationKeyframes[0].Position;
    }
    #endregion

    #region Attack 2 Logic
    private IEnumerator SmashAttackSequence()
    {
        _isSmashing = true;
        _showSmashOverlapGizmo = false;

        float rotationTime = 0f;
        while (rotationTime < _smashDelay)
        {
            LookAtPlayer();
            rotationTime += Time.deltaTime;
            if (Vector3.Angle(transform.forward, (_player.position - transform.position).normalized) < 5f)
            {
                break;
            }
            yield return null;
        }

        _smashTargetPoint = _player.position;

        for (int k = 0; k < _smashAnimationKeyframes.Length - 1; k++)
        {
            SmashKeyframe startKeyframe = _smashAnimationKeyframes[k];
            SmashKeyframe endKeyframe = _smashAnimationKeyframes[k + 1];

            if (endKeyframe.IsTargetable)
            {
                endKeyframe.Position = transform.InverseTransformPoint(_smashTargetPoint);
            }

            float segmentDuration = endKeyframe.Time - startKeyframe.Time;
            if (segmentDuration <= 0)
            {
                _smashVisualTransform.localPosition = endKeyframe.Position;
                _smashVisualTransform.localScale = endKeyframe.Scale;
            }
            else
            {
                float startTime = Time.time;
                while (Time.time < startTime + segmentDuration)
                {
                    float t = (Time.time - startTime) / segmentDuration;
                    _smashVisualTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, t);
                    _smashVisualTransform.localScale = Vector3.Lerp(startKeyframe.Scale, endKeyframe.Scale, t);
                    yield return null;
                }
                _smashVisualTransform.localPosition = endKeyframe.Position;
                _smashVisualTransform.localScale = endKeyframe.Scale;
            }

            if (endKeyframe.IsTargetable)
            {
                PerformSmashDamage(_smashTargetPoint);
            }
        }

        _isSmashing = false;
        _smashVisualTransform.localPosition = _smashAnimationKeyframes[0].Position;
        _smashVisualTransform.localScale = _smashAnimationKeyframes[0].Scale;
    }

    private void PerformSmashDamage(Vector3 damageCenter)
    {
        _lastSmashOverlapCenter = damageCenter;
        _lastSmashOverlapRadius = _smashRadius;
        _showSmashOverlapGizmo = true;

        if (_smashRadiusPrefab != null)
        {
            GameObject visualEffect = Instantiate(_smashRadiusPrefab, damageCenter, Quaternion.identity);
            StartCoroutine(ExpandSmashRadius(visualEffect.transform, _smashRadius));
        }

        Collider[] hitColliders = Physics.OverlapSphere(damageCenter, _smashRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                Debug.Log("Player hit by Smash Attack!");
            }
        }
        Invoke("DisableSmashOverlapGizmo", 1f);
    }

    private void DisableSmashOverlapGizmo()
    {
        _showSmashOverlapGizmo = false;
    }

    private IEnumerator ExpandSmashRadius(Transform effectTransform, float targetRadius)
    {
        float duration = 0.5f;
        float elapsedTime = 0f;
        Vector3 initialScale = Vector3.zero;
        Vector3 targetScale = new Vector3(targetRadius * 2, effectTransform.localScale.y, targetRadius * 2);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            effectTransform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            yield return null;
        }

        Destroy(effectTransform.gameObject, 0.5f);
    }
    #endregion
}