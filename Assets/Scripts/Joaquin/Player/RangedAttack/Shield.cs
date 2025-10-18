using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Clase que maneja el comportamiento del escudo lanzado por el jugador.
/// </summary>
public class Shield : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float baseSpeed = 25f;
    [SerializeField] private float maxSpeed = 100f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private LayerMask collisionLayers;

    [Header("Rebound Settings")]
    [SerializeField] private bool canRebound = true;
    [SerializeField] private int reboundCount = 0;
    [SerializeField] private int maxRebounds = 2;
    [SerializeField] private float reboundDetectionRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Shield Trail VFX")]
    [SerializeField] private ParticleSystem shieldTrailVFX;
    [SerializeField] private TrailRenderer shieldTrail;

    private enum ShieldState { Inactive, Thrown, Returning, Rebounding }
    private ShieldState currentState = ShieldState.Inactive;

    private float currentSpeed;
    private Vector3 startPosition;
    private Vector3 lastPosition;
    private Transform returnTarget;
    private Transform currentTarget;
    private List<Transform> hitTargets = new List<Transform>();

    private PlayerShieldController owner;

    private Material shieldTrailVFXMatInstance;
    private Material shieldTrailMatInstance;

    private void Awake()
    {
        InitializeShieldVFX();
    }

    /// <summary>
    /// Función que es llamada por el PlayerShieldController para lanzar el escudo.
    /// </summary>
    /// <param name="owner"> Referencia al controlador del jugador </param>
    /// <param name="direction"> Orientación del escudo en la dirección del lanzamiento </param>
    /// <param name="canRebound"> Indica si el escudo puede rebotar entre enemigos </param>
    public void Throw(PlayerShieldController owner, Vector3 direction, bool canRebound, int maxRebounds, float reboundDetectionRadius, float damage, float speed, float distance)
    {
        this.owner = owner;
        this.returnTarget = owner.transform;
        transform.forward = direction;
        startPosition = transform.position;
        lastPosition = transform.position;

        this.attackDamage = (int)damage;
        this.baseSpeed = speed;
        this.maxDistance = distance;
        currentSpeed = baseSpeed;

        reboundCount = 0;
        this.canRebound = canRebound;
        this.maxRebounds = maxRebounds;
        this.reboundDetectionRadius = reboundDetectionRadius;
        hitTargets.Clear();

        currentState = ShieldState.Thrown;
        gameObject.SetActive(true);

        PlayTrailVFX(true);
    }

    /// <summary>
    /// Función que maneja el movimiento y estado del escudo en cada frame.
    /// </summary>
    private void Update()
    {
        if (currentState == ShieldState.Inactive) return;

        currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.deltaTime, maxSpeed);

        if (currentState == ShieldState.Thrown)
        {
            transform.position += transform.forward * currentSpeed * Time.deltaTime;

            if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
            {
                StartReturning();
            }
        }
        else if (currentState == ShieldState.Rebounding)
        {
            if (currentTarget == null)
            {
                StartReturning();
                return;
            }

            Vector3 directionToTarget = (currentTarget.position - transform.position).normalized;
            transform.position += directionToTarget * currentSpeed * Time.deltaTime;
            transform.forward = directionToTarget;

            if (Vector3.Distance(transform.position, currentTarget.position) < 1.0f)
            {
                PerformHitDetection(currentTarget);
            }
        }
        else if (currentState == ShieldState.Returning)
        {
            Vector3 directionToTarget = (returnTarget.position - transform.position).normalized;
            transform.position += directionToTarget * currentSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, returnTarget.position) < 1.0f)
            {
                owner.CatchShield();
                currentState = ShieldState.Inactive;
                PlayTrailVFX(false);
                gameObject.SetActive(false);
            }
        }
    }

    private void FixedUpdate()
    {
        if (currentState == ShieldState.Inactive) return;

        Vector3 moveDir = transform.forward;
        float step = currentSpeed * Time.fixedDeltaTime;

        if (Physics.Raycast(transform.position, moveDir, out RaycastHit hit, step + 0.5f, collisionLayers))
        {
            PerformHitDetection(hit.transform);
        }
    }

    // Detecta colisiones con enemigos u otros objetos en las capas especificadas
    private void OnTriggerEnter(Collider other)
    {
        if (currentState == ShieldState.Inactive) return;

        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            PerformHitDetection(other.transform);
        }
    }

    /// <summary>
    /// Función que maneja la lógica cuando el escudo golpea un objetivo.
    /// </summary>
    /// <param name="hitTransform"> Transform del objetivo golpeado </param>
    private void PerformHitDetection(Transform hitTransform)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(hitTransform.position, 0.5f, enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            CombatEventsManager.TriggerPlayerHitEnemy(enemy.gameObject, false);

            HealthController healthController = enemy.GetComponent<HealthController>();
            if (healthController != null)
            {
                if (!hitTargets.Contains(hitTransform))
                {
                    hitTargets.Add(hitTransform);
                }

                bool isCritical;
                float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(attackDamage, transform, enemy.transform, out isCritical);

                healthController.TakeDamage(attackDamage);

                ReportDebug("Golpe a " + enemy.name + " por " + attackDamage + " de danio.", 1);
            }

            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                ShieldDummy shieldDummy = damageable as ShieldDummy;

                if (shieldDummy != null)
                {
                    shieldDummy.HitByShield();
                    ReportDebug("Golpe a " + enemy.name + ": DUMMY DE ESCUDO DETECTADO. Contando golpe.", 1);
                }
                else
                {
                    if (!hitTargets.Contains(hitTransform))
                    {
                        hitTargets.Add(hitTransform);
                    }

                    bool isCritical;
                    float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(attackDamage, transform, enemy.transform, out isCritical);
                    damageable.TakeDamage(attackDamage);

                    ReportDebug("Golpe a " + enemy.name + " por " + attackDamage + " de danio.", 1);
                }
            }

            BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
            if (bloodKnight != null)
            {
                if (!hitTargets.Contains(hitTransform))
                {
                    hitTargets.Add(hitTransform);
                }

                bloodKnight.OnPlayerCounterAttack();

                ReportDebug("Golpe a " + enemy.name + " por " + attackDamage + " de danio.", 1);
            }
        }

        Transform nextTarget = FindNextReboundTarget();

        if (nextTarget != null)
        {
            reboundCount++;
            currentTarget = nextTarget;
            currentState = ShieldState.Rebounding;
        }
        else
        {
            StartReturning();
        }
    }

    // Detecta cuando el escudo sale de la colisión con un enemigo u objeto en las capas especificadas
    private void OnTriggerExit(Collider other)
    {
        if (currentState != ShieldState.Thrown) return;

        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            RemoveHitDetection(other.transform);
        }
    }

    /// <summary>
    /// Metodo para eliminar un objetivo de la lista de objetivos golpeados por primera vez.
    /// </summary>
    /// <param name="hitTransform"> Transform del objetivo golpeado </param>
    private void RemoveHitDetection(Transform hitTransform)
    {
        if (hitTargets.Contains(hitTransform))
        {
            hitTargets.Remove(hitTransform);
        }
    }

    /// <summary>
    /// Función que encuentra el siguiente objetivo para rebotar.
    /// </summary>
    private Transform FindNextReboundTarget()
    {
        if (!canRebound || reboundCount >= maxRebounds)
        {
            ReportDebug("Maximo de rebotes alcanzado o rebotes desactivados.", 1);
            return null;
        }

        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, reboundDetectionRadius, enemyLayer);

        Transform bestTarget = null;
        float closestDistance = float.MaxValue;

        // Filtrar y encontrar el mejor objetivo
        foreach (Collider targetCollider in potentialTargets)
        {
            // Omitir enemigos que ya han sido golpeados en esta secuencia
            if (hitTargets.Contains(targetCollider.transform))
            {
                continue;
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetCollider.transform.position);
            float distanceToPlayer = Vector3.Distance(transform.position, returnTarget.position);

            // El rebote es válido solo si el enemigo está más cerca que el jugador
            if (distanceToTarget < distanceToPlayer)
            {
                if (distanceToTarget < closestDistance)
                {
                    closestDistance = distanceToTarget;
                    bestTarget = targetCollider.transform;
                    ReportDebug("Siguiente objetivo de rebote encontrado: " + bestTarget.name, 1);
                }
            }
        }
        return bestTarget;
    }

    // Inicia el retorno del escudo al jugador
    private void StartReturning()
    {
        currentState = ShieldState.Returning;
    }

    #region VFX Methods

    /// <summary>
    /// Inicializa el sistema de VFX del escudo (partículas y trail renderer).
    /// </summary>
    private void InitializeShieldVFX()
    {
        if (shieldTrailVFX == null || shieldTrail == null) return;

        shieldTrailVFX.gameObject.SetActive(true);
        shieldTrail.gameObject.SetActive(true);

        var psRenderer = shieldTrailVFX.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null && psRenderer.sharedMaterial != null)
        {
            shieldTrailVFXMatInstance = new Material(psRenderer.sharedMaterial);
            psRenderer.material = shieldTrailVFXMatInstance;
        }

        var trailRenderer = shieldTrail.GetComponent<TrailRenderer>();
        if (trailRenderer != null && trailRenderer.sharedMaterial != null)
        {
            shieldTrailMatInstance = new Material(trailRenderer.sharedMaterial);
            trailRenderer.material = shieldTrailMatInstance;
        }

        shieldTrailVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        shieldTrailVFX.Clear(true);

        shieldTrail.emitting = false;
        shieldTrail.Clear();
    }

    /// <summary>
    /// Activa los efectos visuales del escudo.
    /// </summary>
    private void PlayTrailVFX(bool active)
    {
        if (shieldTrailVFX == null || shieldTrail == null) return;

        var emission = shieldTrailVFX.emission;
        emission.enabled = active;

        if (active)
        {
            if (!shieldTrailVFX.isPlaying) shieldTrailVFX.Play();

            shieldTrail.Clear();
            shieldTrail.emitting = true;
        }
        else
        {
            shieldTrailVFX.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            shieldTrailVFX.Clear(false);

            shieldTrail.emitting = false;
        }
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        switch (currentState)
        {
            case ShieldState.Thrown:
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(startPosition, transform.position);
                break;
            case ShieldState.Rebounding:
                Gizmos.color = Color.yellow;
                if (currentTarget != null)
                    Gizmos.DrawLine(transform.position, currentTarget.position);
                break;
            case ShieldState.Returning:
                Gizmos.color = Color.green;
                if (returnTarget != null)
                    Gizmos.DrawLine(transform.position, returnTarget.position);
                break;
        }

        // Radio de búsqueda
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, reboundDetectionRadius);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[Shield] {message}");
                break;
            case 2:
                Debug.LogWarning($"[Shield] {message}");
                break;
            case 3:
                Debug.LogError($"[Shield] {message}");
                break;
            default:
                Debug.Log($"[Shield] {message}");
                break;
        }
    }
}