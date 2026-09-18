using UnityEngine;

namespace TanksGame.Core
{
    // Paleta compartida de personalización de tanques (colores, símbolos de
    // calcomanía y banderas). Vive aquí, en Core, para que tanto la pantalla de
    // programación (UI, para las miniaturas) como la vista del tablero durante la
    // partida (Visual, para pintar los tanques reales) usen exactamente los mismos
    // valores por índice -- así un tanque se ve igual en la pantalla de
    // personalización y en el campo de batalla.
    //
    // El número del tanque YA NO es una elección de personalización: ahora se pinta
    // automáticamente en el casco con el número de orden en que se programó (ver
    // Tank.cs / BoardView.cs), así los jugadores pueden identificar cada tanque en
    // el tablero sin ambigüedad. Las banderas se dibujan por código (franjas/formas
    // simplificadas de banderas reales), no vienen de ningún asset externo.
    //
    // Nota: los mismos valores de Patron/Color/Calcomania/Bandera están duplicados
    // como arrays privados dentro de PantallaProgramacionTanques.cs (para no tocar
    // esa pantalla, ya probada) -- si algún día cambian aquí, hay que actualizarlos
    // ahí también para que sigan viéndose igual en el juego.
    public static class PaletaSkins
    {
        public static readonly Color[] ColoresPrincipales =
        {
            new Color(0.30f, 0.36f, 0.22f), // Verde militar
            new Color(0.55f, 0.47f, 0.33f), // Arena
            new Color(0.35f, 0.36f, 0.38f), // Gris urbano
            new Color(0.20f, 0.28f, 0.34f), // Azul marino
            new Color(0.45f, 0.04f, 0.04f), // Rojo sangre
            new Color(0.06f, 0.06f, 0.07f), // Negro ónix
            new Color(0.62f, 0.49f, 0.10f), // Dorado
            new Color(0.28f, 0.09f, 0.42f), // Púrpura real
        };

        // Calcomanías: las 4 originales + diseños más "épicos" para reemplazar el
        // espacio que antes ocupaba el selector de NÚMERO (ahora automático).
        public static readonly string[] SimbolosCalcomania =
        {
            "★", "✖", "●", "▲", "☠", "⚔", "✦", "⚡"
        };

        // ------------------------------------------------------------------
        // BANDERAS: países involucrados en la 1ra y 2da Guerra Mundial.
        // Dibujadas por código (franjas/formas simplificadas), sin assets externos.
        // ------------------------------------------------------------------
        public enum TipoBandera { Horizontal, Vertical, Especial }

        public struct DefinicionBandera
        {
            public string Nombre;
            public TipoBandera Tipo;
            public Color[] Colores; // franjas en orden (horizontal: arriba->abajo; vertical: izq->der)
            public DefinicionBandera(string nombre, TipoBandera tipo, params Color[] colores)
            {
                Nombre = nombre; Tipo = tipo; Colores = colores;
            }
        }

        private static readonly Color Blanco = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color Negro = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color RojoBandera = new Color(0.80f, 0.10f, 0.10f);
        private static readonly Color AzulBandera = new Color(0.10f, 0.20f, 0.55f);

        public static readonly DefinicionBandera[] Banderas =
        {
            new DefinicionBandera("México", TipoBandera.Vertical,
                new Color(0.0f, 0.45f, 0.2f), Blanco, RojoBandera),
            new DefinicionBandera("Rusia / URSS", TipoBandera.Horizontal,
                Blanco, AzulBandera, RojoBandera),
            new DefinicionBandera("Estados Unidos", TipoBandera.Especial, RojoBandera, Blanco, AzulBandera),
            new DefinicionBandera("Alemania", TipoBandera.Horizontal,
                Negro, RojoBandera, new Color(0.95f, 0.75f, 0.10f)),
            new DefinicionBandera("Japón", TipoBandera.Especial, Blanco, RojoBandera),
            new DefinicionBandera("China", TipoBandera.Especial, RojoBandera, new Color(0.95f, 0.85f, 0.15f)),
            new DefinicionBandera("Reino Unido", TipoBandera.Especial, AzulBandera, Blanco, RojoBandera),
            new DefinicionBandera("Francia", TipoBandera.Vertical,
                AzulBandera, Blanco, RojoBandera),
            new DefinicionBandera("Italia", TipoBandera.Vertical,
                new Color(0.0f, 0.45f, 0.2f), Blanco, RojoBandera),
            new DefinicionBandera("Austria", TipoBandera.Horizontal,
                RojoBandera, Blanco, RojoBandera),
            new DefinicionBandera("Imperio Otomano", TipoBandera.Especial, RojoBandera, Blanco),
            new DefinicionBandera("Canadá", TipoBandera.Especial, RojoBandera, Blanco),
            new DefinicionBandera("Australia", TipoBandera.Especial, AzulBandera, Blanco),
            new DefinicionBandera("Polonia", TipoBandera.Horizontal,
                Blanco, RojoBandera),
            new DefinicionBandera("Países Bajos", TipoBandera.Horizontal,
                RojoBandera, Blanco, AzulBandera),
            new DefinicionBandera("Brasil", TipoBandera.Especial,
                new Color(0.0f, 0.45f, 0.2f), new Color(0.95f, 0.85f, 0.15f)),
            new DefinicionBandera("India", TipoBandera.Horizontal,
                new Color(0.95f, 0.55f, 0.15f), Blanco, new Color(0.0f, 0.45f, 0.2f)),
            new DefinicionBandera("Bélgica", TipoBandera.Vertical,
                Negro, new Color(0.95f, 0.75f, 0.10f), RojoBandera),
        };

