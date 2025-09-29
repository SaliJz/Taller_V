// EnemigoEscudoAI.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemigoEscudoAI: mueve al enemigo, gestiona proteccion de aliados, escudo frontal,
/// activa "armadura demonica" (golpea aliados cercanos con reduccion) y delega da�o al VidaEnemigoEscudo.
/// Mejoras: evita quedarse parado � fallback de movimiento si NavMesh falla,
/// permite movimiento durante armadura y asegura que NavMeshAgent no quede en isStopped.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemigoEscudoAI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Script que maneja salud y UI")]
    [SerializeField] private VidaEnemigoEscudo enemyHealth;
    [Tooltip("Componente que spawnea el prefab de ataque")]
    [SerializeField] private EnemyMeleeAttack meleeAttack;
    [Tooltip("GameObject hijo que contiene collider y EnemyShieldBehaviour")]
    [SerializeField] private GameObject shieldObject;
    [SerializeField] private float anguloFrontal = 120f;
    [SerializeField] private bool escudoFrontalActivo = false;

    [Header("Movimiento / IA")]
    [SerializeField] private float radioBusquedaAliados = 15f;
    [SerializeField] private float distanciaFrenteAliado = 1.2f;
    [SerializeField] private LayerMask capaAliados = ~0;
    [SerializeField] private float toleranciaPosicionProteccion = 0.3f;

    [Header("Armadura Demonica")]
    [SerializeField] private float reduccionDanioPercent = 0.25f;
    [SerializeField] private float duracionArmadura = 10f;
    [SerializeField] private float cooldownArmadura = 4.5f;
    [SerializeField] private float radioArmaduraAfecto = 8f;

    private NavMeshAgent agente;
    private EnemyShieldBehaviour shieldBehaviour;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    // proteccion
    private GameObject aliadoProtegido = null;
    private Vector3 posicionObjetivoProteccion;
    private bool estaProtegiendo = false;

    // armadura
    private bool armaduraActiva = false;
    private bool armaduraEnCooldown = false;
    private Coroutine corrutinaArmadura = null;

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();

        if (enemyHealth == null) enemyHealth = GetComponent<VidaEnemigoEscudo>();
        if (meleeAttack == null) meleeAttack = GetComponent<EnemyMeleeAttack>();

        // shield child auto-find
        if (shieldObject == null)
        {
            var found = transform.Find("ShieldObject");
            if (found != null) shieldObject = found.gameObject;
        }

        if (shieldObject != null)
        {
            shieldBehaviour = shieldObject.GetComponent<EnemyShieldBehaviour>();
            if (shieldBehaviour == null)
            {
                Debug.LogWarning($"[{name}] shieldObject no tiene EnemyShieldBehaviour. Agr�galo para control de bloqueo.");
            }
            else
            {
                // PASAR como owner el componente que implemente IDamageable (preferentemente VidaEnemigoEscudo)
                IDamageable owner = enemyHealth as IDamageable;
                if (owner == null) owner = GetComponent<IDamageable>(); // fallback
                if (owner != null)
                {
                    shieldBehaviour.SetOwner(owner);
                }
                else
                {
                    Debug.LogWarning($"[{name}] No se encontr� un IDamageable para asignar como owner del ShieldBehaviour.");
                }

                shieldBehaviour.SetFrontalAngle(anguloFrontal);
                shieldBehaviour.SetActive(escudoFrontalActivo);
                shieldObject.SetActive(escudoFrontalActivo);
            }
        }
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        playerTransform = p ? p.transform : null;
        if (playerTransform == null) Debug.LogWarning($"[{name}] Jugador no encontrado en la escena.");
        else playerTransform.TryGetComponent(out playerHealth);

        // asegurar agente activo
        if (agente != null)
        {
            agente.isStopped = false;
            if (agente.speed <= 0f) agente.speed = 3.5f; // valor por defecto razonable si qued� en 0
        }
    }

    private void Update()
    {
        // Prioridad: armadura cuando baja de vida (disparar pero NO bloquear movimiento)
        if (enemyHealth != null)
        {
            float hpPercent = enemyHealth.CurrentHealth / Mathf.Max(1f, enemyHealth.MaxHealth);

            if (hpPercent <= 0.25f && !armaduraActiva && !armaduraEnCooldown)
            {
                ActivarArmaduraDemonica();
                // no return: permitimos que siga evaluando movimiento/ataque
            }

            // Si no esta en low HP, buscar aliado a proteger
            if (hpPercent > 0.25f)
            {
                GameObject aliado = BuscarAliadoConMenorVidaEnRango(radioBusquedaAliados);
                if (aliado != null)
                {
                    if (!estaProtegiendo || aliado != aliadoProtegido)
                    {
                        IniciarProteccion(aliado);
                    }
                    ActualizarPosicionProteccion();
                    return; // si est� protegiendo, mantenemos comportamiento de protecci�n exclusivo
                }
                else
                {
                    if (estaProtegiendo) DetenerProteccion();
                }
            }
        }

        // Si hay jugador: perseguir o intentar melee (delegado)
        if (playerTransform != null)
        {
            float distanciaJugador = Vector3.Distance(transform.position, playerTransform.position);

            if (meleeAttack != null && distanciaJugador <= meleeAttack.attackRange)
            {
                meleeAttack.TryAttack(playerTransform);
            }
            else
            {
                // Moverse hacia el jugador siempre que no est� protegiendo a otro (PERMITIR movimiento durante armadura)
                if (!estaProtegiendo)
                {
                    TryMoveTo(playerTransform.position);
                }
            }
        }
    }

    #region Proteccion / posicionamiento / escudo frontal
    private void IniciarProteccion(GameObject aliado)
    {
        aliadoProtegido = aliado;
        estaProtegiendo = true;
        DesplegarEscudoFrontal(true);
        if (agente != null) agente.isStopped = false;
    }

    private void DetenerProteccion()
    {
        estaProtegiendo = false;
        aliadoProtegido = null;
        DesplegarEscudoFrontal(false);
        // asegurarnos que el agente pueda moverse de nuevo
        if (agente != null) agente.isStopped = false;
    }

    private void ActualizarPosicionProteccion()
    {
        if (aliadoProtegido == null) return;

        if (playerTransform == null)
        {
            Vector3 deseada = aliadoProtegido.transform.position - aliadoProtegido.transform.forward * distanciaFrenteAliado;
            // mover hacia la posici�n deseada
            TryMoveTo(deseada);
            return;
        }

        Vector3 dirAliadoAJugador = (playerTransform.position - aliadoProtegido.transform.position).normalized;
        posicionObjetivoProteccion = aliadoProtegido.transform.position + dirAliadoAJugador * distanciaFrenteAliado;

        TryMoveTo(posicionObjetivoProteccion);

        if (Vector3.Distance(transform.position, posicionObjetivoProteccion) <= toleranciaPosicionProteccion)
        {
            Vector3 lookDir = (playerTransform.position - transform.position).Flat();
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
            }
        }
    }

    private void DesplegarEscudoFrontal(bool activar)
    {
        escudoFrontalActivo = activar;
        if (shieldObject != null) shieldObject.SetActive(activar);
        if (shieldBehaviour != null) shieldBehaviour.SetActive(activar);
    }
    #endregion

    #region Armadura demonica
    private void ActivarArmaduraDemonica()
    {
        if (corrutinaArmadura != null) StopCoroutine(corrutinaArmadura);
        corrutinaArmadura = StartCoroutine(RutinaArmaduraDemonica());
    }

    private IEnumerator RutinaArmaduraDemonica()
    {
        armaduraActiva = true;
        float reduccion = reduccionDanioPercent;

        // Aplicar reduccion a si mismo via VidaEnemigoEscudo (metodo ApplyDamageReduction)
        if (enemyHealth != null)
        {
            enemyHealth.ApplyDamageReduction(reduccion, duracionArmadura);
        }

        // Aplicar reduccion a aliados cercanos que sean enemigos (ahora por layer "Enemy")
        Collider[] hits = Physics.OverlapSphere(transform.position, radioArmaduraAfecto);
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        foreach (var c in hits)
        {
            GameObject go = c.gameObject;
            if (go == this.gameObject) continue;

            if (go.layer != enemyLayer) continue;

            IntentarAplicarReduccionDanio(go, reduccion, duracionArmadura);
        }

        if (estaProtegiendo) DetenerProteccion();

        // NOTA: no paramos el movimiento � la IA seguir� movi�ndose/atacando mientras la armadura est� activa
        yield return new WaitForSeconds(duracionArmadura);

        armaduraActiva = false;

        armaduraEnCooldown = true;
        StartCoroutine(RutinaCooldownArmadura());
        yield break;
    }

    private IEnumerator RutinaCooldownArmadura()
    {
        yield return new WaitForSeconds(cooldownArmadura);
        armaduraEnCooldown = false;
    }

    /// <summary>
    /// Intenta aplicar la reduccion llamando ApplyDamageReduction si existe VidaEnemigoEscudo,
    /// si no busca metodos publicos o usa SendMessage como fallback.
    /// </summary>
    private bool IntentarAplicarReduccionDanio(GameObject target, float reduccionPercent, float duracion)
    {
        if (target == null) return false;

        // preferir VidaEnemigoEscudo.ApplyDamageReduction
        if (target.TryGetComponent<VidaEnemigoEscudo>(out var eh))
        {
            try
            {
                eh.ApplyDamageReduction(reduccionPercent, duracion);
                return true;
            }
            catch { /* ignore */ }
        }

        // luego intentar metodos con reflection (nombres comunes)
        var comps = target.GetComponents<MonoBehaviour>();
        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();

            string[] nombresMetodos = {
                "ApplyDamageReduction", "ApplyArmor", "AddDamageModifier", "AddDamageReduction",
                "AplicarReduccionDanio", "AplicarArmadura", "AgregarModificadorDanio", "AgregarReduccionDanio"
            };

            foreach (var mname in nombresMetodos)
            {
                var method = t.GetMethod(mname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(comp, new object[] { reduccionPercent, duracion });
                        return true;
                    }
                    catch { }
                }
            }
        }

        // fallback: SendMessage (no lanza si no existe)
        try
        {
            target.SendMessage("ApplyDamageReduction", new object[] { reduccionPercent, duracion }, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("AplicarReduccionDanio", new object[] { reduccionPercent, duracion }, SendMessageOptions.DontRequireReceiver);
            return true;
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region Danio / Integracion con Shield
    /// <summary>
    /// Metodo publico que otras mecanicas llaman. Comprueba el escudo frontal para proyectiles y
    /// delega el da�o real al VidaEnemigoEscudo.
    /// </summary>
    public void TomarDanio(float cantidad, Vector3 posicionOrigen, GameObject objetoOrigen = null)
    {
        // manejo de proyectil-escudo
        if (escudoFrontalActivo && objetoOrigen != null)
        {
            try
            {
                if (objetoOrigen.CompareTag("Escudo"))
                {
                    if (shieldBehaviour != null)
                    {
                        bool handled = shieldBehaviour.HandleProjectileHit(objetoOrigen, posicionOrigen);
                        if (handled) return;
                    }
                }
            }
            catch (UnityException) { /* tag podria no existir -> seguir flujo */ }
        }

        // aplicar da�o real en el componente de vida
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(cantidad, isCritical: false);
        }
    }
    #endregion

    #region Buscar aliado y utilitarios
    private GameObject BuscarAliadoConMenorVidaEnRango(float radio)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radio);
        GameObject mejor = null;
        float mejorPercent = 2f;
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        foreach (var c in hits)
        {
            GameObject go = c.gameObject;
            if (go == this.gameObject) continue;

            if (go.layer != enemyLayer) continue;

            if (IntentarObtenerPercentVida(go, out float percent))
            {
                if (percent < mejorPercent)
                {
                    mejorPercent = percent;
                    mejor = go;
                }
            }
        }
        return mejor;
    }

    private bool IntentarObtenerPercentVida(GameObject go, out float percent)
    {
        percent = 1f;

        // si tiene VidaEnemigoEscudo, usarlo
        if (go.TryGetComponent<VidaEnemigoEscudo>(out var eh))
        {
            if (eh.MaxHealth > 0f)
            {
                percent = Mathf.Clamp01(eh.CurrentHealth / eh.MaxHealth);
                return true;
            }
            return false;
        }

        // fallback: inspeccionar componentes por nombres comunes
        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();

            string[] posiblesActual = { "currentHealth", "CurrentHealth", "health", "Health", "hp", "HP", "saludActual", "salud", "vida", "Vida" };
            string[] posiblesMax = { "maxHealth", "MaxHealth", "maxHP", "MaxHP", "maxhp", "saludMax", "saludMaxima", "vidaMax" };

            float? cur = null;
            float? max = null;

            foreach (var name in posiblesActual)
            {
                var prop = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int)))
                {
                    object val = prop.GetValue(comp);
                    cur = Convert.ToSingle(val);
                    break;
                }
                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
                {
                    object val = field.GetValue(comp);
                    cur = Convert.ToSingle(val);
                    break;
                }
            }

            foreach (var name in posiblesMax)
            {
                var prop = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int)))
                {
                    object val = prop.GetValue(comp);
                    max = Convert.ToSingle(val);
                    break;
                }
                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
                {
                    object val = field.GetValue(comp);
                    max = Convert.ToSingle(val);
                    break;
                }
            }

            if (cur.HasValue && max.HasValue && max.Value > 0f)
            {
                percent = Mathf.Clamp01(cur.Value / max.Value);
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Gizmos y diagnostico
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.up * 1.5f);
        Gizmos.DrawWireSphere(transform.position, radioBusquedaAliados);
        Gizmos.DrawWireSphere(transform.position, radioArmaduraAfecto);
    }
    #endregion

    #region Movement helper (NavMesh fallback)
    /// <summary>
    /// Intenta mover con NavMeshAgent; si est� deshabilitado o la ruta es inv�lida,
    /// hace un movimiento directo con transform (fallback) para evitar quedarse bloqueado.
    /// </summary>
    private void TryMoveTo(Vector3 destination)
    {
        if (agente == null)
        {
            // sin NavMeshAgent -> movimiento simple
            transform.position = Vector3.MoveTowards(transform.position, destination, 3f * Time.deltaTime);
            return;
        }

        // asegurar agente usable
        if (!agente.enabled)
        {
            agente.enabled = true;
        }

        // si por alguna raz�n el agente no est� en NavMesh (versi�n Unity con isOnNavMesh)
#if UNITY_2020_1_OR_NEWER
        if (!agente.isOnNavMesh)
        {
            // fallback: moverse manualmente
            transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(agente.speed, 3f) * Time.deltaTime);
            return;
        }
