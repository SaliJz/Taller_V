//using System.Collections;
//using UnityEngine;

//public class Bruja_Shooter : MonoBehaviour
//{
//    public Transform player;
//    public Transform firePoint;              // punto desde donde salen los proyectiles
//    public GameObject projectilePrefab;
//    public float projectileSpeed = 12f;

//    [Header("Ciclo de ataque")]
//    public float windupTime = 0.6f;          // tiempo de carga antes de disparar (se puede interrumpir)
//    public int shotsPerAttack = 3;
//    public float timeBetweenShots = 0.25f;
//    public float attackCooldown = 1.2f;      // tiempo entre ataques completos

//    Coroutine attackRoutine;
//    bool isAttacking = false;
//    bool cancelAttack = false;

//    void Awake()
//    {
//        if (player == null)
//        {
//            GameObject p = GameObject.FindGameObjectWithTag("Player");
//            if (p) player = p.transform;
//        }
//        if (firePoint == null) firePoint = transform;
//    }

//    void Update()
//    {
//        // orientarse siempre hacia el player para tener direccion de disparo
//        if (player != null)
//        {
//            Vector3 dir = player.position - transform.position;
//            dir.y = 0f;
//            if (dir.sqrMagnitude > 0.001f)
//            {
//                Quaternion target = Quaternion.LookRotation(dir);
//                transform.rotation = Quaternion.Slerp(transform.rotation, target, 8f * Time.deltaTime);
//            }
//        }
//    }

//    public void StartAttackCycle()
//    {
//        if (attackRoutine == null)
//        {
//            attackRoutine = StartCoroutine(AttackCycle());
//        }
//    }

//    public void StopAttackCycle()
//    {
//        cancelAttack = true;
//        if (attackRoutine != null)
//        {
//            StopCoroutine(attackRoutine);
//            attackRoutine = null;
//        }
//        isAttacking = false;
//        cancelAttack = false;
//    }

//    public void CancelCurrentAttack()
//    {
//        // llamada externa para interrumpir ataque (por ejemplo al recibir danio)
//        cancelAttack = true;
//    }

//    IEnumerator AttackCycle()
//    {
//        while (true)
//        {
//            isAttacking = true;
//            cancelAttack = false;

//            // windup (se puede cancelar)
//            float t = 0f;
//            while (t < windupTime)
//            {
//                if (cancelAttack)
//                {
//                    isAttacking = false;
//                    cancelAttack = false;
//                    attackRoutine = null;
//                    yield break;
//                }
//                t += Time.deltaTime;
//                yield return null;
//            }

//            // disparos
//            for (int i = 0; i < shotsPerAttack; i++)
//            {
//                if (cancelAttack)
//                {
//                    isAttacking = false;
//                    cancelAttack = false;
//                    attackRoutine = null;
//                    yield break;
//                }

//                ShootOnce();
//                yield return new WaitForSeconds(timeBetweenShots);
//            }

//            isAttacking = false;

//            // cooldown antes del siguiente ataque
//            float cd = 0f;
//            while (cd < attackCooldown)
//            {
//                if (cancelAttack)
//                {
//                    cancelAttack = false;
//                    attackRoutine = null;
//                    yield break;
//                }
//                cd += Time.deltaTime;
//                yield return null;
//            }
//        }
//    }

//    void ShootOnce()
//    {
//        if (projectilePrefab == null || firePoint == null || player == null) return;

//        Vector3 dir = (player.position - firePoint.position);
//        dir.y = 0f;
//        dir.Normalize();

//        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
//        Rigidbody rb = proj.GetComponent<Rigidbody>();
//        if (rb != null)
//        {
//            rb.velocity = dir * projectileSpeed;
//        }

//        // si el proyectil tiene script propio, configurarlo
//        Bruja_Projectile pscript = proj.GetComponent<Bruja_Projectile>();
//        if (pscript != null)
//        {
//            pscript.SetDirection(dir);
//        }
//    }

//    public bool IsAttacking()
//    {
//        return isAttacking;
//    }
//}
// Bruja_Shooter.cs
using System.Collections;
using UnityEngine;

public class Bruja_Shooter : MonoBehaviour
{
    public Transform player;
    public Transform firePoint;              // punto desde donde salen los proyectiles (child)
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;

    [Header("Ciclo de ataque")]
    public float windupTime = 0.6f;          // tiempo de carga antes de disparar (se puede interrumpir)
    public int shotsPerAttack = 3;
    public float timeBetweenShots = 0.25f;
    public float attackCooldown = 1.2f;      // tiempo entre ataques completos
    public float maxAttackRange = 12f;       // rango maximo para considerar ataque

    [Header("Busqueda de direccion al obviar paredes")]
    public int sampleAngleSteps = 12;        // cuantas direcciones probar alrededor del jugador
    public float sampleRadiusMin = 0.8f;
    public float sampleRadiusMax = 2.2f;
    public int sampleRadiusSteps = 2;

