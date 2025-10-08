using System.Collections;
using System.Collections.Generic;
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

    // tiempo de reintento en caso las casillas se creen justo después de Start
    public int reintentosRecolectar = 30; // frames
    public float delayEntreReintentos = 0.05f;

    void Start()
    {
        // Asegurar tablero listo para que FichaBase/otros lo usen.
        EnsureReady();

        // Si después de EnsureReady no hay casillas, iniciar reintentos en runtime
        if (casillas.Count == 0)
        {
            StartCoroutine(RetryCollectRoutine());
        }
    }

    public int CasillasCount => casillas != null ? casillas.Count : 0;

    /// <summary>
    /// Asegura que el Board esté listo: si ya hay casillas no hace nada.
    /// Si generarAutomatico==true intentará generar el tablero; si no, recolectará casillas en escena.
    /// Método público porque otros scripts (p.ej. FichaBase) lo llaman.
    /// </summary>
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
            // Si el usuario ya rellenó casillasList en el inspector, la usamos.
            if (casillasList != null && casillasList.Count > 0)
            {
                SyncDictFromList();
            }
            else
            {
                RecolectarCasillasExistentes();
            }
        }
    }

    // --- Generacion automatica (como antes) ---
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

    // --- Recoleccion de casillas ya existentes en escena (incluye inactivos de la escena) ---
    public void RecolectarCasillasExistentes()
    {
        // Si el usuario ya puso manualmente casillas en el Inspector, las respetamos.
        // Solo si la lista está vacía es cuando buscamos automáticamente en la escena.
        if (casillasList != null && casillasList.Count > 0)
        {
            SyncDictFromList();
            return;
        }

        casillas.Clear();
        casillasList.Clear();

        Casilla[] encontradas;

        if (padreCasillas != null)
        {
            // incluir inactivos bajo el padre
            encontradas = padreCasillas.GetComponentsInChildren<Casilla>(true);
        }
        else
        {
            // Recolectar todas las Casilla en la escena, incluyendo GameObjects inactivos,
            // pero evitando assets/prefabs que estén en project (no pertenecen a una escena válida).
            var todas = Resources.FindObjectsOfTypeAll<Casilla>();
            List<Casilla> lista = new List<Casilla>();
            foreach (var c in todas)
            {
                if (c == null) continue;
                var go = c.gameObject;
                // asegurarnos que pertenece a una escena válida (evita prefabs/assets)
                if (!go.scene.IsValid()) continue;
                lista.Add(c);
            }
            encontradas = lista.ToArray();
        }

        foreach (var c in encontradas)
        {
            if (c == null) continue;

            // Si no tiene coord asignada, generar una a partir de la posición
            if (c.coord == new Vector2Int(int.MinValue, int.MinValue))
            {
                Vector2Int auto = AutoAsignarCoordPorPosicion(c.transform.position);
                // si hay conflicto, desplazar en +X hasta encontrar libre
                while (casillas.ContainsKey(auto))
                {
                    auto.x += 1;
                }
                c.Inicializar(auto, c.transform.position, tamCasilla, this);
                Debug.Log($"BoardGenerator: Casilla '{c.name}' no tenía coord; se asignó {auto} automáticamente.");
            }
            else
            {
                // garantizar initializacion (por si fue asignada manualmente pero falta world/tam/board)
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

        if (casillas.Count == 0)
        {
            Debug.LogWarning("BoardGenerator: no se encontraron casillas en la escena.");
        }
    }

    // Si recolección falla en Start porque las casillas se crean después, reintentar unos frames.
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

        if (casillas.Count == 0)
            Debug.LogWarning("BoardGenerator: RetryCollectRoutine terminó sin encontrar casillas.");
    }

    // --- Sincronización helper ---
    // Llenar el diccionario a partir de casillasList (cuando el usuario pone la lista manualmente)
    private void SyncDictFromList()
    {
        casillas.Clear();
        if (casillasList == null) casillasList = new List<Casilla>();
        for (int i = 0; i < casillasList.Count; i++)
        {
            var c = casillasList[i];
            if (c == null) continue;
            if (!c.gameObject.scene.IsValid()) continue; // evitar assets/prefabs

            // si no tiene coord asignada, intentar auto-asignar
            if (c.coord == new Vector2Int(int.MinValue, int.MinValue))
            {
                Vector2Int auto = AutoAsignarCoordPorPosicion(c.transform.position);
                // evitar colisiones: desplazar en +X hasta libre
                while (casillas.ContainsKey(auto))
                    auto.x += 1;
                c.Inicializar(auto, c.transform.position, tamCasilla, this);
                Debug.Log($"BoardGenerator: (SyncDictFromList) Casilla '{c.name}' no tenía coord; se asignó {auto} automáticamente.");
            }
            else
            {
                c.Inicializar(c.coord, c.transform.position, c.tam > 0 ? c.tam : tamCasilla, this);
            }

            if (!casillas.ContainsKey(c.coord))
                casillas[c.coord] = c;
            else
                Debug.LogWarning($"SyncDictFromList: coord duplicada {c.coord} en '{c.name}', ignorada.");
        }
    }

    // Llenar casillasList a partir del diccionario (por ejemplo después de generar automáticamente)
    private void SyncListFromDict()
    {
        if (casillasList == null) casillasList = new List<Casilla>();
        casillasList.Clear();
        foreach (var kv in casillas)
        {
            if (kv.Value != null) casillasList.Add(kv.Value);
        }
    }

    // Calcula coord aproximada desde posición world (redondeando por tamCasilla)
    private Vector2Int AutoAsignarCoordPorPosicion(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position; // relativo al board
        int x = Mathf.RoundToInt(local.x / tamCasilla);
        int y = Mathf.RoundToInt(local.z / tamCasilla); // z -> y en coordenadas de tablero
        return new Vector2Int(x, y);
    }

    // --- FALLBACK robusto: si el diccionario no tiene la casilla, buscar en la lista y cachear ---
    public bool ExisteCasilla(Vector2Int coord)
    {
        if (casillas.ContainsKey(coord)) return true;

        // buscar en lista (caso: casillasList fue rellenada por inspector o en runtime)
        if (casillasList != null)
        {
            foreach (var c in casillasList)
            {
                if (c != null && c.coord == coord)
                {
                    casillas[coord] = c; // cachear para siguientes consultas
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
                    casillas[coord] = item; // cachear
                    return item;
                }
            }
        }

        // Como último recurso, intentar recolectar de la escena (útil si se crearon tardíamente)
        RecolectarCasillasExistentes();
        if (casillas.TryGetValue(coord, out c)) return c;

        return null;
    }

    public Vector3 GetWorldPos(Vector2Int coord)
    {
        Casilla c = GetCasilla(coord);
        return c != null ? c.worldPos : Vector3.zero;
    }

    // ahora GetAllCasillas devuelve la lista pública (pero como IEnumerable para compatibilidad)
    public IEnumerable<Casilla> GetAllCasillas() => casillasList;

    public List<Casilla> RayCasillasEnDireccion(Vector2Int inicio, Vector2Int dir)
    {
        List<Casilla> lista = new List<Casilla>();
        Vector2Int cur = inicio + dir;
        while (ExisteCasilla(cur))
        {
            Casilla c = GetCasilla(cur);
            if (c == null) break;
            lista.Add(c);
            if (c.tieneOcupante) break;
            cur += dir;
        }
        return lista;
    }

    // Reserva una ruta completa para 'requester'. Devuelve true si pudo reservar todas las casillas.
    public bool TryReserveRuta(List<Vector2Int> ruta, GameObject requester)
    {
        if (ruta == null || ruta.Count == 0) return false;
        // chequear primero
        foreach (var coord in ruta)
        {
            var c = GetCasilla(coord);
            if (c == null) return false;
            if (c.tieneOcupante && c.ocupanteGO != requester) return false;
            if (c.reservadoPor != null && c.reservadoPor != requester) return false;
        }
        // reservar
        foreach (var coord in ruta)
        {
            var c = GetCasilla(coord);
            c.reservadoPor = requester;
        }
        return true;
    }

    // Liberar reservas (solo si las poseía requester)
    public void ReleaseRutaReservation(List<Vector2Int> ruta, GameObject requester)
    {
        if (ruta == null) return;
        foreach (var coord in ruta)
        {
            var c = GetCasilla(coord);
            if (c != null && c.reservadoPor == requester) c.reservadoPor = null;
        }
    }

    // --- Herramientas de editor / debug ---
    [ContextMenu("Refresh Casillas (Recolectar)")]
    public void ContextRefreshCasillas()
    {
        RecolectarCasillasExistentes();
    }

#if UNITY_EDITOR
    // Cuando editas la lista en el Inspector, sincronizamos el diccionario (solo en editor).
    void OnValidate()
    {
        // Evitar llamadas peligrosas fuera de modo edit/runtime checks simples
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (casillasList == null) casillasList = new List<Casilla>();
            // limpiar nulos de la lista
            casillasList.RemoveAll(item => item == null);
            // sincronizar diccionario ligero (no usar FindObjectsOfTypeAll en OnValidate)
            SyncDictFromList();
        }
    }
