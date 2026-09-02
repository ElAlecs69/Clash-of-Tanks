namespace TanksGame.Core
{
    public enum CellType
    {
        Free,
        Obstacle,
        Mine,
        Hospital
    }

    public class BoardCell
    {
        public CellType Type = CellType.Free;
    }
}
