using System.Collections.Generic;
using UnityEngine;

namespace TanksGame.Core
{
    // Tablero configurable de Width x Height casillas.
    // El documento original propone 8x8, pero el tamaño es ahora un parámetro
    // para poder probar tableros de distintas dimensiones (NxM).
    public class GridBoard
    {
        public readonly int Width;
        public readonly int Height;
        private readonly BoardCell[,] cells;

        // Contador de ronda propio del tablero: arranca en 0 y sube en 1 cada
        // vez que se llama a AvanzarRonda() (una vez por ronda, al empezarla).
        // Es la base del sistema de minas: una mina queda "armada" recién
        // cuando rondaActual sea MAYOR que la ronda en la que se colocó.
        private int rondaActual = 0;

        public GridBoard(int width = 8, int height = 8)
        {
            Width = width;
            Height = height;
            cells = new BoardCell[Width, Height];

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    cells[x, y] = new BoardCell();
        }

        public bool IsInside(Vector2Int pos) =>
            pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < Height;

        public BoardCell GetCell(Vector2Int pos) => IsInside(pos) ? cells[pos.x, pos.y] : null;

        public bool IsObstacle(Vector2Int pos) =>
            IsInside(pos) && cells[pos.x, pos.y].Type == CellType.Obstacle;

        public bool IsHospital(Vector2Int pos) =>
            IsInside(pos) && cells[pos.x, pos.y].Type == CellType.Hospital;

        public void SetObstacle(Vector2Int pos)
        {
            if (IsInside(pos)) cells[pos.x, pos.y].Type = CellType.Obstacle;
        }

        public void SetHospital(Vector2Int pos)
        {
            if (IsInside(pos)) cells[pos.x, pos.y].Type = CellType.Hospital;
        }

        public void PlaceMine(Vector2Int pos)
        {
            if (!IsInside(pos)) return;
            cells[pos.x, pos.y].Type = CellType.Mine;
            // Queda "sellada" con el número de la ronda actual: no importa en
            // qué momento de ExecuteRound() se llame a esto (antes o después
            // de AvanzarRonda), la mina solo se arma a partir de la PRÓXIMA
            // vez que rondaActual avance más allá de este número.
            cells[pos.x, pos.y].RondaColocacion = rondaActual;
        }

        // Avanza el contador de ronda del tablero. TurnManager la llama UNA
        // vez al empezar cada ExecuteRound(). A diferencia del viejo
        // ArmarMinasPendientes() (que tenía que "acordarse" de recorrer y
        // armar cada mina existente, y solo funcionaba si se llamaba justo
        // antes de ejecutar las instrucciones), esto es solo un contador: la
        // condición de armado se recalcula sola en IsMineArmed/TryConsumeMine
        // comparando contra RondaColocacion, así que no depende de en qué
        // orden exacto se llamen las cosas dentro de la ronda.
        public void AvanzarRonda() => rondaActual++;

        public bool IsMineArmed(Vector2Int pos) =>
            IsInside(pos) && cells[pos.x, pos.y].Type == CellType.Mine
            && rondaActual > cells[pos.x, pos.y].RondaColocacion;

        // Si hay una mina ARMADA en 'pos', la retira y devuelve true (para
        // aplicar daño). Una mina desarmada (recién colocada esta misma
        // ronda) nunca se consume aquí.
        public bool TryConsumeMine(Vector2Int pos)
        {
            if (!IsInside(pos)) return false;
            if (cells[pos.x, pos.y].Type == CellType.Mine
                && rondaActual > cells[pos.x, pos.y].RondaColocacion)
            {
                cells[pos.x, pos.y].Type = CellType.Free;
                cells[pos.x, pos.y].RondaColocacion = -1;
                return true;
            }
            return false;
        }

        // Todas las celdas que actualmente tienen una mina (armada o recién
        // colocada todavía desarmada), para que la vista pueda dibujarlas.
        public IEnumerable<Vector2Int> MinePositions()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (cells[x, y].Type == CellType.Mine)
                        yield return new Vector2Int(x, y);
        }
    }
}