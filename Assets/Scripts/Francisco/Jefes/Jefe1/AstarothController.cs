using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public struct SmashKeyframe
{
    public Vector3 Position;
    public Vector3 Scale;
    public float Time;
    public bool IsTargetable;
}

public partial class AstarothController : MonoBehaviour
{
    #region Enums

    public enum BossState
    {
        Moving,
        Attacking,
        SpecialAbility,
        DefensiveBlock,
        MudWave
    }

    private enum CombatPatternStep
    {
        Whip,
        ShortMove,
        Smash,
        LongMove
    }

    #endregion

    #region State & General Settings

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;

    private Coroutine _combatLoopCoroutine;
    private bool _isCombatLoopActive = false;
    private CombatPatternStep _combatPatternStep = CombatPatternStep.Whip;
    private CombatPatternStep _resumeCombatStep = CombatPatternStep.Whip;

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
    private float _originalSpeed;

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
    private bool _isStomping = false;

    #endregion

    #region Ability: Defensive Block

    [Header("Ability: Bloqueo Defensivo")]
    [SerializeField] private bool _enableDefensiveBlock = true;
    [SerializeField] private int _defensiveBlockHitLimit = 3;
    [SerializeField] private float _defensiveBlockHitWindow = 3f;
    [SerializeField] private float _defensiveBlockInvulnerableDuration = 1.5f;
    [SerializeField] private float _defensiveBlockExplosionExpandDuration = 0.35f;
    [SerializeField] private float _defensiveBlockExplosionDamage = 15f;
    [SerializeField] private float _defensiveBlockExplosionRadius = 6f;
    [SerializeField] private float _defensiveBlockKnockbackForce = 10f;
    [SerializeField] private GameObject _defensiveBlockWarningPrefab;
    [SerializeField] private GameObject _defensiveBlockExplosionPrefab;

    private bool _isDefensiveBlocking;
    private bool _defensiveBlockWindowActive;
    private int _hitsAfterStomp;
    private float _defensiveBlockWindowStart;

    #endregion

    #region Ability: Mud Wave

    [Header("Ability: Ola de Lodo")]
    [SerializeField] private bool _enableMudWave = true;
    [SerializeField] private float _mudWaveTriggerDistance = 12f;
    [SerializeField] private float _mudWaveFleeDuration = 1.2f;
    [SerializeField] private float _mudWaveChargeSpeed = 34f;
    [SerializeField] private float _mudWaveMinChargeDistance = 24f;
    [SerializeField] private float _mudWaveOvershootDistance = 10f;
    [SerializeField] private float _mudWaveCooldown = 7f;
    [SerializeField] private float _mudWaveWarningTime = 0.25f;
    [SerializeField] private float _mudWaveDamage = 15f;
    [SerializeField] private float _mudWaveHitRadius = 3f;
    [SerializeField] private float _mudWaveKnockbackForce = 10f;
    [SerializeField] private float _mudWaveAnimatorSpeedMultiplier = 1.8f;
    [SerializeField] private GameObject _mudWaveWarningPrefab;

    [Header("Ola de Lodo - Wind VFX")]
    [SerializeField] private GameObject _mudWaveWindVFXRoot;

    private bool _isMudWaving;
    private float _farDistanceTimer;
    private float _mudWaveCooldownTimer;

    #endregion

    #region Ability: Attack 1 (Whip)

    [Header("Attack 1: Latigazo Desgarrador")]
    [SerializeField] private Transform _whipDamageOrigin;
    [SerializeField] private float _whipHitRadius = 3.5f;
    [SerializeField] private float _Attack1Damage = 9f;
    [SerializeField] private float _attack1Cooldown = 7f;

    [Header("Attack 1 Timings")]
    [Tooltip("Tiempo de espera antes del 1er golpe")]
    [SerializeField] private float _whipDelay1 = 1.05f;
    [Tooltip("Tiempo de espera entre 1er y 2do golpe")]
    [SerializeField] private float _whipDelay2 = 0.2f;
    [Tooltip("Tiempo de espera entre 2do y 3er golpe")]
    [SerializeField] private float _whipDelay3 = 0.2f;

