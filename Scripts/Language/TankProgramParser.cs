using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TanksGame.Core;

namespace TanksGame.Language
{
    // Convierte texto del mini-lenguaje en una lista de ProgramStep ejecutable por el
    // TurnManager. Soporta dos formas de escribir un IF:
    //
    // Formato nuevo (bloque con llaves, el que genera la Terminal):
    //   IF (RADAR(N) > 0) {
    //       MISIL(N)
    //   }
    //
    // Formato viejo (2 líneas, se sigue aceptando por compatibilidad):
    //   IF RADAR(N) > 0
    //   MISIL(N)
    //
    // LIMITACIÓN ACTUAL: cada bloque IF (de cualquiera de los dos formatos) admite
    // EXACTAMENTE una instrucción — el motor de turnos (TurnManager/ProgramStep) fue
    // diseñado así desde el principio. "BUCLE" / "FIN" / "INICIO" / "INICIO:" se
    // aceptan como marcadores sin efecto: el programa completo ya se repite solo en
    // bucle (ver TankAgent.GetNextStep), así que no hace falta que hagan nada.
    public static class TankProgramParser
    {
        private static readonly Regex InstrWithDirRegex =
            new Regex(@"^(MOV|AMT|RADAR|MISIL)\(([NSEO]?)\)$", RegexOptions.IgnoreCase);

        private static readonly Regex IfViejoRegex =
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
                var sinComentario = QuitarComentario(raw);
                var trimmed = sinComentario.Trim();
                if (trimmed.Length > 0) lines.Add(trimmed);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // --- Formato nuevo: IF (condición) { ---
                if (TryParseIfBlockHeader(line, out var condicionCruda))
                {
                    var condition = ParseCondition(condicionCruda);

                    int indiceInstruccion = i + 1;
                    if (indiceInstruccion >= lines.Count)
                        throw new FormatException($"El bloque IF de la línea {i + 1} no tiene ninguna instrucción antes de cerrar.");

                    var lineaInstruccion = lines[indiceInstruccion];
                    if (lineaInstruccion == "}")
                        throw new FormatException(
                            $"El bloque IF de la línea {i + 1} está vacío: debe tener exactamente una instrucción entre las llaves.");

                    var instruction = ParseInstruction(lineaInstruccion);

                    int indiceCierre = indiceInstruccion + 1;
                    if (indiceCierre >= lines.Count || lines[indiceCierre] != "}")
                        throw new FormatException(
                            $"El bloque IF de la línea {i + 1} debe cerrar con '}}' justo después de la instrucción " +
                            "(por ahora solo se admite UNA instrucción por bloque IF).");

                    steps.Add(new ProgramStep { Condition = condition, Instruction = instruction });
                    i = indiceCierre;
                    continue;
                }

                // --- Formato viejo: IF <condición> seguida de la instrucción en la línea de abajo ---
                var ifViejoMatch = IfViejoRegex.Match(line);
                if (ifViejoMatch.Success)
                {
                    var condition = ParseCondition(ifViejoMatch.Groups[1].Value.Trim());
                    if (i + 1 >= lines.Count)
                        throw new FormatException($"La línea {i + 1} (IF) no tiene una instrucción asociada debajo.");

                    i++;
                    var instruction = ParseInstruction(lines[i]);
                    steps.Add(new ProgramStep { Condition = condition, Instruction = instruction });
                    continue;
                }

                // --- Marcadores sin efecto ---
                var lineUpper = line.ToUpperInvariant();
                if (lineUpper == "BUCLE" || lineUpper == "FIN" || lineUpper == "INICIO" || lineUpper == "INICIO:")
                    continue;

                // --- Instrucción normal, sin condición ---
                steps.Add(new ProgramStep { Condition = null, Instruction = ParseInstruction(line) });
            }

            return steps;
        }

        private static string QuitarComentario(string linea)
        {
            int indice = linea.IndexOf('#');
            return indice >= 0 ? linea.Substring(0, indice) : linea;
        }

        // Reconoce el encabezado de un bloque IF nuevo: "IF (condición) {", con conteo
        // manual de paréntesis (no una regex simple) para no confundirse cuando la
        // condición ya tiene paréntesis propios, como en "IF (RADAR(N) > 0) {" — ahí el
        // primer ')' que aparece es el de RADAR(N), no el que cierra la condición completa.
        private static bool TryParseIfBlockHeader(string line, out string condicionCruda)
        {
            condicionCruda = null;

            if (!line.StartsWith("IF", StringComparison.OrdinalIgnoreCase))
                return false;

            int indiceApertura = line.IndexOf('(');
            if (indiceApertura < 0) return false;

            int balance = 0;
            int indiceCierre = -1;
            for (int i = indiceApertura; i < line.Length; i++)
            {
                if (line[i] == '(') balance++;
                else if (line[i] == ')')
                {
                    balance--;
                    if (balance == 0) { indiceCierre = i; break; }
                }
            }
            if (indiceCierre < 0) return false;

            string resto = line.Substring(indiceCierre + 1).Trim();
            if (resto != "{") return false;

            condicionCruda = line.Substring(indiceApertura + 1, indiceCierre - indiceApertura - 1).Trim();
            return true;
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
                var dirTexto = dirMatch.Groups[2].Value;

                InstructionType type;
                switch (typeText)
                {
                    case "MOV": type = InstructionType.Mov; break;
                    case "AMT": type = InstructionType.Amt; break;
                    case "RADAR": type = InstructionType.Radar; break;
                    case "MISIL": type = InstructionType.Misil; break;
                    default: throw new FormatException($"Instrucción desconocida: '{line}'");
                }

                if (string.IsNullOrEmpty(dirTexto))
                {
                    throw new FormatException(
                        $"'{typeText}()' necesita una dirección (N/S/E/O) — usa la rosa de los vientos para completarla.");
                }

                return new Instruction { Type = type, Dir = ParseDirection(dirTexto) };
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