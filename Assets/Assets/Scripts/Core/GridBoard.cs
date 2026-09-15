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
            // Desarmada al colocarla: no puede detonar en esta misma ronda.
            cells[pos.x, pos.y].MinaArmada = false;
        }

        // Arma todas las minas que ya existían de rondas anteriores (las
        // recién colocadas en la ronda actual todavía no pasaron por aquí,
        // así que siguen desarmadas hasta la ronda siguiente). Llamar UNA vez
        // al principio de cada ronda, antes de ejecutar las instrucciones.
        public void ArmarMinasPendientes()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (cells[x, y].Type == CellType.Mine)
                        cells[x, y].MinaArmada = true;
        }

        public bool IsMineArmed(Vector2Int pos) =>
            IsInside(pos) && cells[pos.x, pos.y].Type == CellType.Mine && cells[pos.x, pos.y].MinaArmada;

        // Si hay una mina ARMADA en 'pos', la retira y devuelve true (para
        // aplicar daño). Una mina desarmada (recién colocada esta misma
        // ronda) nunca se consume aquí.
        public bool TryConsumeMine(Vector2Int pos)
        {
            if (!IsInside(pos)) return false;
            if (cells[pos.x, pos.y].Type == CellType.Mine && cells[pos.x, pos.y].MinaArmada)
            {
                cells[pos.x, pos.y].Type = CellType.Free;
                cells[pos.x, pos.y].MinaArmada = false;
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