using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class AstarothController
{
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
        if (_isDead) return;
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
        if (_isDead) yield break;

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
            warning.transform.localScale = new Vector3(_defensiveBlockExplosionRadius * 2f, 0.1f, _defensiveBlockExplosionRadius * 2f);
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
        if (_isDead) yield break;

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
        if (_isDead) return;

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
        if (_isDead) return;
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
        if (_isDead) yield break;

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

        _isMudWaving = false;
        _currentState = BossState.Moving;

        _combatPatternStep = _resumeCombatStep; 
        if (_isDead) yield break;

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
}
