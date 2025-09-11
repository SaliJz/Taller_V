// Bruja_Controller.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Coordina movimiento, disparo y reaccion a golpes (modificado para NavMesh y linea de vista)
public class Bruja_Controller : MonoBehaviour
{
    public Bruja_Movement movimiento;
    public Bruja_Shooter shooter;
    public Bruja_Health salud;

    [Header("Parametros de IA")]
    public float maxAttackRange = 12f;         // distancia maxima desde donde la bruja quiere disparar
    public float keepDistance = 7f;            // distancia que intenta mantener (si el player se acerca, se aleja)
    public float retreatOnInterruptTime = 0.9f;
    public float postRecoveredRetreatTime = 1.2f;

    [Header("Busqueda de posicion de flanqueo")]
    public int flankAngleSteps = 12;
    public float flankDistance = 9f;           // distancia objetivo alrededor del player para flanquear
    public float flankSampleRadius = 2f;       // variacion para buscar posiciones navegables

    bool isOverwhelmed = false;
    Coroutine aiRoutine;

    void Start()
    {
        if (movimiento == null) movimiento = GetComponent<Bruja_Movement>();
        if (shooter == null) shooter = GetComponent<Bruja_Shooter>();
        if (salud == null) salud = GetComponent<Bruja_Health>();

        // registrar callback
        if (salud != null)
        {
            salud.OnDamaged += HandleDamaged;
            salud.OnInterrupted += HandleInterrupted;
            salud.OnOverwhelmed += HandleOverwhelmed;
            salud.OnRecovered += HandleRecovered;
        }

        // pasar offset de firepoint al movimiento si existe (para simulaciones de LOS futuras)
        if (shooter != null && movimiento != null && shooter.firePoint != null)
        {
            Vector3 offset = shooter.firePoint.position - transform.position;
            movimiento.SetFirePointOffset(offset);
        }

        aiRoutine = StartCoroutine(AILoop());
    }

    void OnDestroy()
    {
        if (salud != null)
        {
            salud.OnDamaged -= HandleDamaged;
            salud.OnInterrupted -= HandleInterrupted;
            salud.OnOverwhelmed -= HandleOverwhelmed;
            salud.OnRecovered -= HandleRecovered;
        }
    }

    IEnumerator AILoop()
    {
        while (true)
        {
            if (isOverwhelmed)
            {
                // no ataca, se queda quieta (segun pedido)
                movimiento.StopMoving();
                shooter.StopAttackCycle();
                movimiento.FacePlayer();
                yield return null;
                continue;
            }

            float dist = movimiento.DistanceToPlayer();

            // si el player esta muy cerca -> alejarse
            if (dist < keepDistance)
            {
                shooter.StopAttackCycle();
                movimiento.MoveAwayFromPlayer();
            }
            else
            {
                // si esta fuera de rango de ataque, acercarse lo suficiente para tener direccion
                if (dist > maxAttackRange)
                {
                    shooter.StopAttackCycle();
                    movimiento.MoveTowardsPlayer();
                }
                else
                {
                    // dentro de rango: preferimos atacar si hay linea de vista
                    if (shooter.HasLineOfSight())
                    {
                        // orientarse y empezar ciclo de ataque
                        movimiento.FacePlayer();
                        shooter.StartAttackCycle();
                    }
                    else
                    {
                        // si no hay linea de vista, intentar flanquear buscando una posicion navegable que tenga LOS
                        shooter.StopAttackCycle();

                        Vector3 flankPos;
                        if (FindFlankPosition(out flankPos))
                        {
                            movimiento.MoveToPosition(flankPos);
                        }
                        else
                        {
                            // si no conseguimos posicion de flanqueo, acercarnos para intentar obtener LOS
                            movimiento.MoveTowardsPlayer();
                        }
                    }
                }
            }

            yield return new WaitForFixedUpdate();
        }
    }

    // Buscar una posicion alrededor del player que este en NavMesh y desde la cual la bruja tendria linea de vista
    bool FindFlankPosition(out Vector3 result)
    {
        result = transform.position;
        if (movimiento == null || shooter == null || shooter.player == null) return false;
        Transform player = shooter.player;
        Vector3 fireOffset = (shooter.firePoint != null) ? (shooter.firePoint.position - transform.position) : Vector3.zero;

        for (int i = 0; i < flankAngleSteps; i++)
        {
            float angle = (360f / flankAngleSteps) * i;
            Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 candidate = player.position + dir * flankDistance;

            // variar la altura/offset para samplear mejor la navmesh
            Vector3 sampleTarget = candidate;

            // samplear navmesh cerca del candidato
            NavMeshHit hit;
            if (NavMesh.SamplePosition(sampleTarget, out hit, flankSampleRadius, NavMesh.AllAreas))
            {
                Vector3 navPos = hit.position;

                // simular desde esa posicion si la bruja (mas el offset del firepoint) tendria LOS
                Vector3 simulatedFirePos = navPos + fireOffset;

                if (Vector3.Distance(simulatedFirePos, player.position) > maxAttackRange * 1.2f)
                {
                    // si desde alli esta demasiado lejos, ignorar
                    continue;
                }

                if (shooter.HasLineOfSightFrom(simulatedFirePos))
                {
                    result = navPos;
                    return true;
                }
            }
        }

        return false;
    }

