using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador del enemigo Drogath, un tanque de apoyo que vincula aliados
/// otorgándoles regeneración de superarmor mientras estén en rango.
/// Al morir libera una onda demoníaca que otorga superarmor temporal.
/// </summary>
public class DrogathEnemy : MonoBehaviour
{
    #region --- Inspector Configuration ---

    [Header("Core References")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private GameObject visualHit;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyToughness enemyToughness;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Transform playerTransform; 

    [Header("Bond System")]
    [Tooltip("Radio máximo para vincular aliados")]
    [SerializeField] private float bondRadius = 15f;
    [Tooltip("Máximo de vínculos simultáneos")]
    [SerializeField] private int maxBonds = 2;
    [Tooltip("Regeneración de superarmor por segundo para aliados vinculados")]
    [SerializeField] private float toughnessRegenPerSecond = 6f;
    [Tooltip("Máximo de superarmor que puede regenerar en un aliado")]
    [SerializeField] private float maxToughnessRegen = 30f;
    [Tooltip("Cooldown antes de poder revincular al mismo aliado")]
    [SerializeField] private float rebondCooldown = 4f;
    [Tooltip("Intervalo para chequear vínculos y regenerar superarmor")]
    [SerializeField] private float bondUpdateInterval = 0.25f;
    [Tooltip("Si está activo, puede activar la dureza en aliados que la tengan desactivada")]
    [SerializeField] private bool canEnableToughnessOnAllies = false;

    [Header("Shield System")]
    [Tooltip("Ángulo frontal del escudo (en grados, desde el centro)")]
    [SerializeField] private float frontalBlockAngle = 75f;
    [SerializeField] private Transform shieldForwardOverride = null;

    [Header("Movement & Combat")]
    [Tooltip("Velocidad de movimiento hacia el jugador")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("Distancia de parada respecto al jugador")]
    [SerializeField] private float stoppingDistance = 2f;
    [Tooltip("Daño del ataque cuerpo a cuerpo")]
    [SerializeField] private float meleeDamage = 10f;
    [Tooltip("Intervalo entre ataques")]
    [SerializeField] private float attackInterval = 1.5f;
    [Tooltip("Rango de ataque cuerpo a cuerpo")]
    [SerializeField] private float attackRange = 2.5f;
    [Tooltip("Empuje aplicado al jugador al ser golpeado")]
    [SerializeField] private float knockbackForce = 4f;

    [Header("Death Effect")]
    [Tooltip("Radio de la onda demoníaca al morir")]
    [SerializeField] private float deathEffectRadius = 15f;
    [Tooltip("Cantidad de superarmor otorgado al morir")]
    [SerializeField] private float deathSuperArmor = 10f;
    [Tooltip("Duración del superarmor otorgado al morir")]
    [SerializeField] private float deathSuperArmorDuration = 5f;

    [Header("Layers")]
    [SerializeField] private LayerMask allyLayers = ~0;
    [SerializeField] private LayerMask playerLayer;

    [Header("Visual Feedback")]
    [SerializeField] private LineRenderer bondLineRendererPrefab;
    [SerializeField] private Color bondLineColor = Color.cyan;
    [SerializeField] private float bondLineWidth = 0.1f;

    [Header("Debugging")]
    [SerializeField] private bool canDebug = false;

    #endregion

    #region Private State

    private class BondInfo
    {
        public GameObject ally;
        public EnemyToughness toughness;
        public LineRenderer lineRenderer;
        public float currentRegen;
    }

    private List<BondInfo> activeBonds = new List<BondInfo>();
    private Dictionary<GameObject, float> rebondCooldowns = new Dictionary<GameObject, float>();
    private Coroutine bondUpdateRoutine;
    private Coroutine combatRoutine;
    private bool hasHitPlayer = false;
    private bool isAttacking = false;
    private bool isDead = false;
    private float attackTimer = 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyToughness == null) enemyToughness = GetComponent<EnemyToughness>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

        if (enemyHealth == null)
        {
            ReportDebug("EnemyHealth no encontrado. Drogath requiere este componente.", 3);
            enabled = false;
            return;
        }

        if (navAgent == null)
        {
            ReportDebug("NavMeshAgent no encontrado. Drogath requiere este componente.", 3);
            enabled = false;
            return;
        }