    private bool _isAttackingWithWhip;
    private bool _lastWhipHitPlayer = false;

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

    [Header("Attack 2 Ground Indicator")]
    [SerializeField] private Transform _smashGroundIndicator;
    [SerializeField] private GameObject _smashGroundIndicatorPrefab;
    [SerializeField] private float _smashTargetLockBeforeImpact = 0.5f;
    [SerializeField] private float _smashIndicatorGroundOffset = 0.05f;

    private bool _isSmashing;
    private Vector3 _smashTargetPoint;
    private Vector3 _lastPlayerPosition;
    private Vector3 _lastSmashOverlapCenter;
    private float _lastSmashOverlapRadius;
    private bool _showSmashOverlapGizmo;
    private bool _lastSmashHitPlayer = false;

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
    [SerializeField] private float _pulseDelay = 1.05f;

    [Header("Evolución Pulso Carnal")]
    [SerializeField] private float _speedBuffPerPulse = 0.20f;
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

    private bool _isDead = false;
    private int _totalAttemptsLanded = 0;
    private int _totalAttemptsExecuted = 0;
    private int _hitsReceivedFromPlayer = 0;
    private float _difficultyTimer = 0f;
    private float _currentDifficultyMultiplier = 1f;

    #endregion

    #region Enraged Phase

    [Header("Enraged Phase (25% HP)")]
    [SerializeField] private float _enragedHealthThreshold = 0.25f;
    private bool _isEnraged = false;
    private float _baseAttack1Cooldown;
    private float _baseAttack2Cooldown;

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
    private static readonly int AnimID_IsDeath = Animator.StringToHash("DeathTrigger");
    private static readonly int AnimID_InsAttacking = Animator.StringToHash("InsAttacking");
    private static readonly int AnimID_Attack = Animator.StringToHash("Attack");
    private static readonly int AnimID_ExitSA = Animator.StringToHash("ExitSA");

    private const int ATTACK_NONE = 0;
    private const int ATTACK_WHIP = 1;
    private const int ATTACK_SMASH = 2;
    private const int ATTACK_SPECIAL = 3;

