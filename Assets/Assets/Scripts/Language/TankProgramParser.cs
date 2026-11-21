using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TanksGame.Core;

namespace TanksGame.Language
{
    // Convierte texto del mini-lenguaje en una lista de ProgramStep ejecutable por el
    // TurnManager.
    //
    // Condicional (una sola linea, una sola accion):
    //   IF (RADAR(N) > 0) THEN (MISIL(N))
    //   IF (VIDA < 40) THEN (REPARAR)
    //
    // La condicion puede encadenar varias condiciones simples con "Y" o "AND"
    // (todas deben cumplirse):
    //   IF (RADAR(N) < 0 Y RADAR(O) < 0) THEN (MOV(S))
    //
    // MINA coloca UNA mina por uso. Formas:
    //   MINA()      -> en la casilla del tanque
    //   MINA(dir)   -> en la casilla vecina en esa direccion; dir puede ser
    //                  N, S, E, O (conexas) o NO, NE, SE, SO (diagonales)
    //
    // REPARAR (sin argumentos): recupera vida si el tanque esta sobre un
    // hospital y le quedan reparaciones. Cuantas y cuanta vida se eligen al
    // configurar la partida (ver ReglasPartida).
    //
    // El parser no distingue mayusculas de minusculas. "BUCLE" / "FIN" / "INICIO" /
    // "INICIO:" se aceptan como marcadores sin efecto: el programa completo ya se
    // repite solo en bucle (ver TankAgent.GetNextStep).
    public static class TankProgramParser
    {
        private static readonly Regex InstrWithDirRegex =
            new Regex(@"^(MOV|AMT|RADAR|MISIL)\(([NSEO]?)\)$", RegexOptions.IgnoreCase);

        // MINA con argumentos entre parentesis (puede venir vacio: MINA()).
        private static readonly Regex MinaConArgsRegex =
            new Regex(@"^MINA\(([^()]*)\)$", RegexOptions.IgnoreCase);

        // Una linea que empieza con la palabra IF.
        private static readonly Regex InicioIfRegex =
            new Regex(@"^IF(?![A-Za-z0-9_])", RegexOptions.IgnoreCase);

        private const string AyudaIf = "El IF se escribe en una sola línea: IF (condición) THEN (acción).";

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
                    // --- Condicional: IF (condición) THEN (acción) ---
                    if (InicioIfRegex.IsMatch(line))
                    {
                        ParseLineaIf(line, out var condicionCruda, out var accionCruda);
                        var condition = ParseCondition(condicionCruda);
                        var instruction = ParseInstruction(accionCruda);
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
                catch (FormatException ex)
                {
                    // Los errores de ParseCondition/ParseInstruction no traen número de
                    // línea -- se lo agregamos acá, que es el único lugar donde sabemos a
                    // qué línea del editor corresponde 'line'.
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

        // Devuelve el indice del ')' que cierra el '(' ubicado en 'indiceApertura'
        // (contando parentesis anidados, como en RADAR(N) dentro de la condicion),
        // o -1 si no se cierra.
        private static int IndiceCierreParentesis(string texto, int indiceApertura)
        {
            int balance = 0;
            for (int i = indiceApertura; i < texto.Length; i++)
            {
                if (texto[i] == '(') balance++;
                else if (texto[i] == ')')
                {
                    balance--;
                    if (balance == 0) return i;
                }
            }
            return -1;
        }

        // Separa "IF (condición) THEN (acción)" en sus dos partes (sin los
        // parentesis externos). Lanza FormatException si la linea no respeta esa forma.
        private static void ParseLineaIf(string line, out string condicion, out string accion)
        {
            string resto = line.Substring(2).TrimStart();
            if (resto.Length == 0 || resto[0] != '(')
                throw new FormatException($"Falta el paréntesis de la condición. {AyudaIf}");

            int cierreCondicion = IndiceCierreParentesis(resto, 0);
            if (cierreCondicion < 0)
                throw new FormatException($"Falta cerrar el paréntesis de la condición. {AyudaIf}");

            condicion = resto.Substring(1, cierreCondicion - 1).Trim();
            if (condicion.Length == 0)
                throw new FormatException($"La condición del IF está vacía. {AyudaIf}");

            string despues = resto.Substring(cierreCondicion + 1).TrimStart();
            if (!despues.StartsWith("THEN", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"Falta THEN después de la condición. {AyudaIf}");

            despues = despues.Substring(4).TrimStart();
            if (despues.Length == 0 || despues[0] != '(')
                throw new FormatException($"La acción después de THEN debe ir entre paréntesis. {AyudaIf}");

            int cierreAccion = IndiceCierreParentesis(despues, 0);
            if (cierreAccion < 0)
                throw new FormatException($"Falta cerrar el paréntesis de la acción. {AyudaIf}");

            if (despues.Substring(cierreAccion + 1).Trim().Length > 0)
                throw new FormatException($"No puede haber nada después de THEN (acción). {AyudaIf}");

            accion = despues.Substring(1, cierreAccion - 1).Trim();
            if (accion.Length == 0)
                throw new FormatException($"La acción del IF está vacía. {AyudaIf}");
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

            var minaMatch = MinaConArgsRegex.Match(upper);
            if (minaMatch.Success)
                return new Instruction { Type = InstructionType.Mina, MinaDir = ParseDireccionMina(minaMatch.Groups[1].Value, line) };

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

        // MINA() -> casilla propia; MINA(dir) -> casilla vecina en esa direccion
        // (N, S, E, O, NO, NE, SE o SO).
        private static DireccionMina ParseDireccionMina(string argumentos, string lineaOriginal)
        {
            if (argumentos.Length == 0) return DireccionMina.Propia;

            switch (argumentos)
            {
                case "N": return DireccionMina.N;
                case "S": return DireccionMina.S;
                case "E": return DireccionMina.E;
                case "O": return DireccionMina.O;
                case "NO": return DireccionMina.NO;
                case "NE": return DireccionMina.NE;
                case "SE": return DireccionMina.SE;
                case "SO": return DireccionMina.SO;
            }

            throw new FormatException(
                $"MINA no reconoce '{lineaOriginal}'. Usa MINA() o MINA(dir) con dir = N, S, E, O, NO, NE, SE o SO.");
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