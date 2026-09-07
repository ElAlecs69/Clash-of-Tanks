using System.Collections.Generic;
using TanksGame.Core;
using TanksGame.Language;

namespace TanksGame.Gameplay
{
    public class TankAgent
    {
        public Tank Tank;
        public List<ProgramStep> Program = new List<ProgramStep>();
        public int ProgramPointer = 0;

        public TankAgent(Tank tank)
        {
            Tank = tank;
        }

        // Devuelve la siguiente línea del programa y avanza el puntero.
        // El programa se repite en bucle (loop) al llegar al final.
        public ProgramStep GetNextStep()
        {
            if (Program.Count == 0) return null;
            var step = Program[ProgramPointer % Program.Count];
            ProgramPointer++;
            return step;
        }
    }
}
