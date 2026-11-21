using UnityEngine;
using TanksGame.Core;

namespace TanksGame.Language
{
    // Sección 7: instrucciones disponibles.
    public enum InstructionType { Mov, Amt, Mina, Misil, Radar, Escudo, Esperar, Reparar }

    // Donde coloca la mina la instruccion MINA (una sola casilla por uso):
    //   Propia -> MINA()    : en la casilla del propio tanque.
    //   N,S,E,O,NO,NE,SE,SO -> MINA(dir) : en la casilla vecina en esa direccion.
    public enum DireccionMina { Propia, N, S, E, O, NO, NE, SE, SO }

    public static class DireccionMinaExtensions
    {
        public static Vector2Int ToOffset(this DireccionMina dir)
        {
            switch (dir)
            {
                case DireccionMina.N:  return new Vector2Int(0, 1);
                case DireccionMina.S:  return new Vector2Int(0, -1);
                case DireccionMina.E:  return new Vector2Int(1, 0);
                case DireccionMina.O:  return new Vector2Int(-1, 0);
                case DireccionMina.NO: return new Vector2Int(-1, 1);
                case DireccionMina.NE: return new Vector2Int(1, 1);
                case DireccionMina.SE: return new Vector2Int(1, -1);
                case DireccionMina.SO: return new Vector2Int(-1, -1);
                default:               return Vector2Int.zero;
            }
        }
    }

    public class Instruction
    {
        public InstructionType Type;
        public Direction? Dir; // usado por Mov, Amt, Radar, Misil
        public DireccionMina MinaDir = DireccionMina.Propia; // usado solo por Mina

        public override string ToString()
        {
            if (Type == InstructionType.Mina)
            {
                return MinaDir == DireccionMina.Propia ? "MINA()" : $"MINA({MinaDir})";
            }
            return Dir.HasValue ? $"{Type}({Dir})" : Type.ToString();
        }
    }
}