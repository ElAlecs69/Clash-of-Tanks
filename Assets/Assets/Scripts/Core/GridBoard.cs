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
            if (IsInside(pos)) cells[pos.x, pos.y].Type = CellType.Mine;
        }

        // Si hay una mina en 'pos', la retira y devuelve true (para aplicar daño).
        public bool TryConsumeMine(Vector2Int pos)
        {
            if (!IsInside(pos)) return false;
            if (cells[pos.x, pos.y].Type == CellType.Mine)
            {
                cells[pos.x, pos.y].Type = CellType.Free;
                return true;
            }
            return false;
        }
    }
}