    Coroutine attackRoutine;
    bool isAttacking = false;
    bool cancelAttack = false;

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (firePoint == null) firePoint = transform;
    }

    void Update()
    {
        // orientacion pasiva: mantenemos una rotacion suave hacia el player (para que el firePoint apunte bien)
        if (player != null)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 8f * Time.deltaTime);
            }
        }
    }

    public void StartAttackCycle()
    {
        if (attackRoutine == null)
        {
            attackRoutine = StartCoroutine(AttackCycle());
        }
    }

    public void StopAttackCycle()
    {
        cancelAttack = true;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        cancelAttack = false;
    }

    public void CancelCurrentAttack()
    {
        // llamada externa para interrumpir ataque (por ejemplo al recibir danio)
        cancelAttack = true;
    }

    IEnumerator AttackCycle()
    {
        while (true)
        {
            isAttacking = true;
            cancelAttack = false;

            // comprobar que hay una direccion valida antes del windup
            Vector3 chosenDir = FindBestAimDirection();
            if (chosenDir == Vector3.zero)
            {
                // no hay direccion valida (pared bloqueando) -> no atacar ahora
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }

            // windup (se puede cancelar)
            float t = 0f;
            while (t < windupTime)
            {
                if (cancelAttack)
                {
                    isAttacking = false;
                    cancelAttack = false;
                    attackRoutine = null;
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            // disparos: usamos la direccion elegida (si durante los disparos la direccion se bloquea, no cambiamos)
            for (int i = 0; i < shotsPerAttack; i++)
            {
                if (cancelAttack)
                {
                    isAttacking = false;
                    cancelAttack = false;
                    attackRoutine = null;
                    yield break;
                }

                ShootOnce(chosenDir);
                yield return new WaitForSeconds(timeBetweenShots);
            }

            isAttacking = false;

            // cooldown antes del siguiente ataque
            float cd = 0f;
            while (cd < attackCooldown)
            {
                if (cancelAttack)
                {
                    cancelAttack = false;
                    attackRoutine = null;
                    yield break;
                }
                cd += Time.deltaTime;
                yield return null;
            }
        }
    }

    // Disparar una vez en una direccion dada (direccion normalizada)
    void ShootOnce(Vector3 direction)
    {
        if (projectilePrefab == null || firePoint == null || player == null) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = direction * projectileSpeed;
        }

        Bruja_Projectile pscript = proj.GetComponent<Bruja_Projectile>();
        if (pscript != null)
        {
            pscript.SetDirection(direction);
        }
    }

    // Comprueba si desde el firePoint actual hay linea de vista directa al player
    public bool HasLineOfSight()
    {
        if (player == null || firePoint == null) return false;
        return HasLineOfSightFrom(firePoint.position);
    }

    // Comprueba linea de vista desde una posicion arbitraria
    public bool HasLineOfSightFrom(Vector3 origin)
    {
        if (player == null) return false;
        Vector3 dir = player.position - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit hit;
        if (Physics.Raycast(origin, dir.normalized, out hit, dist))
        {
            // si el primer hit es parte del player -> hay vision. Usamos IsChildOf para compatibilidad con colliders hijos.
            if (hit.transform == player || hit.transform.IsChildOf(player))
            {
                return true;
            }
            return false; // algo bloquea antes del player (pared u otro obstaculo)
        }

        // no golpeo nada - considerar visible
        return true;
    }

    // Intenta encontrar una direccion alternativa hacia el jugador "obviando" la pared:
    // busca puntos alrededor del player y elige el primero que sea visible desde el firePoint
    public Vector3 FindBestAimDirection()
    {
        if (player == null || firePoint == null) return Vector3.zero;

        // primer intento: direccion directa
        Vector3 directDir = (player.position - firePoint.position);
        if (directDir.magnitude <= maxAttackRange && HasLineOfSightFrom(firePoint.position))
        {
            return directDir.normalized;
        }

        // samplear puntos alrededor del player para encontrar un punto de su area visible
        for (int r = 0; r < sampleRadiusSteps; r++)
        {
            float radius = Mathf.Lerp(sampleRadiusMin, sampleRadiusMax, (float)r / Mathf.Max(1, sampleRadiusSteps - 1));
            for (int i = 0; i < sampleAngleSteps; i++)
            {
                float angle = (360f / sampleAngleSteps) * i;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                Vector3 candidatePoint = player.position + offset;

                // limitar rango: solo aceptar si el candidato esta razonablemente cerca (antes de disparar no queremos que sea muy lejano)
                float candDist = Vector3.Distance(firePoint.position, candidatePoint);
                if (candDist > maxAttackRange * 1.2f) continue;

                // raycast desde firePoint hacia el punto candidato
                Vector3 dirToCandidate = candidatePoint - firePoint.position;
                float dist = dirToCandidate.magnitude;
                if (dist < 0.01f) continue;

                RaycastHit hit;
                if (Physics.Raycast(firePoint.position, dirToCandidate.normalized, out hit, dist))
                {
                    // si el primer hit es parte del player (o childs) -> aceptamos
                    if (hit.transform == player || hit.transform.IsChildOf(player))
                    {
                        return dirToCandidate.normalized;
                    }
                    // sino, este candidato esta bloqueado; seguir probando
                }
                else
                {
                    // ningun hit entre firePoint y candidato -> aceptamos
                    return dirToCandidate.normalized;
                }
            }
        }

        // si no se encontro ninguna direccion valida, devolver Vector3.zero
        return Vector3.zero;
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }
}


