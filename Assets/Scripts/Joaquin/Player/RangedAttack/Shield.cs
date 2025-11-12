using System.Collections;
using System.Collections.Generic;
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

    [SerializeField] private bool debugMode = false;

    private enum ShieldState { Inactive, Thrown, Returning, Rebounding }
    private ShieldState currentState = ShieldState.Inactive;

    private float currentSpeed;
    private Vector3 startPosition;
    private Vector3 lastPosition;
    private Transform returnTarget;
    private Transform currentTarget;
    private List<Transform> hitTargets = new List<Transform>();

    private PlayerShieldController owner;

    private Coroutine deactivationCoroutine;
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
        if (deactivationCoroutine != null)
        {
            StopCoroutine(deactivationCoroutine);
            deactivationCoroutine = null;
        }

        this.owner = owner;
        this.returnTarget = owner.transform;
        transform.forward = direction;
        startPosition = transform.position;
        lastPosition = transform.position;

        this.attackDamage = Mathf.RoundToInt(damage);
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

            //if (Vector3.Distance(transform.position, currentTarget.position) < 1.0f)
            //{
            //    PerformHitDetection(currentTarget);
            //}
        }
        else if (currentState == ShieldState.Returning)
        {
            Vector3 directionToTarget = (returnTarget.position - transform.position).normalized;
            transform.position += directionToTarget * currentSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, returnTarget.position) < 1.0f)
            {
                if (deactivationCoroutine == null)
                {
                    deactivationCoroutine = StartCoroutine(SafeDeactivateShieldCoroutine());
                }

                //owner.CatchShield();
                //currentState = ShieldState.Inactive;
                //PlayTrailVFX(false);
                //gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == ShieldState.Inactive) return;

        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            PerformHitDetection(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentState == ShieldState.Inactive) return;
     
        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            if (hitTargets.Contains(other.transform))
            {
                hitTargets.Remove(other.transform);
            }
        }
    }

    private void PerformHitDetection(Transform hitTransform)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(hitTransform.position, 0.5f, enemyLayer);

        const DamageType damageTypeForDummy = DamageType.Shield;
        const AttackDamageType shieldDamageType = AttackDamageType.Ranged;

        foreach (Collider enemy in hitEnemies)
        {
            if (!hitTargets.Contains(hitTransform))
            {
                hitTargets.Add(hitTransform);
            }

            CombatEventsManager.TriggerPlayerHitEnemy(enemy.gameObject, false);

            bool isCritical;
            float finalDamage = CriticalHitSystem.CalculateDamage(attackDamage, transform, enemy.transform, out isCritical);

            // Intentar aplicar daño a IDamageable
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                TutorialCombatDummy tutorialDummy = damageable as TutorialCombatDummy;
                DummyArmor dummyArmor = damageable as DummyArmor;

                if (tutorialDummy != null)
                {
                    tutorialDummy.TakeDamage(finalDamage, false, damageTypeForDummy);
                    ReportDebug("Golpe a " + enemy.name + ": DUMMY DE TUTORIAL. Daño: " + finalDamage, 1);
                }
                else
                {
                    damageable.TakeDamage(finalDamage, isCritical, shieldDamageType);
                    ReportDebug("Golpe a " + enemy.name + " por " + finalDamage + " de daño.", 1);
                }
            }

            // Manejar BloodKnight de forma especial
            BloodKnightBoss bloodKnight = enemy.GetComponent<BloodKnightBoss>();
            if (bloodKnight != null)
            {
                bloodKnight.OnPlayerCounterAttack();
                ReportDebug("Golpe a BloodKnight: " + enemy.name, 1);
            }

            ExplosiveHead explosiveHead = enemy.GetComponent<ExplosiveHead>();
            if (explosiveHead != null)
            {
                explosiveHead.StartPriming(true); 
                ReportDebug("Golpe a ExplosiveHead: " + enemy.name + ". Iniciando temporizador forzado.", 1);
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

    /// <summary>
    /// Función que encuentra el siguiente enemigo para rebotar.
    /// Solo busca en enemyLayer, no en collisionLayers general.
    /// </summary>
    private Transform FindNextReboundTarget()
    {
        if (!canRebound || reboundCount >= maxRebounds)
        {
            ReportDebug("Máximo de rebotes alcanzado o rebotes desactivados.", 1);
            return null;
        }

        // Buscar solo enemigos en el radio (enemyLayer)
        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, reboundDetectionRadius, enemyLayer);

        Transform bestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Collider targetCollider in potentialTargets)
        {
            // Omitir enemigos ya golpeados
            if (hitTargets.Contains(targetCollider.transform))
            {
                ReportDebug("Enemigo " + targetCollider.name + " omitido (ya golpeado).", 1);
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
                    ReportDebug("Candidato a rebote: " + bestTarget.name + " a " + distanceToTarget + "m", 1);
                }
            }
            else
            {
                ReportDebug("Enemigo " + targetCollider.name + " omitido (más lejos que el jugador).", 1);
            }
        }

        return bestTarget;
    }

    private void StartReturning()
    {
        currentState = ShieldState.Returning;
        ReportDebug("Escudo retornando al jugador.", 1);
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

    private IEnumerator SafeDeactivateShieldCoroutine()
    {
        owner.CatchShield();
        currentState = ShieldState.Inactive;

        // Detener VFX antes de desactivar el GameObject
        if (shieldTrailVFX != null && shieldTrail != null)
        {
            try
            {
                var emission = shieldTrailVFX.emission;
                emission.enabled = false;

                if (shieldTrailVFX.isPlaying)
                {
                    shieldTrailVFX.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }

                shieldTrail.emitting = false;
            }
            catch (System.Exception ex)
            {
                ReportDebug($"Excepción al detener VFX: {ex.Message}", 2);
            }
        }

        // Esperar un frame para que se procesen los cambios de VFX
        yield return null;

        // Limpiar VFX después de que se detengan
        if (shieldTrailVFX != null && shieldTrail != null && gameObject.activeInHierarchy)
        {
            try
            {
                shieldTrailVFX.Clear(false);
                shieldTrail.Clear();
            }
            catch (System.Exception ex)
            {
                ReportDebug($"Excepción al limpiar VFX: {ex.Message}", 2);
            }
        }

        // Esperar otro frame antes de desactivar
        yield return null;

        gameObject.SetActive(false);

        deactivationCoroutine = null;
    }

    private void OnDestroy()
    {
        if (shieldTrailVFXMatInstance != null)
        {
            Destroy(shieldTrailVFXMatInstance);
            shieldTrailVFXMatInstance = null;
        }

        if (shieldTrailMatInstance != null)
        {
            Destroy(shieldTrailMatInstance);
            shieldTrailMatInstance = null;
        }
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        switch (currentState)
        {
            case ShieldState.Thrown:
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(startPosition, transform.position);
                break;
            case ShieldState.Rebounding:
                Gizmos.color = Color.yellow;
                if (currentTarget != null) Gizmos.DrawLine(transform.position, currentTarget.position);
                break;
            case ShieldState.Returning:
                Gizmos.color = Color.green;
                if (returnTarget != null) Gizmos.DrawLine(transform.position, returnTarget.position);
                break;
        }

        // Radio de búsqueda de rebote (solo para enemigos)
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