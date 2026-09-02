using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TanksGame.Core;

namespace TanksGame.Language
{
    // Convierte texto como:
    //
    //   IF RADAR(N) > 0
    //   MISIL
    //   IF VIDA < 40
    //   REPARAR
    //   MOV(E)
    //
    // en una lista de ProgramStep ejecutable por el TurnManager.
    public static class TankProgramParser
    {
        private static readonly Regex InstrWithDirRegex =
            new Regex(@"^(MOV|AMT|RADAR)\(([NSEO])\)$", RegexOptions.IgnoreCase);

        private static readonly Regex IfRegex =
            new Regex(@"^IF\s+(.+)$", RegexOptions.IgnoreCase);

        private static readonly Regex ConditionRegex =
            new Regex(@"^(VIDA|MISILES|RADAR\(([NSEO])\))\s*(>=|<=|==|>|<)\s*(-?\d+(\.\d+)?)$",
                RegexOptions.IgnoreCase);

        public static List<ProgramStep> Parse(string script)
        {
            var steps = new List<ProgramStep>();
            var lines = new List<string>();

            foreach (var raw in script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = raw.Trim();
                if (trimmed.Length > 0) lines.Add(trimmed);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var ifMatch = IfRegex.Match(line);

                if (ifMatch.Success)
                {
                    var condition = ParseCondition(ifMatch.Groups[1].Value.Trim());
                    if (i + 1 >= lines.Count)
                        throw new FormatException($"La línea {i + 1} (IF) no tiene una instrucción asociada debajo.");

                    i++;
                    var instruction = ParseInstruction(lines[i]);
                    steps.Add(new ProgramStep { Condition = condition, Instruction = instruction });
                }
                else
                {
                    steps.Add(new ProgramStep { Condition = null, Instruction = ParseInstruction(line) });
                }
            }

            return steps;
        }

        private static Condition ParseCondition(string text)
        {
            var compact = text.Replace(" ", "");
            var match = ConditionRegex.Match(compact);
            if (!match.Success)
                throw new FormatException($"Condición inválida: '{text}'");

            var operandText = match.Groups[1].Value.ToUpperInvariant();
            var condition = new Condition();

            if (operandText.StartsWith("VIDA"))
                condition.Operand = ConditionOperand.Vida;
            else if (operandText.StartsWith("MISILES"))
                condition.Operand = ConditionOperand.Misiles;
            else
            {
                condition.Operand = ConditionOperand.Radar;
                condition.RadarDirection = ParseDirection(match.Groups[2].Value);
            }

            switch (match.Groups[3].Value)
            {
                case ">": condition.Op = ComparisonOp.GreaterThan; break;
                case "<": condition.Op = ComparisonOp.LessThan; break;
                case ">=": condition.Op = ComparisonOp.GreaterOrEqual; break;
                case "<=": condition.Op = ComparisonOp.LessOrEqual; break;
                case "==": condition.Op = ComparisonOp.Equal; break;
                default: throw new FormatException($"Operador desconocido en: '{text}'");
            }

            condition.Value = float.Parse(match.Groups[4].Value);
            return condition;
        }

        private static Instruction ParseInstruction(string line)
        {
            var upper = line.ToUpperInvariant().Replace(" ", "");
            var dirMatch = InstrWithDirRegex.Match(upper);

            if (dirMatch.Success)
            {
                var typeText = dirMatch.Groups[1].Value;
                var dir = ParseDirection(dirMatch.Groups[2].Value);
                InstructionType type;
                switch (typeText)
                {
                    case "MOV": type = InstructionType.Mov; break;
                    case "AMT": type = InstructionType.Amt; break;
                    case "RADAR": type = InstructionType.Radar; break;
                    default: throw new FormatException($"Instrucción desconocida: '{line}'");
                }
                return new Instruction { Type = type, Dir = dir };
            }

            switch (upper)
            {
                case "MINA": return new Instruction { Type = InstructionType.Mina };
                case "MISIL": return new Instruction { Type = InstructionType.Misil };
                case "ESCUDO": return new Instruction { Type = InstructionType.Escudo };
                case "ESPERAR": return new Instruction { Type = InstructionType.Esperar };
                case "REPARAR": return new Instruction { Type = InstructionType.Reparar };
                default: throw new FormatException($"Instrucción no reconocida: '{line}'");
            }
        }

        private static Direction ParseDirection(string text)
        {
            switch (text.ToUpperInvariant())
            {
                case "N": return Direction.N;
                case "S": return Direction.S;
                case "E": return Direction.E;
                case "O": return Direction.O;
                default: throw new FormatException($"Dirección inválida: '{text}'");
            }
        }
    }
}
