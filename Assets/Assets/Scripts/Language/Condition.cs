using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TanksGame.Core;

namespace TanksGame.Language
{
    public enum ConditionOperand { Vida, Misiles, Radar }
    public enum ComparisonOp { GreaterThan, LessThan, GreaterOrEqual, LessOrEqual, Equal }

    // Representa condiciones como:
    //   IF VIDA < 40
    //   IF RADAR(N) > 0
    //   IF MISILES > 0
    // ...o varias condiciones simples unidas con Y/AND, como:
    //   IF (RADAR(N) < 0 Y RADAR(O) < 0) { ... }
    // En ese caso 'Sub' trae cada condición simple y se exige que TODAS se
    // cumplan (AND); Operand/RadarDirection/Op/Value de esta instancia se
    // ignoran cuando 'Sub' no es null.
    public class Condition
    {
        public ConditionOperand Operand;
        public Direction? RadarDirection; // solo cuando Operand == Radar
        public ComparisonOp Op;
        public float Value;
        public List<Condition> Sub;

        public bool Evaluate(Tank tank, Func<Tank, Direction, int> radarFunc)
        {
            if (Sub != null)
                return Sub.All(c => c.Evaluate(tank, radarFunc));

            float left;
            switch (Operand)
            {
                case ConditionOperand.Vida:
                    left = tank.Health;
                    break;
                case ConditionOperand.Misiles:
                    left = tank.Missiles;
                    break;
                case ConditionOperand.Radar:
                    left = radarFunc(tank, RadarDirection ?? Direction.N);
                    break;
                default:
                    left = 0f;
                    break;
            }

            switch (Op)
            {
                case ComparisonOp.GreaterThan: return left > Value;
                case ComparisonOp.LessThan: return left < Value;
                case ComparisonOp.GreaterOrEqual: return left >= Value;
                case ComparisonOp.LessOrEqual: return left <= Value;
                case ComparisonOp.Equal: return Mathf.Approximately(left, Value);
            }
            return false;
        }
    }
}