namespace TanksGame.Core
{
    // Snapshot de la personalización visual elegida para un tanque: patrón, color
    // principal, calcomanía, número y bandera, identificados por índice -- los
    // mismos índices que usa PantallaProgramacionTanques para sus swatches, y que
    // interpreta PaletaSkins (colores/símbolos reales por índice).
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
