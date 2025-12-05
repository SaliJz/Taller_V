using System.Collections;
using System.Collections.Generic;
using DG.Tweening.Plugins.Options;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.EventSystems.EventTrigger;

// DATA STRUCTURES
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
    #region Enums
    public enum BossState
    {
        Moving,
        Attacking,
        SpecialAbility
    }

    private enum ComboType
    {
        None,
        WhipWhipSmash,
        SmashWhip,
        WhipCircle
    }
    #endregion

    // CONFIGURATION & VARIABLES
    #region State & General Settings

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;
    private ComboType _currentCombo = ComboType.None;

    [Header("Player Settings")]
    [SerializeField] private Transform _player;

    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    private int _specialAbilityUsedCount = 0;
    private EnemyHealth _enemyHealth;

    #endregion

    #region Movement Settings

    [Header("Movement Settings")]
    [SerializeField] private float _stoppingDistance = 15f;
    [SerializeField] private float _safeDistance = 2.5f;
    private NavMeshAgent _navMeshAgent;
    [SerializeField] private Animator _animator;

    #endregion

    #region Ability: Defensive Stomp

    [Header("Defense Mechanism: Backstep Stomp")]
    [SerializeField] private float _stompTriggerDistance = 5f;
    [SerializeField] private float _knockbackDistance = 5f;
    [SerializeField] private float _stompCooldown = 8f;
    [SerializeField] private bool _enableStompDamage = true;
    [SerializeField] private float _stompDamage = 15f;
    [SerializeField] private float _stompRadius = 5f;
    [SerializeField] private float _stompTelegraphTime = 0.6f;
    [SerializeField] private GameObject _stompVFXPrefab;
    private float _stompTimer = 0f;

    #endregion

    #region Ability: Attack 1 (Whip)

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
    private Collider[] _whipHitBuffer = new Collider[5];

    #endregion

    #region Ability: Attack 2 (Smash)

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
    private Vector3 _lastPlayerPosition;
    private Vector3 _lastSmashOverlapCenter;
    private float _lastSmashOverlapRadius;
    private bool _showSmashOverlapGizmo;

    #endregion

    #region Ability: Special (Pulso Carnal)

    [Header("Special Ability: Pulso Carnal")]
    [SerializeField] private float _pulseExpansionDuration = 3f;
    [SerializeField] private float _pulseWaitDuration = 1f;
    [SerializeField] private float _pulseSlowPercentage = 0.5f;
    [SerializeField] private float _pulseSlowDuration = 2f;
    [SerializeField] private int _pulseDamage = 5;
    [SerializeField] private GameObject _nervesVisualizationPrefab;
    [SerializeField] private GameObject _crackEffectPrefab;
    [SerializeField] private float _postPulseAttackDelay = 0.8f;
    [SerializeField] private Transform _headsTransform;
    [SerializeField] private float _headDownRotationAngle = -45f;
    [SerializeField] private float _headAnimationDuration = 0.5f;
    [SerializeField] private float _roomMaxRadius = 45f;
    [SerializeField] private bool _calculateRoomRadiusOnStart = true;
    [SerializeField] private float _movementSpeedForPulse = 10f;

    [Header("Evolución Pulso Carnal")]
    [SerializeField] private float _speedBuffPerPulse = 0.20f; // 20%
    private float _currentEvolutionMultiplier = 1.0f;

    private bool _isUsingSpecialAbility;
    private float[] _healthThresholdsForPulse = { 0.67f, 0.34f };
    private bool _isPulseAttackBlocked = false;
    private bool _isSpecialAbilityPending = false;
    private List<GameObject> _instantiatedEffects = new List<GameObject>();
    private Vector3 _roomCenter = Vector3.zero;

    #endregion

    #region Visuals (Telegraphs & VFX)

    [Header("Attack Telegraphs")]
    [SerializeField] private GameObject _stompWarningPrefab;
    [SerializeField] private GameObject _whipTelegraphPrefab;
    [SerializeField] private GameObject _smashTelegraphPrefab;
    [SerializeField] private GameObject _smashWarningPrefab;
    [SerializeField] private float _telegraphDuration = 1f;

    [Header("VFX")]
    [SerializeField] private TrailRenderer _trailRenderer;

    [Header("Dodge Feedback")]
    [SerializeField] private GameObject _dodgeIndicatorPrefab;
    [SerializeField] private float _dodgeIndicatorDuration = 1.5f;

    #endregion

    #region Combat Analytics

    [Header("Combat Analytics")]
    [SerializeField] private bool _enableAdaptiveDifficulty = true;
    [SerializeField] private float _difficultyAdjustmentInterval = 15f;
    [SerializeField] private float _comboCheckInterval = 8f;
    [SerializeField] private float _comboChance = 0.3f;
    private float _comboTimer = 0f;

    private int _totalAttemptsLanded = 0;
    private int _totalAttemptsExecuted = 0;
    private int _hitsReceivedFromPlayer = 0;
    private float _difficultyTimer = 0f;
    private float _currentDifficultyMultiplier = 1f;
    private bool _lastWhipHitPlayer = false;
    private bool _lastSmashHitPlayer = false;

    #endregion

    #region Enraged Phase

    [Header("Enraged Phase (25% HP)")]
    [SerializeField] private float _enragedHealthThreshold = 0.25f;
    [SerializeField] private Renderer[] _eyeRenderers;
    [SerializeField] private Material _enragedEyeMaterial;
    private bool _isEnraged = false;
    private float _attackSpeedMultiplier = 1f;
    private float _baseAttack1Cooldown;
    private float _baseAttack2Cooldown;
    private float _basePulseExpansionDuration;
    private float _basePulseWaitDuration;
    private Material[] _originalEyeMaterials;

    #endregion

    #region Audio & Camera

    [Header("SFX - Astaroth")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip presenceSFX;
    [SerializeField] private AudioClip walkSFX;
    [SerializeField] private AudioClip whipAttackSFX;
    [SerializeField] private AudioClip smashAttackSFX;
    [SerializeField] private AudioClip pulseAttackSFX;
    [SerializeField] private AudioClip pulseRocksSFX;
    [SerializeField] private AudioClip stompSFX;
    [SerializeField] private AudioClip phase2RageSFX;
    [SerializeField] private AudioClip damageReceivedSFX;
    [SerializeField] private AudioClip deathSFX;

    // Audio Interno
    private float _audioIdleTimer;
    private float _audioIdleInterval;
    private float _audioStepTimer;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineCamera _vcam;
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _amplitude = 2f;
    [SerializeField] private float _frequency = 2f;
    private CinemachineBasicMultiChannelPerlin _noise;

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool _showRoomGizmos = true;

    #endregion

    #region Animation Hashes

    private static readonly int AnimID_IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int AnimID_IsDeath = Animator.StringToHash("IsDeath");

    private static readonly int AnimID_Attack = Animator.StringToHash("Attack");
    private static readonly int AnimID_ExitSA = Animator.StringToHash("ExitSA");

    private const int ATTACK_NONE = 0;
    private const int ATTACK_WHIP = 1;
    private const int ATTACK_SMASH = 2;
    private const int ATTACK_SPECIAL = 3;

    #endregion

    // UNITY LIFECYCLE
    #region Unity Lifecycle

    private void Awake()
    {
        if (_enemyHealth == null) _enemyHealth = GetComponent<EnemyHealth>();
        if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

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

        if (_player != null)
        {
            _lastPlayerPosition = _player.position;
        }

        if (_enemyHealth != null) _enemyHealth.SetMaxHealth(_maxHealth);
        _currentHealth = _maxHealth;

        if (_vcam != null) _noise = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;

        _navMeshAgent.stoppingDistance = Mathf.Max(_stoppingDistance, _safeDistance);
    }

    private void OnEnable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnDeath += HandleEnemyDeath;
            _enemyHealth.OnHealthChanged += HandleEnemyHealthChange;
            _enemyHealth.OnDamaged += HandleDamageReceived;
        }
    }

    private void OnDisable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnDeath -= HandleEnemyDeath;
            _enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
            _enemyHealth.OnDamaged -= HandleDamageReceived;
        }
    }

    private void OnDestroy()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnDeath -= HandleEnemyDeath;
            _enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
            _enemyHealth.OnDamaged -= HandleDamageReceived;
        }
    }

    private void Update()
    {
        if (_player == null) return;

        // Comprobar aturdimiento
        if (_enemyHealth != null && _enemyHealth.IsStunned)
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
            }
            return;
        }

        // Actualizaciones del sistema
        HandleAudioLoop();
        CheckHealthThresholds();

        // Dificultad adaptativa
        if (_enableAdaptiveDifficulty)
        {
            _difficultyTimer += Time.deltaTime;
            if (_difficultyTimer >= _difficultyAdjustmentInterval)
            {
                AdjustDifficultyBasedOnPerformance();
                _difficultyTimer = 0f;
            }
        }

        // State Machine
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

    // CORE LOGIC & STATE MACHINE
    #region Core Logic

    private void HandleMovement()
    {
        if (_isUsingSpecialAbility)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        _navMeshAgent.updateRotation = true;

        // Cooldowns
        _attack1Timer -= Time.deltaTime;
        _attack2Timer -= Time.deltaTime;
        _comboTimer -= Time.deltaTime;
        _stompTimer -= Time.deltaTime;

        if (_animator != null)
        {
            bool isMoving = _navMeshAgent.velocity.magnitude > 0.1f && !_navMeshAgent.isStopped;
            _animator.SetBool(AnimID_IsRunning, isMoving);
        }

        // Comprobar el pisoton defensivo
        if (distanceToPlayer < _stompTriggerDistance && _stompTimer <= 0f)
        {
            if (TryExecuteDefensiveStomp())
            {
                return;
            }
        }

        if (_currentState != BossState.Moving) return;
        if (_isPulseAttackBlocked) return;

        // Comprobar Combo
        if (_currentCombo == ComboType.None && _comboTimer <= 0f)
        {
            if (Random.value < _comboChance)
            {
                DecideCombo();
            }
            _comboTimer = _comboCheckInterval;
        }

        if (_currentCombo != ComboType.None)
        {
            _currentState = BossState.Attacking;
            StartCoroutine(ExecuteCombo(_currentCombo));
            _currentCombo = ComboType.None;
            return;
        }

        // Comprobar ataques
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
            // Movimiento estandar
            if (distanceToPlayer > _stoppingDistance)
            {
                _navMeshAgent.isStopped = false;
                _navMeshAgent.SetDestination(_player.position);
            }
            else
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.velocity = Vector3.zero;
                LookAtPlayer();
            }
        }
    }

    private void HandleAttacks()
    {
        if (_isUsingSpecialAbility)
        {
            _isAttackingWithWhip = false;
            _isSmashing = false;
            _navMeshAgent.isStopped = true;
            _currentState = BossState.SpecialAbility;
            return;
        }

        if (!_isAttackingWithWhip && !_isSmashing)
        {
            _navMeshAgent.isStopped = false;
            _currentState = BossState.Moving;
        }
    }

    private void DecideCombo()
    {
        if (_isEnraged)
        {
            int random = Random.Range(0, 100);
            if (random < 40) _currentCombo = ComboType.WhipWhipSmash;
            else if (random < 70) _currentCombo = ComboType.SmashWhip;
            else _currentCombo = ComboType.None;
        }
        else
        {
            if (Random.Range(0, 100) < 20) _currentCombo = ComboType.WhipWhipSmash;
        }
    }

    private IEnumerator ExecuteCombo(ComboType combo)
    {
        Debug.Log($"<color=cyan>[Astaroth] Executing Combo: {combo}</color>");

        switch (combo)
        {
            case ComboType.WhipWhipSmash:
                yield return StartCoroutine(QuickWhipAttack());
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(QuickWhipAttack());
                yield return new WaitForSeconds(0.2f);
                yield return StartCoroutine(SmashAttackSequence());
                break;

            case ComboType.SmashWhip:
                yield return StartCoroutine(SmashAttackSequence());
                yield return new WaitForSeconds(0.4f);
                yield return StartCoroutine(QuickWhipAttack());
                break;
        }

        _attack1Timer = _attack1Cooldown;
        _attack2Timer = _attack2Cooldown;

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _currentCombo = ComboType.None;

        if (CheckForPendingSpecialAbility()) yield break;

        _currentState = BossState.Moving;
        _navMeshAgent.isStopped = false;

        Debug.Log($"<color=cyan>[Astaroth] Combo finished, returning to Moving state</color>");
    }

    #endregion

    // HEALTH & PHASE MANAGEMENT
    #region Health & Phases

    private void HandleDamageReceived()
    {
        if (audioSource != null && damageReceivedSFX != null && _currentHealth > 0)
        {
            audioSource.PlayOneShot(damageReceivedSFX);
        }

        if (_isUsingSpecialAbility) return;

        _hitsReceivedFromPlayer++;

        if (_hitsReceivedFromPlayer % 5 == 0 && !_isAttackingWithWhip && !_isSmashing)
        {
            Debug.Log($"<color=orange>[Astaroth] Rage triggered after {_hitsReceivedFromPlayer} hits!</color>");

            if (Vector3.Distance(transform.position, _player.position) <= _whipRange)
            {
                _currentState = BossState.Attacking;
                StartCoroutine(QuickCounterAttack());
            }
        }
    }

    private void HandleEnemyHealthChange(float newCurrentHealth, float newMaxHealth)
    {
        _currentHealth = newCurrentHealth;
        _maxHealth = newMaxHealth;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _isUsingSpecialAbility = false;
        _isEnraged = false;

        if (AsyncMusicController.Instance != null)
        {
            AsyncMusicController.Instance.PlayMusic(MusicState.Calm);
        }

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

        if (_animator != null)
        {
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
            _animator.SetBool(AnimID_IsRunning, false);
            _animator.SetBool(AnimID_IsDeath, true);
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void CheckHealthThresholds()
    {
        if (_isUsingSpecialAbility || _isSpecialAbilityPending) return;

        float healthPercentage = _currentHealth / _maxHealth;

        if (_specialAbilityUsedCount < _healthThresholdsForPulse.Length)
        {
            if (healthPercentage <= _healthThresholdsForPulse[_specialAbilityUsedCount])
            {
                if (_isAttackingWithWhip || _isSmashing || _currentCombo != ComboType.None)
                {
                    _isSpecialAbilityPending = true;
                    Debug.Log($"<color=magenta>[Astaroth] Pulso Carnal Threshold Reached. Waiting for current attack/combo to finish...</color>");
                    return;
                }

                StopAllCoroutines();

                _isAttackingWithWhip = false;
                _isSmashing = false;
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();

                if (_whipVisualTransform != null) _whipVisualTransform.localPosition = _whipAnimationKeyframes[0].Position;
                if (_trailRenderer != null) _trailRenderer.enabled = false;

                _currentState = BossState.SpecialAbility;
                StartCoroutine(PulsoCarnal());
                _specialAbilityUsedCount++;

                Debug.Log($"<color=magenta>[Astaroth] Starting Pulso Carnal!</color>");
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
        _attackSpeedMultiplier = 2.5f;
        _stoppingDistance = 20f;
        _navMeshAgent.speed = 5f;

        // Actualizar cooldowns de ataques
        _attack1Cooldown = _baseAttack1Cooldown / _attackSpeedMultiplier;
        _attack2Cooldown = _baseAttack2Cooldown / _attackSpeedMultiplier;

        _pulseExpansionDuration = 1.5f; // Reducido de 3s
        _pulseWaitDuration = 0.5f; // Reducido de 1s

        if (phase2RageSFX != null) AudioSource.PlayClipAtPoint(phase2RageSFX, transform.position);

        Debug.Log("Astaroth entered ENRAGED phase!");
    }

    private void AdjustDifficultyBasedOnPerformance()
    {
        if (_totalAttemptsExecuted == 0) return;

        float hitRate = (float)_totalAttemptsLanded / _totalAttemptsExecuted;
        float healthPercent = _currentHealth / _maxHealth;

        Debug.Log($"[Astaroth Analytics] Hit Rate: {hitRate:P0} | Health: {healthPercent:P0} | Attempts: {_totalAttemptsLanded}/{_totalAttemptsExecuted}");

        if (hitRate < 0.2f && healthPercent > 0.5f)
        {
            _currentDifficultyMultiplier = Mathf.Min(_currentDifficultyMultiplier + 0.15f, 2.5f);
            _attack1Cooldown = _baseAttack1Cooldown / _currentDifficultyMultiplier;
            _attack2Cooldown = _baseAttack2Cooldown / _currentDifficultyMultiplier;

            if (_navMeshAgent != null) _navMeshAgent.speed = Mathf.Min(_navMeshAgent.speed + 0.5f, 7f);

            Debug.Log($"<color=red>[Astaroth] DIFFICULTY INCREASED! Multiplier: {_currentDifficultyMultiplier:F2}</color>");
        }
        else if (hitRate > 0.6f && healthPercent > 0.3f)
        {
            _currentDifficultyMultiplier = Mathf.Max(_currentDifficultyMultiplier - 0.1f, 0.7f);
            _attack1Cooldown = _baseAttack1Cooldown / _currentDifficultyMultiplier;
            _attack2Cooldown = _baseAttack2Cooldown / _currentDifficultyMultiplier;

            Debug.Log($"<color=yellow>[Astaroth] Difficulty slightly decreased. Multiplier: {_currentDifficultyMultiplier:F2}</color>");
        }
        else
        {
            Debug.Log($"<color=green>[Astaroth] Difficulty balanced. Multiplier: {_currentDifficultyMultiplier:F2}</color>");
        }

        _totalAttemptsLanded = 0;
        _totalAttemptsExecuted = 0;
    }

    #endregion

    // COMBAT LOGIC: ATTACKS
    #region Attack 1 Logic (Whip)

    private IEnumerator WhipAttackSequence()
    {
        _isAttackingWithWhip = true;
        _showWhipImpactGizmo = false;
        _navMeshAgent.isStopped = true;

        if (_animator != null) _animator.SetInteger(AnimID_Attack, ATTACK_WHIP);

        float anticipationTime = 0.8f;
        yield return StartCoroutine(LookAtPlayerSmoothCoroutine(anticipationTime, 120f));

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.enabled = true;
        }

        for (int i = 0; i < _whipAttackCount; i++)
        {
            _totalAttemptsExecuted++;
            _lastWhipHitPlayer = false;

            if (audioSource != null && whipAttackSFX != null) audioSource.PlayOneShot(whipAttackSFX);

            bool damageDealtThisWhip = false;

            // Calculo de posicion del jugador
            Vector3 rayOrigin = _whipVisualTransform.position;
            Vector3 targetPosCandidate = PredictPlayerPosition(0.1f);
            Vector3 directionToTarget = (targetPosCandidate - rayOrigin).normalized;

            // Ajustar distancia al rango máximo
            float distance = Vector3.Distance(rayOrigin, targetPosCandidate);
            Vector3 finalTargetPoint = (distance > _whipRange) ? rayOrigin + (directionToTarget * _whipRange) : targetPosCandidate;

            _whipTargetPoint = finalTargetPoint;
            _showWhipImpactGizmo = true;

            for (int k = 0; k < _whipAnimationKeyframes.Length - 1; k++)
            {
                WhipKeyframe startKeyframe = _whipAnimationKeyframes[k];
                WhipKeyframe endKeyframe = _whipAnimationKeyframes[k + 1];

                if (endKeyframe.IsTargetable)
                {
                    _lastWhipRaycastOrigin = rayOrigin;
                    _lastWhipRaycastDirection = targetPosCandidate;
                    _showWhipRaycastGizmo = true;

                    int layerMask = LayerMask.GetMask("Obstacle", "Wall");
                    RaycastHit hit;

                    if (Physics.Raycast(rayOrigin, directionToTarget, out hit, distance, layerMask))
                    {
                        _whipTargetPoint = hit.point;
                    }

                    _whipImpactPoint = _whipTargetPoint;
                    _showWhipImpactGizmo = true;

                    endKeyframe.Position = transform.InverseTransformPoint(_whipTargetPoint);
                }

                // Tiempo de duración por ataque
                float segmentDuration = Random.Range((endKeyframe.Time - startKeyframe.Time) * 0.65f, (endKeyframe.Time - startKeyframe.Time));
                float startTime = Time.time;

                while (Time.time < startTime + segmentDuration)
                {
                    float timer = (Time.time - startTime) / segmentDuration;
                    _whipVisualTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, timer);

                    if (!damageDealtThisWhip && endKeyframe.IsTargetable)
                    {
                        int hitCount = Physics.OverlapSphereNonAlloc(_whipVisualTransform.position, 1.5f, _whipHitBuffer, LayerMask.GetMask("Player"));

                        for (int j = 0; j < hitCount; j++)
                        {
                            Collider collider = _whipHitBuffer[j];
                            if (collider != null)
                            {
                                ExecuteAttack(collider.gameObject, _whipVisualTransform.position, _Attack1Damage);
                                damageDealtThisWhip = true;
                                _lastWhipHitPlayer = true;
                                _totalAttemptsLanded++;

                                Debug.Log($"<color=red>[Astaroth] Whip HIT! Distance: {Vector3.Distance(_whipVisualTransform.position, _player.position):F2}m</color>");
                                break;
                            }
                        }
                    }
                    yield return null;
                }

                _whipVisualTransform.localPosition = endKeyframe.Position;
            }

            if (!_lastWhipHitPlayer)
            {
                Debug.Log($"<color=green>[Player] Dodged Whip Attack #{i + 1}!</color>");
                ShowDodgeIndicator();
            }

            float pauseTime = Random.Range(0.5f, 0.75f);

            // Pequeña pausa entre latigazos
            yield return new WaitForSeconds(pauseTime);
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }

        if (_animator != null) _animator.SetInteger(AnimID_Attack, ATTACK_NONE);

        _isAttackingWithWhip = false;
        _showWhipRaycastGizmo = false;
        _showWhipImpactGizmo = false;
        _whipVisualTransform.localPosition = _whipAnimationKeyframes[0].Position;

        if (CheckForPendingSpecialAbility()) yield break;

        _currentState = BossState.Moving;
        _navMeshAgent.isStopped = false;
    }

    private IEnumerator QuickCounterAttack()
    {
        _isAttackingWithWhip = true;
        _navMeshAgent.isStopped = true;
        _totalAttemptsExecuted++;

        Debug.Log($"<color=orange>[Astaroth] COUNTER ATTACK!</color>");

        if (_whipTelegraphPrefab != null)
        {
            Vector3 rayOrigin = _whipVisualTransform.position;
            Vector3 directionToPlayer = (_player.position - rayOrigin).normalized;
            Vector3 targetPoint = rayOrigin + directionToPlayer * _whipRange;

            StartCoroutine(ShowWhipTelegraph(rayOrigin, targetPoint));
        }

        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(QuickWhipAttack());

        _isAttackingWithWhip = false;
    }

    private IEnumerator QuickWhipAttack()
    {
        _isAttackingWithWhip = true;
        _totalAttemptsExecuted++;
        _lastWhipHitPlayer = false;

        if (audioSource != null && whipAttackSFX != null) audioSource.PlayOneShot(whipAttackSFX);

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.enabled = true;
        }

        LookAtPlayer();

        Vector3 rayOrigin = _whipVisualTransform.position;
        Vector3 predictedPlayerPos = PredictPlayerPosition(0.3f);
        Vector3 directionToPlayer = (predictedPlayerPos - rayOrigin).normalized;

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

        bool damageDealt = false;
        HashSet<Collider> hitColliders = new HashSet<Collider>();

        for (int k = 0; k < _whipAnimationKeyframes.Length - 1; k++)
        {
            WhipKeyframe startKeyframe = _whipAnimationKeyframes[k];
            WhipKeyframe endKeyframe = _whipAnimationKeyframes[k + 1];

            if (endKeyframe.IsTargetable)
            {
                endKeyframe.Position = transform.InverseTransformPoint(_whipTargetPoint);
            }

            float segmentDuration = (endKeyframe.Time - startKeyframe.Time) * 0.6f;
            float startTime = Time.time;

            while (Time.time < startTime + segmentDuration)
            {
                float t = (Time.time - startTime) / segmentDuration;
                _whipVisualTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, t);

                if (!damageDealt && endKeyframe.IsTargetable)
                {
                    Vector3 currentWhipWorldPos = _whipVisualTransform.position;
                    Collider[] nearbyColliders = Physics.OverlapSphere(currentWhipWorldPos, 1.5f, LayerMask.GetMask("Player"));

                    foreach (Collider col in nearbyColliders)
                    {
                        if (col.CompareTag("Player") && !hitColliders.Contains(col))
                        {
                            hitColliders.Add(col);
                            PlayerHealth playerHealth = col.GetComponent<PlayerHealth>();
                            if (playerHealth != null)
                            {
                                ExecuteAttack(col.gameObject, rayOrigin, _Attack1Damage * 0.8f);
                                damageDealt = true;
                                _lastWhipHitPlayer = true;
                                _totalAttemptsLanded++;
                                Debug.Log($"<color=orange>[Astaroth] Quick Whip HIT!</color>");
                                break;
                            }
                        }
                    }
                }
                yield return null;
            }
            _whipVisualTransform.localPosition = endKeyframe.Position;
        }

        if (!_lastWhipHitPlayer)
        {
            Debug.Log($"<color=green>[Player] Dodged Quick Whip!</color>");
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }

        _whipVisualTransform.localPosition = _whipAnimationKeyframes[0].Position;
        _isAttackingWithWhip = false;
    }

    #endregion

    #region Attack 2 Logic (Smash)

    private IEnumerator SmashAttackSequence()
    {
        _isSmashing = true;
        _showSmashOverlapGizmo = false;
        _navMeshAgent.isStopped = false;

        if (_animator != null) _animator.SetInteger(AnimID_Attack, ATTACK_SMASH);

        float rotationTime = 0f;
        Vector3 initialTargetPos = _player.position;

        while (rotationTime < _smashDelay)
        {
            LookAtPlayer();

            if (Vector3.Distance(transform.position, _player.position) > 3f)
            {
                _navMeshAgent.SetDestination(_player.position);
            }
            else
            {
                _navMeshAgent.isStopped = true;
            }

            rotationTime += Time.deltaTime;
            yield return null;
        }

        _navMeshAgent.isStopped = true;
        _smashTargetPoint = _player.position;

        SpawnGroundTelegraph(_smashWarningPrefab, _smashTargetPoint, _smashRadius, 1.2f);

        yield return new WaitForSeconds(0.5f);

        if (audioSource != null && smashAttackSFX != null) audioSource.PlayOneShot(smashAttackSFX);

        HashSet<GameObject> hitByDirectImpact = new HashSet<GameObject>();

        for (int k = 0; k < _smashAnimationKeyframes.Length - 1; k++)
        {
            SmashKeyframe startKeyframe = _smashAnimationKeyframes[k];
            SmashKeyframe endKeyframe = _smashAnimationKeyframes[k + 1];

            if (endKeyframe.IsTargetable)
            {
                endKeyframe.Position = transform.InverseTransformPoint(_smashTargetPoint);
            }

            float segmentDuration = endKeyframe.Time - startKeyframe.Time;
            if (segmentDuration > 0)
            {
                float startTime = Time.time;
                while (Time.time < startTime + segmentDuration)
                {
                    float t = (Time.time - startTime) / segmentDuration;
                    _smashVisualTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, t);
                    _smashVisualTransform.localScale = Vector3.Lerp(startKeyframe.Scale, endKeyframe.Scale, t);

                    CheckDirectRockImpact(hitByDirectImpact);

                    yield return null;
                }
            }

            _smashVisualTransform.localPosition = endKeyframe.Position;
            _smashVisualTransform.localScale = endKeyframe.Scale;

            if (endKeyframe.IsTargetable)
            {
                PerformSmashDamage(_smashTargetPoint, hitByDirectImpact);
            }
        }

        if (_animator != null) _animator.SetInteger(AnimID_Attack, ATTACK_NONE);

        _isSmashing = false;
        _showSmashOverlapGizmo = false;
        _smashVisualTransform.localPosition = _smashAnimationKeyframes[0].Position;
        _smashVisualTransform.localScale = _smashAnimationKeyframes[0].Scale;

        if (CheckForPendingSpecialAbility()) yield break;

        _currentState = BossState.Moving;
        _navMeshAgent.isStopped = false;
    }

    private void CheckDirectRockImpact(HashSet<GameObject> alreadyHit)
    {
        Vector3 rockWorldPosition = _smashVisualTransform.position;
        float rockRadius = _smashVisualTransform.localScale.x * 0.5f;

        Collider[] nearbyColliders = Physics.OverlapSphere(rockWorldPosition, rockRadius);

        foreach (var col in nearbyColliders)
        {
            GameObject entity = col.gameObject;

            if (entity.CompareTag("Player"))
            {
                GameObject playerRoot = entity.transform.root.gameObject;

                if (alreadyHit.Contains(playerRoot))
                {
                    continue;
                }

                alreadyHit.Add(playerRoot);

                PlayerHealth playerHealth = playerRoot.GetComponent<PlayerHealth>();
                if (playerHealth == null) playerHealth = entity.GetComponent<PlayerHealth>();

                if (playerHealth != null)
                {
                    ExecuteAttack(playerRoot, rockWorldPosition, _Attack2Damage);
                    _totalAttemptsExecuted++;
                    _totalAttemptsLanded++;
                    Debug.Log($"<color=red>[Astaroth] Direct Rock HIT! Damage: {_Attack2Damage}</color>");
                }
            }
        }
    }

    private void PerformSmashDamage(Vector3 damageCenter, HashSet<GameObject> alreadyHitByRock)
    {
        _totalAttemptsExecuted++;
        _lastSmashHitPlayer = false;

        _lastSmashOverlapCenter = damageCenter;
        _lastSmashOverlapRadius = _smashRadius;
        _showSmashOverlapGizmo = true;

        Vector3 smashGroundPosition = GetGroundPosition(damageCenter);
        smashGroundPosition.x = damageCenter.x;
        smashGroundPosition.z = damageCenter.z;
        smashGroundPosition.y += 0.1f;

        if (_smashRadiusPrefab != null)
        {
            GameObject visualEffect = Instantiate(_smashRadiusPrefab, smashGroundPosition, Quaternion.identity);
            _instantiatedEffects.Add(visualEffect);
            StartCoroutine(ExpandSmashRadiusWithDamage(visualEffect.transform, _smashRadius, smashGroundPosition, alreadyHitByRock));
        }

        if (!_lastSmashHitPlayer)
        {
            Debug.Log($"<color=green>[Player] Dodged Smash Attack!</color>");
            ShowDodgeIndicator();
        }

        Invoke("DisableSmashOverlapGizmo", 1f);
    }

    private void DisableSmashOverlapGizmo()
    {
        _showSmashOverlapGizmo = false;
    }

    private IEnumerator ExpandSmashRadiusWithDamage(Transform effectTransform, float targetRadius, Vector3 groundPosition, HashSet<GameObject> alreadyHitByRock)
    {
        float duration = 0.5f;
        float elapsedTime = 0f;
        Vector3 initialScale = Vector3.zero;
        float fixedYScale = 0.5f;
        Vector3 targetScale = new Vector3(targetRadius * 2, fixedYScale, targetRadius * 2);

        HashSet<GameObject> hitByShockwaveEntity = new HashSet<GameObject>();

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            Vector3 currentScale = Vector3.Lerp(initialScale, targetScale, t);
            currentScale.y = fixedYScale;
            effectTransform.localScale = currentScale;

            float currentRadius = Mathf.Lerp(0f, targetRadius, t);

            Collider[] hitColliders = Physics.OverlapSphere(groundPosition, currentRadius * 1.2f);

            foreach (var hitCollider in hitColliders)
            {
                GameObject entity = hitCollider.transform.root.gameObject;

                if (alreadyHitByRock.Contains(entity) || hitByShockwaveEntity.Contains(entity))
                {
                    continue;
                }

                if (entity.CompareTag("Player"))
                {
                    float heightDifference = Mathf.Abs(entity.transform.position.y - groundPosition.y);

                    if (heightDifference < 2f)
                    {
                        float distanceFromCenter = Vector3.Distance(entity.transform.position, groundPosition);

                        if (distanceFromCenter <= currentRadius)
                        {
                            hitByShockwaveEntity.Add(entity);

                            ExecuteAttack(entity, groundPosition, _Attack2Damage);
                            ApplySafeKnockback(entity, groundPosition, 10f);
                            _lastSmashHitPlayer = true;
                            _totalAttemptsLanded++;

                            Debug.Log($"<color=red>[Astaroth] Shockwave HIT! Distance: {distanceFromCenter:F2}m</color>");
                        }
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>[Player] Avoided Shockwave by jumping! Height diff: {heightDifference:F2}m</color>");
                    }
                }
            }

            yield return null;
        }

        effectTransform.localScale = targetScale;
        Destroy(effectTransform.gameObject, 0.5f);
    }

    #endregion

    #region Special Ability: Pulso Carnal

    private bool CheckForPendingSpecialAbility()
    {
        if (_isSpecialAbilityPending)
        {
            Debug.Log($"<color=magenta>[Astaroth] Pending Pulso Carnal found. Transitioning to SpecialAbility state.</color>");

            _isAttackingWithWhip = false;
            _isSmashing = false;
            _currentCombo = ComboType.None;
            _isSpecialAbilityPending = false;

            if (_whipVisualTransform != null) _whipVisualTransform.localPosition = _whipAnimationKeyframes[0].Position;
            if (_trailRenderer != null) _trailRenderer.enabled = false;

            _currentState = BossState.SpecialAbility;
            StartCoroutine(PulsoCarnal());
            _specialAbilityUsedCount++;

            return true;
        }
        return false;
    }

    private IEnumerator PulsoCarnal()
    {
        _isUsingSpecialAbility = true;

        if (_animator != null) _animator.SetInteger(AnimID_Attack, ATTACK_SPECIAL);

        // Moverse al centro
        yield return StartCoroutine(MoveToCenter(_roomCenter));

        _navMeshAgent.isStopped = true;
        Vector3 groundPos = GetGroundPosition(transform.position);

        if (_headsTransform != null)
        {
            yield return StartCoroutine(AnimateHeadDown());
        }

        // Esperar breve momento antes del pulso
        if (_nervesVisualizationPrefab != null)
        {
            GameObject pulseObj = Instantiate(_nervesVisualizationPrefab, groundPos, Quaternion.identity);

            FleshPulseController pulseController = pulseObj.GetComponent<FleshPulseController>();

            if (pulseController != null)
            {
                pulseController.Initialize(
                    _roomMaxRadius,
                    _pulseExpansionDuration,
                    _pulseDamage,
                    _pulseSlowPercentage,
                    _pulseSlowDuration
                );
            }
            else
            {
                Debug.LogWarning("El prefab asignado a _nervesVisualizationPrefab no tiene el script FleshPulseController.");
            }

            _instantiatedEffects.Add(pulseObj);
        }

        yield return new WaitForSeconds(_pulseExpansionDuration + _pulseWaitDuration);

        if (_headsTransform != null)
        {
            StartCoroutine(AnimateHeadUp());
        }

        ShakeCamera(_shakeDuration, _amplitude, _frequency);

        if (pulseAttackSFX != null) AudioSource.PlayClipAtPoint(pulseAttackSFX, transform.position);
        if (pulseRocksSFX != null) AudioSource.PlayClipAtPoint(pulseRocksSFX, transform.position);

        if (_crackEffectPrefab != null)
        {
            GameObject crackEffect = Instantiate(_crackEffectPrefab, groundPos, Quaternion.identity, null);
            _instantiatedEffects.Add(crackEffect);
            Destroy(crackEffect, 2f);
        }

        ApplyEvolutionBuff(); // Aplicar buff de evolución

        _attack1Timer = 0f;
        _attack2Timer = 5f;

        StartCoroutine(BlockAttacksAfterPulse());

        if (_animator != null)
        {
            _animator.SetBool(AnimID_ExitSA, true);
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
        }

        yield return new WaitForSeconds(1f);

        if (_animator != null) _animator.SetBool(AnimID_ExitSA, false);

        _isUsingSpecialAbility = false;
        _currentState = BossState.Moving;
    }

    private void ApplyEvolutionBuff()
    {
        // Aumentar multiplicador base (1.0 -> 1.2 -> 1.4)
        _currentEvolutionMultiplier += _speedBuffPerPulse;

        // Aumentar velocidad de movimiento
        if (_navMeshAgent != null)
        {
            _navMeshAgent.speed *= (1f + _speedBuffPerPulse);
        }

        // Aumentar velocidad de animación
        if (_animator != null)
        {
            _animator.speed = _currentEvolutionMultiplier;
        }

        // Reducir Cooldowns (Más agresivo)
        _attack1Cooldown = _baseAttack1Cooldown / _currentEvolutionMultiplier;
        _attack2Cooldown = _baseAttack2Cooldown / _currentEvolutionMultiplier;

        Debug.Log($"<color=purple>[Astaroth] EVOLUCIÓN: Velocidad aumentada un 20%. Total: {_currentEvolutionMultiplier}x</color>");
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
        _attack2Timer = Mathf.Max(_attack2Timer, 2f);
        yield return new WaitForSeconds(_postPulseAttackDelay);
        _isPulseAttackBlocked = false;
    }

    private void CalculateRoomRadius()
    {
        // Lanza un rayo hacia abajo para detectar en que suelo esta parado
        RaycastHit hit;
        int groundLayer = LayerMask.GetMask("Ground");

        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 10f, groundLayer))
        {
            Collider floorCollider = hit.collider;

            _roomCenter = floorCollider.bounds.center;
            _roomCenter.y = transform.position.y;

            float calculatedRadius = Mathf.Max(floorCollider.bounds.extents.x, floorCollider.bounds.extents.z);

            // Si _calculateRoomRadiusOnStart es true, sobrescribimos el valor del inspector
            if (_calculateRoomRadiusOnStart)
            {
                _roomMaxRadius = Mathf.Max(5f, calculatedRadius - 2f);
                Debug.Log($"[Astaroth] Sala detectada: {floorCollider.name} | Centro: {_roomCenter} | Radio calculado: {_roomMaxRadius}m");
            }
            else
            {
                Debug.Log($"[Astaroth] Sala detectada: {floorCollider.name} | Usando radio manual: {_roomMaxRadius}m");
            }
        }
        else
        {
            _roomCenter = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            if (_calculateRoomRadiusOnStart) _roomMaxRadius = 25f;
            Debug.LogWarning("[Astaroth] No se detectó suelo bajo el Boss (Layer 'Ground'). Usando posición actual como centro.");
        }
    }

    #endregion

    #region Defensive Stomp

    private bool TryExecuteDefensiveStomp()
    {
        Debug.Log($"<color=green>[Astaroth] Pisotón Defensivo Activado. Jugador demasiado cerca.</color>");
        _currentState = BossState.Attacking;
        StartCoroutine(DefensiveStompSequence());
        _stompTimer = _stompCooldown;
        return true;
    }

    private IEnumerator DefensiveStompSequence()
    {
        Debug.Log($"[Astaroth] Iniciando Secuencia de Pisotón Defensivo.");

        _navMeshAgent.isStopped = true;
        LookAtPlayer();

        SpawnGroundTelegraph(_stompWarningPrefab, transform.position, _stompRadius, _stompTelegraphTime);

        yield return new WaitForSeconds(_stompTelegraphTime);

        PerformStompImpact();

        yield return new WaitForSeconds(0.5f);

        _currentState = BossState.Moving;
        _navMeshAgent.isStopped = false;
    }

    private void PerformStompImpact()
    {
        if (audioSource != null && stompSFX != null) audioSource.PlayOneShot(stompSFX);

        if (_stompVFXPrefab != null)
        {
            Instantiate(_stompVFXPrefab, transform.position, Quaternion.identity);
        }

        ShakeCamera(0.3f, 2f, 2f);

        Collider[] colliders = Physics.OverlapSphere(transform.position, _stompRadius, LayerMask.GetMask("Player"));
        foreach (var col in colliders)
        {
            GameObject target = col.gameObject;

            if (_enableStompDamage)
            {
                ExecuteAttack(target, transform.position, _stompDamage);
            }

            ApplySafeKnockback(target, transform.position, 10f);
        }
    }

    #endregion

    // SYSTEMS & HELPERS
    #region Audio System

    private void HandleAudioLoop()
    {
        if (_navMeshAgent == null || !_navMeshAgent.enabled) return;

        bool isMoving = !_navMeshAgent.isStopped && _navMeshAgent.velocity.sqrMagnitude > 0.5f;

        if (isMoving)
        {
            _audioStepTimer += Time.deltaTime;
            float stepRate = 1f;

            if (_audioStepTimer >= stepRate)
            {
                if (audioSource != null && walkSFX != null)
                {
                    audioSource.PlayOneShot(walkSFX, 0.5f);
                }
                _audioStepTimer = 0f;
            }
            ResetIdleAudioTimer();
        }
        else
        {
            _audioIdleTimer += Time.deltaTime;
            if (_audioIdleTimer >= _audioIdleInterval)
            {
                if (audioSource != null && presenceSFX != null)
                {
                    audioSource.PlayOneShot(presenceSFX);
                }
                ResetIdleAudioTimer();
            }
        }
    }

    private void ResetIdleAudioTimer()
    {
        _audioIdleTimer = 0f;
        _audioIdleInterval = Random.Range(5f, 9f);
    }

    #endregion

    #region Movement & Orientation Helpers

    private void LookAtPlayer()
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _navMeshAgent.angularSpeed);
    }

    private IEnumerator LookAtPlayerSmoothCoroutine(float duration, float maxDegreesPerSecond)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 directionToPlayer = (_player.position - transform.position).normalized;
            directionToPlayer.y = 0;

            if (directionToPlayer != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                float maxRotationThisFrame = maxDegreesPerSecond * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxRotationThisFrame);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Vector3 PredictPlayerPosition(float timeAhead)
    {
        if (_player == null) return Vector3.zero;

        Vector3 playerVelocity = (_player.position - _lastPlayerPosition) / Time.deltaTime;
        _lastPlayerPosition = _player.position;

        if (playerVelocity.magnitude < 0.1f)
        {
            return _player.position;
        }

        Vector3 predictedPos = _player.position + (playerVelocity * timeAhead);

        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(predictedPos, out navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return navHit.position;
        }

        return _player.position;
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

    #endregion

    #region Combat Systems

    private void ExecuteAttack(GameObject target, Vector3 position, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                Debug.Log($"<color=blue>[Astaroth] Ataque bloqueado por el jugador. Daño restante: {remainingDamage}</color>");
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent<PlayerHealth>(out var healthOnly))
        {
            healthOnly.TakeDamage(damageAmount, false, AttackDamageType.Melee);
            Debug.Log($"<color=blue>[Astaroth] Ataque exitoso al jugador. Daño: {damageAmount}</color>");
        }
    }

    private void ApplySafeKnockback(GameObject target, Vector3 explosionCenter, float force)
    {
        Vector3 direction = (target.transform.position - explosionCenter).normalized;
        direction.y = 0f;

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
        {
            StartCoroutine(KnockbackCCRoutine(cc, direction, force, 0.5f));
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(direction * force, ForceMode.Impulse);
        }
    }

    private IEnumerator KnockbackCCRoutine(CharacterController cc, Vector3 direction, float force, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (cc == null) yield break;
            cc.SimpleMove(direction * force);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Visuals & Feedback Helpers

    private void SpawnGroundTelegraph(GameObject prefab, Vector3 centerPosition, float radius, float duration)
    {
        if (prefab == null) return;

        Vector3 groundPos = GetGroundPosition(centerPosition);

        GameObject instance = Instantiate(prefab, groundPos, Quaternion.identity);
        _instantiatedEffects.Add(instance);

        instance.transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);

        Destroy(instance, duration);
    }

    private IEnumerator ShowWhipTelegraph(Vector3 start, Vector3 end)
    {
        if (_whipTelegraphPrefab != null)
        {
            GameObject telegraph = Instantiate(_whipTelegraphPrefab);
            LineRenderer line = telegraph.GetComponent<LineRenderer>();

            if (line != null)
            {
                line.SetPosition(0, start);
                line.SetPosition(1, end);

                float elapsed = 0f;
                Color startColor = new Color(1, 1, 0, 0.3f);
                Color endColor = new Color(1, 0, 0, 0.8f);

                while (elapsed < _telegraphDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / _telegraphDuration;
                    Color currentColor = Color.Lerp(startColor, endColor, t);
                    line.startColor = currentColor;
                    line.endColor = currentColor;
                    yield return null;
                }
            }

            Destroy(telegraph);
        }
    }

    private void ShowDodgeIndicator()
    {
        if (_dodgeIndicatorPrefab == null || _player == null) return;

        GameObject indicator = Instantiate(_dodgeIndicatorPrefab, _player.position + Vector3.up * 2f, Quaternion.identity);
        Destroy(indicator, _dodgeIndicatorDuration);
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

    #endregion

    #region Gizmos

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
        if (_showRoomGizmos)
        {
            Gizmos.color = new Color(1, 0, 1, 0.3f);
            Gizmos.DrawSphere(_roomCenter, 0.5f);
            Gizmos.DrawWireSphere(_roomCenter, _roomMaxRadius);
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

    #endregion
}