using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TanksGame.UI
{
    // Tutorial de bienvenida: 3 pantallas sobre fondo negro, con los elementos
    // apareciendo uno tras otro con un degradado de opacidad.
    //
    //   Parte 1: imagen a la IZQUIERDA (dos tanques luchando) + texto a la derecha.
    //   Parte 2: imagen a la IZQUIERDA (tanque lanzando un misil) + instrucciones.
    //   Parte 3: texto a la IZQUIERDA + imagen a la DERECHA (tablero con tanques
    //            personalizados).
    //
    // Las imágenes las asigna MenuPrincipal desde el Inspector (campos
    // "Tutorial Imagen Parte 1/2/3"). Mientras un campo esté vacío se dibuja un
    // marco con el nombre de la imagen que va ahí.
    //
    // Se muestra automáticamente SOLO la primera vez que se abre el juego
    // (PlayerPrefs "TutorialVisto") y se puede volver a ver desde Opciones.
    public class TutorialJuego : MonoBehaviour
    {
        public const string ClavePrefs = "TutorialVisto";

        public static bool YaSeVio => PlayerPrefs.GetInt(ClavePrefs, 0) == 1;

        public static void MarcarComoVisto()
        {
            PlayerPrefs.SetInt(ClavePrefs, 1);
            PlayerPrefs.Save();
        }

        public bool Visible => raiz != null && raiz.activeSelf;

        // ------------------------------------------------------------------
        // TEXTOS (edítalos aquí). Admiten rich text: <b>, <i>, <color=#RRGGBB>.
        // Los números de daño salen de las reglas actuales de TurnManager:
        // si cambias las reglas, actualiza también estas líneas.
        // ------------------------------------------------------------------
        private const string O = "<color=#DEAD33>";   // oro
        private const string G = "<color=#B8B8B8>";   // gris claro
        private const string F = "</color>";

        private const string Kicker1 = "BIENVENIDO, COMANDANTE";
        private const string Titulo1 = "PROGRAMA TU TANQUE";
        private const string Cuerpo1 =
            "Aquí el combate no se gana con reflejos: se gana con " + O + "código" + F + ".\n\n" +
            "Escribirás un script con las acciones de tu tanque: moverse, disparar, " +
            "escanear con el radar, poner minas o activar el escudo.\n\n" +
            "Cuando empiece la partida, tu tanque ejecutará una instrucción por ronda, " +
            "él solo, y al llegar al final del script volverá a empezar. " +
            "Sobrevive y destruye a tus rivales.\n\n" +
            O + "Piensa bien tu estrategia... tu programa pelea por ti." + F;

        private const string Kicker2 = "TU ARSENAL";
        private const string Titulo2 = "INSTRUCCIONES";
        private const string Subtitulo2 =
            "Pulsa una instrucción del panel y, si pide dirección, elígela en la rosa (N, S, E, O).";

        private const string Kicker3 = "A TU MANERA";
        private const string Titulo3 = "PERSONALIZA Y ELIGE EL CAMPO";
        private const string Cuerpo3 =
            "Antes de pelear, dale identidad a tu tanque: elige su " + O + "color principal" + F +
            ", su " + O + "patrón" + F + " y sus " + O + "calcomanías" + F +
            ". Cada tanque puede ser distinto.\n\n" +
            "También decides el " + O + "tamaño del tablero" + F + " con los botones " +
            "<b>-</b> y <b>+</b> de la barra superior: un campo pequeño significa combates " +
            "rápidos y cerrados; uno grande, más espacio para maniobrar y tender emboscadas.\n\n" +
            "Cuando todo esté listo, pulsa " + O + "INICIAR" + F + ".";

        private const string EjemploCodigo =
            "<color=#777777># Si hay un tanque al norte, dispara</color>\n" +
            "<color=#4FC3F7>IF</color> (<color=#4FC3F7>RADAR</color>(<color=#FFB74D>N</color>)>0) {\n" +
            "    <color=#4FC3F7>MISIL</color>(<color=#FFB74D>N</color>)\n" +
            "}\n" +
            "<color=#4FC3F7>MOV</color>(<color=#FFB74D>E</color>)";

        private static string Entrada(string nombre, string descripcion)
        {
            return O + "<b>" + nombre + "</b>" + F + "\n" + G + descripcion + F;
        }

        private static readonly string InstruccionesColumnaA =
            Entrada("MOV(dir)", "Avanza una casilla. Si chocas con un obstáculo u otro tanque, te hace daño (-12%).") + "\n\n" +
            Entrada("AMT(dir)", "Ráfaga de ametralladora: daña (-25%) al primer tanque de esa línea. No gasta munición.") + "\n\n" +
            Entrada("MISIL(dir)", "También daña (-25%) al primer tanque de la línea, pero gasta 1 de tus misiles. Los obstáculos lo bloquean.") + "\n\n" +
            Entrada("MINA", "Deja una mina en tu casilla. Se arma en la ronda siguiente y daña (-20%) al tanque que termine encima.") + "\n\n" +
            Entrada("ESCUDO", "Te protege de todo daño durante esa ronda.");

        private static readonly string InstruccionesColumnaB =
            Entrada("RADAR(dir)  <  >  =", "Mide hasta el primer tanque en esa dirección: distancia positiva si hay uno, negativa si no. Ej.: RADAR(N)>0") + "\n\n" +
            Entrada("IF ( ) { }", "Ejecuta UNA instrucción solo si se cumple la condición (VIDA, MISILES o RADAR). Une varias con Y.") + "\n\n" +
            Entrada("ESPERAR", "No hace nada en este turno.") + "\n\n" +
            Entrada("BUCLE", "Tu script se repite solo cuando llega al final.");

        // ------------------------------------------------------------------
        // ESTILO
        // ------------------------------------------------------------------
        private static readonly Color Oro = new Color(0.87f, 0.68f, 0.20f, 1f);
        private static readonly Color Rojo = new Color(0.75f, 0.10f, 0.10f, 1f);
        private static readonly Color RojoOscuro = new Color(0.45f, 0.05f, 0.05f, 1f);
        private static readonly Color ColorTexto = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color ColorPunto = new Color(0.35f, 0.35f, 0.35f, 1f);

        private const float DuracionAparicion = 0.7f;
        private const float RetrasoEntreElementos = 0.28f;

        private class Pagina
        {
            public GameObject go;
            public readonly List<CanvasGroup> elementos = new List<CanvasGroup>();
        }

        private Sprite[] imagenes;
        private float difuminadoBordes = 0.15f;
        private readonly List<UnityEngine.Object> recursosCreados = new List<UnityEngine.Object>();
        private Font fuente;
        private Action alClick;

        private GameObject raiz;
        private CanvasGroup grupoRaiz;
        private Pagina[] paginas;
        private Image[] puntos;
        private Button botonAnterior;
        private Button botonSiguiente;
        private Text textoSiguiente;
        private int actual;
        private Coroutine rutinaAparicion;
        private Coroutine rutinaRaiz;

        // Crea el tutorial (oculto) como hijo del Canvas 'canvas'. 'anfitrion' es el
        // GameObject donde se agrega este componente (el del menú principal).
        public static TutorialJuego Crear(Transform canvas, GameObject anfitrion,
            Sprite[] imagenesPartes, Font fuentePersonalizada, Action alHacerClick,
            float difuminadoDeBordes = 0.15f)
        {
            var tutorial = anfitrion.AddComponent<TutorialJuego>();
            tutorial.difuminadoBordes = difuminadoDeBordes;
            tutorial.imagenes = imagenesPartes ?? new Sprite[3];
            tutorial.fuente = fuentePersonalizada;
            tutorial.alClick = alHacerClick;
            tutorial.Construir(canvas);
            return tutorial;
        }

        // ------------------------------------------------------------------
        // ABRIR / CERRAR
        // ------------------------------------------------------------------

        public void Abrir()
        {
            if (raiz == null) return;

            // Se marca como visto en cuanto se abre: así solo sale la primera vez
            // aunque el jugador cierre el juego a la mitad.
            MarcarComoVisto();

            raiz.SetActive(true);
            raiz.transform.SetAsLastSibling();

            if (rutinaRaiz != null) StopCoroutine(rutinaRaiz);
            rutinaRaiz = StartCoroutine(AparecerRaiz());

            MostrarPagina(0);
        }

        public void Cerrar()
        {
            if (raiz == null) return;
            if (rutinaAparicion != null) StopCoroutine(rutinaAparicion);
            if (rutinaRaiz != null) StopCoroutine(rutinaRaiz);
            rutinaAparicion = null;
            rutinaRaiz = null;
            raiz.SetActive(false);
        }

        private void Update()
        {
            if (!Visible || Keyboard.current == null) return;

            // ESC lo maneja MenuPrincipal.Update (para que no cierre además Opciones).
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
                Siguiente();
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                Anterior();
        }

        private void Siguiente()
        {
            if (alClick != null) alClick();
            if (actual >= paginas.Length - 1) { Cerrar(); return; }
            MostrarPagina(actual + 1);
        }

        private void Anterior()
        {
            if (actual <= 0) return;
            if (alClick != null) alClick();
            MostrarPagina(actual - 1);
        }

        private void MostrarPagina(int indice)
        {
            actual = Mathf.Clamp(indice, 0, paginas.Length - 1);

            for (int i = 0; i < paginas.Length; i++)
            {
                paginas[i].go.SetActive(i == actual);
                puntos[i].color = i == actual ? Oro : ColorPunto;
            }

            botonAnterior.gameObject.SetActive(actual > 0);
            textoSiguiente.text = actual >= paginas.Length - 1 ? "FINALIZAR" : "SIGUIENTE";

            // Todos los elementos de la página arrancan invisibles y van
            // apareciendo uno tras otro.
            foreach (var cg in paginas[actual].elementos) cg.alpha = 0f;

            if (rutinaAparicion != null) StopCoroutine(rutinaAparicion);
            rutinaAparicion = StartCoroutine(AparecerElementos(paginas[actual]));
        }

        // ------------------------------------------------------------------
        // ANIMACIONES (con tiempo sin escalar, por si el juego estuviera en pausa)
        // ------------------------------------------------------------------

        private IEnumerator AparecerRaiz()
        {
            grupoRaiz.alpha = 0f;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                grupoRaiz.alpha = Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            grupoRaiz.alpha = 1f;
        }

        private IEnumerator AparecerElementos(Pagina pagina)
        {
            float t = 0f;
            float total = (pagina.elementos.Count - 1) * RetrasoEntreElementos + DuracionAparicion;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < pagina.elementos.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * RetrasoEntreElementos) / DuracionAparicion);
                    pagina.elementos[i].alpha = Mathf.SmoothStep(0f, 1f, k);
                }
                yield return null;
            }
            foreach (var cg in pagina.elementos) cg.alpha = 1f;
        }

        // ------------------------------------------------------------------
        // CONSTRUCCIÓN
        // ------------------------------------------------------------------

        private void Construir(Transform canvas)
        {
            raiz = new GameObject("Tutorial", typeof(RectTransform));
            raiz.transform.SetParent(canvas, false);
            Estirar(raiz.GetComponent<RectTransform>());

            var fondo = raiz.AddComponent<Image>();
            fondo.color = Color.black;          // fondo totalmente negro (y bloquea los clics de abajo)
            grupoRaiz = raiz.AddComponent<CanvasGroup>();

            paginas = new Pagina[3];

            // Parte 1: imagen izquierda + texto derecha
            paginas[0] = ConstruirPagina(true, Imagen(0), "Dos tanques luchando",
                Kicker1, Titulo1, null, Cuerpo1, null, null);

            // Parte 2: imagen izquierda + instrucciones en dos columnas + ejemplo
            paginas[1] = ConstruirPagina(true, Imagen(1), "Un tanque lanzando un misil",
                Kicker2, Titulo2, Subtitulo2, InstruccionesColumnaA, InstruccionesColumnaB, EjemploCodigo);

            // Parte 3: texto izquierda + imagen derecha
            paginas[2] = ConstruirPagina(false, Imagen(2), "Tablero con los tanques personalizados",
                Kicker3, Titulo3, null, Cuerpo3, null, null);

            ConstruirNavegacion();
            raiz.SetActive(false);
        }

        private Sprite Imagen(int i)
        {
            var original = imagenes != null && i < imagenes.Length ? imagenes[i] : null;
            return DifuminarBordes(original, difuminadoBordes);
        }

        // Devuelve una COPIA del sprite con los bordes en transparencia difuminada
        // (efecto viñeta): el alfa baja suavemente hacia cada borde, así la imagen
        // no se ve como un rectángulo. 'fraccion' es qué tan adentro llega el
        // difuminado, como fracción del lado más corto (0 = sin efecto).
        //
        // Se copia pasando por una RenderTexture, así funciona aunque la textura
        // NO tenga activado "Read/Write" en su importación.
        private Sprite DifuminarBordes(Sprite origen, float fraccion)
        {
            if (origen == null || fraccion <= 0f) return origen;

            try
            {
                Texture2D textura = origen.texture;
                Rect zona = origen.textureRect;
                int ancho = Mathf.RoundToInt(zona.width);
                int alto = Mathf.RoundToInt(zona.height);

                var rt = RenderTexture.GetTemporary(textura.width, textura.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(textura, rt);

                var activaPrevia = RenderTexture.active;
                RenderTexture.active = rt;
                var copia = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
                copia.ReadPixels(new Rect(zona.x, zona.y, ancho, alto), 0, 0);
                RenderTexture.active = activaPrevia;
                RenderTexture.ReleaseTemporary(rt);

                var pixeles = copia.GetPixels32();
                float difuminadoPx = Mathf.Max(1f, fraccion * Mathf.Min(ancho, alto));

                for (int y = 0; y < alto; y++)
                {
                    float ay = Suavizar((Mathf.Min(y, alto - 1 - y) + 0.5f) / difuminadoPx);
                    for (int x = 0; x < ancho; x++)
                    {
                        float ax = Suavizar((Mathf.Min(x, ancho - 1 - x) + 0.5f) / difuminadoPx);
                        int i = y * ancho + x;
                        // Producto de ambos ejes: las esquinas quedan redondeadas y suaves.
                        pixeles[i].a = (byte)Mathf.RoundToInt(pixeles[i].a * ax * ay);
                    }
                }

                copia.SetPixels32(pixeles);
                copia.wrapMode = TextureWrapMode.Clamp;
                copia.Apply();

                var sprite = Sprite.Create(copia, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
                recursosCreados.Add(copia);
                recursosCreados.Add(sprite);
                return sprite;
            }
            catch (Exception e)
            {
                Debug.LogWarning("TutorialJuego: no se pudo difuminar los bordes de la imagen, se usa tal cual. " + e.Message);
                return origen;
            }
        }

        private static float Suavizar(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void OnDestroy()
        {
            foreach (var recurso in recursosCreados)
                if (recurso != null) Destroy(recurso);
        }

        private Pagina ConstruirPagina(bool imagenALaIzquierda, Sprite sprite, string descripcionImagen,
            string kicker, string titulo, string subtitulo, string cuerpo, string cuerpo2, string ejemplo)
        {
            var pagina = new Pagina();
            var go = new GameObject("Pagina", typeof(RectTransform));
            go.transform.SetParent(raiz.transform, false);
            Estirar(go.GetComponent<RectTransform>());
            pagina.go = go;

            // Zona útil: deja libre la franja inferior para los botones de navegación.
            const float yMin = 0.17f, yMax = 0.92f;
            Vector2 izqMin = new Vector2(0.05f, yMin), izqMax = new Vector2(0.47f, yMax);
            Vector2 derMin = new Vector2(0.53f, yMin), derMax = new Vector2(0.95f, yMax);
            // En la parte 2 el texto necesita más ancho (dos columnas).
            if (cuerpo2 != null) { derMin = new Vector2(0.50f, yMin); derMax = new Vector2(0.97f, yMax); izqMax = new Vector2(0.46f, yMax); }

            CrearSlotImagen(pagina, go.transform,
                imagenALaIzquierda ? izqMin : derMin, imagenALaIzquierda ? izqMax : derMax,
                sprite, descripcionImagen);

            var columna = NuevoRect("Texto", go.transform,
                imagenALaIzquierda ? derMin : izqMin, imagenALaIzquierda ? derMax : izqMax);

            // Sobretítulo
            var tKicker = CrearTexto(columna, "Kicker", kicker, 22, FontStyle.Bold, TextAnchor.LowerLeft, Rojo);
            Anclar(tKicker.rectTransform, 0f, 0.93f, 1f, 1f);
            Registrar(pagina, tKicker.gameObject);

            // Título "artístico": dorado, con contorno negro y sombra roja
            var tTitulo = CrearTexto(columna, "Titulo", titulo, 64, FontStyle.Bold, TextAnchor.MiddleLeft, Oro);
            tTitulo.resizeTextForBestFit = true;
            tTitulo.resizeTextMinSize = 28;
            tTitulo.resizeTextMaxSize = 64;
            var sombra = tTitulo.gameObject.AddComponent<Shadow>();
            sombra.effectColor = RojoOscuro;
            sombra.effectDistance = new Vector2(5f, -5f);
            var contorno = tTitulo.gameObject.AddComponent<Outline>();
            contorno.effectColor = new Color(0f, 0f, 0f, 0.95f);
            contorno.effectDistance = new Vector2(2f, -2f);
            Anclar(tTitulo.rectTransform, 0f, 0.77f, 1f, 0.93f);
            Registrar(pagina, tTitulo.gameObject);

            // Línea decorativa (barra roja + filete dorado)
            var linea = NuevoRect("Linea", columna, new Vector2(0f, 0.76f), new Vector2(1f, 0.76f));
            var barra = new GameObject("Barra", typeof(RectTransform));
            barra.transform.SetParent(linea, false);
            var barraImg = barra.AddComponent<Image>();
            barraImg.color = Rojo;
            barraImg.raycastTarget = false;
            var barraRect = barra.GetComponent<RectTransform>();
            barraRect.anchorMin = barraRect.anchorMax = new Vector2(0f, 0.5f);
            barraRect.pivot = new Vector2(0f, 0.5f);
            barraRect.sizeDelta = new Vector2(110f, 6f);
            barraRect.anchoredPosition = Vector2.zero;
            var filete = new GameObject("Filete", typeof(RectTransform));
            filete.transform.SetParent(linea, false);
            var fileteImg = filete.AddComponent<Image>();
            fileteImg.color = new Color(Oro.r, Oro.g, Oro.b, 0.8f);
            fileteImg.raycastTarget = false;
            var fileteRect = filete.GetComponent<RectTransform>();
            fileteRect.anchorMin = new Vector2(0f, 0.5f);
            fileteRect.anchorMax = new Vector2(1f, 0.5f);
            fileteRect.offsetMin = new Vector2(120f, -1f);
            fileteRect.offsetMax = new Vector2(0f, 1f);
            Registrar(pagina, linea.gameObject);

            float topeCuerpo = 0.73f;
            if (!string.IsNullOrEmpty(subtitulo))
            {
                var tSub = CrearTexto(columna, "Subtitulo", subtitulo, 20, FontStyle.Italic, TextAnchor.MiddleLeft,
                    new Color(0.75f, 0.75f, 0.75f, 1f));
                tSub.resizeTextForBestFit = true;
                tSub.resizeTextMinSize = 12;
                tSub.resizeTextMaxSize = 20;
                Anclar(tSub.rectTransform, 0f, 0.675f, 1f, 0.745f);
                Registrar(pagina, tSub.gameObject);
                topeCuerpo = 0.67f;
            }

            float baseCuerpo = ejemplo != null ? 0.30f : 0f;

            if (cuerpo2 == null)
            {
                var tCuerpo = CrearCuerpo(columna, "Cuerpo", cuerpo, 28);
                Anclar(tCuerpo.rectTransform, 0f, baseCuerpo, 1f, topeCuerpo);
                Registrar(pagina, tCuerpo.gameObject);
            }
            else
            {
                var tA = CrearCuerpo(columna, "CuerpoA", cuerpo, 20);
                Anclar(tA.rectTransform, 0f, baseCuerpo, 0.485f, topeCuerpo);
                Registrar(pagina, tA.gameObject);

                var tB = CrearCuerpo(columna, "CuerpoB", cuerpo2, 20);
                Anclar(tB.rectTransform, 0.515f, baseCuerpo, 1f, topeCuerpo);
                Registrar(pagina, tB.gameObject);
            }

            if (ejemplo != null)
                CrearCajaEjemplo(pagina, columna, ejemplo);

            return pagina;
        }

        private Text CrearCuerpo(Transform padre, string nombre, string contenido, int tamanoMax)
        {
            var t = CrearTexto(padre, nombre, contenido, tamanoMax, FontStyle.Normal, TextAnchor.UpperLeft, ColorTexto);
            t.lineSpacing = 1.1f;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = tamanoMax;
            return t;
        }

        private void CrearCajaEjemplo(Pagina pagina, Transform columna, string codigo)
        {
            var caja = NuevoRect("Ejemplo", columna, new Vector2(0f, 0f), new Vector2(1f, 0.27f));
            var marco = caja.gameObject.AddComponent<Image>();
            marco.color = new Color(Oro.r, Oro.g, Oro.b, 0.5f);
            marco.raycastTarget = false;
            Registrar(pagina, caja.gameObject);

            var interior = NuevoRect("Interior", caja, Vector2.zero, Vector2.one);
            interior.offsetMin = new Vector2(2f, 2f);
            interior.offsetMax = new Vector2(-2f, -2f);
            var fondo = interior.gameObject.AddComponent<Image>();
            fondo.color = new Color(0.04f, 0.04f, 0.05f, 0.95f);
            fondo.raycastTarget = false;

            var etiqueta = CrearTexto(interior, "Etiqueta", "EJEMPLO", 16, FontStyle.Bold, TextAnchor.UpperLeft, Rojo);
            Anclar(etiqueta.rectTransform, 0f, 0f, 1f, 1f);
            etiqueta.rectTransform.offsetMin = new Vector2(14f, 0f);
            etiqueta.rectTransform.offsetMax = new Vector2(-14f, -8f);

            var texto = CrearTexto(interior, "Codigo", codigo, 20, FontStyle.Normal, TextAnchor.MiddleLeft, ColorTexto);
            texto.resizeTextForBestFit = true;
            texto.resizeTextMinSize = 12;
            texto.resizeTextMaxSize = 20;
            Anclar(texto.rectTransform, 0f, 0f, 1f, 1f);
            texto.rectTransform.offsetMin = new Vector2(18f, 8f);
            texto.rectTransform.offsetMax = new Vector2(-14f, -30f);
        }

        // Hueco para la imagen que diseñarás: si hay sprite se muestra tal cual
        // (conservando su proporción); si no, un marco con la descripción.
        private void CrearSlotImagen(Pagina pagina, Transform padre, Vector2 aMin, Vector2 aMax,
            Sprite sprite, string descripcion)
        {
            var slot = NuevoRect("SlotImagen", padre, aMin, aMax);
            Registrar(pagina, slot.gameObject);

            if (sprite != null)
            {
                var img = slot.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                return;
            }

            var marco = slot.gameObject.AddComponent<Image>();
            marco.color = new Color(Oro.r, Oro.g, Oro.b, 0.55f);
            marco.raycastTarget = false;

            var interior = NuevoRect("Interior", slot, Vector2.zero, Vector2.one);
            interior.offsetMin = new Vector2(3f, 3f);
            interior.offsetMax = new Vector2(-3f, -3f);
            var fondo = interior.gameObject.AddComponent<Image>();
            fondo.color = new Color(0.06f, 0.06f, 0.06f, 1f);
            fondo.raycastTarget = false;

            var texto = CrearTexto(interior, "Texto", "ESPACIO PARA IMAGEN\n<size=20>" + descripcion + "</size>",
                28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.45f, 0.45f, 0.45f, 1f));
            texto.lineSpacing = 1.3f;
        }

        private void ConstruirNavegacion()
        {
            // Anterior (izquierda del centro) y Siguiente (derecha del centro)
            botonAnterior = CrearBoton("BotonAnterior", "ANTERIOR", new Vector2(-190f, 70f), Anterior, out _);
            botonSiguiente = CrearBoton("BotonSiguiente", "SIGUIENTE", new Vector2(190f, 70f), Siguiente, out textoSiguiente);

            // Puntos indicadores de página
            puntos = new Image[paginas.Length];
            for (int i = 0; i < puntos.Length; i++)
            {
                var p = new GameObject("Punto" + (i + 1), typeof(RectTransform));
                p.transform.SetParent(raiz.transform, false);
                var img = p.AddComponent<Image>();
                img.color = ColorPunto;
                img.raycastTarget = false;
                var r = p.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(14f, 14f);
                r.anchoredPosition = new Vector2((i - (puntos.Length - 1) * 0.5f) * 30f, 70f);
                puntos[i] = img;
            }

            // Omitir (arriba a la derecha)
            var omitir = new GameObject("BotonOmitir", typeof(RectTransform));
            omitir.transform.SetParent(raiz.transform, false);
            var omitirImg = omitir.AddComponent<Image>();
            omitirImg.color = new Color(1f, 1f, 1f, 0f);
            var omitirBtn = omitir.AddComponent<Button>();
            var colores = omitirBtn.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = Oro;
            omitirBtn.colors = colores;
            omitirBtn.targetGraphic = omitirImg;
            omitirBtn.onClick.AddListener(() => { if (alClick != null) alClick(); Cerrar(); });
            var omitirRect = omitir.GetComponent<RectTransform>();
            omitirRect.anchorMin = omitirRect.anchorMax = new Vector2(1f, 1f);
            omitirRect.pivot = new Vector2(1f, 1f);
            omitirRect.sizeDelta = new Vector2(170f, 48f);
            omitirRect.anchoredPosition = new Vector2(-30f, -24f);
            var omitirTexto = CrearTexto(omitirRect, "Texto", "OMITIR", 20, FontStyle.Bold, TextAnchor.MiddleRight,
                new Color(0.65f, 0.65f, 0.65f, 1f));
            omitirTexto.raycastTarget = false;
        }

        private Button CrearBoton(string nombre, string texto, Vector2 posicion,
            UnityEngine.Events.UnityAction accion, out Text textoUI)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(raiz.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.55f, 0.08f, 0.08f, 1f);

            var borde = go.AddComponent<Outline>();
            borde.effectColor = Oro;
            borde.effectDistance = new Vector2(2f, -2f);

            var boton = go.AddComponent<Button>();
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1f, 0.9f, 0.55f);
            colores.pressedColor = new Color(0.6f, 0.45f, 0.15f);
            boton.colors = colores;
            boton.targetGraphic = img;
            boton.onClick.AddListener(accion);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 58f);
            rect.anchoredPosition = posicion;

            textoUI = CrearTexto(rect, "Texto", texto, 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            return boton;
        }

        // ------------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------------

        private Text CrearTexto(Transform padre, string nombre, string contenido, int tamano,
            FontStyle estilo, TextAnchor alineacion, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.font = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = contenido;
            t.fontSize = tamano;
            t.fontStyle = estilo;
            t.alignment = alineacion;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Estirar(t.rectTransform);
            return t;
        }

        private static void Registrar(Pagina pagina, GameObject go)
        {
            pagina.elementos.Add(go.AddComponent<CanvasGroup>());
        }

        private static RectTransform NuevoRect(string nombre, Transform padre, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        private static void Anclar(RectTransform r, float xMin, float yMin, float xMax, float yMax)
        {
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
        }

        private static void Estirar(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
    }
}