#endif
}

//using System.Collections.Generic;
//using UnityEngine;

//public class BoardGenerator : MonoBehaviour
//{
//    [Header("Opciones")]
//    [Tooltip("Si true genera automaticamente una cuadricula. Si false, recogera casillas existentes en 'padreCasillas' o en la escena.")]
//    public bool generarAutomatico = false;

//    [Header("Prefabs casillas (solo si generarAutomatico true)")]
//    public GameObject prefabCasillaBlanca;
//    public GameObject prefabCasillaNegra;

//    [Header("Parametros tablero (solo si generarAutomatico true)")]
//    public int ancho = 8;
//    public int alto = 8;
//    public float tamCasilla = 1f;
//    public Transform padreCasillas;

//    private Dictionary<Vector2Int, Casilla> casillas = new Dictionary<Vector2Int, Casilla>();

//    void Start()
//    {
//        EnsureReady();
//    }

//    public int CasillasCount => casillas != null ? casillas.Count : 0;

//    public void EnsureReady()
//    {
//        if (CasillasCount > 0) return;
//        if (generarAutomatico)
//        {
//            if (prefabCasillaBlanca == null || prefabCasillaNegra == null)
//            {
//                Debug.LogWarning($"BoardGenerator: generarAutomatico=true pero faltan prefabs.");
//                RecolectarCasillasExistentes();
//                return;
//            }
//            GenerarTableroAutomatico();
//        }
//        else RecolectarCasillasExistentes();
//    }