#endif
        // activamos agente y pedimos destino
        agente.isStopped = false;
        bool ok = agente.SetDestination(destination);

        // si SetDestination no pudo o la ruta es inv�lida, hace fallback manual
        if (!ok)
        {
            agente.ResetPath();
            transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(agente.speed, 3f) * Time.deltaTime);
            return;
        }

        // si la ruta qued� inv�lida luego de calcularla, fallback manual
        if (!agente.pathPending && agente.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            agente.ResetPath();
            transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(agente.speed, 3f) * Time.deltaTime);
        }
    }
    #endregion
}

/// <summary>
/// Extensiones utiles para Vector3 (colocar en un solo archivo si prefieres).
/// </summary>
public static class Vector3Extensions
{
    public static Vector3 Flat(this Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}


//// EnemigoEscudoAI.cs
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.AI;

///// <summary>
///// EnemigoEscudoAI: mueve al enemigo, gestiona proteccion de aliados, escudo frontal,
///// activa "armadura demonica" (golpea aliados cercanos con reduccion) y delega da�o al VidaEnemigoEscudo.
///// </summary>
//[RequireComponent(typeof(NavMeshAgent))]
//public class EnemigoEscudoAI : MonoBehaviour
//{
//    [Header("Refs")]
//    [Tooltip("Script que maneja salud y UI")]
//    [SerializeField] private VidaEnemigoEscudo enemyHealth;
//    [Tooltip("Componente que spawnea el prefab de ataque")]
//    [SerializeField] private EnemyMeleeAttack meleeAttack;
//    [Tooltip("GameObject hijo que contiene collider y EnemyShieldBehaviour")]
//    [SerializeField] private GameObject shieldObject;
//    [SerializeField] private float anguloFrontal = 120f;
//    [SerializeField] private bool escudoFrontalActivo = false;

//    [Header("Movimiento / IA")]
//    [SerializeField] private float radioBusquedaAliados = 15f;
//    [SerializeField] private float distanciaFrenteAliado = 1.2f;
//    [SerializeField] private LayerMask capaAliados = ~0;
//    [SerializeField] private float toleranciaPosicionProteccion = 0.3f;

//    [Header("Armadura Demonica")]
//    [SerializeField] private float reduccionDanioPercent = 0.25f;
//    [SerializeField] private float duracionArmadura = 10f;
//    [SerializeField] private float cooldownArmadura = 4.5f;
//    [SerializeField] private float radioArmaduraAfecto = 8f;

//    private NavMeshAgent agente;
//    private EnemyShieldBehaviour shieldBehaviour;
//    private Transform playerTransform;
//    private PlayerHealth playerHealth;

//    // proteccion
//    private GameObject aliadoProtegido = null;
//    private Vector3 posicionObjetivoProteccion;
//    private bool estaProtegiendo = false;

//    // armadura
//    private bool armaduraActiva = false;
//    private bool armaduraEnCooldown = false;
//    private Coroutine corrutinaArmadura = null;

//    private void Awake()
//    {
//        agente = GetComponent<NavMeshAgent>();

//        if (enemyHealth == null) enemyHealth = GetComponent<VidaEnemigoEscudo>();
//        if (meleeAttack == null) meleeAttack = GetComponent<EnemyMeleeAttack>();

//        // shield child auto-find
//        if (shieldObject == null)
//        {
//            var found = transform.Find("ShieldObject");
//            if (found != null) shieldObject = found.gameObject;
//        }

//        if (shieldObject != null)
//        {
//            shieldBehaviour = shieldObject.GetComponent<EnemyShieldBehaviour>();
//            if (shieldBehaviour == null)
//            {
//                Debug.LogWarning($"[{name}] shieldObject no tiene EnemyShieldBehaviour. Agr�galo para control de bloqueo.");
//            }
//            else
//            {
//                // PASAR como owner el componente que implemente IDamageable (preferentemente VidaEnemigoEscudo)
//                IDamageable owner = enemyHealth as IDamageable;
//                if (owner == null) owner = GetComponent<IDamageable>(); // fallback
//                if (owner != null)
//                {
//                    shieldBehaviour.SetOwner(owner);
//                }
//                else
//                {
//                    Debug.LogWarning($"[{name}] No se encontr� un IDamageable para asignar como owner del ShieldBehaviour.");
//                }

//                shieldBehaviour.SetFrontalAngle(anguloFrontal);
//                shieldBehaviour.SetActive(escudoFrontalActivo);
//                shieldObject.SetActive(escudoFrontalActivo);
//            }
//        }
//    }

//    private void Start()
//    {
//        var p = GameObject.FindGameObjectWithTag("Player");
//        playerTransform = p ? p.transform : null;
//        if (playerTransform == null) Debug.LogWarning($"[{name}] Jugador no encontrado en la escena.");
//        else playerTransform.TryGetComponent(out playerHealth);
//    }

//    private void Update()
//    {
//        // Prioridad: armadura cuando baja de vida
//        if (enemyHealth != null)
//        {
//            float hpPercent = enemyHealth.CurrentHealth / Mathf.Max(1f, enemyHealth.MaxHealth);

//            if (hpPercent <= 0.25f && !armaduraActiva && !armaduraEnCooldown)
//            {
//                ActivarArmaduraDemonica();
//                return;
//            }

//            // Si no esta en low HP, buscar aliado a proteger
//            if (hpPercent > 0.25f)
//            {
//                GameObject aliado = BuscarAliadoConMenorVidaEnRango(radioBusquedaAliados);
//                if (aliado != null)
//                {
//                    if (!estaProtegiendo || aliado != aliadoProtegido)
//                    {
//                        IniciarProteccion(aliado);
//                    }
//                    ActualizarPosicionProteccion();
//                    return;
//                }
//                else
//                {
//                    if (estaProtegiendo) DetenerProteccion();
//                }
//            }
//        }

//        // Si hay jugador: perseguir o intentar melee (delegado)
//        if (playerTransform != null)
//        {
//            float distanciaJugador = Vector3.Distance(transform.position, playerTransform.position);

//            if (meleeAttack != null && distanciaJugador <= meleeAttack.attackRange)
//            {
//                meleeAttack.TryAttack(playerTransform);
//            }
//            else
//            {
//                if (!estaProtegiendo && !armaduraActiva)
//                {
//                    agente.isStopped = false;
//                    agente.SetDestination(playerTransform.position);
//                }
//            }
//        }
//    }

//    #region Proteccion / posicionamiento / escudo frontal
//    private void IniciarProteccion(GameObject aliado)
//    {
//        aliadoProtegido = aliado;
//        estaProtegiendo = true;
//        DesplegarEscudoFrontal(true);
//    }

//    private void DetenerProteccion()
//    {
//        estaProtegiendo = false;
//        aliadoProtegido = null;
//        DesplegarEscudoFrontal(false);
//    }

//    private void ActualizarPosicionProteccion()
//    {
//        if (aliadoProtegido == null) return;

//        if (playerTransform == null)
//        {
//            Vector3 deseada = aliadoProtegido.transform.position - aliadoProtegido.transform.forward * distanciaFrenteAliado;
//            agente.isStopped = false;
//            agente.SetDestination(deseada);
//            return;
//        }

//        Vector3 dirAliadoAJugador = (playerTransform.position - aliadoProtegido.transform.position).normalized;
//        posicionObjetivoProteccion = aliadoProtegido.transform.position + dirAliadoAJugador * distanciaFrenteAliado;

//        agente.isStopped = false;
//        agente.SetDestination(posicionObjetivoProteccion);

//        if (Vector3.Distance(transform.position, posicionObjetivoProteccion) <= toleranciaPosicionProteccion)
//        {
//            agente.isStopped = true;
//            Vector3 lookDir = (playerTransform.position - transform.position).Flat();
//            if (lookDir.sqrMagnitude > 0.001f)
//            {
//                Quaternion rot = Quaternion.LookRotation(lookDir);
//                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
//            }
//        }
//    }

//    private void DesplegarEscudoFrontal(bool activar)
//    {
//        escudoFrontalActivo = activar;
//        if (shieldObject != null) shieldObject.SetActive(activar);
//        if (shieldBehaviour != null) shieldBehaviour.SetActive(activar);
//    }
//    #endregion

//    #region Armadura demonica
//    private void ActivarArmaduraDemonica()
//    {
//        if (corrutinaArmadura != null) StopCoroutine(corrutinaArmadura);
//        corrutinaArmadura = StartCoroutine(RutinaArmaduraDemonica());
//    }

//    private IEnumerator RutinaArmaduraDemonica()
//    {
//        armaduraActiva = true;
//        float reduccion = reduccionDanioPercent;

//        // Aplicar reduccion a si mismo via VidaEnemigoEscudo (metodo ApplyDamageReduction)
//        if (enemyHealth != null)
//        {
//            enemyHealth.ApplyDamageReduction(reduccion, duracionArmadura);
//        }

//        // Aplicar reduccion a aliados cercanos que sean enemigos (ahora por layer "Enemy")
//        Collider[] hits = Physics.OverlapSphere(transform.position, radioArmaduraAfecto);
//        int enemyLayer = LayerMask.NameToLayer("Enemy");
//        foreach (var c in hits)
//        {
//            GameObject go = c.gameObject;
//            if (go == this.gameObject) continue;

//            if (go.layer != enemyLayer) continue;

//            IntentarAplicarReduccionDanio(go, reduccion, duracionArmadura);
//        }

//        if (estaProtegiendo) DetenerProteccion();

//        yield return new WaitForSeconds(duracionArmadura);

//        armaduraActiva = false;

//        armaduraEnCooldown = true;
//        StartCoroutine(RutinaCooldownArmadura());
//        yield break;
//    }

//    private IEnumerator RutinaCooldownArmadura()
//    {
//        yield return new WaitForSeconds(cooldownArmadura);
//        armaduraEnCooldown = false;
//    }

//    /// <summary>
//    /// Intenta aplicar la reduccion llamando ApplyDamageReduction si existe VidaEnemigoEscudo,
//    /// si no busca metodos publicos o usa SendMessage como fallback.
//    /// </summary>
//    private bool IntentarAplicarReduccionDanio(GameObject target, float reduccionPercent, float duracion)
//    {
//        if (target == null) return false;

//        // preferir VidaEnemigoEscudo.ApplyDamageReduction
//        if (target.TryGetComponent<VidaEnemigoEscudo>(out var eh))
//        {
//            try
//            {
//                eh.ApplyDamageReduction(reduccionPercent, duracion);
//                return true;
//            }
//            catch { /* ignore */ }
//        }

//        // luego intentar metodos con reflection (nombres comunes)
//        var comps = target.GetComponents<MonoBehaviour>();
//        foreach (var comp in comps)
//        {
//            if (comp == null) continue;
//            var t = comp.GetType();

//            string[] nombresMetodos = {
//                "ApplyDamageReduction", "ApplyArmor", "AddDamageModifier", "AddDamageReduction",
//                "AplicarReduccionDanio", "AplicarArmadura", "AgregarModificadorDanio", "AgregarReduccionDanio"
//            };

//            foreach (var mname in nombresMetodos)
//            {
//                var method = t.GetMethod(mname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
//                if (method != null)
//                {
//                    try
//                    {
//                        method.Invoke(comp, new object[] { reduccionPercent, duracion });
//                        return true;
//                    }
//                    catch { }
//                }
//            }
//        }

//        // fallback: SendMessage (no lanza si no existe)
//        try
//        {
//            target.SendMessage("ApplyDamageReduction", new object[] { reduccionPercent, duracion }, SendMessageOptions.DontRequireReceiver);
//            target.SendMessage("AplicarReduccionDanio", new object[] { reduccionPercent, duracion }, SendMessageOptions.DontRequireReceiver);
//            return true;
//        }
//        catch
//        {
//            return false;
//        }
//    }
//    #endregion

//    #region Danio / Integracion con Shield
//    /// <summary>
//    /// Metodo publico que otras mecanicas llaman. Comprueba el escudo frontal para proyectiles y
//    /// delega el da�o real al VidaEnemigoEscudo.
//    /// </summary>
//    public void TomarDanio(float cantidad, Vector3 posicionOrigen, GameObject objetoOrigen = null)
//    {
//        // manejo de proyectil-escudo
//        if (escudoFrontalActivo && objetoOrigen != null)
//        {
//            try
//            {
//                if (objetoOrigen.CompareTag("Escudo"))
//                {
//                    if (shieldBehaviour != null)
//                    {
//                        bool handled = shieldBehaviour.HandleProjectileHit(objetoOrigen, posicionOrigen);
//                        if (handled) return;
//                    }
//                }
//            }
//            catch (UnityException) { /* tag podria no existir -> seguir flujo */ }
//        }

//        // aplicar da�o real en el componente de vida
//        if (enemyHealth != null)
//        {
//            enemyHealth.TakeDamage(cantidad, isCritical: false);
//        }
//    }
//    #endregion

//    #region Buscar aliado y utilitarios
//    private GameObject BuscarAliadoConMenorVidaEnRango(float radio)
//    {
//        Collider[] hits = Physics.OverlapSphere(transform.position, radio);
//        GameObject mejor = null;
//        float mejorPercent = 2f;
//        int enemyLayer = LayerMask.NameToLayer("Enemy");

//        foreach (var c in hits)
//        {
//            GameObject go = c.gameObject;
//            if (go == this.gameObject) continue;

//            if (go.layer != enemyLayer) continue;

//            if (IntentarObtenerPercentVida(go, out float percent))
//            {
//                if (percent < mejorPercent)
//                {
//                    mejorPercent = percent;
//                    mejor = go;
//                }
//            }
//        }
//        return mejor;
//    }

//    private bool IntentarObtenerPercentVida(GameObject go, out float percent)
//    {
//        percent = 1f;

//        // si tiene VidaEnemigoEscudo, usarlo
//        if (go.TryGetComponent<VidaEnemigoEscudo>(out var eh))
//        {
//            if (eh.MaxHealth > 0f)
//            {
//                percent = Mathf.Clamp01(eh.CurrentHealth / eh.MaxHealth);
//                return true;
//            }
//            return false;
//        }

//        // fallback: inspeccionar componentes por nombres comunes
//        var comps = go.GetComponents<MonoBehaviour>();
//        foreach (var comp in comps)
//        {
//            if (comp == null) continue;
//            var t = comp.GetType();

//            string[] posiblesActual = { "currentHealth", "CurrentHealth", "health", "Health", "hp", "HP", "saludActual", "salud", "vida", "Vida" };
//            string[] posiblesMax = { "maxHealth", "MaxHealth", "maxHP", "MaxHP", "maxhp", "saludMax", "saludMaxima", "vidaMax" };

//            float? cur = null;
//            float? max = null;

//            foreach (var name in posiblesActual)
//            {
//                var prop = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
//                if (prop != null && (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int)))
//                {
//                    object val = prop.GetValue(comp);
//                    cur = Convert.ToSingle(val);
//                    break;
//                }
//                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
//                if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
//                {
//                    object val = field.GetValue(comp);
//                    cur = Convert.ToSingle(val);
//                    break;
//                }
//            }

//            foreach (var name in posiblesMax)
//            {
//                var prop = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
//                if (prop != null && (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int)))
//                {
//                    object val = prop.GetValue(comp);
//                    max = Convert.ToSingle(val);
//                    break;
//                }
//                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
//                if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
//                {
//                    object val = field.GetValue(comp);
//                    max = Convert.ToSingle(val);
//                    break;
//                }
//            }

//            if (cur.HasValue && max.HasValue && max.Value > 0f)
//            {
//                percent = Mathf.Clamp01(cur.Value / max.Value);
//                return true;
//            }
//        }
//        return false;
//    }
//    #endregion

//    #region Gizmos y diagnostico
//    private void OnDrawGizmos()
//    {
//        Gizmos.color = Color.green;
//        Gizmos.DrawRay(transform.position, Vector3.up * 1.5f);
//        Gizmos.DrawWireSphere(transform.position, radioBusquedaAliados);
//        Gizmos.DrawWireSphere(transform.position, radioArmaduraAfecto);
//    }
//    #endregion
//}

///// <summary>
///// Extensiones utiles para Vector3 (colocar en un solo archivo si prefieres).
///// </summary>
//public static class Vector3Extensions
//{
//    public static Vector3 Flat(this Vector3 v)
//    {
//        return new Vector3(v.x, 0f, v.z);
//    }
//}


