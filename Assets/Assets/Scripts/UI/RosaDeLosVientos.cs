using UnityEngine;
using UnityEngine.UI;

namespace TanksGame.UI
{
    // Rosa de los vientos del tablero (N, S, E, O).
    //
    // Se dibuja SOLA por código (textura procedural con la misma forma que la
    // imagen de referencia: estrella de 4 puntas con cada punta mitad clara,
    // mitad oscura), así que no hace falta importar ningún asset.
    //
    // Orientación según las reglas del juego (ver Direction.ToOffset):
    //   N = +Z del mundo (y+ en la celda), S = -Z, E = +X, O = -X.
    //
    // Como la cámara orbita con clic derecho (OrbitZoomCamera), la rosa gira
    // en cada frame para que la N siempre apunte hacia donde está el norte
    // del tablero EN PANTALLA. Las letras se quedan siempre derechas.
    //
    // Uso: GameplayUI la crea en ConstruirUI() con RosaDeLosVientos.Crear(...).
    public class RosaDeLosVientos : MonoBehaviour
    {
        // Distancias normalizadas (punta = 1) tomadas de la imagen de referencia.
        private const float Hombro = 0.124f;

        private static readonly Color ColorClaro = new Color(1f, 1f, 0.6f, 1f);
        private static readonly Color ColorOscuro = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color ColorBorde = Color.black;

        private RectTransform rectRosa;
        private RectTransform rectN, rectE, rectS, rectO;
        private float radioLetras;
        private Camera camara;

        // Crea la rosa como hijo de 'padre' (el Canvas). 'anclaje' va de 0 a 1
        // sobre la pantalla; 'tamano' es el diámetro de la estrella en unidades
        // del canvas.
        public static RosaDeLosVientos Crear(Transform padre, Vector2 anclaje, float tamano, Camera camaraJuego)
        {
            var raiz = new GameObject("RosaDeLosVientos");
            raiz.transform.SetParent(padre, false);
            var rect = raiz.AddComponent<RectTransform>();
            rect.anchorMin = anclaje;
            rect.anchorMax = anclaje;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(tamano, tamano);

            var rosa = raiz.AddComponent<RosaDeLosVientos>();
            rosa.camara = camaraJuego;
            rosa.Construir(tamano);
            return rosa;
        }

        private void Construir(float tamano)
        {
            // Estrella (se rota en LateUpdate).
            var estrellaGo = new GameObject("Estrella");
            estrellaGo.transform.SetParent(transform, false);
            var imagen = estrellaGo.AddComponent<RawImage>();
            imagen.texture = GenerarTexturaEstrella(384);
            imagen.raycastTarget = false;
            rectRosa = estrellaGo.GetComponent<RectTransform>();
            rectRosa.anchorMin = rectRosa.anchorMax = new Vector2(0.5f, 0.5f);
            rectRosa.pivot = new Vector2(0.5f, 0.5f);
            rectRosa.sizeDelta = new Vector2(tamano, tamano);

            // Letras (fuera de la estrella, siempre derechas).
            radioLetras = tamano * 0.5f + 16f;
            rectN = CrearLetra("N");
            rectE = CrearLetra("E");
            rectS = CrearLetra("S");
            rectO = CrearLetra("O");
        }

