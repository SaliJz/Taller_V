using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

// DATA STRUCTURES
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

    // CONFIGURATION & VARIABLES
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
    [SerializeField] private float _defensiveBlockExplosionDamage = 15f;
    [SerializeField] private float _defensiveBlockExplosionRadius = 6f;
    [SerializeField] private float _defensiveBlockKnockbackForce = 10f;
    [SerializeField] private Color _defensiveBlockTint = new Color(0.25f, 0.9f, 1f, 1f);
    [SerializeField] private GameObject _defensiveBlockWarningPrefab;
    [SerializeField] private GameObject _defensiveBlockExplosionPrefab;
    [SerializeField] private float _defensiveBlockExplosionExpandDuration = 0.35f;

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
    [SerializeField] private Color _mudWaveTint = new Color(0.45f, 0.22f, 0.08f, 1f);
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
    //[SerializeField] private float _whipRange = 25f;

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
    //[SerializeField] private GameObject _objectToHideDuringSmash;
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
    [SerializeField] private GameObject _smashWarningPrefab;
    [SerializeField] private float _telegraphDuration = 1f;

    [Header("VFX")]
    [SerializeField] private TrailRenderer _trailRenderer;

    [Header("Dodge Feedback")]
    [SerializeField] private GameObject _dodgeIndicatorPrefab;
    [SerializeField] private float _dodgeIndicatorDuration = 1.5f;

    private Renderer[] _bossRenderers;
    private Color[] _bossOriginalColors;

    #endregion

    #region Combat Analytics

    [Header("Combat Analytics")]
    [SerializeField] private bool _enableAdaptiveDifficulty = true;
    [SerializeField] private float _difficultyAdjustmentInterval = 15f;

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

    // UNITY LIFECYCLE
    #region Unity Lifecycle

    private void Awake()
    {
        if (_enemyHealth == null) _enemyHealth = GetComponent<EnemyHealth>();
        if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        CacheBossRenderers();

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
        if (_player == null) return;

        if (_enemyHealth != null && _enemyHealth.IsStunned)
        {
            Debug.Log("<color=yellow>[Astaroth] Stunned - Halting actions.</color>");
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
            _currentState != BossState.SpecialAbility)
        {
            StartCombatLoop();
        }

        _stompTimer -= Time.deltaTime;
    }

    #endregion

    // CORE LOGIC
    #region Deterministic Combat Loop

    private void StartCombatLoop()
    {
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

    // HEALTH & PHASE MANAGEMENT
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

        StopCombatLoop();
        StopAllCoroutines();

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _isUsingSpecialAbility = false;
        _isEnraged = false;
        _isDefensiveBlocking = false;
        _isMudWaving = false;

        if (_enemyHealth != null) _enemyHealth.enabled = false;
        StopMudWaveWindVFX();
        DestroyAllInstantiatedEffects();
        RestoreBossTint();

        if (_navMeshAgent != null)
        {
            _navMeshAgent.enabled = false;
        }

        if (_animator != null)
        {
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
            _animator.SetBool(AnimID_InsAttacking, false);
            _animator.SetBool(AnimID_IsRunning, false);
            _animator.SetTrigger(AnimID_IsDeath);
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);
    }

    private void CheckHealthThresholds()
    {
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
                RestoreBossTint();

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
        if (phase2RageSFX != null) AudioSource.PlayClipAtPoint(phase2RageSFX, transform.position);
        Debug.Log("Astaroth entered ENRAGED phase!");
    }

    private void AdjustDifficultyBasedOnPerformance()
    {
        if (_totalAttemptsExecuted == 0) return;
        _totalAttemptsLanded = 0;
        _totalAttemptsExecuted = 0;
    }

    #endregion

    // COMBAT LOGIC
    #region Attack 1 Logic

    private IEnumerator WhipAttackSequence()
    {
        _isAttackingWithWhip = true;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }

        LookAtPlayer();

        if (_animator != null)
        {
            _animator.SetBool(AnimID_IsRunning, false);
            _animator.SetBool(AnimID_InsAttacking, true);
            _animator.SetInteger(AnimID_Attack, ATTACK_WHIP);
        }

        if (_whipTelegraphPrefab != null && _whipDamageOrigin != null)
        {
            SpawnGroundTelegraph(_whipTelegraphPrefab, _whipDamageOrigin.position, _whipHitRadius, _whipDelay1);
        }

        yield return new WaitForSeconds(_whipDelay1);
        PlayWhipSoundCrisp();
        CheckWhipHitbox("Golpe 1");

        yield return new WaitForSeconds(_whipDelay2);
        PlayWhipSoundCrisp();
        CheckWhipHitbox("Golpe 2");

        yield return new WaitForSeconds(_whipDelay3);
        PlayWhipSoundCrisp();
        CheckWhipHitbox("Golpe 3");

        yield return new WaitForSeconds(0.6f);

        if (_animator != null)
        {
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
            _animator.SetBool(AnimID_InsAttacking, false);
        }

        _isAttackingWithWhip = false;
    }

    private void PlayWhipSoundCrisp()
    {
        if (audioSource != null && whipAttackSFX != null)
        {
            audioSource.Stop();
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(whipAttackSFX);
        }
    }

    private void CheckWhipHitbox(string debugHitName)
    {
        if (_whipDamageOrigin == null) return;

        Collider[] hits = Physics.OverlapSphere(_whipDamageOrigin.position, _whipHitRadius, LayerMask.GetMask("Player"));

        bool playerHit = false;
        foreach (var hit in hits)
        {
            ExecuteAttack(hit.gameObject, _whipDamageOrigin.position, _Attack1Damage);
            playerHit = true;
            _lastWhipHitPlayer = true;
        }

        if (playerHit)
        {
            _totalAttemptsLanded++;
            Debug.Log($"<color=red>[Astaroth] {debugHitName} CONECTADO.</color>");
        }
    }

    #endregion

    #region Attack 2 Logic

    private IEnumerator SmashAttackSequence()
    {
        _isSmashing = true;
        _showSmashOverlapGizmo = false;

        if (_smashVisualTransform != null) _smashVisualTransform.gameObject.SetActive(true);
        //if (_objectToHideDuringSmash != null) _objectToHideDuringSmash.SetActive(false);

        if (_animator != null)
        {
            _animator.SetBool(AnimID_IsRunning, false);
            _animator.SetBool(AnimID_InsAttacking, true);
            _animator.SetInteger(AnimID_Attack, ATTACK_SMASH);
        }

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }

        LookAtPlayer();

        _smashTargetPoint = _player.position;
        yield return StartCoroutine(TrackSmashGroundIndicator());

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

            if (endKeyframe.IsTargetable) PerformSmashDamage(_smashTargetPoint, hitByDirectImpact);
        }

        if (_animator != null)
        {
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
            _animator.SetBool(AnimID_InsAttacking, false);
        }

        _isSmashing = false;
        ResetSmashVisuals();
    }

    private IEnumerator TrackSmashGroundIndicator()
    {
        Transform indicator = _smashGroundIndicator;
        GameObject createdIndicator = null;

        if (indicator == null && _smashGroundIndicatorPrefab != null)
        {
            Vector3 startPosition = GetGroundPosition(_smashTargetPoint);
            startPosition.y += _smashIndicatorGroundOffset;

            createdIndicator = Instantiate(_smashGroundIndicatorPrefab, startPosition, Quaternion.identity);
            _instantiatedEffects.Add(createdIndicator);
            indicator = createdIndicator.transform;
        }

        if (indicator != null)
        {
            indicator.gameObject.SetActive(true);
            indicator.localScale = new Vector3(_smashRadius * 2f, 0.1f, _smashRadius * 2f);
        }
        else
        {
            SpawnGroundTelegraph(_smashWarningPrefab, _smashTargetPoint, _smashRadius, _smashDelay);
            yield return new WaitForSeconds(_smashDelay);
            yield break;
        }

        float elapsed = 0f;
        float lockTime = Mathf.Max(0f, _smashDelay - _smashTargetLockBeforeImpact);

        while (elapsed < _smashDelay)
        {
            if (_player != null && elapsed < lockTime)
            {
                _smashTargetPoint = _player.position;
            }

            Vector3 ground = GetGroundPosition(_smashTargetPoint);
            ground.y += _smashIndicatorGroundOffset;
            indicator.position = ground;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (createdIndicator != null)
        {
            Destroy(createdIndicator);
        }
        else if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
        }
    }

    private void ResetSmashVisuals()
    {
        if (_smashVisualTransform != null)
        {
            _smashVisualTransform.gameObject.SetActive(false);
            if (_smashAnimationKeyframes != null && _smashAnimationKeyframes.Length > 0)
            {
                _smashVisualTransform.localPosition = _smashAnimationKeyframes[0].Position;
                _smashVisualTransform.localScale = _smashAnimationKeyframes[0].Scale;
            }
        }

        if (_smashGroundIndicator != null) _smashGroundIndicator.gameObject.SetActive(false);

        //if (_objectToHideDuringSmash != null) _objectToHideDuringSmash.SetActive(true);
        _showSmashOverlapGizmo = false;
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
                if (alreadyHit.Contains(playerRoot)) continue;

                alreadyHit.Add(playerRoot);
                if (playerRoot.TryGetComponent<PlayerHealth>(out var health) || entity.TryGetComponent<PlayerHealth>(out health))
                {
                    ExecuteAttack(playerRoot, rockWorldPosition, _Attack2Damage);
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
        smashGroundPosition.y += 0.1f;

        if (_smashRadiusPrefab != null)
        {
            GameObject visualEffect = Instantiate(_smashRadiusPrefab, smashGroundPosition, Quaternion.identity);
            _instantiatedEffects.Add(visualEffect);
            Destroy(visualEffect, 0.6f);
            StartCoroutine(ExpandSmashRadiusWithDamage(visualEffect.transform, _smashRadius, smashGroundPosition, alreadyHitByRock));
        }

        if (!_lastSmashHitPlayer) ShowDodgeIndicator();
        Invoke("DisableSmashOverlapGizmo", 1f);
    }

    private void DisableSmashOverlapGizmo() => _showSmashOverlapGizmo = false;

    private IEnumerator ExpandSmashRadiusWithDamage(Transform effectTransform, float targetRadius, Vector3 groundPosition, HashSet<GameObject> alreadyHitByRock)
    {
        float duration = 0.5f;
        float elapsedTime = 0f;
        Vector3 initialScale = Vector3.zero;
        Vector3 targetScale = new Vector3(targetRadius * 2, 0.5f, targetRadius * 2);

        HashSet<GameObject> hitByShockwaveEntity = new HashSet<GameObject>();

        while (elapsedTime < duration && effectTransform != null)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            effectTransform.localScale = Vector3.Lerp(initialScale, targetScale, t);

            float currentRadius = Mathf.Lerp(0f, targetRadius, t);
            Collider[] hitColliders = Physics.OverlapSphere(groundPosition, currentRadius * 1.2f);

            foreach (var hitCollider in hitColliders)
            {
                GameObject entity = hitCollider.transform.root.gameObject;
                if (alreadyHitByRock.Contains(entity) || hitByShockwaveEntity.Contains(entity)) continue;

                if (entity.CompareTag("Player"))
                {
                    float heightDifference = Mathf.Abs(entity.transform.position.y - groundPosition.y);
                    if (heightDifference < 2f)
                    {
                        if (Vector3.Distance(entity.transform.position, groundPosition) <= currentRadius)
                        {
                            hitByShockwaveEntity.Add(entity);
                            ExecuteAttack(entity, groundPosition, _Attack2Damage);
                            ApplySafeKnockback(entity, groundPosition, 10f);
                            _lastSmashHitPlayer = true;
                            _totalAttemptsLanded++;
                        }
                    }
                }
            }

            yield return null;
        }

        effectTransform.localScale = targetScale;
        Destroy(effectTransform.gameObject, 0.5f);
    }

    #endregion

    #region Special Ability

    private IEnumerator PulsoCarnal()
    {
        _isUsingSpecialAbility = true;

        if (_animator != null)
        {
            _animator.SetBool(AnimID_IsRunning, true);
        }

        yield return StartCoroutine(MoveToCenter(_roomCenter));

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }

        if (_animator != null)
        {
            _animator.SetBool(AnimID_IsRunning, false);
            _animator.SetBool(AnimID_ExitSA, false);
            _animator.SetBool(AnimID_InsAttacking, true);
            _animator.SetInteger(AnimID_Attack, ATTACK_SPECIAL);
        }

        yield return new WaitForSeconds(_pulseDelay);

        Vector3 groundPos = GetGroundPosition(transform.position);

        if (_headsTransform != null) yield return StartCoroutine(AnimateHeadDown());

        if (_nervesVisualizationPrefab != null)
        {
            GameObject pulseObj = Instantiate(_nervesVisualizationPrefab, groundPos, Quaternion.identity);
            FleshPulseController pulseController = pulseObj.GetComponent<FleshPulseController>();
            if (pulseController != null)
            {
                pulseController.Initialize(_roomMaxRadius, _pulseExpansionDuration, _pulseDamage, _pulseSlowPercentage, _pulseSlowDuration);
            }

            _instantiatedEffects.Add(pulseObj);
        }

        yield return new WaitForSeconds(_pulseExpansionDuration + _pulseWaitDuration);

        if (_headsTransform != null) StartCoroutine(AnimateHeadUp());

        ShakeCamera(_shakeDuration, _amplitude, _frequency);

        if (pulseAttackSFX != null) AudioSource.PlayClipAtPoint(pulseAttackSFX, transform.position);

        if (_crackEffectPrefab != null)
        {
            GameObject crackEffect = Instantiate(_crackEffectPrefab, groundPos, Quaternion.identity, null);
            _instantiatedEffects.Add(crackEffect);
            Destroy(crackEffect, 2f);
        }

        ApplyEvolutionBuff();
        StartCoroutine(BlockAttacksAfterPulse());

        if (_animator != null)
        {
            _animator.SetBool(AnimID_ExitSA, true);
            _animator.SetBool(AnimID_InsAttacking, false);
            _animator.SetInteger(AnimID_Attack, ATTACK_NONE);
        }

        yield return new WaitForSeconds(1f);

        if (_animator != null) _animator.SetBool(AnimID_ExitSA, false);

        _isUsingSpecialAbility = false;
        _currentState = BossState.Moving;

        StartCombatLoop();
    }

    private void ApplyEvolutionBuff()
    {
        _currentEvolutionMultiplier += _speedBuffPerPulse;
        if (_navMeshAgent != null) _navMeshAgent.speed *= (1f + _speedBuffPerPulse);
        if (_animator != null) _animator.speed = _currentEvolutionMultiplier;
        Debug.Log($"<color=purple>[Astaroth] EVOLUCIÓN: Velocidad aumentada. Total: {_currentEvolutionMultiplier}x</color>");
    }

    private IEnumerator MoveToCenter(Vector3 targetCenter)
    {
        if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh) yield break;

        _navMeshAgent.isStopped = false;
        _navMeshAgent.speed = _movementSpeedForPulse;
        _navMeshAgent.SetDestination(targetCenter);

        float safetyTimer = 0f;
        while (_navMeshAgent.pathPending || _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance)
        {
            if (_navMeshAgent.remainingDistance == float.PositiveInfinity) break;
            safetyTimer += Time.deltaTime;
            if (safetyTimer >= 5f) break;
            yield return null;
        }

        _navMeshAgent.speed = _originalSpeed;
    }

    private IEnumerator AnimateHeadDown()
    {
        if (_headsTransform == null) yield break;

        Quaternion start = _headsTransform.localRotation;
        Quaternion target = start * Quaternion.Euler(_headDownRotationAngle, 0, 0);
        float elapsed = 0f;

        while (elapsed < _headAnimationDuration)
        {
            elapsed += Time.deltaTime;
            _headsTransform.localRotation = Quaternion.Slerp(start, target, elapsed / _headAnimationDuration);
            yield return null;
        }

        _headsTransform.localRotation = target;
    }

    private IEnumerator AnimateHeadUp()
    {
        if (_headsTransform == null) yield break;

        Quaternion start = _headsTransform.localRotation;
        Quaternion target = Quaternion.identity;
        float elapsed = 0f;

        while (elapsed < _headAnimationDuration)
        {
            elapsed += Time.deltaTime;
            _headsTransform.localRotation = Quaternion.Slerp(start, target, elapsed / _headAnimationDuration);
            yield return null;
        }

        _headsTransform.localRotation = target;
    }

    private IEnumerator BlockAttacksAfterPulse()
    {
        _isPulseAttackBlocked = true;
        yield return new WaitForSeconds(_postPulseAttackDelay);
        _isPulseAttackBlocked = false;
    }

    private void CalculateRoomRadius()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 10f, LayerMask.GetMask("Ground")))
        {
            _roomCenter = hit.collider.bounds.center;
            _roomCenter.y = transform.position.y;
            float calculatedRadius = Mathf.Max(hit.collider.bounds.extents.x, hit.collider.bounds.extents.z);
            if (_calculateRoomRadiusOnStart) _roomMaxRadius = Mathf.Max(5f, calculatedRadius - 2f);
        }
        else
        {
            _roomCenter = transform.position;
            if (_calculateRoomRadiusOnStart) _roomMaxRadius = 25f;
        }
    }

    #endregion

    #region Defensive Stomp

    private void InterruptAndPerformStomp()
    {
        Debug.Log($"<color=red>[Astaroth] INTERRUPCIÓN INMEDIATA: Pisotón.</color>");

        PrepareCombatInterrupt();
        DestroyAllInstantiatedEffects();
        ResetSmashVisuals();

        _currentState = BossState.Attacking;
        _isStomping = true;
        _stompTimer = _stompCooldown;

        StartCoroutine(DefensiveStompSequence());
    }

    private IEnumerator DefensiveStompSequence()
    {
        LookAtPlayer();
        SpawnGroundTelegraph(_stompWarningPrefab, transform.position, _stompRadius, _stompTelegraphTime);

        yield return new WaitForSeconds(_stompTelegraphTime);

        PerformStompImpact();
        OpenDefensiveBlockWindow();

        yield return new WaitForSeconds(0.5f);

        _isStomping = false;

        _currentState = BossState.Moving;
        if (_navMeshAgent != null && _navMeshAgent.enabled) _navMeshAgent.isStopped = false;

        _combatPatternStep = _resumeCombatStep;
        StartCombatLoop();
    }

    private void PerformStompImpact()
    {
        if (audioSource != null && stompSFX != null) audioSource.PlayOneShot(stompSFX);
        if (_stompVFXPrefab != null) Instantiate(_stompVFXPrefab, transform.position, Quaternion.identity);

        ShakeCamera(0.3f, 2f, 2f);

        Collider[] colliders = Physics.OverlapSphere(transform.position, _stompRadius, LayerMask.GetMask("Player"));
        foreach (var col in colliders)
        {
            GameObject target = col.gameObject;
            if (_enableStompDamage) ExecuteAttack(target, transform.position, _stompDamage);
            ApplySafeKnockback(target, transform.position, 10f);
        }
    }

    #endregion

    #region Defensive Block

    private void OpenDefensiveBlockWindow()
    {
        if (!_enableDefensiveBlock) return;

        _defensiveBlockWindowActive = true;
        _hitsAfterStomp = 0;
        _defensiveBlockWindowStart = Time.time;
    }

    private void UpdateDefensiveBlockWindow()
    {
        if (!_defensiveBlockWindowActive) return;

        if (Time.time - _defensiveBlockWindowStart > _defensiveBlockHitWindow)
        {
            _defensiveBlockWindowActive = false;
            _hitsAfterStomp = 0;
        }
    }

    private void InterruptAndPerformDefensiveBlock()
    {
        if (_isDefensiveBlocking) return;

        PrepareCombatInterrupt();
        DestroyAllInstantiatedEffects();
        ResetSmashVisuals();

        _defensiveBlockWindowActive = false;
        _hitsAfterStomp = 0;

        StartCoroutine(DefensiveBlockSequence());
    }

    private IEnumerator DefensiveBlockSequence()
    {
        _isDefensiveBlocking = true;
        _currentState = BossState.DefensiveBlock;

        Vector3 blockCenter = transform.position;
        Vector3 warningCenter = GetGroundPosition(blockCenter);

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            _navMeshAgent.ResetPath();
        }

        if (_enemyHealth != null) _enemyHealth.SetDynamicVulnerability(1f);

        GameObject warning = null;
        if (_defensiveBlockWarningPrefab != null)
        {
            warning = Instantiate(_defensiveBlockWarningPrefab, warningCenter, Quaternion.identity);
            warning.transform.localScale = new Vector3(
                _defensiveBlockExplosionRadius * 2f,
                0.1f,
                _defensiveBlockExplosionRadius * 2f
            );
            _instantiatedEffects.Add(warning);
        }

        float waitTimer = 0f;
        while (waitTimer < _defensiveBlockInvulnerableDuration)
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.velocity = Vector3.zero;
            }

            transform.position = blockCenter;

            waitTimer += Time.deltaTime;
            yield return null;
        }

        if (warning != null)
        {
            _instantiatedEffects.Remove(warning);
            Destroy(warning);
        }

        GameObject explosion = null;
        if (_defensiveBlockExplosionPrefab != null)
        {
            explosion = Instantiate(_defensiveBlockExplosionPrefab, blockCenter, Quaternion.identity);
            explosion.transform.localScale = Vector3.zero;
            _instantiatedEffects.Add(explosion);
        }

        ShakeCamera(0.3f, 2.5f, 2f);

        yield return StartCoroutine(ExpandDefensiveBlockExplosion(explosion, blockCenter));

        if (explosion != null)
        {
            _instantiatedEffects.Remove(explosion);
            Destroy(explosion, 0.2f);
        }

        if (_enemyHealth != null) _enemyHealth.SetDynamicVulnerability(0f);

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.Warp(blockCenter);
            _navMeshAgent.isStopped = false;
        }

        _isDefensiveBlocking = false;
        _currentState = BossState.Moving;

        _combatPatternStep = _resumeCombatStep;
        StartCombatLoop();
    }

    private IEnumerator ExpandDefensiveBlockExplosion(GameObject explosion, Vector3 blockCenter)
    {
        float elapsed = 0f;
        HashSet<GameObject> damagedTargets = new HashSet<GameObject>();

        while (elapsed < _defensiveBlockExplosionExpandDuration)
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.velocity = Vector3.zero;
            }

            transform.position = blockCenter;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _defensiveBlockExplosionExpandDuration);
            float currentRadius = Mathf.Lerp(0f, _defensiveBlockExplosionRadius, t);

            if (explosion != null)
            {
                explosion.transform.position = blockCenter;
                explosion.transform.localScale = Vector3.one * (currentRadius * 2f);
            }

            DealAreaDamageOnce(blockCenter, currentRadius, _defensiveBlockExplosionDamage, _defensiveBlockKnockbackForce, damagedTargets);

            yield return null;
        }

        DealAreaDamageOnce(blockCenter, _defensiveBlockExplosionRadius, _defensiveBlockExplosionDamage, _defensiveBlockKnockbackForce, damagedTargets);
    }

    #endregion

    #region Mud Wave

    private void UpdateMudWaveTrigger(float distanceToPlayer)
    {
        if (!_enableMudWave ||
            _isMudWaving ||
            _isDefensiveBlocking ||
            _isStomping ||
            _isAttackingWithWhip ||
            _isSmashing ||
            _currentState == BossState.Attacking ||
            _currentState == BossState.SpecialAbility)
        {
            _farDistanceTimer = 0f;
            return;
        }

        if (_mudWaveCooldownTimer > 0f) _mudWaveCooldownTimer -= Time.deltaTime;

        if (distanceToPlayer > _mudWaveTriggerDistance)
        {
            _farDistanceTimer += Time.deltaTime;

            if (_farDistanceTimer >= _mudWaveFleeDuration && _mudWaveCooldownTimer <= 0f)
            {
                InterruptAndPerformMudWave();
            }
        }
        else
        {
            _farDistanceTimer = 0f;
        }
    }

    private void InterruptAndPerformMudWave()
    {
        if (_isMudWaving) return;

        PrepareCombatInterrupt();
        DestroyAllInstantiatedEffects();
        ResetSmashVisuals();

        _farDistanceTimer = 0f;
        _mudWaveCooldownTimer = _mudWaveCooldown;

        StartCoroutine(MudWaveSequence());
    }

    private IEnumerator MudWaveSequence()
    {
        _isMudWaving = true;
        _currentState = BossState.MudWave;

        if (_player == null || _navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnNavMesh)
        {
            _isMudWaving = false;
            StartCombatLoop();
            yield break;
        }

        Vector3 chargeDirection = _player.position - transform.position;
        chargeDirection.y = 0f;
        chargeDirection = chargeDirection.sqrMagnitude > 0.01f ? chargeDirection.normalized : transform.forward;

        Vector3 chargeTarget = GetMudWaveChargeTarget(transform.position, _player.position, chargeDirection);
        float chargeDistance = Vector3.Distance(transform.position, chargeTarget);

        transform.rotation = Quaternion.LookRotation(chargeDirection);
        SetBossTint(_mudWaveTint);

        if (_mudWaveWarningPrefab != null)
        {
            Vector3 warningPosition = GetGroundPosition(transform.position + chargeDirection * (chargeDistance * 0.5f));
            GameObject warning = Instantiate(_mudWaveWarningPrefab, warningPosition, Quaternion.LookRotation(chargeDirection));
            warning.transform.localScale = new Vector3(_mudWaveHitRadius * 2f, 0.1f, chargeDistance);
            _instantiatedEffects.Add(warning);
            Destroy(warning, _mudWaveWarningTime);
        }

        yield return new WaitForSeconds(_mudWaveWarningTime);

        float previousAnimatorSpeed = 1f;
        if (_animator != null)
        {
            previousAnimatorSpeed = _animator.speed;
            _animator.speed = previousAnimatorSpeed * _mudWaveAnimatorSpeedMultiplier;
            _animator.SetBool(AnimID_IsRunning, true);
        }

        PlayMudWaveWindVFX();

        _navMeshAgent.ResetPath();
        _navMeshAgent.updateRotation = false;
        _navMeshAgent.isStopped = true;
        _navMeshAgent.velocity = Vector3.zero;

        HashSet<GameObject> hitPlayers = new HashSet<GameObject>();
        float safetyTimer = 0f;
        float maxChargeTime = Mathf.Max(0.35f, chargeDistance / Mathf.Max(1f, _mudWaveChargeSpeed) + 0.1f);

        Vector3 startPosition = transform.position;
        Vector3 finalTarget = chargeTarget;

        while (safetyTimer < maxChargeTime)
        {
            float step = _mudWaveChargeSpeed * Time.deltaTime;
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, finalTarget, step);

            if (NavMesh.SamplePosition(nextPosition, out NavMeshHit sampleHit, 1f, NavMesh.AllAreas))
            {
                transform.position = sampleHit.position;
            }
            else
            {
                break;
            }

            transform.rotation = Quaternion.LookRotation(chargeDirection);

            CheckMudWaveHit(hitPlayers);

            if (Vector3.Distance(transform.position, finalTarget) <= 0.2f)
            {
                break;
            }

            safetyTimer += Time.deltaTime;
            yield return null;
        }

        CheckMudWaveHit(hitPlayers);

        StopMudWaveWindVFX();

        if (_animator != null)
        {
            _animator.speed = previousAnimatorSpeed;
            _animator.SetBool(AnimID_IsRunning, false);
        }

        _navMeshAgent.Warp(transform.position);
        _navMeshAgent.speed = _originalSpeed;
        _navMeshAgent.updateRotation = true;
        _navMeshAgent.isStopped = false;

        RestoreBossTint();

        _isMudWaving = false;
        _currentState = BossState.Moving;

        _combatPatternStep = _resumeCombatStep;
        StartCombatLoop();
    }

    private Vector3 GetMudWaveChargeTarget(Vector3 origin, Vector3 playerPosition, Vector3 direction)
    {
        Vector3 flatPlayerPosition = new Vector3(playerPosition.x, origin.y, playerPosition.z);
        float distanceToPlayer = Vector3.Distance(origin, flatPlayerPosition);
        float desiredDistance = Mathf.Max(_mudWaveMinChargeDistance, distanceToPlayer + _mudWaveOvershootDistance);

        return GetSafeNavMeshChargeTarget(origin, direction, desiredDistance);
    }

    private Vector3 GetSafeNavMeshChargeTarget(Vector3 origin, Vector3 direction, float distance)
    {
        Vector3 desiredTarget = origin + direction * distance;

        if (NavMesh.Raycast(origin, desiredTarget, out NavMeshHit navHit, NavMesh.AllAreas))
        {
            desiredTarget = navHit.position - direction * 0.5f;
        }

        if (NavMesh.SamplePosition(desiredTarget, out NavMeshHit sampleHit, 2f, NavMesh.AllAreas))
        {
            return sampleHit.position;
        }

        return origin;
    }

    private void CheckMudWaveHit(HashSet<GameObject> hitPlayers)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _mudWaveHitRadius, LayerMask.GetMask("Player"));

        foreach (var hit in hits)
        {
            GameObject target = hit.transform.root != null ? hit.transform.root.gameObject : hit.gameObject;
            if (hitPlayers.Contains(target)) continue;

            hitPlayers.Add(target);
            ExecuteAttack(target, transform.position, _mudWaveDamage);
            ApplySafeKnockback(target, transform.position, _mudWaveKnockbackForce);
        }
    }

    private void PlayMudWaveWindVFX()
    {
        if (_mudWaveWindVFXRoot == null) return;

        _mudWaveWindVFXRoot.SetActive(true);

        ParticleSystem[] particles = _mudWaveWindVFXRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particles)
        {
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void StopMudWaveWindVFX()
    {
        if (_mudWaveWindVFXRoot == null) return;

        ParticleSystem[] particles = _mudWaveWindVFXRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particles)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _mudWaveWindVFXRoot.SetActive(false);
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
            if (_audioStepTimer >= 1f)
            {
                if (audioSource != null && walkSFX != null) audioSource.PlayOneShot(walkSFX, 0.5f);
                _audioStepTimer = 0f;
            }

            _audioIdleTimer = 0f;
        }
        else
        {
            _audioIdleTimer += Time.deltaTime;
            if (_audioIdleTimer >= _audioIdleInterval)
            {
                if (audioSource != null && presenceSFX != null) audioSource.PlayOneShot(presenceSFX);
                _audioIdleTimer = 0f;
                _audioIdleInterval = Random.Range(5f, 9f);
            }
        }
    }

    #endregion

    #region Movement & Orientation Helpers

    private void LookAtPlayer()
    {
        if (_player == null || _navMeshAgent == null) return;

        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _navMeshAgent.angularSpeed);
        }
    }

    private Vector3 GetGroundPosition(Vector3 rayOrigin)
    {
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin + Vector3.up * 2f, Vector3.down, out hit, 30f, LayerMask.GetMask("Ground")))
        {
            return hit.point + Vector3.up * 0.01f;
        }

        return new Vector3(rayOrigin.x, 0.01f, rayOrigin.z);
    }

    #endregion

    #region Combat Systems

    private void DealAreaDamageOnce(Vector3 center, float radius, float damage, float knockbackForce, HashSet<GameObject> damagedTargets)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, LayerMask.GetMask("Player"));

        foreach (var hit in hits)
        {
            GameObject target = hit.transform.root != null ? hit.transform.root.gameObject : hit.gameObject;
            if (damagedTargets.Contains(target)) continue;

            damagedTargets.Add(target);
            ExecuteAttack(target, center, damage);
            ApplySafeKnockback(target, center, knockbackForce);
        }
    }


    private void ExecuteAttack(GameObject target, Vector3 position, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);
                if (remainingDamage > 0f) health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent<PlayerHealth>(out var healthOnly))
        {
            healthOnly.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private void DealAreaDamage(Vector3 center, float radius, float damage, float knockbackForce)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, LayerMask.GetMask("Player"));

        HashSet<GameObject> damagedTargets = new HashSet<GameObject>();
        foreach (var hit in hits)
        {
            GameObject target = hit.transform.root != null ? hit.transform.root.gameObject : hit.gameObject;
            if (damagedTargets.Contains(target)) continue;

            damagedTargets.Add(target);
            ExecuteAttack(target, center, damage);
            ApplySafeKnockback(target, center, knockbackForce);
        }
    }

    private void ApplySafeKnockback(GameObject target, Vector3 explosionCenter, float force)
    {
        PlayerMovement playerMove = target.GetComponent<PlayerMovement>();
        if (playerMove != null && playerMove.IsDashing) return;

        Vector3 direction = (target.transform.position - explosionCenter).normalized;
        direction.y = 0f;

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
        {
            StartCoroutine(KnockbackCCRoutine(cc, direction, force, 0.5f, playerMove));
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(direction * force, ForceMode.Impulse);
        }
    }

    private IEnumerator KnockbackCCRoutine(CharacterController cc, Vector3 direction, float force, float duration, PlayerMovement playerMove = null)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cc == null) yield break;
            if (playerMove != null && playerMove.IsDashing) yield break;

            if (cc.enabled) cc.SimpleMove(direction * force);

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
            if (effect != null) Destroy(effect);
        }

        _instantiatedEffects.Clear();
    }

    private void CacheBossRenderers()
    {
        _bossRenderers = GetComponentsInChildren<Renderer>();
        _bossOriginalColors = new Color[_bossRenderers.Length];

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            Material mat = _bossRenderers[i].material;
            _bossOriginalColors[i] = mat.HasProperty("_Color") ? mat.color : Color.white;
        }
    }

    private void SetBossTint(Color tint)
    {
        if (_bossRenderers == null) return;

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            if (_bossRenderers[i] == null) continue;

            Material mat = _bossRenderers[i].material;
            if (mat.HasProperty("_Color")) mat.color = tint;
        }
    }

    private void RestoreBossTint()
    {
        if (_bossRenderers == null || _bossOriginalColors == null) return;

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            if (_bossRenderers[i] == null) continue;

            Material mat = _bossRenderers[i].material;
            if (mat.HasProperty("_Color")) mat.color = _bossOriginalColors[i];
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (_whipDamageOrigin != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(_whipDamageOrigin.position, _whipHitRadius);
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
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, _smashDetectionRadius);

            Gizmos.color = new Color(0.45f, 0.22f, 0.08f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _mudWaveTriggerDistance);

            Gizmos.color = new Color(0.8f, 0.55f, 0.2f, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward * _mudWaveMinChargeDistance);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _defensiveBlockExplosionRadius);
        }
    }

    #endregion
}