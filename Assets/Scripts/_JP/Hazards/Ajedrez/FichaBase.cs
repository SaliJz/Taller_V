using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class FichaBase : MonoBehaviour
{
    [Header("Referencias")]
    public BoardGenerator tablero;
    public GameObject marcadorTrailPrefab;
    public string tagJugador = "Player";

    [Header("Parametros comportamiento")]
    public Vector2Int coordActual = new Vector2Int(int.MinValue, int.MinValue);
    public float tamCasilla = 1f;
    public float tiempoEsperaInicial = 1f;
    public float tiempoAntesMover = 1f;
    public float tilesPorSegundo = 6f;
    public int dano = 10;
    public float fuerzaEmpuje = 5f;

    [Header("Dano por colision")]
    public bool danoPorColision = true;
    public float cooldownDanoColision = 0.6f; // segundos entre danos por contacto

    // (Opcionales - ya no usados para la detección manual)
    [Header("Deteccion sin Rigidbody/Triggers (la ficha)")]
    [Tooltip("Radio de deteccion desde el centro de la ficha para detectar al player")]
    public float detectionRadius = 0.6f;
    [Tooltip("Capa del player para optimizar OverlapSphere (usa la capa del player)")]
    public LayerMask playerLayer = ~0;
    [Tooltip("Cada cuanto comprobar solapamiento en segundos")]
    public float manualCheckInterval = 0.12f;

    protected bool estaMoviendo = false;
    protected Transform jugadorTransform;

    // control interno
    private float ultimoDanoPorColision = -Mathf.Infinity;

    // para detectar direccion de movimiento de la pieza atacante (si se mueve por transform)
    private Vector3 _lastPosition;
    private Vector3 _lastMovementDir = Vector3.zero;
    private const float MOVEMENT_EPS = 1e-6f;

    // cache del collider de la ficha (si lo necesitas)
    private Collider _col;

    protected virtual void Start()
    {
        if (tablero == null) tablero = FindObjectOfType<BoardGenerator>();
        _lastPosition = transform.position;
        _col = GetComponent<Collider>();
        StartCoroutine(InitializeRoutine());
    }

    private IEnumerator InitializeRoutine()
    {
        if (tablero != null) tablero.EnsureReady();
        int attempts = 0;
        while ((tablero == null || tablero.CasillasCount == 0) && attempts < 20)
        {
            attempts++;
            yield return null;
        }
        if (tablero == null || tablero.CasillasCount == 0)
        {
            Debug.LogWarning($"Ficha '{name}': no hay tablero/casillas.");
            yield break;
        }

        if (coordActual == new Vector2Int(int.MinValue, int.MinValue))
        {
            float minDist = float.MaxValue;
            Casilla mejor = null;
            foreach (var c in tablero.GetAllCasillas())
            {
                if (c == null) continue;
                float d = Vector3.Distance(new Vector3(c.worldPos.x, 0f, c.worldPos.z), new Vector3(transform.position.x, 0f, transform.position.z));
                if (d < minDist)
                {
                    minDist = d;
                    mejor = c;
                }
            }
            if (mejor != null)
            {
                coordActual = mejor.coord;
                transform.position = mejor.worldPos + Vector3.up * 0.1f;
                mejor.SetOcupante(gameObject);
            }
        }
        else
        {
            var cas = tablero.GetCasilla(coordActual);
            if (cas != null)
            {
                cas.SetOcupante(gameObject);
                transform.position = cas.worldPos + Vector3.up * 0.1f;
            }
        }

        foreach (var c in tablero.GetAllCasillas())
        {
            if (c != null) { tamCasilla = c.tam; break; }
        }

        var pj = GameObject.FindGameObjectWithTag(tagJugador);
        if (pj != null) jugadorTransform = pj.transform;
    }

    void Update()
    {
        // actualizar deteccion de movimiento de la ficha (si se mueve por transform)
        Vector3 delta = transform.position - _lastPosition;
        if (Time.deltaTime > 0f)
        {
            if (delta.sqrMagnitude > MOVEMENT_EPS)
            {
                _lastMovementDir = delta.normalized;
            }
            _lastPosition = transform.position;
        }

        // Nota: detección manual por OverlapSphere removida — ahora solo daño por colisión física
        if (!estaMoviendo) RevisarDetectarJugador();
    }

    public abstract List<Vector2Int> ObtenerCasillasPosibles();

    protected void RevisarDetectarJugador()
    {
        if (tablero == null) return;
        if (jugadorTransform == null)
        {
            var pj = GameObject.FindGameObjectWithTag(tagJugador);
            if (pj != null) jugadorTransform = pj.transform;
        }
        if (jugadorTransform == null) return;

        Vector3 posJugador = jugadorTransform.position;
        Vector2Int coordJugador = WorldPosACoord(posJugador);

        List<Vector2Int> posibles = ObtenerCasillasPosibles();
        if (posibles.Contains(coordJugador))
        {
            StartCoroutine(RutinaAtaque(coordJugador));
        }
    }

    protected IEnumerator RutinaAtaque(Vector2Int destino)
    {
        estaMoviendo = true;

        yield return new WaitForSeconds(tiempoEsperaInicial);

        List<Vector2Int> ruta = CalcularRutaHasta(destino);
        if (ruta == null || ruta.Count == 0)
        {
            estaMoviendo = false;
            yield break;
        }

        foreach (var c in ruta)
        {
            var cas = tablero.GetCasilla(c);
            if (cas != null) cas.AgregarMarca(marcadorTrailPrefab, tiempoAntesMover + (ruta.Count / tilesPorSegundo) + 0.2f);
        }

        yield return new WaitForSeconds(tiempoAntesMover);

        bool completed = false;
        int recomputeAttempts = 0;
        while (!completed && recomputeAttempts < 6)
        {
            ruta = CalcularRutaHasta(destino);
            if (ruta == null || ruta.Count == 0) break;

            bool aborted = false;
            foreach (var paso in ruta)
            {
                if (jugadorTransform == null)
                {
                    var pj = GameObject.FindGameObjectWithTag(tagJugador);
                    if (pj != null) jugadorTransform = pj.transform;
                }
                if (jugadorTransform == null) { aborted = true; break; }

                Casilla llegada = tablero.GetCasilla(paso);
                if (llegada == null) { aborted = true; break; }

                if (llegada.tieneOcupante && llegada.ocupanteGO != null && llegada.ocupanteGO.CompareTag(tagJugador))
                {
                    // atacar si jugador ya esta ahi
                    AttackPlayer(transform.position);
                    aborted = true;
                    break;
                }

                int stepTries = 0;
                bool reserved = false;
                while (stepTries < 8 && !reserved)
                {
                    stepTries++;
                    if (llegada.TryReserve(gameObject)) reserved = true;
                    else yield return new WaitForSeconds(0.05f);
                }

                if (!reserved)
                {
                    Vector2Int dir = new Vector2Int(Mathf.Clamp(paso.x - coordActual.x, -1, 1), Mathf.Clamp(paso.y - coordActual.y, -1, 1));
                    bool movedToAlt = false;
                    Vector2Int[] alternativos = new Vector2Int[]
                    {
                        coordActual + new Vector2Int(dir.x, 0),
                        coordActual + new Vector2Int(0, dir.y)
                    };
                    foreach (var alt in alternativos)
                    {
                        if (!tablero.ExisteCasilla(alt)) continue;
                        var cAlt = tablero.GetCasilla(alt);
                        if (cAlt == null) continue;
                        if (!cAlt.tieneOcupante && (cAlt.reservadoPor == null || cAlt.reservadoPor == gameObject))
                        {
                            if (cAlt.TryReserve(gameObject))
                            {
                                yield return StartCoroutine(MoverPaso(alt));
                                movedToAlt = true;
                                break;
                            }
                        }
                    }
                    if (movedToAlt) continue;

                    aborted = true;
                    break;
                }

                yield return StartCoroutine(MoverPaso(paso));

                llegada.ReleaseReservation(gameObject);
            }

            if (!aborted)
            {
                completed = true;
                break;
            }

            recomputeAttempts++;
            yield return new WaitForSeconds(0.05f);
        }

        if (!completed && jugadorTransform != null)
        {
            Vector2Int coordJugador = WorldPosACoord(jugadorTransform.position);
            if (coordJugador == destino)
            {
                if (EsAdyacente(coordActual, coordJugador))
                {
                    AttackPlayer(transform.position);
                }
            }
        }

        estaMoviendo = false;
    }

    private IEnumerator MoverPaso(Vector2Int destinoPaso)
    {
        Vector3 target = tablero.GetWorldPos(destinoPaso) + Vector3.up * 0.1f;
        Vector3 start = transform.position;
        float distancia = Vector3.Distance(start, target);
        float velocidad = tilesPorSegundo * tamCasilla;
        float tiempo = velocidad > 0 ? distancia / velocidad : 0.1f;
        float t = 0f;

        Casilla origen = tablero.GetCasilla(coordActual);
        if (origen != null) origen.SetOcupante(null);
        Casilla llegada = tablero.GetCasilla(destinoPaso);
        if (llegada != null) llegada.SetOcupante(gameObject);

        while (t < tiempo)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, velocidad * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = target;
        coordActual = destinoPaso;
    }

    // --- HANDLERS DE COLISION / TRIGGER (solo daño por colisión) ---
    void OnCollisionEnter(Collision collision)
    {
        if (!danoPorColision) return;
        if (Time.time - ultimoDanoPorColision < cooldownDanoColision) return;
        if (collision == null) return;

        // Buscar jugador en la jerarquía del objeto colisionado
        Transform t = collision.transform;
        Transform found = null;
        while (t != null)
        {
            if (t.CompareTag(tagJugador))
            {
                found = t;
                break;
            }
            t = t.parent;
        }

        if (found != null)
        {
            ultimoDanoPorColision = Time.time;
            Vector3 contactPoint = collision.GetContact(0).point;
            AttackPlayer(contactPoint);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!danoPorColision) return;
        if (Time.time - ultimoDanoPorColision < cooldownDanoColision) return;
        if (other == null) return;
        if (other.gameObject == gameObject) return;

        // Buscar jugador en la jerarquía del collider entrante
        Transform t = other.transform;
        Transform found = null;
        while (t != null)
        {
            if (t.CompareTag(tagJugador))
            {
                found = t;
                break;
            }
            t = t.parent;
        }

        if (found != null)
        {
            ultimoDanoPorColision = Time.time;
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            AttackPlayer(contactPoint);
        }
    }

    // ---- ATTACK: lateral push sin componente vertical aumentado ----
    protected void AttackPlayer(Vector3 attackerPosition)
    {
        GameObject playerGO = (jugadorTransform != null) ? jugadorTransform.gameObject : GameObject.FindWithTag(tagJugador);
        if (playerGO == null)
        {
            Debug.LogWarning($"Ficha '{name}': no se encontro jugador para atacar.");
            return;
        }

        // bajar vida (misma llamada que la version anterior)
        var ph = playerGO.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            try
            {
                ph.TakeDamage(dano);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Ficha '{name}': error al llamar TakeDamage: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Ficha '{name}': PlayerHealth no encontrado en '{playerGO.name}'.");
        }

        // empujar usando el Rigidbody del player (SIN componente vertical nuevo)
        Rigidbody rbPlayer = playerGO.GetComponent<Rigidbody>();
        if (rbPlayer == null) rbPlayer = playerGO.GetComponentInChildren<Rigidbody>();
        if (rbPlayer == null) rbPlayer = playerGO.GetComponentInParent<Rigidbody>();

        if (rbPlayer != null && !rbPlayer.isKinematic)
        {
            // calcular direccion horizontal (XZ) desde attackerPosition hacia player
            Vector3 dir = playerGO.transform.position - attackerPosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < MOVEMENT_EPS)
            {
                // fallback usar vector desde attacker transform.forward proyectado en XZ
                dir = transform.forward;
                dir.y = 0f;
            }
            dir.Normalize();

            // magnitud del impulso (se puede ajustar segun 'fuerzaEmpuje' y masa del player)
            float massFactor = Mathf.Max(1f, rbPlayer.mass);
            float impulseMag = fuerzaEmpuje * Mathf.Sqrt(massFactor);
            Vector3 impulse = dir * impulseMag;

            // preservamos componente vertical previa para evitar que el AddForce la modifique
            float prevVy = rbPlayer.linearVelocity.y;

            rbPlayer.AddForce(impulse, ForceMode.Impulse);

            // restauramos la componente vertical exactamente como estaba antes del empuje
            rbPlayer.linearVelocity = new Vector3(rbPlayer.linearVelocity.x, prevVy, rbPlayer.linearVelocity.z);
        }
        else
        {
            // fallback si por algun motivo no hay rigidbody (mantenemos el manual fallback)
            float massFactorFallback = 1f;
            if (rbPlayer != null) massFactorFallback = Mathf.Max(1f, rbPlayer.mass);

            Vector3 dir = (playerGO.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < MOVEMENT_EPS) dir = Vector3.forward;
            dir.Normalize();
            Vector3 deltaFallback = new Vector3(dir.x, 0f, dir.z) * Mathf.Max(0.25f, fuerzaEmpuje * 0.12f * Mathf.Sqrt(massFactorFallback));
            float duration = Mathf.Clamp(0.12f + fuerzaEmpuje * 0.03f, 0.12f, 0.5f);

            StartCoroutine(ManualPushFallback(playerGO.transform, deltaFallback, duration));
        }
    }

    private IEnumerator ManualPushFallback(Transform target, Vector3 delta, float duration)
    {
        if (target == null) yield break;
        Vector3 start = target.position;
        Vector3 end = new Vector3(start.x + delta.x, start.y, start.z + delta.z); // conservar altura original
        float t = 0f;
        while (t < duration)
        {
            if (target == null) yield break;
            Vector3 lerp = Vector3.Lerp(start, end, t / duration);
            lerp.y = start.y;
            target.position = lerp;
            t += Time.deltaTime;
            yield return null;
        }
        if (target != null) target.position = end;
    }

    protected virtual List<Vector2Int> CalcularRutaHasta(Vector2Int destino)
    {
        List<Vector2Int> ruta = new List<Vector2Int>();
        Vector2Int actual = coordActual;
        Vector2Int dir = new Vector2Int(Mathf.Clamp(destino.x - actual.x, -1, 1), Mathf.Clamp(destino.y - actual.y, -1, 1));
        while (actual != destino)
        {
            actual += dir;
            ruta.Add(actual);
            if (!tablero.ExisteCasilla(actual)) break;
            Casilla c = tablero.GetCasilla(actual);
            if (c != null && c.tieneOcupante) break;
        }
        return ruta;
    }

    protected Vector2Int WorldPosACoord(Vector3 world)
    {
        if (tablero == null) return Vector2Int.zero;
        int bestX = -1, bestY = -1;
        float minDist = float.MaxValue;
        foreach (var cas in tablero.GetAllCasillas())
        {
            if (cas == null) continue;
            Vector3 w = cas.worldPos;
            float d = Vector3.Distance(new Vector3(w.x, 0f, w.z), new Vector3(world.x, 0f, world.z));
            if (d < minDist)
            {
                minDist = d;
                bestX = cas.coord.x;
                bestY = cas.coord.y;
            }
        }
        if (bestX == -1) return Vector2Int.zero;
        return new Vector2Int(bestX, bestY);
    }

    private bool EsAdyacente(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) == 1 || (dx == 1 && dy == 1);
    }

    // Gizmo para ver el radio de deteccion en Scene (solo en editor)
    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (_col == null) _col = GetComponent<Collider>();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
#endif
    }
}
