using System.Collections.Generic;
using UnityEngine;
using System.Collections;

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
        // si no está alineado en fila/columna, devolver ruta vacía (no mueve en diagonales)
        if (dir.x != 0 && dir.y != 0) return ruta;
        Vector2Int cur = coordActual;
        while (cur != destino)
        {
            cur += dir;
            ruta.Add(cur);
            if (!tablero.ExisteCasilla(cur)) break;
            Casilla c = tablero.GetCasilla(cur);
            if (c != null && c.tieneOcupante) break;
        }
        return ruta;
    }

    // Rutina específica para torre: intenta reservar la ruta recta y si lo logra se desplaza DIRECTAMENTE hasta destino.
    public IEnumerator RutinaAtaqueDirecta(Vector2Int destino)
    {
        estaMoviendo = true;

        yield return new WaitForSeconds(tiempoEsperaInicial);

        List<Vector2Int> ruta = CalcularRutaHasta(destino);
        if (ruta == null || ruta.Count == 0)
        {
            estaMoviendo = false;
            yield break;
        }

        // si la ruta no es recta (debe serlo por CalcularRutaHasta), abortar aquí
        // Estimación de ETA (tiempoBefore + distancia/velocidad)
        float eta = tiempoAntesMover + (ruta.Count / Mathf.Max(1f, tilesPorSegundo));

        // Intentar reservar toda la ruta (preferimos mover de una vez)
        bool reserved = tablero.RequestRouteReservation(ruta, gameObject, eta);

        if (!reserved)
        {
            // fallback: intentar reservar la casilla final directamente (si está libre)
            var ultima = ruta[ruta.Count - 1];
            var cFinal = tablero.GetCasilla(ultima);
            if (cFinal != null && !cFinal.tieneOcupante && (cFinal.reservadoPor == null || cFinal.reservadoPor == gameObject))
            {
                if (tablero.RequestRouteReservation(new List<Vector2Int> { ultima }, gameObject, eta))
                {
                    ruta = new List<Vector2Int> { ultima };
                    reserved = true;
                }
            }
        }

        if (!reserved)
        {
            // no se pudo reservar nada, no nos quedamos bloqueando
            estaMoviendo = false;
            yield break;
        }

        // marcar ruta para feedback visual (aunque nos moveremos directo)
        foreach (var c in ruta)
        {
            var cas = tablero.GetCasilla(c);
            if (cas != null) cas.AgregarMarca(marcadorTrailPrefab, tiempoAntesMover + (ruta.Count / tilesPorSegundo) + 0.2f);
        }

        // esperar intención
        yield return new WaitForSeconds(tiempoAntesMover);

        // notificar que empezamos (evita preemption)
        tablero.NotifyRouteStarted(gameObject);

        // Movimiento directo: calculamos el world target como la casilla final
        Vector2Int finalCoord = ruta[ruta.Count - 1];
        Casilla origen = tablero.GetCasilla(coordActual);
        Casilla destinoCas = tablero.GetCasilla(finalCoord);
        if (destinoCas == null)
        {
            // limpieza en caso de fallo
            tablero.ReleaseRutaReservation(ruta, gameObject);
            tablero.NotifyRouteFinished(gameObject);
            estaMoviendo = false;
            yield break;
        }

        Vector3 start = transform.position;
        Vector3 target = destinoCas.worldPos + Vector3.up * 0.1f;
        float distancia = Vector3.Distance(start, target);
        float velocidad = tilesPorSegundo * tamCasilla;
        float tiempo = velocidad > 0 ? distancia / velocidad : 0.01f;
        float t = 0f;

        // actualizar ocupantes: liberar origen, marcar destino (pero mantenemos reserva hasta llegar)
        if (origen != null) origen.SetOcupante(null);
        destinoCas.SetOcupante(gameObject);

        while (t < tiempo)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, velocidad * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = target;
        coordActual = finalCoord;

        // liberar todas las reservas asociadas a esta ruta (ya llegó)
        tablero.ReleaseRutaReservation(ruta, gameObject);

        // comprobar si el jugador está en la casilla final para atacar
        if (destinoCas.tieneOcupante && destinoCas.ocupanteGO != null && destinoCas.ocupanteGO.CompareTag(tagJugador))
        {
            AttackPlayer(transform.position);
        }

        // notificar que terminamos
        tablero.NotifyRouteFinished(gameObject);

        estaMoviendo = false;
    }
}
