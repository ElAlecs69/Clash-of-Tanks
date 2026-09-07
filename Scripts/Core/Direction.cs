using UnityEngine;

namespace TanksGame.Core
{
    public enum Direction { N, S, E, O }

    public static class DirectionExtensions
    {
        public static Vector2Int ToOffset(this Direction dir)
        {
            switch (dir)
            {
                case Direction.N: return new Vector2Int(0, 1);
                case Direction.S: return new Vector2Int(0, -1);
                case Direction.E: return new Vector2Int(1, 0);
                case Direction.O: return new Vector2Int(-1, 0);
            }
            return Vector2Int.zero;
        }
    }
}
