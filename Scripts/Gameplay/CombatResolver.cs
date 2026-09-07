using System.Collections.Generic;
using UnityEngine;
using TanksGame.Core;

namespace TanksGame.Gameplay
{
    public static class CombatResolver
    {
        // Recorre el tablero desde 'origin' en dirección 'dir' hasta encontrar
        // un tanque vivo, un obstáculo, o el límite del tablero.
        // Un obstáculo bloquea el disparo (sección 9 y 11).
        public static (Tank hitTank, int distance, bool blockedByObstacle) Trace(
            GridBoard board, IEnumerable<Tank> tanks, Vector2Int origin, Direction dir)
        {
            var offset = dir.ToOffset();
            var pos = origin;
            int distance = 0;

            while (true)
            {
                pos += offset;
                distance++;

                if (!board.IsInside(pos))
                    return (null, distance, false);

                if (board.IsObstacle(pos))
                    return (null, distance, true);

                foreach (var t in tanks)
                {
                    if (t.IsAlive && t.Position == pos)
                        return (t, distance, false);
                }
            }
        }
    }
}