    #endregion

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
            if (vcam != null) _vcam = vcam;
        }
    }

    private void Start()
    {
        if (_navMeshAgent != null)
        {
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.stoppingDistance = Mathf.Max(_stoppingDistance, _safeDistance);
            _originalSpeed = _navMeshAgent.speed;
        }

        _baseAttack1Cooldown = _attack1Cooldown;
        _baseAttack2Cooldown = _attack2Cooldown;

        if (_calculateRoomRadiusOnStart) CalculateRoomRadius();
        else _roomCenter = new Vector3(transform.position.x, 0f, transform.position.z);

        if (_trailRenderer != null) _trailRenderer.enabled = false;
        if (_mudWaveWindVFXRoot != null) _mudWaveWindVFXRoot.SetActive(false);

        if (_player == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _player = playerGO.transform;
        }

        if (_player != null) _lastPlayerPosition = _player.position;

        if (_enemyHealth != null) _enemyHealth.SetMaxHealth(_maxHealth);
        _currentHealth = _maxHealth;

        if (_vcam != null) _noise = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;

        StartCombatLoop();
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

    private void Update()
    {
        if (_isDead) return;
        if (_player == null) return;

        if (_enemyHealth != null && _enemyHealth.IsStunned)
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.velocity = Vector3.zero;
            }

            return;
        }

        HandleAudioLoop();
        CheckHealthThresholds();

        if (_enableAdaptiveDifficulty)
        {
            _difficultyTimer += Time.deltaTime;
            if (_difficultyTimer >= _difficultyAdjustmentInterval)
            {
                AdjustDifficultyBasedOnPerformance();
                _difficultyTimer = 0f;
            }
        }

        if (_currentState == BossState.SpecialAbility)
        {
            if (_isCombatLoopActive) StopCombatLoop();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        UpdateDefensiveBlockWindow();
        UpdateMudWaveTrigger(distanceToPlayer);

        if (_isDefensiveBlocking || _isMudWaving) return;

        if (distanceToPlayer < _stompTriggerDistance &&
            _stompTimer <= 0f &&
            !_isStomping &&
            !_isAttackingWithWhip &&
            !_isSmashing &&
            !_isDefensiveBlocking &&
            !_isMudWaving)
        {
            InterruptAndPerformStomp();
            return;
        }

        if (!_isCombatLoopActive &&
            !_isStomping &&
            !_isDefensiveBlocking &&
            !_isMudWaving &&
            !_isAttackingWithWhip &&
            !_isSmashing &&
            !_isUsingSpecialAbility &&
            _currentState == BossState.Moving)
        {
            StartCombatLoop();
        }


        _stompTimer -= Time.deltaTime;
    }

    #endregion

    #region Deterministic Combat Loop

    private void StartCombatLoop()
    {
        if (_isDead) return;
        if (_isCombatLoopActive) return;

        ResetAttackState();

        _combatLoopCoroutine = StartCoroutine(CombatPatternCycle());
        _isCombatLoopActive = true;
    }

    private void StopCombatLoop()
    {
        if (_combatLoopCoroutine != null) StopCoroutine(_combatLoopCoroutine);
        _isCombatLoopActive = false;
        if (_navMeshAgent != null) _navMeshAgent.speed = _originalSpeed;
    }

    private IEnumerator CombatPatternCycle()
    {
        yield return new WaitForSeconds(1f);

        while (true)
        {
            if (_player != null && _currentState == BossState.Moving)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

                if (distanceToPlayer > _mudWaveTriggerDistance)
                {
                    _combatPatternStep = CombatPatternStep.Smash;
                }
                else if (distanceToPlayer >= _stompTriggerDistance && distanceToPlayer <= _mudWaveTriggerDistance)
                {
                    _combatPatternStep = CombatPatternStep.Whip;
                }
            }

            switch (_combatPatternStep)
            {
                case CombatPatternStep.Whip:
                    _currentState = BossState.Attacking;
                    yield return StartCoroutine(WhipAttackSequence());
                    ResetAttackState();
                    _combatPatternStep = CombatPatternStep.ShortMove;
                    break;

                case CombatPatternStep.ShortMove:
                    yield return StartCoroutine(MoveForDuration(3f, _originalSpeed));
                    _combatPatternStep = CombatPatternStep.Smash;
                    break;

                case CombatPatternStep.Smash:
                    _currentState = BossState.Attacking;
                    yield return StartCoroutine(SmashAttackSequence());
                    ResetAttackState();
                    _combatPatternStep = CombatPatternStep.LongMove;
                    break;

                case CombatPatternStep.LongMove:
                    yield return StartCoroutine(MoveForDuration(5f, _originalSpeed * 0.4f));
                    _combatPatternStep = CombatPatternStep.Whip;
                    break;
            }
        }
    }

    private IEnumerator MoveForDuration(float duration, float speed)
    {
        _currentState = BossState.Moving;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = speed;
        }

        float timer = 0f;
        while (timer < duration)
        {
            SimpleMoveBehavior();
            timer += Time.deltaTime;
            yield return null;
        }

        if (_navMeshAgent != null) _navMeshAgent.speed = _originalSpeed;
    }

    private void ResetAttackState()
    {
        if (_isDead) return;
        if (_animator != null)
        {
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
            _animator.SetBool(AnimID_InsAttacking, false);
        }

        _isAttackingWithWhip = false;
        _isSmashing = false;

        ResetSmashVisuals();
    }

    private void SimpleMoveBehavior()
    {
        if (_player == null || _navMeshAgent == null) return;

        _navMeshAgent.updateRotation = true;
        float distance = Vector3.Distance(transform.position, _player.position);

        bool isMoving = _navMeshAgent.velocity.magnitude > 0.1f && !_navMeshAgent.isStopped;
        if (_animator != null) _animator.SetBool(AnimID_IsRunning, isMoving);

        if (distance > _stoppingDistance)
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

    private void PrepareCombatInterrupt()
    {
        _resumeCombatStep = _combatPatternStep;

        StopCombatLoop();
        ResetAttackState();

        if (_trailRenderer != null) _trailRenderer.enabled = false;

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            _navMeshAgent.ResetPath();
        }
    }

    #endregion

    #region Health & Phases

    private void HandleDamageReceived()
    {
        if (audioSource != null && damageReceivedSFX != null && _currentHealth > 0)
        {
            audioSource.PlayOneShot(damageReceivedSFX);
        }

        _hitsReceivedFromPlayer++;

        if (_enableDefensiveBlock && _defensiveBlockWindowActive && !_isDefensiveBlocking)
        {
            if (Time.time - _defensiveBlockWindowStart > _defensiveBlockHitWindow)
            {
                _defensiveBlockWindowActive = false;
                _hitsAfterStomp = 0;
                return;
            }

            _hitsAfterStomp++;

            if (_hitsAfterStomp > _defensiveBlockHitLimit)
            {
                InterruptAndPerformDefensiveBlock();
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
        if (_isDead) return;

        _isDead = true;

        StopCombatLoop();
        StopAllCoroutines();

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _isUsingSpecialAbility = false;
        _isEnraged = false;
        _isDefensiveBlocking = false;
        _isMudWaving = false;

        if (_enemyHealth != null) _enemyHealth.SetDynamicVulnerability(0f);

        StopMudWaveWindVFX();
        DestroyAllInstantiatedEffects();

        if (_navMeshAgent != null)
        {
            _navMeshAgent.enabled = false;
        }

        if (_animator != null)
        {
            _animator.speed = 1f;

            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
            _animator.SetBool(AnimID_InsAttacking, false);
            _animator.SetBool(AnimID_IsRunning, false);
            _animator.SetBool(AnimID_ExitSA, false);

            _animator.ResetTrigger(AnimID_IsDeath);
            _animator.SetTrigger(AnimID_IsDeath);
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void CheckHealthThresholds()
    {
        if (_isDead) return;
        if (_isUsingSpecialAbility || _isSpecialAbilityPending) return;

        float healthPercentage = _currentHealth / _maxHealth;

        if (_specialAbilityUsedCount < _healthThresholdsForPulse.Length)
        {
            if (healthPercentage <= _healthThresholdsForPulse[_specialAbilityUsedCount])
            {
                StopCombatLoop();
                StopAllCoroutines();
                DestroyAllInstantiatedEffects();
                ResetSmashVisuals();

                _isAttackingWithWhip = false;
                _isSmashing = false;
                _isDefensiveBlocking = false;
                _isMudWaving = false;

                StopMudWaveWindVFX();

                if (_enemyHealth != null) _enemyHealth.SetDynamicVulnerability(0f);

                if (_navMeshAgent != null && _navMeshAgent.enabled)
                {
                    _navMeshAgent.isStopped = true;
                    _navMeshAgent.velocity = Vector3.zero;
                    _navMeshAgent.ResetPath();
                }

                if (_trailRenderer != null) _trailRenderer.enabled = false;

                if (_animator != null)
                {
                    _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
                    _animator.SetBool(AnimID_InsAttacking, false);
                }

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
        if (phase2RageSFX != null) AudioSource.PlayClipAtPoint(phase2RageSFX, transform.position);
    }

    private void AdjustDifficultyBasedOnPerformance()
    {
        if (_totalAttemptsExecuted == 0) return;
        _totalAttemptsLanded = 0;
        _totalAttemptsExecuted = 0;
    }

    #endregion
}