        private RectTransform CrearLetra(string letra)
        {
            var go = new GameObject("Letra_" + letra);
            go.transform.SetParent(transform, false);
            var texto = go.AddComponent<Text>();
            texto.text = letra;
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 26;
            texto.fontStyle = FontStyle.Bold;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = Color.white;
            texto.raycastTarget = false;

            // Contorno oscuro para que se lea sobre el bosque.
            var contorno = go.AddComponent<Outline>();
            contorno.effectColor = new Color(0f, 0f, 0f, 0.95f);
            contorno.effectDistance = new Vector2(2f, -2f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(40f, 40f);
            return rect;
        }

        private void LateUpdate()
        {
            if (camara == null)
            {
                camara = Camera.main;
                if (camara == null) camara = FindObjectOfType<Camera>();
                if (camara == null) return;
            }

            // Ejes de la cámara proyectados sobre el suelo: "arriba en pantalla"
            // = hacia donde mira la cámara; "derecha en pantalla" = su derecha.
            Vector3 adelante = camara.transform.forward;
            adelante.y = 0f;
            if (adelante.sqrMagnitude < 1e-6f) adelante = camara.transform.up; // vista cenital
            adelante.y = 0f;
            adelante.Normalize();
            Vector3 derecha = camara.transform.right;
            derecha.y = 0f;
            derecha.Normalize();

            // Posición en pantalla del norte (+Z) y de los demás puntos.
            Vector2 norte = DireccionEnPantalla(Vector3.forward, derecha, adelante);

            // Ángulo (horario, desde "arriba") al que cae el norte. En UI un
            // giro positivo en Z es antihorario, de ahí el signo menos.
            float anguloNorte = Mathf.Atan2(norte.x, norte.y) * Mathf.Rad2Deg;
            rectRosa.localRotation = Quaternion.Euler(0f, 0f, -anguloNorte);

            rectN.anchoredPosition = norte * radioLetras;
            rectS.anchoredPosition = -norte * radioLetras;
            Vector2 este = DireccionEnPantalla(Vector3.right, derecha, adelante);
            rectE.anchoredPosition = este * radioLetras;
            rectO.anchoredPosition = -este * radioLetras;
        }

        private static Vector2 DireccionEnPantalla(Vector3 dirMundo, Vector3 derecha, Vector3 adelante)
        {
            var v = new Vector2(Vector3.Dot(dirMundo, derecha), Vector3.Dot(dirMundo, adelante));
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.up;
        }

        // ------------------------------------------------------------------
        // TEXTURA PROCEDURAL DE LA ESTRELLA
        // ------------------------------------------------------------------

        // Cada punta = 2 triángulos (una mitad clara y una oscura), como en la
        // imagen de referencia. Coordenadas con la punta en distancia 1.
        private struct Triangulo
        {
            public Vector2 a, b, c;
            public Color color;
            public Triangulo(Vector2 a, Vector2 b, Vector2 c, Color color)
            { this.a = a; this.b = b; this.c = c; this.color = color; }
        }

        private static Texture2D GenerarTexturaEstrella(int tam)
        {
            float h = Hombro;
            var o = Vector2.zero;
            var tris = new[]
            {
                // Norte: izquierda oscura, derecha clara
                new Triangulo(o, new Vector2(0, 1),  new Vector2(-h,  h), ColorOscuro),
                new Triangulo(o, new Vector2(0, 1),  new Vector2( h,  h), ColorClaro),
                // Este: arriba oscura, abajo clara
                new Triangulo(o, new Vector2(1, 0),  new Vector2( h,  h), ColorOscuro),
                new Triangulo(o, new Vector2(1, 0),  new Vector2( h, -h), ColorClaro),
                // Sur: izquierda clara, derecha oscura
                new Triangulo(o, new Vector2(0, -1), new Vector2(-h, -h), ColorClaro),
                new Triangulo(o, new Vector2(0, -1), new Vector2( h, -h), ColorOscuro),
                // Oeste: arriba clara, abajo oscura
                new Triangulo(o, new Vector2(-1, 0), new Vector2(-h,  h), ColorClaro),
                new Triangulo(o, new Vector2(-1, 0), new Vector2(-h, -h), ColorOscuro),
            };

            var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixeles = new Color[tam * tam];

            const int ss = 3;                    // supermuestreo 3x3 contra dientes de sierra
            const float grosorBorde = 0.006f;    // grosor del contorno negro (unidades normalizadas)
            float escala = 2.1f;                 // la estrella ocupa ~95% del lienzo

            for (int py = 0; py < tam; py++)
            {
                for (int px = 0; px < tam; px++)
                {
                    float r = 0, g = 0, b = 0, alfa = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = ((px + (sx + 0.5f) / ss) / tam - 0.5f) * escala;
                            float v = ((py + (sy + 0.5f) / ss) / tam - 0.5f) * escala;
                            var p = new Vector2(u, v);

                            for (int i = 0; i < tris.Length; i++)
                            {
                                var t = tris[i];
                                if (!DentroTriangulo(p, t.a, t.b, t.c)) continue;
                                float d = Mathf.Min(DistSegmento(p, t.a, t.b),
                                          Mathf.Min(DistSegmento(p, t.b, t.c), DistSegmento(p, t.c, t.a)));
                                Color col = d < grosorBorde ? ColorBorde : t.color;
                                r += col.r; g += col.g; b += col.b; alfa += 1f;
                                break;
                            }
                        }
                    }
                    float n = ss * ss;
                    pixeles[py * tam + px] = alfa > 0f
                        ? new Color(r / alfa, g / alfa, b / alfa, alfa / n)
                        : new Color(0, 0, 0, 0);
                }
            }

            tex.SetPixels(pixeles);
            tex.Apply(true);
            return tex;
        }

        private static bool DentroTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cruz(p, a, b), d2 = Cruz(p, b, c), d3 = Cruz(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static float Cruz(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }

        private static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
