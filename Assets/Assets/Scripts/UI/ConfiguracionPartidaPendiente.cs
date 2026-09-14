using System.Collections.Generic;

namespace TanksGame.Core
{
    // Puente simple y estático entre PantallaProgramacionTanques (UI) y el
    // GameManager de la escena de juego (Gameplay): la pantalla de programación
    // llena esto justo antes de cargar la escena de juego, y el GameManager lo lee
    // en su propio Awake() para armar la partida con los tanques y el tamaño de
    // tablero que el jugador acaba de programar, en vez de usar su configuración
    // fija del Inspector.
    //
    // Vive en Core (no en UI ni en Gameplay) a propósito: así ninguno de los dos
    // pasa a depender del namespace del otro, cada uno solo depende de Core.
    //
    // Si Hay es false, el GameManager sigue usando su configuración normal del
    // Inspector — así la escena de juego también se puede abrir y probar sola,
    // sin pasar por la pantalla de programación.
    public static class ConfiguracionPartidaPendiente
    {
        public static bool Hay { get; private set; }
        public static IReadOnlyList<string> ScriptsPorTanque { get; private set; }
        public static IReadOnlyList<TanqueSkinDatos> SkinsPorTanque { get; private set; }
        public static int TamanoTablero { get; private set; }

        public static void Establecer(IReadOnlyList<string> scriptsPorTanque, IReadOnlyList<TanqueSkinDatos> skinsPorTanque, int tamanoTablero)
        {
            ScriptsPorTanque = scriptsPorTanque;
            SkinsPorTanque = skinsPorTanque;
            TamanoTablero = tamanoTablero;
            Hay = true;
        }

        // El GameManager llama a esto apenas termina de leer los datos, para que si
        // el jugador vuelve a la pantalla de programación y de ahí carga la escena
        // de juego "a mano" sin haber programado nada nuevo, no se reuse por
        // accidente la configuración de la partida anterior.
        public static void Limpiar()
        {
            Hay = false;
            ScriptsPorTanque = null;
            SkinsPorTanque = null;
        }
    }
}