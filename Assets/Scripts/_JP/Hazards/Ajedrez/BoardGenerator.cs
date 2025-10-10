using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoardGenerator : MonoBehaviour
{
    [Header("Opciones")]
    [Tooltip("Si true genera automaticamente una cuadricula. Si false, recogera casillas existentes en 'padreCasillas' o en la escena.")]
    public bool generarAutomatico = false;

    [Header("Prefabs casillas (solo si generarAutomatico true)")]
    public GameObject prefabCasillaBlanca;
    public GameObject prefabCasillaNegra;

    [Header("Parametros tablero (solo si generarAutomatico true)")]
    public int ancho = 8;
    public int alto = 8;
    public float tamCasilla = 1f;
    public Transform padreCasillas;

    [Tooltip("Lista visible en inspector con las casillas recogidas/generadas.")]
    public List<Casilla> casillasList = new List<Casilla>();

    // Diccionario interno para búsquedas rápidas por coordenada
    private Dictionary<Vector2Int, Casilla> casillas = new Dictionary<Vector2Int, Casilla>();

    // Reserva con info (owner + eta)
    private class ReservationInfo { public GameObject owner; public float eta; }
    private Dictionary<Vector2Int, ReservationInfo> routeReservations = new Dictionary<Vector2Int, ReservationInfo>();

    // Si un owner ya empezó a moverse por su ruta, lo marcamos aquí (no puede ser preemptado)
    private HashSet<GameObject> startedOwners = new HashSet<GameObject>();

    // tiempo de reintento en caso las casillas se creen justo después de Start
    public int reintentosRecolectar = 30; // frames
    public float delayEntreReintentos = 0.05f;

    void Start()
    {
        EnsureReady();
        if (casillas.Count == 0) StartCoroutine(RetryCollectRoutine());
    }

    public int CasillasCount => casillas != null ? casillas.Count : 0;

    public void EnsureReady()
    {
        if (CasillasCount > 0) return;

        if (generarAutomatico)
        {
            if (prefabCasillaBlanca == null || prefabCasillaNegra == null)
            {
                Debug.LogWarning($"BoardGenerator: generarAutomatico=true pero faltan prefabs. Intentando recolectar casillas existentes.");
                RecolectarCasillasExistentes();
                return;
            }
            GenerarTableroAutomatico();
            SyncListFromDict();
        }
        else
        {
            if (casillasList != null && casillasList.Count > 0) SyncDictFromList();
            else RecolectarCasillasExistentes();
        }
    }

    #region generación / recolección (sin cambios funcionales relevantes)
    public void GenerarTableroAutomatico()
    {
        if (padreCasillas != null)
        {
            for (int i = padreCasillas.childCount - 1; i >= 0; i--)
                DestroyImmediate(padreCasillas.GetChild(i).gameObject);
        }

        casillas.Clear();
        casillasList.Clear();

        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                bool esBlanca = (x + y) % 2 == 0;
                GameObject prefab = esBlanca ? prefabCasillaBlanca : prefabCasillaNegra;
                Vector3 pos = transform.position + new Vector3(x * tamCasilla, 0f, y * tamCasilla);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity, padreCasillas);
                go.name = $"Casilla_{x}_{y}";
                Casilla c = go.GetComponent<Casilla>();
                if (c == null) c = go.AddComponent<Casilla>();
                c.Inicializar(new Vector2Int(x, y), pos, tamCasilla, this);

                casillas[new Vector2Int(x, y)] = c;
                casillasList.Add(c);
            }
        }
    }

    public void RecolectarCasillasExistentes()
    {
        if (casillasList != null && casillasList.Count > 0)
        {
            SyncDictFromList();
            return;
        }

        casillas.Clear();
        casillasList.Clear();

        Casilla[] encontradas;
        if (padreCasillas != null) encontradas = padreCasillas.GetComponentsInChildren<Casilla>(true);
        else
        {
            var todas = Resources.FindObjectsOfTypeAll<Casilla>();
            List<Casilla> lista = new List<Casilla>();
            foreach (var c in todas)
            {
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                lista.Add(c);
            }
            encontradas = lista.ToArray();
        }

        foreach (var c in encontradas)
        {
            if (c == null) continue;
            if (c.coord == new Vector2Int(int.MinValue, int.MinValue))
            {
                Vector2Int auto = AutoAsignarCoordPorPosicion(c.transform.position);
                while (casillas.ContainsKey(auto)) auto.x += 1;
                c.Inicializar(auto, c.transform.position, tamCasilla, this);
                Debug.Log($"BoardGenerator: Casilla '{c.name}' no tenía coord; se asignó {auto} automáticamente.");
            }
            else
            {
                c.Inicializar(c.coord, c.transform.position, c.tam > 0 ? c.tam : tamCasilla, this);
            }

            if (!casillas.ContainsKey(c.coord))
            {
                casillas[c.coord] = c;
                casillasList.Add(c);
            }
            else
            {
                Debug.LogWarning($"Coord duplicada detectada en casillas: {c.coord} en '{c.name}'. Se ignorará esta entrada duplicada.");
            }
        }

        if (casillas.Count == 0) Debug.LogWarning("BoardGenerator: no se encontraron casillas en la escena.");
    }

    private IEnumerator RetryCollectRoutine()
    {
        int tries = 0;
        while (tries < reintentosRecolectar && casillas.Count == 0)
        {
            RecolectarCasillasExistentes();
            if (casillas.Count > 0) break;
            tries++;
            yield return new WaitForSeconds(delayEntreReintentos);
        }
        if (casillas.Count == 0) Debug.LogWarning("BoardGenerator: RetryCollectRoutine terminó sin encontrar casillas.");
    }

    private void SyncDictFromList()
    {
        casillas.Clear();
        if (casillasList == null) casillasList = new List<Casilla>();
        foreach (var c in casillasList)
        {
            if (c == null) continue;
            if (!c.gameObject.scene.IsValid()) continue;
            if (c.coord == new Vector2Int(int.MinValue, int.MinValue))
            {
                Vector2Int auto = AutoAsignarCoordPorPosicion(c.transform.position);
                while (casillas.ContainsKey(auto)) auto.x += 1;
                c.Inicializar(auto, c.transform.position, tamCasilla, this);
                Debug.Log($"BoardGenerator: (SyncDictFromList) Casilla '{c.name}' no tenía coord; se asignó {auto} automáticamente.");
            }
            else
            {
                c.Inicializar(c.coord, c.transform.position, c.tam > 0 ? c.tam : tamCasilla, this);
            }

            if (!casillas.ContainsKey(c.coord)) casillas[c.coord] = c;
            else Debug.LogWarning($"SyncDictFromList: coord duplicada {c.coord} en '{c.name}', ignorada.");
        }
    }

    private void SyncListFromDict()
    {
        if (casillasList == null) casillasList = new List<Casilla>();
        casillasList.Clear();
        foreach (var kv in casillas) if (kv.Value != null) casillasList.Add(kv.Value);
    }

    private Vector2Int AutoAsignarCoordPorPosicion(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        int x = Mathf.RoundToInt(local.x / tamCasilla);
        int y = Mathf.RoundToInt(local.z / tamCasilla);
        return new Vector2Int(x, y);
    }
    #endregion

    #region API de casillas (fallbacks y limpieza)
    public bool ExisteCasilla(Vector2Int coord)
    {
        if (casillas.ContainsKey(coord)) return true;
        if (casillasList != null)
        {
            foreach (var c in casillasList)
            {
                if (c != null && c.coord == coord)
                {
                    casillas[coord] = c;
                    return true;
                }
            }
        }
        return false;
    }

    public Casilla GetCasilla(Vector2Int coord)
    {
        if (casillas.TryGetValue(coord, out Casilla c)) return c;
        if (casillasList != null)
        {
            foreach (var item in casillasList)
            {
                if (item != null && item.coord == coord)
                {
                    casillas[coord] = item;
                    return item;
                }
            }
        }

        RecolectarCasillasExistentes(); // último recurso
        casillas.TryGetValue(coord, out c);
        return c;
    }

    public Vector3 GetWorldPos(Vector2Int coord)
    {
        Casilla c = GetCasilla(coord);
        return c != null ? c.worldPos : Vector3.zero;
    }

    public IEnumerable<Casilla> GetAllCasillas() => casillasList;
    #endregion

    #region Reserva/Coordinación (nuevas funciones)
    /// <summary>
    /// Solicita reservar una ruta completa. Devuelve true si se pudo reservar.
    /// La prioridad la gana quien tenga menor 'eta' (si la reserva actual NO ha empezado todavía).
    /// No preemptamos reservas cuyos owners ya empezaron a moverse.
    /// </summary>
    public bool RequestRouteReservation(List<Vector2Int> ruta, GameObject requester, float eta)
    {
        if (ruta == null || ruta.Count == 0) return false;
        // validaciones rápidas: no reservar casillas ocupadas por terceros
        foreach (var coord in ruta)
        {
            var c = GetCasilla(coord);
            if (c == null) return false;
            if (c.tieneOcupante && c.ocupanteGO != requester) return false;

            if (routeReservations.TryGetValue(coord, out ReservationInfo existing) && existing.owner != requester)
            {
                // si el owner ya empezó, no lo preemptamos
                if (startedOwners.Contains(existing.owner)) return false;

                // si el requestor es más rápido, preemptamos la reserva (liberamos las casillas conflictivas)
                if (eta + 0.001f < existing.eta)
                {
                    ReleaseAllReservationsOfOwner(existing.owner);
                    // continue para evaluar el resto
                }
                else
                {
                    // existing tiene prioridad o mismo eta -> no reservar
                    return false;
                }
            }
        }

        // si llegamos acá, podemos reservar la ruta completamente
        foreach (var coord in ruta)
        {
            var c = GetCasilla(coord);
            if (c == null) continue;
            // asignar reservadoPor en la casilla (solo si está libre o ya reservado por requester)
            if (c.reservadoPor == null || c.reservadoPor == requester) c.reservadoPor = requester;
            routeReservations[coord] = new ReservationInfo() { owner = requester, eta = eta };
        }
        return true;
    }

    /// <summary>
    /// Libera una lista de coordenadas reservadas por 'requester' (compatibilidad con uso previo).
    /// </summary>
    public void ReleaseRutaReservation(List<Vector2Int> ruta, GameObject requester)
    {
        if (ruta == null) return;
        foreach (var coord in ruta)
        {
            ReleaseRutaReservationForCoord(coord, requester);
        }
    }

    /// <summary>
    /// Libera la reserva de una sola casilla si pertenece a 'requester'.
    /// </summary>
    public void ReleaseRutaReservationForCoord(Vector2Int coord, GameObject requester)
    {
        var c = GetCasilla(coord);
        if (c != null && c.reservadoPor == requester) c.reservadoPor = null;
        if (routeReservations.TryGetValue(coord, out ReservationInfo info) && info.owner == requester)
        {
            routeReservations.Remove(coord);
        }
    }

    /// <summary>
    /// Libera todas las reservas asociadas a 'owner' (usado para preemption).
    /// </summary>
    public void ReleaseAllReservationsOfOwner(GameObject owner)
    {
        if (owner == null) return;
        var keys = routeReservations.Where(kv => kv.Value.owner == owner).Select(kv => kv.Key).ToList();
        foreach (var k in keys)
        {
            var c = GetCasilla(k);
            if (c != null && c.reservadoPor == owner) c.reservadoPor = null;
            routeReservations.Remove(k);
        }
        startedOwners.Remove(owner);
    }

    /// <summary>
    /// Llamar cuando el owner comienza a ejecutar la ruta (evita preempt por otros).
    /// </summary>
    public void NotifyRouteStarted(GameObject owner)
    {
        if (owner == null) return;
        startedOwners.Add(owner);
    }

    /// <summary>
    /// Llamar cuando el owner ha acabado su ruta (limpia marcas internas).
    /// </summary>
    public void NotifyRouteFinished(GameObject owner)
    {
        if (owner == null) return;
        startedOwners.Remove(owner);
        // limpiar cualquier reserva residuo por owner (seguridad)
        ReleaseAllReservationsOfOwner(owner);
    }
    #endregion

    #region Raycasts / utilidades (sin cambios relevantes)
    public List<Casilla> RayCasillasEnDireccion(Vector2Int inicio, Vector2Int dir)
    {
        List<Casilla> lista = new List<Casilla>();
        Vector2Int cur = inicio + dir;
        while (ExisteCasilla(cur))
        {
            Casilla c = GetCasilla(cur);
            lista.Add(c);
            if (c.tieneOcupante) break;
            cur += dir;
        }
        return lista;
    }
    #endregion

    #region Editor tools
    [ContextMenu("Refresh Casillas (Recolectar)")]
    public void ContextRefreshCasillas() { RecolectarCasillasExistentes(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (casillasList == null) casillasList = new List<Casilla>();
            casillasList.RemoveAll(item => item == null);
            SyncDictFromList();
        }
    }
#endif
    #endregion
}

