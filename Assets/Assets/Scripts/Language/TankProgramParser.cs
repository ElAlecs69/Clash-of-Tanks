using System;
using System.Collections.Generic;
using System.Linq;
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
    // La condición de un IF (en cualquiera de los dos formatos) puede
    // encadenar varias condiciones simples con "Y" o "AND" (todas deben
    // cumplirse), por ejemplo:
    //   IF (RADAR(N) < 0 Y RADAR(O) < 0) {
    //       MOV(S)
    //   }
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
            new Regex(@"^(VIDA|MISILES|RADAR\(([NSEO])\))\s*(>=|<=|==|=|>|<)\s*(-?\d+(\.\d+)?)$",
                RegexOptions.IgnoreCase);

        // Separa condiciones simples unidas con "Y" o "AND" (con espacios
        // alrededor), por ejemplo "RADAR(N)<0 Y RADAR(O)<0". Como el grupo es
        // no-capturante, Split() no incluye el separador en el resultado.
        private static readonly Regex AndSplitRegex =
            new Regex(@"\s+(?:Y|AND)\s+", RegexOptions.IgnoreCase);

        public static List<ProgramStep> Parse(string script)
        {
            var steps = new List<ProgramStep>();

            // Guardamos, junto a cada línea ya "limpia" (sin comentario, sin espacios
            // sobrantes), el número de línea ORIGINAL del editor. Antes se usaba
            // directamente el índice dentro de esta lista ya filtrada (i + 1) como si
            // fuera el número de línea real, pero como las líneas en blanco, los
            // comentarios y las líneas que quedan vacías al sacarles el comentario se
            // descartan de la lista, ese índice se iba corriendo apenas había alguna
            // línea vacía o un comentario antes del error -- por eso el mensaje señalaba
            // una línea distinta de la que realmente tenía el problema.
            var lines = new List<(string Texto, int NumeroOriginal)>();

            var lineasCrudas = script.Split('\n');
            for (int n = 0; n < lineasCrudas.Length; n++)
            {
                var sinRetorno = lineasCrudas[n].TrimEnd('\r');
                var sinComentario = QuitarComentario(sinRetorno);
                var trimmed = sinComentario.Trim();
                if (trimmed.Length > 0) lines.Add((trimmed, n + 1));
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Texto;
                var numeroLinea = lines[i].NumeroOriginal;

                try
                {
                    // --- Formato nuevo: IF (condición) { ---
                    if (TryParseIfBlockHeader(line, out var condicionCruda))
                    {
                        var condition = ParseCondition(condicionCruda);

                        int indiceInstruccion = i + 1;
                        if (indiceInstruccion >= lines.Count)
                            throw new FormatException($"El bloque IF de la línea {numeroLinea} no tiene ninguna instrucción antes de cerrar.");

                        var lineaInstruccion = lines[indiceInstruccion].Texto;
                        if (lineaInstruccion == "}")
                            throw new FormatException(
                                $"El bloque IF de la línea {numeroLinea} está vacío: debe tener exactamente una instrucción entre las llaves.");

                        var instruction = ParseInstruction(lineaInstruccion);

                        int indiceCierre = indiceInstruccion + 1;
                        if (indiceCierre >= lines.Count || lines[indiceCierre].Texto != "}")
                            throw new FormatException(
                                $"El bloque IF de la línea {numeroLinea} debe cerrar con '}}' justo después de la instrucción " +
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
                            throw new FormatException($"La línea {numeroLinea} (IF) no tiene una instrucción asociada debajo.");

                        i++;
                        var instruction = ParseInstruction(lines[i].Texto);
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
                catch (FormatException ex) when (!ex.Message.StartsWith("El bloque IF") && !ex.Message.StartsWith("La línea"))
                {
                    // Los errores de ParseCondition/ParseInstruction (condición u
                    // operador inválido, instrucción no reconocida, dirección
                    // inválida, etc.) no traían número de línea -- se lo agregamos acá,
                    // en el único lugar donde sabemos con certeza a qué línea del
                    // editor corresponde 'line'. Los mensajes de más arriba ya lo
                    // incluyen, así que no se les vuelve a agregar.
                    throw new FormatException($"Línea {numeroLinea}: {ex.Message}");
                }
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

        // Punto de entrada: si el texto trae una o más "Y"/"AND", arma una
        // condición compuesta (todas deben cumplirse); si no, es una condición
        // simple de toda la vida.
        private static Condition ParseCondition(string text)
        {
            var partes = AndSplitRegex.Split(text.Trim())
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();

            if (partes.Count == 0)
                throw new FormatException($"Condición inválida: '{text}'");

            if (partes.Count == 1)
                return ParseCondicionSimple(partes[0]);

            return new Condition { Sub = partes.Select(ParseCondicionSimple).ToList() };
        }

        private static Condition ParseCondicionSimple(string text)
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
                case "=": condition.Op = ComparisonOp.Equal; break;
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