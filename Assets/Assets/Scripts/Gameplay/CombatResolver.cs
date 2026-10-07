using System.Collections.Generic;
using UnityEngine;
using TanksGame.Core;

namespace TanksGame.Gameplay
{
    public struct TraceResult
    {
        public Tank Tank;            // tanque vivo golpeado (null si no hay)
        public bool Hospital;        // true si lo primero que se encontró fue un hospital
        public Vector2Int HospitalPos;
        public int Distance;
        public bool Blocked;         // obstáculo
    }

    public static class CombatResolver
    {
        // Igual que Trace, pero distingue un hospital (que también recibe disparos,
        // como un tanque) de un obstáculo. Un tanque parado SOBRE un hospital es lo
        // que se golpea primero.
        public static TraceResult TraceCompleto(
            GridBoard board, IEnumerable<Tank> tanks, Vector2Int origin, Direction dir)
        {
            var offset = dir.ToOffset();
            var pos = origin;
            int distance = 0;
            var lista = new List<Tank>(tanks);

            while (true)
            {
                pos += offset;
                distance++;

                if (!board.IsInside(pos))
                    return new TraceResult { Distance = distance };

                if (board.IsObstacle(pos))
                    return new TraceResult { Distance = distance, Blocked = true };

                foreach (var t in lista)
                    if (t.IsAlive && t.Position == pos)
                        return new TraceResult { Tank = t, Distance = distance };

                if (board.IsHospital(pos))
                    return new TraceResult { Hospital = true, HospitalPos = pos, Distance = distance };
            }
        }

        // Recorre el tablero desde 'origin' en dirección 'dir' hasta encontrar
        // un tanque vivo, un obstáculo, o el límite del tablero.
        // Un obstáculo bloquea el disparo (sección 9 y 11).
        // Versión usada por el RADAR: un hospital NO es un tanque (no da distancia
        // positiva), pero sí bloquea la línea igual que un obstáculo, porque
        // también frena los disparos.
        public static (Tank hitTank, int distance, bool blockedByObstacle) Trace(
            GridBoard board, IEnumerable<Tank> tanks, Vector2Int origin, Direction dir)
        {
            var r = TraceCompleto(board, tanks, origin, dir);
            return (r.Tank, r.Distance, r.Blocked || r.Hospital);
        }
    }
}