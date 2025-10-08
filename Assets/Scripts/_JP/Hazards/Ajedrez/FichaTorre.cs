// Archivo: FichaTorre.cs
using System.Collections.Generic;
using UnityEngine;

public class FichaTorre : FichaBase
{
    public override List<Vector2Int> ObtenerCasillasPosibles()
    {
        List<Vector2Int> lista = new List<Vector2Int>();
        Vector2Int origen = coordActual;
        Vector2Int[] dirs = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            List<Casilla> r = tablero.RayCasillasEnDireccion(origen, d);
            foreach (var c in r) lista.Add(c.coord);
        }
        return lista;
    }

    protected override List<Vector2Int> CalcularRutaHasta(Vector2Int destino)
    {
        List<Vector2Int> ruta = new List<Vector2Int>();
        Vector2Int dir = new Vector2Int(Mathf.Clamp(destino.x - coordActual.x, -1, 1), Mathf.Clamp(destino.y - coordActual.y, -1, 1));
        if (dir.x != 0 && dir.y != 0) return ruta;
        Vector2Int cur = coordActual;
        while (cur != destino)
        {
            cur += dir;
            ruta.Add(cur);
            if (tablero.GetCasilla(cur).tieneOcupante) break;
        }
        return ruta;
    }
}