        navAgent.speed = moveSpeed;
        navAgent.stoppingDistance = stoppingDistance;
        navAgent.acceleration = 8f;
        navAgent.angularSpeed = 120f;
    }

    private void Start()
    {
        // Buscar jugador si no está asignado
        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
        {
            ReportDebug("Jugador no encontrado en la escena.", 2);
        }

        // Iniciar sistema de vínculos
        bondUpdateRoutine = StartCoroutine(BondUpdateRoutine());
        combatRoutine = StartCoroutine(CombatRoutine());

        ReportDebug($"Drogath inicializado. Vida: {enemyHealth.MaxHealth}, Radio: {bondRadius}m", 1);
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeath;
            enemyHealth.OnDamaged += OnDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= OnDamageTaken;
        }
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= OnDamageTaken;
        }

        // Limpiar vínculos
        ClearAllBonds();
    }

    #endregion

    #region Bond System

    private IEnumerator BondUpdateRoutine()
    {
        while (!isDead)
        {
            UpdateRebondCooldowns();
            UpdateExistingBonds();
            TryCreateNewBonds();
            RegenerateBondedAlliesToughness();

            yield return new WaitForSeconds(bondUpdateInterval);
        }
    }

    private void UpdateRebondCooldowns()
    {
        List<GameObject> toRemove = new List<GameObject>();
        List<GameObject> keys = new List<GameObject>(rebondCooldowns.Keys);

        foreach (var key in keys)
        {
            if (key == null) 
            {
                toRemove.Add(key);
                continue;
            }

            rebondCooldowns[key] -= bondUpdateInterval;
            if (rebondCooldowns[key] <= 0)
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            rebondCooldowns.Remove(key);
            if (key != null)
            {
                ReportDebug($"Cooldown de revínculo completado para {key.name}", 1);
            }
        }
    }

    private void UpdateExistingBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            var bond = activeBonds[i];

            // Verificar si el aliado sigue existiendo
            if (bond.ally == null)
            {
                RemoveBond(i, "Aliado destruido");
                continue;
            }

            // Verificar si el aliado murió
            var allyHealth = bond.ally.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead)
            {
                RemoveBond(i, "Aliado murió");
                continue;
            }

            // Verificar distancia
            float distance = Vector3.Distance(transform.position, bond.ally.transform.position);
            if (distance > bondRadius)
            {
                RemoveBond(i, "Fuera de rango");
                continue;
            }

            // Actualizar LineRenderer
            if (bond.lineRenderer != null)
            {
                bond.lineRenderer.SetPosition(0, transform.position + Vector3.up);
                bond.lineRenderer.SetPosition(1, bond.ally.transform.position + Vector3.up);
            }
        }
    }

    private void TryCreateNewBonds()
    {
        if (activeBonds.Count >= maxBonds) return;

        // Buscar aliados en rango
        Collider[] hits = Physics.OverlapSphere(transform.position, bondRadius, allyLayers, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            if (activeBonds.Count >= maxBonds) break;

            GameObject rootObj = hit.transform.root.gameObject;

            // No vincularse a sí mismo
            if (rootObj == gameObject) continue;

            // No vincular si está en cooldown
            if (rebondCooldowns.ContainsKey(rootObj)) continue;

            // No vincular si ya está vinculado
            if (activeBonds.Exists(b => b.ally == rootObj)) continue;

            // Verificar que el aliado no esté muerto
            var allyHealth = rootObj.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead) continue;

            // Buscar componente EnemyToughness
            EnemyToughness toughness = rootObj.GetComponent<EnemyToughness>();
            if (toughness == null) continue;

            // Si no puede activar dureza en otros, verificar que ya esté activa
            if (!canEnableToughnessOnAllies && !toughness.HasToughness)
            {
                continue;
            }

            // Si puede activar dureza, hacerlo
            if (canEnableToughnessOnAllies && !toughness.HasToughness)
            {
                toughness.SetUseToughness(true);
                ReportDebug($"Dureza activada en {rootObj.name} por vínculo de Drogath", 1);
            }

            // Crear vínculo
            CreateBond(rootObj, toughness);
        }
    }

    private void CreateBond(GameObject ally, EnemyToughness toughness)
    {
        BondInfo bond = new BondInfo
        {
            ally = ally,
            toughness = toughness,
            currentRegen = 0f
        };

        // Crear LineRenderer visual
        if (bondLineRendererPrefab != null)
        {
            LineRenderer lr = Instantiate(bondLineRendererPrefab, transform);
            lr.positionCount = 2;
            lr.startWidth = bondLineWidth;
            lr.endWidth = bondLineWidth;
            lr.startColor = bondLineColor;
            lr.endColor = bondLineColor;
            lr.SetPosition(0, transform.position + Vector3.up);
            lr.SetPosition(1, ally.transform.position + Vector3.up);
            bond.lineRenderer = lr;
        }

        activeBonds.Add(bond);
        ReportDebug($"Vínculo creado con {ally.name}. Vínculos activos: {activeBonds.Count}/{maxBonds}", 1);
    }

    private void RemoveBond(int index, string reason)
    {
        if (index < 0 || index >= activeBonds.Count) return;

        var bond = activeBonds[index];

        // Añadir cooldown
        if (bond.ally != null)
        {
            rebondCooldowns[bond.ally] = rebondCooldown;
            ReportDebug($"Vínculo roto con {bond.ally.name} ({reason}). Cooldown: {rebondCooldown}s", 1);
        }

        // Destruir LineRenderer
        if (bond.lineRenderer != null)
        {
            Destroy(bond.lineRenderer.gameObject);
        }

        activeBonds.RemoveAt(index);
    }

    private void ClearAllBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            var bond = activeBonds[i];
            if (bond.lineRenderer != null)
            {
                Destroy(bond.lineRenderer.gameObject);
            }
        }
        activeBonds.Clear();
    }

    private void RegenerateBondedAlliesToughness()
    {
        // Calcular regeneración
        float regenThisFrame = toughnessRegenPerSecond * bondUpdateInterval;

        foreach (var bond in activeBonds)
        {
            if (bond.toughness == null || bond.ally == null) continue;

            // Verificar límite de regeneración total
            if (bond.currentRegen + regenThisFrame > maxToughnessRegen)
            {
                regenThisFrame = maxToughnessRegen - bond.currentRegen;
            }

            if (regenThisFrame > 0)
            {
                // Calcular cuánto puede regenerar realmente
                float currentToughness = bond.toughness.CurrentToughness;
                float maxToughness = bond.toughness.MaxToughness;
                float possibleRegen = Mathf.Min(regenThisFrame, maxToughness - currentToughness);

                if (possibleRegen > 0)
                {
                    float addedToughness = bond.toughness.AddCurrentToughness(regenThisFrame);
                    if (addedToughness > 0)
                    {
                        bond.currentRegen += addedToughness;
                    }

                    if (Mathf.FloorToInt(bond.currentRegen) % 6 == 0 && bond.currentRegen > 0) // Reportar cada 6 puntos regenerados
                    {
                        ReportDebug($"{bond.ally.name} regeneró {bond.currentRegen:F1}/{maxToughnessRegen} de superarmor", 1);
                    }
                }
            }
        }
    }

    #endregion

    #region Combat System

    private IEnumerator CombatRoutine()
    {
        while (!isDead)
        {
            bool shouldAttack = activeBonds.Count == 0;

            if (shouldAttack)
            {
                if (!isAttacking)
                {
                    StartAttackMode();
                }

                UpdateCombat();
            }
            else
            {
                if (isAttacking)
                {
                    StopAttackMode();
                }
            }

            yield return null;
        }
    }

    private void StartAttackMode()
    {
        isAttacking = true;
        attackTimer = 0f;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }

        ReportDebug("Modo ofensivo activado (sin aliados vinculados)", 1);
    }

    private void StopAttackMode()
    {
        isAttacking = false;
        attackTimer = 0f;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        ReportDebug("Modo ofensivo desactivado (vínculos activos)", 1);
    }

    private void UpdateCombat()
    {
        if (playerTransform == null || navAgent == null) return;

        // Verificar si está aturdido
        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            if (!navAgent.isStopped)
            {
                navAgent.isStopped = true;
            }
            return;
        }
        else
        {
            if (navAgent.isStopped && isAttacking)
            {
                navAgent.isStopped = false;
            }
        }

        // Mover hacia el jugador
        if (navAgent.isOnNavMesh && !navAgent.isStopped)
        {
            navAgent.SetDestination(playerTransform.position);
        }

        // Verificar si está en rango de ataque
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= attackRange)
        {
            // Mirar al jugador
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                transform.forward = Vector3.Slerp(transform.forward, directionToPlayer, Time.deltaTime * 5f);
            }

            // Contador de ataque
            attackTimer += Time.deltaTime;

            if (attackTimer >= attackInterval)
            {
                hasHitPlayer = false;
                PerformMeleeAttack();
                attackTimer = 0f;
            }
        }
    }

    private void PerformMeleeAttack()
    {
        if (playerTransform == null) return;

        // Verificar distancia nuevamente
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance > attackRange) return;

        if (hasHitPlayer) return;

        Collider[] hitPlayer = Physics.OverlapSphere(hitPoint.transform.position, attackRange, playerLayer);

        foreach (var hit in hitPlayer)
        {
            var hitTransform = hit.transform;

            // Ejecutar ataque
            ExecuteAttack(hit.gameObject, meleeDamage);

            // Aplicar empuje
            ApplyKnockback(hitTransform);

            ReportDebug($"Drogath atacó al jugador por {meleeDamage} de daño", 1);

            hasHitPlayer = true;
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(hitPoint.transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private void ApplyKnockback(Transform target)
    {
        // Calcula la dirección del empuje desde Kronus hacia el jugador
        Vector3 knockbackDirection = (target.position - transform.position).normalized;
        knockbackDirection.y = 0f;

        // Aplica el empuje
        CharacterController cc = target.GetComponent<CharacterController>();
        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (cc != null)
        {
            // Si el jugador usa CharacterController
            StartCoroutine(ApplyKnockbackOverTime(cc, knockbackDirection * knockbackForce));
        }
        else if (rb != null)
        {
            // Si el jugador usa Rigidbody
            rb.AddForce(knockbackDirection * knockbackForce * 10f, ForceMode.Impulse);
        }

        ReportDebug($"Empuje aplicado al jugador en dirección {knockbackDirection}", 1);
    }

    private IEnumerator ApplyKnockbackOverTime(CharacterController cc, Vector3 knockbackVelocity)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cc != null && cc.enabled)
            {
                cc.Move(knockbackVelocity * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        Vector3 originalScale = visualHit.transform.localScale;

        if (visualHit != null && hitPoint != null)
        {
            visualHit.transform.localScale = Vector3.one * attackRange * 2f;
            visualHit.SetActive(true);
        }
        yield return new WaitForSeconds(0.5f);

        if (visualHit != null && hitPoint != null)
        {
            visualHit.SetActive(false);
            visualHit.transform.localScale = originalScale;
        }
    }

    #endregion

    #region Shield System

    /// <summary>
    /// Verifica si un ataque desde una dirección dada debe ser bloqueado por el escudo.
    /// </summary>
    public bool ShouldBlockDamage(Vector3 attackerPosition)
    {
        // Calcular la dirección desde Drogath hacia el atacante
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0; // Ignorar diferencia de altura

        if (toAttacker.magnitude < 0.01f)
        {
            ReportDebug("Atacante demasiado cerca o en misma posición", 2);
            return false;
        }

        toAttacker.Normalize();

        // Obtener dirección frontal del escudo en plano XZ
        Vector3 shieldForward = GetShieldForward();
        shieldForward.y = 0;
        shieldForward.Normalize();

        // Calcular ángulo entre el forward del escudo y la dirección hacia el atacante
        // Si el atacante está en el arco frontal, el escudo lo bloquea
        float angle = Vector3.Angle(shieldForward, toAttacker);

        // Debug visual
        if (canDebug)
        {
            Debug.DrawRay(transform.position, shieldForward * 3f, Color.green, 0.5f);
            Debug.DrawRay(transform.position, toAttacker * 2.5f, Color.red, 0.5f);
            Debug.DrawLine(transform.position, attackerPosition, Color.yellow, 0.5f);
            DrawBlockCone(shieldForward, frontalBlockAngle * 0.5f);
        }

        // El escudo bloquea si el atacante está en el arco frontal
        bool isBlocked = angle <= (frontalBlockAngle * 0.5f);

        ReportDebug($"Atacante a {angle:F1}° del frente, Cobertura: ±{frontalBlockAngle * 0.5f}°, {(isBlocked ? "BLOQUEADO" : "NO BLOQUEADO")}", 1);

        return isBlocked;
    }

    private Vector3 GetShieldForward()
    {
        if (shieldForwardOverride != null)
        {
            return shieldForwardOverride.forward;
        }
        return transform.forward;
    }

    private void DrawBlockCone(Vector3 forward, float halfAngle)
    {
        int segments = 20;
        float totalAngle = frontalBlockAngle;
        float segmentAngle = totalAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (i * segmentAngle);
            Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * forward;
            Debug.DrawRay(transform.position, dir * 2f, Color.cyan, 0.5f);
        }
    }

    #endregion

    #region Death Effect

    private void OnDamageTaken()
    {
        // Aquí podrías agregar lógica adicional si es necesario cuando Drogath recibe daño.
    }

    private void HandleDeath(GameObject deadEnemy)
    {
        if (isDead) return;
        isDead = true;

        ReportDebug("Drogath murió. Activando Armadura Demoníaca...", 1);

        // Detener NavMeshAgent
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        // Detener rutinas
        if (bondUpdateRoutine != null)
        {
            StopCoroutine(bondUpdateRoutine);
            bondUpdateRoutine = null;
        }
        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        StopAllCoroutines();

        // Limpiar vínculos
        ClearAllBonds();

        // Aplicar efecto de muerte
        ApplyDemonicArmorEffect();
    }

    private void ApplyDemonicArmorEffect()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, deathEffectRadius, allyLayers, QueryTriggerInteraction.Ignore);

        int affectedCount = 0;

        foreach (var hit in hits)
        {
            GameObject rootObj = hit.transform.root.gameObject;

            // No aplicar a sí mismo
            if (rootObj == gameObject) continue; // Verificar que el aliado no sea Drogath

            // Verificar que el aliado no esté muerto
            var allyHealth = rootObj.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead) continue; // Sin vida, no aplicar

            // Verificar que el aliado tenga EnemyToughness
            EnemyToughness allyToughness = rootObj.GetComponent<EnemyToughness>();
            if (allyToughness == null) continue; // Sin dureza, no aplicar

            // Activar dureza si está permitido
            if (canEnableToughnessOnAllies && !allyToughness.HasToughness)
            {
                allyToughness.SetUseToughness(true);
            }

            if (allyToughness.HasToughness)
            {
                allyToughness.ApplyToughnessBuff(deathSuperArmor, deathSuperArmorDuration);
            }

            affectedCount++;
        }

        ReportDebug($"Armadura Demoníaca aplicada a {affectedCount} aliados (+{deathSuperArmor} superarmor por {deathSuperArmorDuration}s)", 1);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Obtiene el número de vínculos activos actuales
    /// </summary>
    public int GetActiveBondsCount()
    {
        return activeBonds.Count;
    }

    /// <summary>
    /// Verifica si Drogath está en modo ataque
    /// </summary>
    public bool IsInAttackMode()
    {
        return isAttacking;
    }

    /// <summary>
    /// Fuerza la rotura de todos los vínculos activos
    /// </summary>
    public void ForceBreakAllBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            RemoveBond(i, "Forzado manualmente");
        }

        ReportDebug("Todos los vínculos fueron rotos manualmente", 2);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!canDebug) return;

        // Radio de vinculación
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, bondRadius);

        // Radio de efecto de muerte
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, deathEffectRadius);

        // Rango de ataque
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Visualizar ángulo del escudo
        Vector3 forward = transform.forward;
        Vector3 right = Quaternion.Euler(0, frontalBlockAngle/2, 0) * forward;
        Vector3 left = Quaternion.Euler(0, -frontalBlockAngle/ 2, 0) * forward;

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + forward * 3f);
        Gizmos.DrawLine(transform.position, transform.position + right * 3f);
        Gizmos.DrawLine(transform.position, transform.position + left * 3f);

        // Dibujar área del escudo
        int segments = 20;
        Vector3 prevPoint = transform.position + left * 3f;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-frontalBlockAngle/ 2, frontalBlockAngle/ 2, i / (float)segments);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = transform.position + dir * 3f;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // Dibujar vínculos activos
        if (Application.isPlaying)
        {
            Gizmos.color = bondLineColor;
            foreach (var bond in activeBonds)
            {
                if (bond.ally != null)
                {
                    Gizmos.DrawLine(transform.position + Vector3.up, bond.ally.transform.position + Vector3.up);

                    // Dibujar esfera pequeña en el aliado vinculado
                    Gizmos.DrawSphere(bond.ally.transform.position + Vector3.up, 0.3f);
                }
            }
        }
    }

    #endregion

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <param name="message">Mensaje a reportar.</param>
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[DrogathEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[DrogathEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[DrogathEnemy] {message}");
                break;
            default:
                Debug.Log($"[DrogathEnemy] {message}");
                break;
        }
    }
}
