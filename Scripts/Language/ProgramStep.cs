namespace TanksGame.Language
{
    // Una línea del programa de un tanque.
    // Si Condition es null, Instruction siempre se ejecuta ese turno.
    // Si Condition no es null, Instruction solo se ejecuta si la condición es verdadera;
    // si es falsa, el tanque efectivamente ESPERA ese turno (sigue contando como su turno).
    public class ProgramStep
    {
        public Condition Condition;
        public Instruction Instruction;
    }
}
