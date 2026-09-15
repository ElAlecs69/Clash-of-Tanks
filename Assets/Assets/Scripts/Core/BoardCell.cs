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

        // Una mina recién colocada empieza DESARMADA: no puede hacer daño en
        // la misma ronda en que se coloca. GridBoard.ArmarMinasPendientes()
        // la arma al inicio de la ronda SIGUIENTE, y solo entonces
        // TurnManager la deja detonar (y encima, solo si el tanque que está
        // sobre ella no se movió durante esa ronda).
        public bool MinaArmada;
    }
}