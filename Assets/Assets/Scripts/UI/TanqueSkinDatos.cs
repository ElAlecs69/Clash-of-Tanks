namespace TanksGame.Core
{
    // Snapshot de la personalización visual elegida para un tanque: patrón, color
    // principal, calcomanía y bandera, identificados por índice -- los mismos
    // índices que usa PantallaProgramacionTanques para sus swatches, y que
    // interpreta PaletaSkins (colores/símbolos/banderas reales por índice).
    //
    // "Numero" YA NO es una elección de personalización: ahora guarda el número de
    // orden en que se programó el tanque (1, 2, 3...), asignado automáticamente, y
    // se pinta directamente en el casco durante la partida para poder identificar
    // cada tanque en el tablero.
    [System.Serializable]
    public struct TanqueSkinDatos
    {
        public int Patron;
        public int Color;
        public int Calcomania;
        public int Numero;
        public int Bandera;
    }
}
