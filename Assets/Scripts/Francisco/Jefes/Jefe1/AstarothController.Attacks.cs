using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AstarothController
{
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
        }
    }

    #endregion

    #region Attack 2 Logic

    private IEnumerator SmashAttackSequence()
    {
        _isSmashing = true;
        _showSmashOverlapGizmo = false;

        if (_smashVisualTransform != null) _smashVisualTransform.gameObject.SetActive(true);

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
}