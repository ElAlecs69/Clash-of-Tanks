using UnityEngine;

namespace TanksGame.Core
{
    // Paleta compartida de personalización de tanques (colores, símbolos de
    // calcomanía, números y colores de bandera). Vive acá, en Core, para que tanto
    // la pantalla de programación (UI, para las miniaturas) como la vista del
    // tablero durante la partida (Visual, para pintar los tanques reales) usen
    // exactamente los mismos valores por índice -- así un tanque se ve igual en la
    // pantalla de personalización y en el campo de batalla.
    //
    // Nota: los mismos 4/4/4/4/5 valores están duplicados como arrays privados
    // dentro de PantallaProgramacionTanques.cs (para no tocar esa pantalla, ya
    // probada) -- si algún día cambian los colores/símbolos ahí, hay que
    // actualizarlos acá también para que sigan viéndose igual en el juego.
    public static class PaletaSkins
    {
        public static readonly Color[] ColoresPrincipales =
        {
            new Color(0.30f, 0.36f, 0.22f), // Verde militar
            new Color(0.55f, 0.47f, 0.33f), // Arena
            new Color(0.35f, 0.36f, 0.38f), // Gris urbano
            new Color(0.20f, 0.28f, 0.34f), // Azul marino
        };

        public static readonly string[] SimbolosCalcomania = { "★", "✖", "●", "▲" };
        public static readonly string[] NumerosTanque = { "01", "07", "13", "99" };

        public static readonly Color[] ColoresBandera =
        {
            new Color(0.75f, 0.15f, 0.15f),
            new Color(0.15f, 0.35f, 0.65f),
            new Color(0.20f, 0.55f, 0.25f),
            new Color(0.80f, 0.65f, 0.15f),
            new Color(0.85f, 0.85f, 0.85f),
        };

        public static Color ObtenerColorPrincipal(int indice) =>
            ColoresPrincipales[Mathf.Clamp(indice, 0, ColoresPrincipales.Length - 1)];

        public static Color ObtenerColorBandera(int indice) =>
            ColoresBandera[Mathf.Clamp(indice, 0, ColoresBandera.Length - 1)];

        public static string ObtenerSimboloCalcomania(int indice) =>
            SimbolosCalcomania[Mathf.Clamp(indice, 0, SimbolosCalcomania.Length - 1)];

        public static string ObtenerNumero(int indice) =>
            NumerosTanque[Mathf.Clamp(indice, 0, NumerosTanque.Length - 1)];

        // Genera, en tiempo de ejecución, una textura 32x32 con el patrón elegido
        // (liso, a cuadros, punteado, o "ruido" tipo camuflaje), tintada con el
        // color principal. Es la misma lógica que usa la pantalla de programación
        // para sus miniaturas de patrón, reutilizada acá para pintar el tanque real
        // en el campo de batalla.
        public static Texture2D GenerarTexturaPatron(int patronIndice, Color colorBase)
        {
            const int n = 32;
            var textura = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            Color oscuro = new Color(colorBase.r * 0.55f, colorBase.g * 0.55f, colorBase.b * 0.55f, 1f);
            Color claro = Color.Lerp(colorBase, Color.white, 0.4f);

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    bool esClaro;
                    switch (patronIndice)
                    {
                        case 0:
                            esClaro = false;
                            break;
                        case 1:
                            esClaro = ((x + y) / 4) % 2 == 0;
                            break;
                        case 2:
                            int cx = (x % 8) - 4, cy = (y % 8) - 4;
                            esClaro = (cx * cx + cy * cy) < 6;
                            break;
                        default:
                            esClaro = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) > 0.55f;
                            break;
                    }
                    textura.SetPixel(x, y, esClaro ? claro : oscuro);
                }
            }
            textura.Apply();
            return textura;
        }
    }
}
