using System.Collections.Generic;
using UnityEngine;

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

    private Dictionary<Vector2Int, Casilla> casillas = new Dictionary<Vector2Int, Casilla>();

    void Start()
    {
        EnsureReady();
    }

    public int CasillasCount => casillas != null ? casillas.Count : 0;

    public void EnsureReady()
    {
        if (CasillasCount > 0) return;
        if (generarAutomatico)
        {
            if (prefabCasillaBlanca == null || prefabCasillaNegra == null)
            {
                Debug.LogWarning($"BoardGenerator: generarAutomatico=true pero faltan prefabs.");
                RecolectarCasillasExistentes();
                return;
            }
            GenerarTableroAutomatico();
        }
        else RecolectarCasillasExistentes();
    }

    public void GenerarTableroAutomatico()
    {
        if (padreCasillas != null)
        {
            for (int i = padreCasillas.childCount - 1; i >= 0; i--)
                DestroyImmediate(padreCasillas.GetChild(i).gameObject);
        }
        casillas.Clear();

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
            }
        }
    }

    public void RecolectarCasillasExistentes()
    {
        casillas.Clear();
        Casilla[] encontradas;

        if (padreCasillas != null)
        {
            List<Casilla> lista = new List<Casilla>();
            foreach (Transform t in padreCasillas)
            {
                var c = t.GetComponent<Casilla>();
                if (c != null) lista.Add(c);
            }
            encontradas = lista.ToArray();
        }
        else encontradas = FindObjectsOfType<Casilla>();

        foreach (var c in encontradas)
        {
            if (c == null) continue;
            if (c.coord == new Vector2Int(int.MinValue, int.MinValue))
                Debug.LogWarning($"Casilla '{c.name}' tiene coord no inicializada.");
            c.worldPos = c.transform.position;
            c.tam = c.tam > 0 ? c.tam : tamCasilla;
            c.SetBoard(this);
            if (!casillas.ContainsKey(c.coord)) casillas[c.coord] = c;
            else Debug.LogWarning($"Coord duplicada detectada en casillas: {c.coord} en '{c.name}'.");
        }
    }

    public bool ExisteCasilla(Vector2Int coord) => casillas.ContainsKey(coord);

    public Casilla GetCasilla(Vector2Int coord)
    {
        casillas.TryGetValue(coord, out Casilla c);
        return c;
    }

    public Vector3 GetWorldPos(Vector2Int coord)
    {
        Casilla c = GetCasilla(coord);
        return c != null ? c.worldPos : Vector3.zero;
    }

    public IEnumerable<Casilla> GetAllCasillas() => casillas.Values;

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
}