        public static Color ObtenerColorPrincipal(int indice) =>
            ColoresPrincipales[Mathf.Clamp(indice, 0, ColoresPrincipales.Length - 1)];

        public static string ObtenerSimboloCalcomania(int indice) =>
            SimbolosCalcomania[Mathf.Clamp(indice, 0, SimbolosCalcomania.Length - 1)];

        public static DefinicionBandera ObtenerBandera(int indice) =>
            Banderas[Mathf.Clamp(indice, 0, Banderas.Length - 1)];

        // Genera, en tiempo de ejecución, una textura 32x32 con el patrón elegido
        // (liso, a cuadros, punteado, o "ruido" tipo camuflaje), tintada con el
        // color principal. Es la misma lógica que usa la pantalla de programación
        // para sus miniaturas de patrón, reutilizada aquí para pintar el tanque real
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

            // Colores de fuego para el patrón "Llamas" (índice 4): no dependen del
            // color principal elegido, para que el patrón se vea igual de "épico"
            // sin importar el color de fondo del tanque.
            Color llamaRoja = new Color(0.55f, 0.03f, 0.01f);
            Color llamaNaranja = new Color(0.92f, 0.42f, 0.04f);
            Color llamaAmarilla = new Color(1f, 0.86f, 0.25f);

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Color pixel;
                    switch (patronIndice)
                    {
                        case 0: // Liso
                            pixel = oscuro;
                            break;
                        case 1: // Cuadros
                            pixel = ((x + y) / 4) % 2 == 0 ? claro : oscuro;
                            break;
                        case 2: // Punteado
                        {
                            int cx = (x % 8) - 4, cy = (y % 8) - 4;
                            pixel = (cx * cx + cy * cy) < 6 ? claro : oscuro;
                            break;
                        }
                        case 3: // Camuflaje (manchas orgánicas)
                            pixel = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) > 0.55f ? claro : oscuro;
                            break;
                        case 4: // Llamas (patrón épico de fuego, colores fijos)
                        {
                            float alturaNormalizada = y / (float)(n - 1); // 0 abajo, 1 arriba
                            float parpadeo = Mathf.PerlinNoise(x * 0.3f, y * 0.35f);
                            float intensidad = Mathf.Clamp01((1f - alturaNormalizada) * 1.1f + (parpadeo - 0.5f) * 0.7f);
                            pixel = intensidad > 0.72f ? llamaAmarilla : (intensidad > 0.38f ? llamaNaranja : llamaRoja);
                            break;
                        }
                        case 5: // Camuflaje digital (bloques pixelados)
                        {
                            const int bloque = 4;
                            int bx = x / bloque, by = y / bloque;
                            pixel = Mathf.PerlinNoise(bx * 0.6f, by * 0.6f) > 0.5f ? claro : oscuro;
                            break;
                        }
                        default:
                            pixel = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) > 0.55f ? claro : oscuro;
                            break;
                    }
                    textura.SetPixel(x, y, pixel);
                }
            }
            textura.Apply();
            return textura;
        }

        // Genera una textura 48x32 (proporción típica 3:2 de bandera) para el índice
        // de país dado. Franjas horizontales/verticales se dibujan de forma genérica;
        // los diseños "Especial" (EE.UU., Japón, Reino Unido, Turquía, Canadá,
        // Australia, Brasil) se resuelven con lógica dedicada porque no son simples
        // franjas.
        public static Texture2D GenerarTexturaBandera(int indice)
        {
            const int ancho = 48, alto = 32;
            var def = ObtenerBandera(indice);
            var textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < alto; y++)
            {
                for (int x = 0; x < ancho; x++)
                {
                    Color color = ColorEnPixelBandera(def, x, y, ancho, alto);
                    textura.SetPixel(x, y, color);
                }
            }
            textura.Apply();
            return textura;
        }

        private static Color ColorEnPixelBandera(DefinicionBandera def, int x, int y, int ancho, int alto)
        {
            int n = def.Colores.Length;
            switch (def.Tipo)
            {
                case TipoBandera.Horizontal:
                {
                    // y=0 es abajo en Texture2D; se invierte para que la primera franja
                    // de "Colores" quede arriba, como se lee la definición.
                    int franja = Mathf.Clamp((alto - 1 - y) * n / alto, 0, n - 1);
                    return def.Colores[franja];
                }
                case TipoBandera.Vertical:
                {
                    int franja = Mathf.Clamp(x * n / ancho, 0, n - 1);
                    return def.Colores[franja];
                }
                default: // Especial: cada bandera con su propia forma simplificada.
                    return ColorPixelBanderaEspecial(def, x, y, ancho, alto);
            }
        }

        private static Color ColorPixelBanderaEspecial(DefinicionBandera def, int x, int y, int ancho, int alto)
        {
            switch (def.Nombre)
            {
                case "Estados Unidos":
                {
                    // 7 franjas rojo/blanco alternadas + cantón azul arriba-izquierda.
                    bool enCanton = x < ancho * 2 / 5 && y >= alto / 2;
                    if (enCanton) return def.Colores[2]; // azul
                    int franja = (alto - 1 - y) * 7 / alto;
                    return franja % 2 == 0 ? def.Colores[0] : def.Colores[1]; // rojo/blanco
                }
                case "Japón":
                {
                    // Círculo rojo centrado sobre fondo blanco (Hinomaru).
                    float cx = ancho * 0.5f, cy = alto * 0.5f, r = alto * 0.32f;
                    float dx = x - cx, dy = y - cy;
                    return (dx * dx + dy * dy) <= r * r ? def.Colores[1] : def.Colores[0];
                }
                case "China":
                {
                    // Campo rojo con una "estrella" amarilla simplificada arriba-izq.
                    float cx = ancho * 0.16f, cy = alto * 0.72f, r = alto * 0.16f;
                    float dx = x - cx, dy = y - cy;
                    return (dx * dx + dy * dy) <= r * r ? def.Colores[1] : def.Colores[0];
                }
                case "Reino Unido":
                {
                    // Cruz simplificada (aprox. Union Jack) sobre fondo azul marino.
                    bool cruzBlanca = Mathf.Abs(x - ancho / 2) < 4 || Mathf.Abs(y - alto / 2) < 3;
                    bool cruzRoja = Mathf.Abs(x - ancho / 2) < 2 || Mathf.Abs(y - alto / 2) < 1;
                    if (cruzRoja) return def.Colores[2];
                    if (cruzBlanca) return def.Colores[1];
                    return def.Colores[0];
                }
                case "Imperio Otomano":
                {
                    // Media luna y estrella simplificadas (blanco) sobre campo rojo.
                    float cx = ancho * 0.4f, cy = alto * 0.5f, r = alto * 0.28f;
                    float dx = x - cx, dy = y - cy;
                    bool enLunaExterior = (dx * dx + dy * dy) <= r * r;
                    float dx2 = x - (cx + r * 0.5f), dy2 = y - cy;
                    bool enLunaInterior = (dx2 * dx2 + dy2 * dy2) <= (r * 0.82f) * (r * 0.82f);
                    if (enLunaExterior && !enLunaInterior) return def.Colores[1];
                    return def.Colores[0];
                }
                case "Canadá":
                {
                    // Franjas roja-blanca-roja con un bloque rojo central simplificando
                    // la hoja de arce.
                    int tercio = ancho / 3;
                    if (x < tercio || x >= ancho - tercio) return def.Colores[0];
                    bool bloqueCentral = Mathf.Abs(x - ancho / 2) < 4 && Mathf.Abs(y - alto / 2) < 6;
                    return bloqueCentral ? def.Colores[0] : def.Colores[1];
                }
                case "Australia":
                {
                    // Fondo azul con un bloque blanco (Union Jack simplificada) arriba-izq.
                    bool enCanton = x < ancho * 2 / 5 && y >= alto / 2;
                    return enCanton ? def.Colores[1] : def.Colores[0];
                }
                case "Brasil":
                {
                    // Rombo amarillo simplificado sobre fondo verde.
                    float nx = (x - ancho / 2f) / (ancho * 0.42f);
                    float ny = (y - alto / 2f) / (alto * 0.42f);
                    return (Mathf.Abs(nx) + Mathf.Abs(ny)) <= 1f ? def.Colores[1] : def.Colores[0];
                }
                default:
                    return def.Colores.Length > 0 ? def.Colores[0] : Color.gray;
            }
        }
    }
}