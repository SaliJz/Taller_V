using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class BaalController : MonoBehaviour
{
    #region Public Enums and State
    public enum BossState
    {
        Moving,
        Attacking,
        Recovering
    }

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;
    #endregion

    #region General Settings
    [Header("Player Settings")]
    [SerializeField] private Transform _player;

    [Header("Movement Settings")]
    [SerializeField] private float _stoppingDistance = 5f;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _accelerationDistance = 10f;
    [SerializeField] private float _acceleratedSpeed = 10f;
    [SerializeField] private float _maxPursuitTime = 5f;
    private float _distanceTimer; 
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    #endregion

    #region Attack 1: Side Slash Combo
    [Header("Attack 1: Combo de Tajo Lateral")]
    [SerializeField] private Transform _slashEffectTransform;
    [SerializeField] private float _slashDistance = 5f;
    [SerializeField] private float _attack1Cooldown = 5f;
    [SerializeField] private float _recoveryTime = 2f;
    [SerializeField] private int _comboCount = 3;
    [SerializeField] private float _timeBetweenSlashes = 0.5f;

    private float _attack1Timer;
    private Vector3 _hitEffectPosition;
    private float _hitEffectDuration = 0.2f;
    private float _hitEffectTimer;
    private Vector3 _lastRaycastOrigin;
    private Vector3 _lastRaycastDirection;
    #endregion

    #region Attack 2: Summon Staff & Swarm
    [Header("Attack 2: Invocación de Báculo y Enjambre")]
    [SerializeField] private Transform _staffTransform;
    [SerializeField] private StaffKeyframe[] _staffAnimationKeyframes;
    [SerializeField] private float _attack2Cooldown = 10f;
    [SerializeField] private float _swarmCastRadius = 1f;
    [SerializeField] private float _swarmCastDistance = 20f;
    [SerializeField] private float _swarmHitboxMoveSpeed = 10f;
    [SerializeField] private Transform _swarmVisualReference;
    [SerializeField] private int _swarmAttackCount = 1;
    [SerializeField] private float _timeBetweenSwarmAttacks = 1.5f;
    [SerializeField] private float _swarmReturnSpeed = 20f;
    [SerializeField] private int _maxRetargetAttempts = 1;

    private float _attack2Timer;
    private List<Vector3> _swarmPath = new List<Vector3>();
    #endregion

    #region VFX
    [Header("VFX")]
    [SerializeField] private TrailRenderer _trailRenderer;
    #endregion

    #region Animation Keyframes
    [System.Serializable]
    public struct StaffKeyframe
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        public float Time;
    }
    #endregion

    #region Unity Lifecycle Methods
    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        if (_player == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                _player = playerGO.transform;
            }
        }
        _attack1Timer = _attack1Cooldown;
        _attack2Timer = _attack2Cooldown;
        _navMeshAgent.updateRotation = false;
        _distanceTimer = 0f;

        if (_staffTransform != null && _staffAnimationKeyframes.Length > 0)
        {
            _staffTransform.localPosition = _staffAnimationKeyframes[0].Position;
            _staffTransform.localRotation = Quaternion.Euler(_staffAnimationKeyframes[0].Rotation);
            _staffTransform.localScale = _staffAnimationKeyframes[0].Scale;
            _staffTransform.gameObject.SetActive(false);
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
            case BossState.Recovering:
                HandleRecovery();
                break;
        }

        _hitEffectTimer -= Time.deltaTime;
    }

    void OnDrawGizmos()
    {
        if (_player != null)
        {
            if (_currentState == BossState.Moving && Vector3.Distance(transform.position, _player.position) <= _stoppingDistance)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, _stoppingDistance);
            }
        }

        if (_hitEffectTimer > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_hitEffectPosition, 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_lastRaycastOrigin, _hitEffectPosition - _lastRaycastOrigin);
        }

        if (_swarmPath.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _swarmPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_swarmPath[i], _swarmPath[i + 1]);
            }
        }
    }
    #endregion

    #region State Logic
    private void HandleMovement()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        LookAtPlayer();
        _attack1Timer -= Time.deltaTime;
        _attack2Timer -= Time.deltaTime;

        if (distanceToPlayer <= _slashDistance + 1f)
        {
            if (_attack1Timer <= 0)
            {
                _currentState = BossState.Attacking;
                StartCoroutine(SlashComboSequence());
                _attack1Timer = _attack1Cooldown;
                _distanceTimer = 0f; 
            }
            _navMeshAgent.isStopped = true;
        }
        else 
        {
            if (_attack2Timer <= 0)
            {
                _distanceTimer += Time.deltaTime;
                if (_distanceTimer >= _maxPursuitTime)
                {
                    _navMeshAgent.isStopped = true;
                    _currentState = BossState.Attacking;
                    StartCoroutine(SummonAndSwarmAttack());
                    _attack2Timer = _attack2Cooldown;
                    _distanceTimer = 0f;
                }
                else
                {
                    int decision = Random.Range(0, 2);
                    if (decision == 0)
                    {
                        _navMeshAgent.speed = _acceleratedSpeed;
                        _navMeshAgent.isStopped = false;
                        _navMeshAgent.SetDestination(_player.position);
                    }
                    else
                    {
                        _navMeshAgent.isStopped = true;
                        _currentState = BossState.Attacking;
                        StartCoroutine(SummonAndSwarmAttack());
                        _attack2Timer = _attack2Cooldown;
                        _distanceTimer = 0f;
                    }
                }
            }
            else
            {
                float currentSpeed = (distanceToPlayer <= _accelerationDistance) ? _acceleratedSpeed : _moveSpeed;
                _navMeshAgent.speed = currentSpeed;
                _navMeshAgent.isStopped = false;
                _navMeshAgent.SetDestination(_player.position);
            }
        }
    }

    private void HandleAttacks()
    {
    }

    private void HandleRecovery()
    {
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _rotationSpeed);
        }
    }
    #endregion

    #region Attack 1 Logic
    private IEnumerator SlashComboSequence()
    {
        _navMeshAgent.isStopped = true;
        _navMeshAgent.ResetPath();

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.enabled = true;
        }

        Vector3 initialLocalPosition = _slashEffectTransform.localPosition;

        for (int i = 0; i < _comboCount; i++)
        {
            yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed));

            Vector3 direction;
            if (i == 2)
            {
                direction = (_player.position - transform.position).normalized;
                direction.y = -0.5f;
                direction.Normalize();
            }
            else
            {
                direction = (_player.position - transform.position).normalized;
            }

            yield return StartCoroutine(PerformSlashVisual(direction));
            _slashEffectTransform.localPosition = initialLocalPosition;
            PerformRaycastHitDetection(direction);

            if (i < _comboCount - 1)
            {
                yield return new WaitForSeconds(_timeBetweenSlashes);
            }
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }

        _currentState = BossState.Recovering;
        yield return new WaitForSeconds(_recoveryTime);
        _currentState = BossState.Moving;
    }

    private IEnumerator RotateToFacePlayer(float speed)
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    private IEnumerator PerformSlashVisual(Vector3 direction)
    {
        Vector3 startLocalPosition = _slashEffectTransform.localPosition;
        Vector3 endWorldPosition = _slashEffectTransform.position + direction * _slashDistance;
        Vector3 endLocalPosition = _slashEffectTransform.parent.InverseTransformPoint(endWorldPosition);

        float duration = 0.2f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            _slashEffectTransform.localPosition = Vector3.Lerp(startLocalPosition, endLocalPosition, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _slashEffectTransform.localPosition = endLocalPosition;
    }

    private void PerformRaycastHitDetection(Vector3 direction)
    {
        _lastRaycastOrigin = transform.position;
        _lastRaycastDirection = direction;
        _hitEffectTimer = _hitEffectDuration;

        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, _slashDistance))
        {
            _hitEffectPosition = hit.point;
            if (hit.collider.CompareTag("Player"))
            {
                Debug.Log("Jugador golpeado por el tajo!");
            }
        }
        else
        {
            _hitEffectPosition = transform.position + direction * _slashDistance;
        }
    }
    #endregion

    #region Attack 2 Logic
    private IEnumerator SummonAndSwarmAttack()
    {
        _navMeshAgent.isStopped = true;

        if (_staffTransform != null)
        {
            _staffTransform.gameObject.SetActive(true);
        }

        yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed));

        yield return StartCoroutine(PerformStaffAnimation());

        if (_staffTransform != null)
        {
            _staffTransform.gameObject.SetActive(false);
        }

        for (int i = 0; i < _swarmAttackCount; i++)
        {
            yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed));
            yield return StartCoroutine(MoveSwarmHitbox());
            yield return new WaitForSeconds(_timeBetweenSwarmAttacks);
        }

        _currentState = BossState.Recovering;
        yield return new WaitForSeconds(_recoveryTime);
        _currentState = BossState.Moving;
    }

    private IEnumerator PerformStaffAnimation()
    {
        if (_staffTransform == null)
        {
            Debug.LogError("Referencia del báculo no asignada.");
            yield break;
        }

        for (int k = 0; k < _staffAnimationKeyframes.Length - 1; k++)
        {
            StaffKeyframe startKeyframe = _staffAnimationKeyframes[k];
            StaffKeyframe endKeyframe = _staffAnimationKeyframes[k + 1];

            float segmentDuration = endKeyframe.Time - startKeyframe.Time;
            float startTime = Time.time;
            while (Time.time < startTime + segmentDuration)
            {
                float t = (Time.time - startTime) / segmentDuration;
                _staffTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, t);
                _staffTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(startKeyframe.Rotation), Quaternion.Euler(endKeyframe.Rotation), t);
                _staffTransform.localScale = Vector3.Lerp(startKeyframe.Scale, endKeyframe.Scale, t);
                yield return null;
            }
            _staffTransform.localPosition = endKeyframe.Position;
            _staffTransform.localRotation = Quaternion.Euler(endKeyframe.Rotation);
            _staffTransform.localScale = endKeyframe.Scale;
        }
    }

    private IEnumerator MoveSwarmHitbox()
    {
        if (_swarmVisualReference == null)
        {
            Debug.LogError("Referencia del enjambre no asignada.");
            yield break;
        }

        _swarmPath.Clear();
        _swarmVisualReference.gameObject.SetActive(true);
        _swarmVisualReference.position = transform.position;

        int retargetsLeft = _maxRetargetAttempts;
        bool hitPlayer = false;

        while (!hitPlayer && retargetsLeft >= 0)
        {
            Vector3 direction = (_player.position - _swarmVisualReference.position).normalized;
            float distanceTraveled = 0f;

            while (distanceTraveled < _swarmCastDistance)
            {
                _swarmVisualReference.position += direction * _swarmHitboxMoveSpeed * Time.deltaTime;
                _swarmPath.Add(_swarmVisualReference.position);
                distanceTraveled += _swarmHitboxMoveSpeed * Time.deltaTime;

                RaycastHit hit;
                if (Physics.SphereCast(_swarmVisualReference.position, _swarmCastRadius, direction, out hit, 0.5f))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        _hitEffectPosition = hit.point;
                        _hitEffectTimer = _hitEffectDuration;
                        Debug.Log("Jugador golpeado por el enjambre de moscas!");
                        hitPlayer = true;
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
                yield return null;
            }
            retargetsLeft--;
        }

        while (Vector3.Distance(_swarmVisualReference.position, transform.position) > 0.5f)
        {
            Vector3 returnDirection = (transform.position - _swarmVisualReference.position).normalized;
            _swarmVisualReference.position += returnDirection * _swarmReturnSpeed * Time.deltaTime;
            _swarmPath.Add(_swarmVisualReference.position);
            yield return null;
        }
        _swarmVisualReference.gameObject.SetActive(false);
    }
    #endregion
}