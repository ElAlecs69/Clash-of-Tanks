using TanksGame.Core;

namespace TanksGame.Language
{
    // Sección 7: instrucciones disponibles.
    public enum InstructionType { Mov, Amt, Mina, Misil, Radar, Escudo, Esperar, Reparar }

    public class Instruction
    {
        public InstructionType Type;
        public Direction? Dir; // usado por Mov, Amt, Radar

        public override string ToString()
        {
            return Dir.HasValue ? $"{Type}({Dir})" : Type.ToString();
        }
    }
}
