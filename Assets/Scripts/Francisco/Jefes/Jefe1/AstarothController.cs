using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

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
        Attacking,
        SpecialAbility
    }

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;
    #endregion

    #region General Settings
    [Header("Player Settings")]
    [SerializeField] private Transform _player;

    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    private int _specialAbilityUsedCount = 0;

    [Header("Movement Settings")]
    [SerializeField] private float _stoppingDistance = 15f;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    #endregion

    #region Attack 1: Whip Attack
    [Header("Attack 1: Latigazo Desgarrador")]
    [SerializeField] private Transform _whipVisualTransform;
    [SerializeField] private WhipKeyframe[] _whipAnimationKeyframes;
    [SerializeField] private float _Attack1Damage = 9f;
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
    [SerializeField] private float _Attack2Damage = 25f;
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

    #region Special Ability: Pulso Carnal
    [Header("Special Ability: Pulso Carnal")]
    [SerializeField] private float _pulseExpansionDuration = 3f;
    [SerializeField] private float _pulseWaitDuration = 1f;
    [SerializeField] private float _pulseSlowPercentage = 0.5f;
    [SerializeField] private float _pulseSlowDuration = 2f;
    [SerializeField] private int _pulseDamage = 1;
    [SerializeField] private GameObject _nervesVisualizationPrefab;
    [SerializeField] private GameObject _crackEffectPrefab;
    [SerializeField] private AudioClip _pulseScreamSound;
    [SerializeField] private AudioClip _rocksFallingSound;
    [SerializeField] private float _postPulseAttackDelay = 0.8f;
    [SerializeField] private Transform _headsTransform;
    [SerializeField] private float _headDownRotationAngle = -45f;
    [SerializeField] private float _headAnimationDuration = 0.5f;
    [SerializeField] private float _roomMaxRadius = 45f;
    [SerializeField] private bool _calculateRoomRadiusOnStart = true;
    [SerializeField] private float _movementSpeedForPulse = 10f;
    private bool _isUsingSpecialAbility;
    private float[] _healthThresholdsForPulse = { 0.67f, 0.34f };
    private bool _isPulseAttackBlocked = false;
    private List<GameObject> _instantiatedEffects = new List<GameObject>();
    private Vector3 _roomCenter = Vector3.zero;
    #endregion

    #region Enraged Phase
    [Header("Enraged Phase (25% HP)")]
    [SerializeField] private float _enragedHealthThreshold = 0.25f;
    [SerializeField] private Renderer[] _eyeRenderers;
    [SerializeField] private Material _enragedEyeMaterial;
    [SerializeField] private AudioClip _enragedRoarSound;
    private bool _isEnraged = false;
    private float _attackSpeedMultiplier = 1f;
    private float _baseAttack1Cooldown;
    private float _baseAttack2Cooldown;
    private float _basePulseExpansionDuration;
    private float _basePulseWaitDuration;
    private Material[] _originalEyeMaterials;
    #endregion

    #region VFX
    [Header("VFX")]
    [SerializeField] private TrailRenderer _trailRenderer;
    #endregion

    #region SFX
    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip deathSFX;
    #endregion

    #region Camera Shake
    [SerializeField] private CinemachineCamera _vcam;
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _amplitude = 2f;
    [SerializeField] private float _frequency = 2f;
    private CinemachineBasicMultiChannelPerlin _noise;
    #endregion

    private EnemyHealth _enemyHealth;

    #region Unity Lifecycle Methods
    private void Awake()
    {
        _enemyHealth = GetComponent<EnemyHealth>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        if (_vcam == null)
        {
            CinemachineCamera vcam = Object.FindFirstObjectByType<CinemachineCamera>();
            if (vcam != null)
            {
                _vcam = vcam;
            }
        }
    }

    private void Start()
    {
        _navMeshAgent.updateRotation = false;
        _attack1Timer = _attack1Cooldown;
        _attack2Timer = _attack2Cooldown;

        _baseAttack1Cooldown = _attack1Cooldown;
        _baseAttack2Cooldown = _attack2Cooldown;
        _basePulseExpansionDuration = _pulseExpansionDuration;
        _basePulseWaitDuration = _pulseWaitDuration;

        if (_calculateRoomRadiusOnStart)
        {
            CalculateRoomRadius();
        }
        else
        {
            _roomCenter = new Vector3(transform.position.x, 0f, transform.position.z);
            Debug.Log($"Using set room radius: {_roomMaxRadius}m");
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }

        if (_player == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                _player = playerGO.transform;
            }
        }

        if (_enemyHealth != null) _enemyHealth.SetMaxHealth(_maxHealth);
        _currentHealth = _maxHealth;

        if (_vcam != null) _noise = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
    }

    private void Update()
    {
        if (_player == null)
        {
            return;
        }

        CheckHealthThresholds();

        switch (_currentState)
        {
            case BossState.Moving:
                HandleMovement();
                break;
            case BossState.Attacking:
                HandleAttacks();
                break;
            case BossState.SpecialAbility:
                break;
        }
    }

    #endregion

    #region Health and Phase Management
    private void OnEnable()
    {
        if (_enemyHealth != null) _enemyHealth.OnDeath += HandleEnemyDeath;
        if (_enemyHealth != null) _enemyHealth.OnHealthChanged += HandleEnemyHealthChange;
    }

    private void OnDisable()
    {
        if (_enemyHealth != null) _enemyHealth.OnDeath -= HandleEnemyDeath;
        if (_enemyHealth != null) _enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
    }

    private void OnDestroy()
    {
        if (_enemyHealth != null) _enemyHealth.OnDeath -= HandleEnemyDeath;
        if (_enemyHealth != null) _enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _isUsingSpecialAbility = false;
        _isEnraged = false;

        StopAllCoroutines();

        DestroyAllInstantiatedEffects();

        if (_navMeshAgent != null)
        {
            if (_navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
                _navMeshAgent.updatePosition = false;
                _navMeshAgent.updateRotation = false;
            }
            else
            {
                _navMeshAgent.enabled = false;
            }
        }

        if (_animator != null) _animator.SetTrigger("Die");
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void DestroyAllInstantiatedEffects()
    {
        foreach (GameObject effect in _instantiatedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
        _instantiatedEffects.Clear();
    }

    private void HandleEnemyHealthChange(float newCurrentHealth, float newMaxHealth)
    {
        _currentHealth = newCurrentHealth;
        _maxHealth = newMaxHealth;
    }

    private void CheckHealthThresholds()
    {
        if (_isUsingSpecialAbility) return;

        float healthPercentage = _currentHealth / _maxHealth;

        if (_specialAbilityUsedCount < _healthThresholdsForPulse.Length)
        {
            if (healthPercentage <= _healthThresholdsForPulse[_specialAbilityUsedCount])
            {
                _currentState = BossState.SpecialAbility;
                StartCoroutine(PulsoCarnal());
                _specialAbilityUsedCount++;
            }
        }

        if (!_isEnraged && healthPercentage <= _enragedHealthThreshold)
        {
            EnterEnragedPhase();
        }
    }

    private void EnterEnragedPhase()
    {
        _isEnraged = true;
        _attackSpeedMultiplier = 2f;

        // Actualizar cooldowns de ataques
        _attack1Cooldown = _baseAttack1Cooldown / _attackSpeedMultiplier;
        _attack2Cooldown = _baseAttack2Cooldown / _attackSpeedMultiplier;

        _pulseExpansionDuration = 1.5f; // Reducido de 3s
        _pulseWaitDuration = 0.5f; // Reducido de 1s

        if (_enragedRoarSound != null)
        {
            AudioSource.PlayClipAtPoint(_enragedRoarSound, transform.position);
        }

        Debug.Log("Astaroth entered ENRAGED phase!");
    }
    #endregion

    #region State Logic
    private void HandleMovement()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        _navMeshAgent.updateRotation = true;

        _attack1Timer -= Time.deltaTime;
        _attack2Timer -= Time.deltaTime;

        if (_isPulseAttackBlocked)
        {
            return;
        }

        if (_attack1Timer <= 0 && distanceToPlayer <= _whipRange)
        {
            _currentState = BossState.Attacking;
            StartCoroutine(WhipAttackSequence());
            _attack1Timer = _attack1Cooldown;
        }
        else if (_attack2Timer <= 0 && distanceToPlayer <= _smashDetectionRadius)
        {
            _currentState = BossState.Attacking;
            StartCoroutine(SmashAttackSequence());
            _attack2Timer = _attack2Cooldown;
        }
        else
        {
            if (distanceToPlayer > _stoppingDistance)
            {
                _navMeshAgent.isStopped = false;
                _navMeshAgent.SetDestination(_player.position);
            }
            else
            {
                _navMeshAgent.isStopped = true;
                LookAtPlayer();
            }
        }
    }

    private void HandleAttacks()
    {
        if (!_isAttackingWithWhip && !_isSmashing)
        {
            _navMeshAgent.isStopped = false;
            _currentState = BossState.Moving;
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
        _navMeshAgent.isStopped = true;

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.enabled = true;
        }

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
                            PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();
                            if (playerHealth != null)
                            {
                                playerHealth.TakeDamage(_Attack1Damage);
                            }
                            Debug.Log("Player was hit by Whip Attack!");
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
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
        _navMeshAgent.isStopped = true;

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

        Vector3 smashGroundPosition = GetGroundPosition(damageCenter);
        smashGroundPosition.x = damageCenter.x;
        smashGroundPosition.z = damageCenter.z;

        if (_smashRadiusPrefab != null)
        {
            GameObject visualEffect = Instantiate(_smashRadiusPrefab, smashGroundPosition, Quaternion.identity);
            _instantiatedEffects.Add(visualEffect);
            StartCoroutine(ExpandSmashRadius(visualEffect.transform, _smashRadius));
        }

        Collider[] hitColliders = Physics.OverlapSphere(damageCenter, _smashRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(_Attack2Damage);
                }
                Debug.Log("Jugador golpeado por Latigazo Demoledor!");
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

    #region Special Ability: Pulso Carnal

    private void CalculateRoomRadius()
    {
        // Buscar el NavMeshSurface para obtener el tamaño de la habitación
        UnityEngine.AI.NavMeshTriangulation triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();

        if (triangulation.vertices.Length > 0)
        {
            // Calcular los límites de la habitación usando los vértices del NavMesh
            Vector3 minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            // Posición del boss en el plano XZ
            foreach (Vector3 vertex in triangulation.vertices)
            {
                minBounds = Vector3.Min(minBounds, vertex);
                maxBounds = Vector3.Max(maxBounds, vertex);
            }

            // Sala centro
            _roomCenter = new Vector3((minBounds.x + maxBounds.x) / 2f, 0f, (minBounds.z + maxBounds.z) / 2f);

            float maxDistance = 0f;

            // Encontrar el vértice más lejano del NavMesh desde el centro de la habitación
            foreach (Vector3 vertex in triangulation.vertices)
            {
                float distance = Vector3.Distance(new Vector3(_roomCenter.x, 0, _roomCenter.z), new Vector3(vertex.x, 0, vertex.z));
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                }
            }

            //_roomMaxRadius = maxDistance;
            Debug.Log($"Room radius calculated: {_roomMaxRadius}m (diameter: {_roomMaxRadius * 2}m)");
        }
        else
        {
            _roomMaxRadius = 25f; // Radio de 25m = 50m de diámetro
            _roomCenter = new Vector3(transform.position.x, 0f, transform.position.z);
            Debug.LogWarning($"No NavMesh found. Using default room radius: {_roomMaxRadius}m");
        }
    }

    private IEnumerator PulsoCarnal()
    {
        _isUsingSpecialAbility = true;

        Debug.Log("Astaroth esta preparando Pulso Carnal!");

        yield return StartCoroutine(MoveToCenter(_roomCenter));

        _navMeshAgent.isStopped = true;

        // Obtener posición del suelo
        Vector3 groundPos = GetGroundPosition(transform.position);

        if (_headsTransform != null)
        {
            yield return StartCoroutine(AnimateHeadDown());
        }

        GameObject nervesVisualization = null;
        if (_nervesVisualizationPrefab != null)
        {
            nervesVisualization = Instantiate(_nervesVisualizationPrefab, groundPos, Quaternion.identity, null);
            _instantiatedEffects.Add(nervesVisualization);
        }

        float expansionTimer = 0f;
        while (expansionTimer < _pulseExpansionDuration)
        {
            expansionTimer += Time.deltaTime;

            if (nervesVisualization != null)
            {
                float expansionProgress = expansionTimer / _pulseExpansionDuration;
                float diameter = _roomMaxRadius * 2;
                float currentSize = expansionProgress * diameter;

                nervesVisualization.transform.localScale = new Vector3(currentSize, 1f, currentSize);
            }

            yield return null;
        }

        yield return new WaitForSeconds(_pulseWaitDuration);

        // Restaurar posición de cabeza
        if (_headsTransform != null)
        {
            StartCoroutine(AnimateHeadUp());
        }

        if (nervesVisualization != null)
        {
            Destroy(nervesVisualization, 0.2f);
        }

        ApplyPulseEffect();

        ShakeCamera(_shakeDuration, _amplitude, _frequency);

        if (_pulseScreamSound != null)
        {
            AudioSource.PlayClipAtPoint(_pulseScreamSound, transform.position);
        }

        if (_rocksFallingSound != null)
        {
            AudioSource.PlayClipAtPoint(_rocksFallingSound, transform.position);
        }

        if (_crackEffectPrefab != null)
        {
            GameObject crackEffect = Instantiate(_crackEffectPrefab, groundPos, Quaternion.identity, null);
            _instantiatedEffects.Add(crackEffect);
            Destroy(crackEffect, 2f);
        }

        StartCoroutine(BlockAttacksAfterPulse());

        yield return new WaitForSeconds(1f);

        _isUsingSpecialAbility = false;
        _currentState = BossState.Moving;
    }

    private IEnumerator MoveToCenter(Vector3 targetCenter)
    {
        if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
        {
            Debug.LogError("MoveToCenter failed: NavMeshAgent is NULL or not on NavMesh.");
            yield break;
        }

        _navMeshAgent.isStopped = false;
        _navMeshAgent.speed = _movementSpeedForPulse;
        _navMeshAgent.SetDestination(targetCenter);
        Debug.Log($"Moving to center: {targetCenter}. Current stopping distance: {_navMeshAgent.stoppingDistance}");

        float safetyTimer = 0f;
        float maxMoveTime = 5f;

        while (_navMeshAgent.pathPending || _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance)
        {
            if (_navMeshAgent.remainingDistance == float.PositiveInfinity)
            {
                Debug.LogWarning("MoveToCenter: remainingDistance is Infinity. Breaking loop to continue ability.");
                break;
            }

            safetyTimer += Time.deltaTime;
            if (safetyTimer >= maxMoveTime)
            {
                Debug.LogError("MoveToCenter TIMEOUT: Boss did not reach center in time. Forcing continuation of PulsoCarnal sequence.");
                break;
            }
            yield return null;
        }

        _navMeshAgent.speed = 3.5f;

        Debug.Log("MoveToCenter complete: Continuing Pulso Carnal sequence.");
    }

    private IEnumerator AnimateHeadDown()
    {
        if (_headsTransform == null) yield break;

        Quaternion startRotation = _headsTransform.localRotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(_headDownRotationAngle, 0, 0);

        float elapsed = 0f;
        while (elapsed < _headAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _headAnimationDuration;
            _headsTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        _headsTransform.localRotation = targetRotation;
    }

    private IEnumerator AnimateHeadUp()
    {
        if (_headsTransform == null) yield break;

        Quaternion startRotation = _headsTransform.localRotation;
        Quaternion targetRotation = Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < _headAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _headAnimationDuration;
            _headsTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        _headsTransform.localRotation = targetRotation;
    }

    private IEnumerator BlockAttacksAfterPulse()
    {
        _isPulseAttackBlocked = true;

        // Bloquear Caos por 2s
        _attack2Timer = Mathf.Max(_attack2Timer, 2f);

        // Retrasar Latigazo por 0.8s
        _attack1Timer = Mathf.Max(_attack1Timer, _postPulseAttackDelay);

        yield return new WaitForSeconds(_postPulseAttackDelay);

        _isPulseAttackBlocked = false;
    }

    private Vector3 GetGroundPosition(Vector3 rayOrigin)
    {
        RaycastHit hit;

        // Intentar raycast hacia abajo para encontrar el suelo
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 20f, LayerMask.GetMask("Ground")))
        {
            return hit.point + Vector3.up * 0.01f; // Pequeño offset para evitar z-fighting
        }

        // Fallback: asumir Y = 0
        return new Vector3(transform.position.x, 0.01f, transform.position.z);
    }

    private void ApplyPulseEffect()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1000f);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                PlayerMovement playerMovement = hitCollider.GetComponent<PlayerMovement>();
                PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();
                PlayerStatsManager statsManager = hitCollider.GetComponent<PlayerStatsManager>();

                if (playerMovement != null)
                {
                    playerMovement.IsDashDisabled = true;
                    
                    StartCoroutine(DesactivePulseEffect(hitCollider));
                }

                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(_pulseDamage);
                }

                if (statsManager != null)
                {
                    float currentSpeed = statsManager.GetStat(StatType.MoveSpeed);
                    float slowAmount = currentSpeed * -_pulseSlowPercentage;
                    float duration = 3.0f;

                    string uniqueKey = $"SlowEffect_{Time.time}";

                    statsManager.ApplyTimedModifier(uniqueKey, StatType.MoveSpeed, slowAmount, duration);
                }

                Debug.Log($"Player hit by Pulso Carnal! Slowed by {_pulseSlowPercentage * 100}% for {_pulseSlowDuration} seconds and took {_pulseDamage} damage.");
            }
        }
    }

    private IEnumerator DesactivePulseEffect(Collider hitCollider)
    {
        yield return new WaitForSeconds(_pulseSlowDuration);

        PlayerMovement playerMovement = hitCollider.GetComponent<PlayerMovement>();

        if (playerMovement != null)
        {
            playerMovement.IsDashDisabled = false;
        }
    }

    public void ShakeCamera(float duration, float amplitude, float frequency)
    {
        if (_noise == null) return;
        StartCoroutine(ShakeRoutine(duration, amplitude, frequency));
    }

    private IEnumerator ShakeRoutine(float duration, float amplitude, float frequency)
    {
        _noise.AmplitudeGain = amplitude;
        _noise.FrequencyGain = frequency;

        yield return new WaitForSeconds(duration);

        _noise.AmplitudeGain = 0f;
        _noise.FrequencyGain = 0f;
    }
    #endregion

    private void OnDrawGizmos()
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

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 5f);
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, _smashDetectionRadius);
        }
    }
}