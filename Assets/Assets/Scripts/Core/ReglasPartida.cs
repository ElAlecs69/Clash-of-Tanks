using System;
using UnityEngine;

namespace TanksGame.Core
{
    // Reglas configurables de una partida. Se eligen en la pantalla de
    // "Configurar partida" (antes de empezar) y viajan hasta el GameManager a
    // traves de ConfiguracionPartidaPendiente.
    //
    // Daños (por defecto):
    //   AMT   : configurable entre 5 y 20 (defecto 12)
    //   MISIL : AMT * 2
    //   MINA  : AMT * 1.5
    [Serializable]
    public class ReglasPartida
    {
        public const int TurnosMinaMin = 1;
        public const int TurnosMinaMax = 10;
        public const int TurnosMinaDefecto = 1;

        public const int TurnosEscudoMin = 1;
        public const int TurnosEscudoMax = 10;
        public const int TurnosEscudoDefecto = 1;

        public const int DanioAmtMin = 5;
        public const int DanioAmtMax = 20;
        public const int DanioAmtDefecto = 12;

        public const int CuracionHospitalMin = 1;
        public const int CuracionHospitalMax = 50;
        public const int CuracionHospitalDefecto = 10;

        // REPARAR: cuantas veces puede usarlo cada tanque en la partida y cuanta
        // vida (%) recupera cada vez.
        public const int ReparacionesMin = 1;
        public const int ReparacionesMax = 10;
        public const int ReparacionesDefecto = 3;

        public const int CuracionRepararMin = 5;
        public const int CuracionRepararMax = 50;
        public const int CuracionRepararDefecto = 20;

        public const float FactorMisil = 2f;
        public const float FactorMina = 1.5f;

        // Turnos que tarda una mina en activarse despues de colocada (1 = queda
        // armada desde la ronda siguiente, que es como funcionaba antes).
        [Range(TurnosMinaMin, TurnosMinaMax)] public int turnosActivacionMina = TurnosMinaDefecto;

        // Turnos que dura el ESCUDO una vez activado (contando la ronda en que
        // se activa).
        [Range(TurnosEscudoMin, TurnosEscudoMax)] public int turnosEscudo = TurnosEscudoDefecto;

        [Range(DanioAmtMin, DanioAmtMax)] public int danioAmt = DanioAmtDefecto;

        // Vida (%) que recupera un tanque al pasar sobre un hospital.
        [Range(CuracionHospitalMin, CuracionHospitalMax)] public int curacionHospital = CuracionHospitalDefecto;

        // Reparaciones (REPARAR) disponibles por tanque.
        [Range(ReparacionesMin, ReparacionesMax)] public int reparaciones = ReparacionesDefecto;

        // Vida (%) que recupera cada REPARAR.
        [Range(CuracionRepararMin, CuracionRepararMax)] public int curacionReparar = CuracionRepararDefecto;

        public float DanioAmt => danioAmt;
        public float DanioMisil => danioAmt * FactorMisil;
        public float DanioMina => danioAmt * FactorMina;

        public void Normalizar()
        {
            turnosActivacionMina = Mathf.Clamp(turnosActivacionMina, TurnosMinaMin, TurnosMinaMax);
            turnosEscudo = Mathf.Clamp(turnosEscudo, TurnosEscudoMin, TurnosEscudoMax);
            danioAmt = Mathf.Clamp(danioAmt, DanioAmtMin, DanioAmtMax);
            curacionHospital = Mathf.Clamp(curacionHospital, CuracionHospitalMin, CuracionHospitalMax);
            reparaciones = Mathf.Clamp(reparaciones, ReparacionesMin, ReparacionesMax);
            curacionReparar = Mathf.Clamp(curacionReparar, CuracionRepararMin, CuracionRepararMax);
        }

        public ReglasPartida Copia()
        {
            var copia = (ReglasPartida)MemberwiseClone();
            copia.Normalizar();
            return copia;
        }
    }
}