//    public void GenerarTableroAutomatico()
//    {
//        if (padreCasillas != null)
//        {
//            for (int i = padreCasillas.childCount - 1; i >= 0; i--)
//                DestroyImmediate(padreCasillas.GetChild(i).gameObject);
//        }
//        casillas.Clear();

//        for (int y = 0; y < alto; y++)
//        {
//            for (int x = 0; x < ancho; x++)
//            {
//                bool esBlanca = (x + y) % 2 == 0;
//                GameObject prefab = esBlanca ? prefabCasillaBlanca : prefabCasillaNegra;
//                Vector3 pos = transform.position + new Vector3(x * tamCasilla, 0f, y * tamCasilla);
//                GameObject go = Instantiate(prefab, pos, Quaternion.identity, padreCasillas);
//                go.name = $"Casilla_{x}_{y}";
//                Casilla c = go.GetComponent<Casilla>();
//                if (c == null) c = go.AddComponent<Casilla>();
//                c.Inicializar(new Vector2Int(x, y), pos, tamCasilla, this);
//                casillas[new Vector2Int(x, y)] = c;
//            }
//        }
//    }

//    public void RecolectarCasillasExistentes()
//    {
//        casillas.Clear();
//        Casilla[] encontradas;

//        if (padreCasillas != null)
//        {
//            List<Casilla> lista = new List<Casilla>();
//            foreach (Transform t in padreCasillas)
//            {
//                var c = t.GetComponent<Casilla>();
//                if (c != null) lista.Add(c);
//            }
//            encontradas = lista.ToArray();
//        }
//        else encontradas = FindObjectsOfType<Casilla>();

//        foreach (var c in encontradas)
//        {
//            if (c == null) continue;
//            if (c.coord == new Vector2Int(int.MinValue, int.MinValue))
//                Debug.LogWarning($"Casilla '{c.name}' tiene coord no inicializada.");
//            c.worldPos = c.transform.position;
//            c.tam = c.tam > 0 ? c.tam : tamCasilla;
//            c.SetBoard(this);
//            if (!casillas.ContainsKey(c.coord)) casillas[c.coord] = c;
//            else Debug.LogWarning($"Coord duplicada detectada en casillas: {c.coord} en '{c.name}'.");
//        }
//    }

//    public bool ExisteCasilla(Vector2Int coord) => casillas.ContainsKey(coord);

//    public Casilla GetCasilla(Vector2Int coord)
//    {
//        casillas.TryGetValue(coord, out Casilla c);
//        return c;
//    }

//    public Vector3 GetWorldPos(Vector2Int coord)
//    {
//        Casilla c = GetCasilla(coord);
//        return c != null ? c.worldPos : Vector3.zero;
//    }

//    public IEnumerable<Casilla> GetAllCasillas() => casillas.Values;

//    public List<Casilla> RayCasillasEnDireccion(Vector2Int inicio, Vector2Int dir)
//    {
//        List<Casilla> lista = new List<Casilla>();
//        Vector2Int cur = inicio + dir;
//        while (ExisteCasilla(cur))
//        {
//            Casilla c = GetCasilla(cur);
//            lista.Add(c);
//            if (c.tieneOcupante) break;
//            cur += dir;
//        }
//        return lista;
//    }

//    // Reserva una ruta completa para 'requester'. Devuelve true si pudo reservar todas las casillas.
//    public bool TryReserveRuta(List<Vector2Int> ruta, GameObject requester)
//    {
//        if (ruta == null || ruta.Count == 0) return false;
//        // chequear primero
//        foreach (var coord in ruta)
//        {
//            var c = GetCasilla(coord);
//            if (c == null) return false;
//            if (c.tieneOcupante && c.ocupanteGO != requester) return false;
//            if (c.reservadoPor != null && c.reservadoPor != requester) return false;
//        }
//        // reservar
//        foreach (var coord in ruta)
//        {
//            var c = GetCasilla(coord);
//            c.reservadoPor = requester;
//        }
//        return true;
//    }

//    // Liberar reservas (solo si las poseía requester)
//    public void ReleaseRutaReservation(List<Vector2Int> ruta, GameObject requester)
//    {
//        if (ruta == null) return;
//        foreach (var coord in ruta)
//        {
//            var c = GetCasilla(coord);
//            if (c != null && c.reservadoPor == requester) c.reservadoPor = null;
//        }
//    }
//}


