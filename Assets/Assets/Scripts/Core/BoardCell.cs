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
        // la misma ronda en que se coloca. En vez de un simple booleano que
        // dependía de que GridBoard.AvanzarRonda() se llamara ANTES de
        // colocar minas nuevas (un orden fácil de romper sin darse cuenta al
        // tocar TurnManager más adelante -- eso fue justo lo que pasó), se
        // guarda el NÚMERO DE RONDA en que se colocó. GridBoard.IsMineArmed()
        // compara ese número contra la ronda actual: la mina queda armada
        // sola en cuanto arranca una ronda POSTERIOR a la que la colocó, sin
        // importar en qué momento exacto de ExecuteRound() se la consulte.
        public int RondaColocacion = -1;
    }
}