    // si recibe cualquier golpe
    void HandleDamaged()
    {
        // placeholder para FX o sonido
    }

    // cuando se interrumpe (por ejemplo durante un ataque)
    void HandleInterrupted()
    {
        if (shooter != null)
        {
            shooter.CancelCurrentAttack();
        }
        if (movimiento != null)
        {
            movimiento.RetreatForSeconds(retreatOnInterruptTime, 1.2f);
        }
    }

    void HandleOverwhelmed()
    {
        isOverwhelmed = true;
        if (shooter != null) shooter.StopAttackCycle();
        if (movimiento != null) movimiento.StopMoving();
    }

    void HandleRecovered()
    {
        isOverwhelmed = false;
        if (movimiento != null) movimiento.RetreatForSeconds(postRecoveredRetreatTime, 1.1f);
    }
}


//using System.Collections;
//using UnityEngine;

//// Coordina movimiento, disparo y reaccion a golpes
//public class Bruja_Controller : MonoBehaviour
//{
//    public Bruja_Movement movimiento;
//    public Bruja_Shooter shooter;
//    public Bruja_Health salud;

//    [Header("Parametros de IA")]
//    public float maxAttackRange = 12f;         // distancia maxima desde donde la bruja quiere disparar
//    public float keepDistance = 7f;            // distancia que intenta mantener (si el player se acerca, se aleja)
//    public float retreatOnInterruptTime = 0.9f;
//    public float postRecoveredRetreatTime = 1.2f;

//    bool isOverwhelmed = false;
//    Coroutine aiRoutine;

//    void Start()
//    {
//        // tratar de autoencontrar componentes si no asignados
//        if (movimiento == null) movimiento = GetComponent<Bruja_Movement>();
//        if (shooter == null) shooter = GetComponent<Bruja_Shooter>();
//        if (salud == null) salud = GetComponent<Bruja_Health>();

//        if (salud != null)
//        {
//            salud.OnDamaged += HandleDamaged;
//            salud.OnInterrupted += HandleInterrupted;
//            salud.OnOverwhelmed += HandleOverwhelmed;
//            salud.OnRecovered += HandleRecovered;
//        }

//        aiRoutine = StartCoroutine(AILoop());
//    }

//    void OnDestroy()
//    {
//        if (salud != null)
//        {
//            salud.OnDamaged -= HandleDamaged;
//            salud.OnInterrupted -= HandleInterrupted;
//            salud.OnOverwhelmed -= HandleOverwhelmed;
//            salud.OnRecovered -= HandleRecovered;
//        }
//    }

//    IEnumerator AILoop()
//    {
//        while (true)
//        {
//            if (isOverwhelmed)
//            {
//                // no ataca, se queda quieta (segun tu pedido "se queda en su misma posicion sin hacer danio")
//                movimiento.StopMoving();
//                shooter.StopAttackCycle();
//                // aun asi orienta hacia player para poder mantener direccion
//                movimiento.FacePlayer();
//                yield return null;
//                continue;
//            }

//            float dist = movimiento.DistanceToPlayer();

//            // si el player esta muy cerca -> alejarse
//            if (dist < keepDistance)
//            {
//                shooter.StopAttackCycle();
//                movimiento.MoveAwayFromPlayer();
//            }
//            else
//            {
//                // si esta fuera de rango de ataque, acercarse lo suficiente para tener direccion
//                if (dist > maxAttackRange)
//                {
//                    shooter.StopAttackCycle();
//                    movimiento.MoveTowardsPlayer();
//                }
//                else
//                {
//                    // dentro de rango, orientarse y empezar ciclo de ataque
//                    movimiento.FacePlayer();
//                    shooter.StartAttackCycle();
//                }
//            }

//            yield return new WaitForFixedUpdate();
//        }
//    }

//    // si recibe cualquier golpe
//    void HandleDamaged()
//    {
//        // aqui puedes poner fx, sonido, particulas, etc
//    }

//    // cuando se interrumpe (por ejemplo durante un ataque)
//    void HandleInterrupted()
//    {
//        // cancelar ataque y retroceder
//        if (shooter != null)
//        {
//            shooter.CancelCurrentAttack();
//        }
//        if (movimiento != null)
//        {
//            movimiento.RetreatForSeconds(retreatOnInterruptTime, 1.2f);
//        }
//    }

//    void HandleOverwhelmed()
//    {
//        isOverwhelmed = true;
//        // parar ataque ya
//        if (shooter != null) shooter.StopAttackCycle();
//        // quedarse en posicion (segun pedido), pero podemos poner una ligera animacion
//        if (movimiento != null) movimiento.StopMoving();
//    }

//    void HandleRecovered()
//    {
//        // cuando deja de recibir golpes, se aleja y recupera comportamiento
//        isOverwhelmed = false;

//        // alejarse un poco al recuperar para evitar daño inmediato
//        if (movimiento != null) movimiento.RetreatForSeconds(postRecoveredRetreatTime, 1.1f);

//        // reanadir ataque es gestionado por el AILoop
//    }
//}
