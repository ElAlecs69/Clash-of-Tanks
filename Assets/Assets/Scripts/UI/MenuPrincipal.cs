using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TanksGame.UI
{
    // Pantalla de inicio, generada por código igual que GameplayUI.
    // Uso: crea una escena nueva (ej. "MenuPrincipal"), agrega un GameObject vacío
    // y este componente, y agrega tanto esta escena como la del juego
    // ("SampleScene" u otra) en File > Build Profiles > Scene List.
    public class MenuPrincipal : MonoBehaviour
    {
        [Header("Escena a cargar al iniciar partida")]
        public string nombreEscenaJuego = "SampleScene";

        [Header("Textos")]
        public string tituloJuego = "CLASH OF TANKS";

        [Header("Logo (opcional). Si lo asignas, reemplaza el texto del título por esta imagen.")]
        public Sprite spriteTituloJuego;
        public Vector2 tamanoLogo = new Vector2(700, 350);

        [Header("Botones del menú (opcional). Si lo asignas, reemplaza la tarjeta oscura generada por código.")]
        public Sprite spriteFondoBotonMenu;

        [Header("Animación de entrada del título/logo")]
        public float duracionAnimacionTitulo = 0.7f;
        [Tooltip("Escala inicial desde la que 'crece' el título/logo hasta llegar a 1.")]
        public float escalaInicialTitulo = 0.6f;

        [Header("Imagen de fondo de Opciones (opcional)")]
        [Tooltip("Si se asigna, se usa como fondo a pantalla completa del panel de Opciones en vez del color plano.")]
        public Sprite fondoOpciones;

        [Header("Imagen de fondo de Créditos (opcional)")]
        [Tooltip("Si se asigna, se usa como fondo de la ventana de Créditos en vez del color plano + degradado rojo.")]
        public Sprite fondoCreditos;

        [Header("Sonidos")]
        [Tooltip("Se reproduce al presionar cualquier botón del menú (Iniciar Juego, Opciones, Créditos, Salir, Volver).")]
        public AudioClip sonidoClick;
        [Tooltip("Se reproduce al modificar algo dentro de Opciones (resolución, pantalla completa, calidad).")]
        public AudioClip sonidoCambio;

        [Header("Música de fondo (solo esta pantalla)")]
        [Tooltip("Se reproduce en bucle mientras estás en el menú principal. Se detiene sola al cargar la escena del juego.")]
        public AudioClip musicaFondo;
        [Range(0f, 1f)]
        public float volumenMusica = 0.5f;

        [Header("Video de fondo")]
        [Tooltip("Si se deja vacÃ­o, se carga Resources/video_inicio automÃ¡ticamente.")]
        public VideoClip videoFondoInicio;
        [Range(0f, 1f)] public float opacidadDegradadoVideo = 0.62f;

        [Header("Créditos")]
        [TextArea(4, 10)]
        public string textoCreditos =
            "CLASH OF TANKS\n\nDesarrollado por: GodAlecs \n\nHecho con Unity";

        // Tamaño de la ventana grande de Créditos, estilo pantalla de ajustes.
        private const float AnchoPanelGrande = 1200f;
        private const float AltoPanelGrande = 760f;

        // Paleta del diseño (inspirado en menús estilo RDR2: negro casi puro con
        // acentos rojos, degradado diagonal en las esquinas y texto gris/blanco).
        private static readonly Color ColorFondoPanel = new Color(0.03f, 0.02f, 0.02f, 1f);
        private static readonly Color ColorAcentoRojo = new Color(0.75f, 0.10f, 0.10f, 1f);
        private static readonly Color ColorAcentoHover = new Color(0.87f, 0.68f, 0.20f, 1f);
        private static readonly Color ColorTextoActivo = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color ColorTextoInactivo = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color ColorLinea = new Color(0.55f, 0.55f, 0.55f, 0.4f);

        private static readonly string[] NombresCalidad = { "BAJO", "MEDIO", "ALTO" };

        private GameObject panelOpciones;
        private GameObject panelCreditos;
        private AudioSource audioSource;
        private AudioSource audioSourceMusica;
        private VideoPlayer reproductorVideo;
        private RenderTexture texturaVideo;

        // Estado de las opciones de video (índices dentro de las listas correspondientes).
        private List<Resolution> resolucionesDisponibles;
        private int indiceResolucion;
        private int indiceCalidad; // 0 = Bajo, 1 = Medio, 2 = Alto (independiente de cuántos niveles reales tenga el proyecto).
        private bool estadoPantallaCompleta;
        private Text textoResolucion;
        private Text textoCalidad;
        private Text textoPantallaCompleta;

        private void Start()
        {
            // Si la escena no tiene ninguna Cámara (y por lo tanto ningún AudioListener),
            // los sonidos no se reproducen. Nos aseguramos de que exista al menos uno.
            if (FindObjectOfType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // AudioSource separado (en bucle) para la música, distinto del que se usa
            // para los sonidos de click/cambio (esos usan PlayOneShot y no deben
            // interrumpir la música). Al no usar DontDestroyOnLoad, la música se
            // detiene sola apenas se carga otra escena (ej. al iniciar la partida).
            audioSourceMusica = gameObject.AddComponent<AudioSource>();
            audioSourceMusica.playOnAwake = false;
            audioSourceMusica.loop = true;
            audioSourceMusica.volume = volumenMusica;
            if (musicaFondo != null)
            {
                audioSourceMusica.clip = musicaFondo;
                audioSourceMusica.Play();
            }

            ConstruirUI();
        }

        private void Update()
        {
            // Permite cerrar los paneles grandes con ESC, como en el menú de referencia.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (panelOpciones != null && panelOpciones.activeSelf) OnCerrarOpciones();
                else if (panelCreditos != null && panelCreditos.activeSelf) OnCerrarCreditos();
            }
        }

        private void ReproducirClick()
        {
            if (sonidoClick != null) audioSource.PlayOneShot(sonidoClick);
        }

        private void ReproducirCambio()
        {
            if (sonidoCambio != null) audioSource.PlayOneShot(sonidoCambio);
        }

        private void ConstruirUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("Canvas_MenuPrincipal");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            // --- Video de fondo a pantalla completa ---
            var fondoGo = new GameObject("Fondo");
            fondoGo.transform.SetParent(canvasGo.transform, false);
            var fondoVideo = fondoGo.AddComponent<RawImage>();
            var fondoRect = fondoGo.GetComponent<RectTransform>();
            fondoRect.anchorMin = Vector2.zero;
            fondoRect.anchorMax = Vector2.one;
            fondoRect.offsetMin = Vector2.zero;
            fondoRect.offsetMax = Vector2.zero;

            PrepararVideoFondo(fondoVideo);

            // Capa de legibilidad: transparente arriba y mÃ¡s oscura hacia abajo.
            var degradadoGo = new GameObject("DegradadoVideo");
            degradadoGo.transform.SetParent(canvasGo.transform, false);
            var degradado = degradadoGo.AddComponent<Image>();
            degradado.sprite = ObtenerSpriteDegradadoVideo(opacidadDegradadoVideo);
            degradado.raycastTarget = false;
            var degradadoRect = degradadoGo.GetComponent<RectTransform>();
            degradadoRect.anchorMin = Vector2.zero;
            degradadoRect.anchorMax = Vector2.one;
            degradadoRect.offsetMin = Vector2.zero;
            degradadoRect.offsetMax = Vector2.zero;

            // --- Título ---
            var tituloGo = new GameObject("Titulo");
            tituloGo.transform.SetParent(canvasGo.transform, false);
            var tituloRect = tituloGo.AddComponent<RectTransform>();
            tituloRect.anchorMin = new Vector2(0.5f, 1f);
            tituloRect.anchorMax = new Vector2(0.5f, 1f);
            tituloRect.pivot = new Vector2(0.5f, 1f);
            tituloRect.anchoredPosition = new Vector2(0, -50);

            if (spriteTituloJuego != null)
            {
                tituloRect.sizeDelta = tamanoLogo;
                var tituloImagen = tituloGo.AddComponent<Image>();
                tituloImagen.sprite = spriteTituloJuego;
                tituloImagen.preserveAspect = true;
            }
            else
            {
                tituloRect.sizeDelta = new Vector2(900, 120);
                var tituloTexto = tituloGo.AddComponent<Text>();
                tituloTexto.text = tituloJuego;
                tituloTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tituloTexto.fontSize = 64;
                tituloTexto.fontStyle = FontStyle.Bold;
                tituloTexto.alignment = TextAnchor.MiddleCenter;
                tituloTexto.color = new Color(0.92f, 0.92f, 0.95f);
            }

            var tituloCanvasGroup = tituloGo.AddComponent<CanvasGroup>();
            StartCoroutine(AnimarFadeInZoom(tituloRect, tituloCanvasGroup, duracionAnimacionTitulo, escalaInicialTitulo));

            // --- Lista de botones, apilados y centrados (4 opciones) ---
            var listaGo = new GameObject("Botones");
            listaGo.transform.SetParent(canvasGo.transform, false);
            var listaRect = listaGo.AddComponent<RectTransform>();
            listaRect.anchorMin = new Vector2(0.5f, 0.5f);
            listaRect.anchorMax = new Vector2(0.5f, 0.5f);
            listaRect.pivot = new Vector2(0.5f, 0.5f);
            listaRect.sizeDelta = new Vector2(440, 350);
            listaRect.anchoredPosition = new Vector2(0, -230);

            CrearBotonDeMenu(listaRect, "INICIAR JUEGO", new Vector2(0, 135), OnIniciarJuego);
            CrearBotonDeMenu(listaRect, "OPCIONES", new Vector2(0, 45), OnOpciones);
            CrearBotonDeMenu(listaRect, "CRÉDITOS", new Vector2(0, -45), OnCreditos);
            CrearBotonDeMenu(listaRect, "SALIR", new Vector2(0, -135), OnSalir);

            ConstruirPanelOpciones(canvasGo.transform);
            ConstruirPanelCreditos(canvasGo.transform);
        }

        private void PrepararVideoFondo(RawImage destino)
        {
            var clip = videoFondoInicio != null
                ? videoFondoInicio
                : Resources.Load<VideoClip>("video_inicio");
            if (clip == null)
            {
                destino.color = new Color(0.03f, 0.03f, 0.05f);
                return;
            }

            texturaVideo = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            reproductorVideo = gameObject.AddComponent<VideoPlayer>();
            reproductorVideo.playOnAwake = false;
            reproductorVideo.isLooping = true;
            reproductorVideo.audioOutputMode = VideoAudioOutputMode.None;
            reproductorVideo.source = VideoSource.VideoClip;
            reproductorVideo.clip = clip;
            reproductorVideo.renderMode = VideoRenderMode.RenderTexture;
            reproductorVideo.targetTexture = texturaVideo;
            reproductorVideo.prepareCompleted += _ => reproductorVideo.Play();
            destino.texture = texturaVideo;
            reproductorVideo.Prepare();
        }

        private static Sprite ObtenerSpriteDegradadoVideo(float opacidad)
        {
            const int alto = 128;
            var textura = new Texture2D(1, alto, TextureFormat.RGBA32, false);
            for (int y = 0; y < alto; y++)
            {
                float t = y / (float)(alto - 1);
                float alfa = Mathf.Lerp(opacidad, opacidad * 0.22f, t);
                textura.SetPixel(0, y, new Color(0f, 0f, 0f, alfa));
            }
            textura.Apply();
            return Sprite.Create(textura, new Rect(0, 0, 1, alto), new Vector2(0.5f, 0.5f));
        }

        private IEnumerator AnimarFadeInZoom(RectTransform rect, CanvasGroup canvasGroup, float duracion, float escalaInicial)
        {
            canvasGroup.alpha = 0f;
            rect.localScale = Vector3.one * escalaInicial;

            float tiempoTranscurrido = 0f;
            while (tiempoTranscurrido < duracion)
            {
                tiempoTranscurrido += Time.deltaTime;
                float t = Mathf.Clamp01(tiempoTranscurrido / duracion);
                float suavizado = Mathf.SmoothStep(0f, 1f, t);

                canvasGroup.alpha = suavizado;
                rect.localScale = Vector3.one * Mathf.Lerp(escalaInicial, 1f, suavizado);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            rect.localScale = Vector3.one;
        }

        // Botón "épico": marco dorado fino detrás de una tarjeta oscura, con una línea
        // decorativa bajo el texto. Se usa SOLO para los 4 botones de la pantalla de inicio.
        private void CrearBotonDeMenu(Transform padre, string texto, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            const float ancho = 420f, alto = 74f, grosorMarco = 3f;

            var marcoGo = new GameObject($"Marco_{texto}");
            marcoGo.transform.SetParent(padre, false);
            var marcoImagen = marcoGo.AddComponent<Image>();
            marcoImagen.color = new Color(0.75f, 0.6f, 0.2f, 0.9f);
            var marcoRect = marcoGo.GetComponent<RectTransform>();
            marcoRect.anchorMin = new Vector2(0.5f, 0.5f);
            marcoRect.anchorMax = new Vector2(0.5f, 0.5f);
            marcoRect.pivot = new Vector2(0.5f, 0.5f);
            marcoRect.sizeDelta = new Vector2(ancho + grosorMarco * 2, alto + grosorMarco * 2);
            marcoRect.anchoredPosition = posicion;

            var botonGo = new GameObject($"Boton_{texto}");
            botonGo.transform.SetParent(padre, false);

            var imagen = botonGo.AddComponent<Image>();
            if (spriteFondoBotonMenu != null)
            {
                imagen.sprite = spriteFondoBotonMenu;
                imagen.type = Image.Type.Sliced;
                imagen.color = Color.white;
            }
            else
            {
                imagen.color = new Color(0.08f, 0.06f, 0.06f, 0.92f);
            }

            var boton = botonGo.AddComponent<Button>();
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1f, 0.9f, 0.55f);
            colores.pressedColor = new Color(0.6f, 0.45f, 0.15f);
            boton.colors = colores;
            boton.targetGraphic = imagen;
            boton.onClick.AddListener(ReproducirClick);
            boton.onClick.AddListener(accion);

            var rect = botonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ancho, alto);
            rect.anchoredPosition = posicion;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(botonGo.transform, false);
            var textoUI = textoGo.AddComponent<Text>();
            textoUI.text = texto;
            textoUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoUI.fontSize = 30;
            textoUI.fontStyle = FontStyle.Bold;
            textoUI.alignment = TextAnchor.MiddleCenter;
            textoUI.color = new Color(0.95f, 0.9f, 0.75f);
            textoUI.raycastTarget = false;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = new Vector2(0, 6);
            textoRect.offsetMax = Vector2.zero;

            var lineaGo = new GameObject("LineaDecorativa");
            lineaGo.transform.SetParent(botonGo.transform, false);
            var lineaImagen = lineaGo.AddComponent<Image>();
            lineaImagen.color = new Color(0.75f, 0.6f, 0.2f, 0.8f);
            lineaImagen.raycastTarget = false;
            var lineaRect = lineaGo.GetComponent<RectTransform>();
            lineaRect.anchorMin = new Vector2(0.5f, 0f);
            lineaRect.anchorMax = new Vector2(0.5f, 0f);
            lineaRect.pivot = new Vector2(0.5f, 0f);
            lineaRect.sizeDelta = new Vector2(ancho * 0.55f, 2f);
            lineaRect.anchoredPosition = new Vector2(0, 10);
        }

        // ------------------------------------------------------------------
        // PANEL DE OPCIONES — versión simple (sin categorías/estadísticas), con
        // fondo a pantalla completa (imagen opcional).
        // ------------------------------------------------------------------

        private void ConstruirPanelOpciones(Transform padre)
        {
            panelOpciones = new GameObject("PanelOpciones");
            panelOpciones.transform.SetParent(padre, false);

            var fondo = panelOpciones.AddComponent<Image>();
            var rectFondo = panelOpciones.GetComponent<RectTransform>();
            rectFondo.anchorMin = Vector2.zero;
            rectFondo.anchorMax = Vector2.one;
            rectFondo.offsetMin = Vector2.zero;
            rectFondo.offsetMax = Vector2.zero;

            if (fondoOpciones != null)
            {
                fondo.sprite = fondoOpciones;
                fondo.type = Image.Type.Simple;
                fondo.preserveAspect = false;
                fondo.color = Color.white;
            }
            else
            {
                fondo.color = ColorFondoPanel;
            }

            var contenedorGo = new GameObject("ContenidoOpciones");
            contenedorGo.transform.SetParent(panelOpciones.transform, false);
            var contenedorRect = contenedorGo.AddComponent<RectTransform>();
            contenedorRect.anchorMin = new Vector2(0.5f, 0.5f);
            contenedorRect.anchorMax = new Vector2(0.5f, 0.5f);
            contenedorRect.pivot = new Vector2(0.5f, 0.5f);
            contenedorRect.sizeDelta = new Vector2(700, 640);
            contenedorRect.anchoredPosition = new Vector2(0, 70);

            CrearTituloDePanel(contenedorRect, "OPCIONES", new Vector2(0, -40));

            CrearEtiquetaSeccion(contenedorRect, "AUDIO", new Vector2(0, -110));
            CrearSlider(contenedorRect, "Volumen general", new Vector2(0, -170),
                AudioListener.volume, valor => AudioListener.volume = valor);

            CrearEtiquetaSeccion(contenedorRect, "VIDEO", new Vector2(0, -240));

            resolucionesDisponibles = Screen.resolutions
                .Select(r => new Resolution { width = r.width, height = r.height })
                .GroupBy(r => (r.width, r.height))
                .Select(g => g.First())
                .OrderBy(r => r.width * r.height)
                .ToList();

            indiceResolucion = Mathf.Max(0, resolucionesDisponibles.FindIndex(
                r => r.width == Screen.width && r.height == Screen.height));

            textoResolucion = CrearSelectorCiclo(contenedorRect, "Resolución", new Vector2(0, -300),
                TextoResolucionActual(), CambiarResolucion);

            estadoPantallaCompleta = Screen.fullScreen;
            textoPantallaCompleta = CrearSelectorCiclo(contenedorRect, "Pantalla completa", new Vector2(0, -360),
                estadoPantallaCompleta ? "SÍ" : "NO", _ => CambiarPantallaCompleta());

            int totalNivelesReales = Mathf.Max(1, QualitySettings.names.Length);
            int nivelRealActual = QualitySettings.GetQualityLevel();
            indiceCalidad = totalNivelesReales <= 1
                ? 1
                : Mathf.Clamp(Mathf.RoundToInt(nivelRealActual * 2f / (totalNivelesReales - 1)), 0, 2);

            textoCalidad = CrearSelectorCiclo(contenedorRect, "Calidad gráfica", new Vector2(0, -420),
                NombresCalidad[indiceCalidad], CambiarCalidad);

            CrearBotonTextoSimple(contenedorRect, "VOLVER", new Vector2(0, -510), OnCerrarOpciones);

            panelOpciones.SetActive(false);
        }

        private string TextoResolucionActual()
        {
            var r = resolucionesDisponibles[indiceResolucion];
            return $"{r.width} x {r.height}";
        }

        private void CambiarResolucion(int direccion)
        {
            int total = resolucionesDisponibles.Count;
            indiceResolucion = (indiceResolucion + direccion + total) % total;
            var r = resolucionesDisponibles[indiceResolucion];
            textoResolucion.text = TextoResolucionActual();
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        }

        private void CambiarPantallaCompleta()
        {
            estadoPantallaCompleta = !estadoPantallaCompleta;
            textoPantallaCompleta.text = estadoPantallaCompleta ? "SÍ" : "NO";
            Screen.fullScreen = estadoPantallaCompleta;
        }

        private void CambiarCalidad(int direccion)
        {
            indiceCalidad = (indiceCalidad + direccion + 3) % 3;
            textoCalidad.text = NombresCalidad[indiceCalidad];

            int totalNivelesReales = Mathf.Max(1, QualitySettings.names.Length);
            int nivelReal = totalNivelesReales <= 1
                ? 0
                : Mathf.RoundToInt(indiceCalidad * (totalNivelesReales - 1) / 2f);

            QualitySettings.SetQualityLevel(nivelReal, applyExpensiveChanges: false);
        }

        private void OnOpciones() => panelOpciones.SetActive(true);
        private void OnCerrarOpciones() => panelOpciones.SetActive(false);

        // ------------------------------------------------------------------
        // PANEL DE CRÉDITOS (sin cambios: ventana grande con degradado rojo).
        // ------------------------------------------------------------------

        private void ConstruirPanelCreditos(Transform padre)
        {
            panelCreditos = CrearVentanaGrande(padre, "PanelCreditos", "CRÉDITOS", fondoCreditos);

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(panelCreditos.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.text = textoCreditos;
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 24;
            texto.alignment = TextAnchor.UpperLeft;
            texto.color = ColorTextoActivo;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = new Vector2(0f, 1f);
            textoRect.anchorMax = new Vector2(0f, 1f);
            textoRect.pivot = new Vector2(0f, 1f);
            textoRect.sizeDelta = new Vector2(1000, 400);
            textoRect.anchoredPosition = new Vector2(50, -150);

            panelCreditos.SetActive(false);
        }

        private void OnCreditos() => panelCreditos.SetActive(true);
        private void OnCerrarCreditos() => panelCreditos.SetActive(false);

        // ------------------------------------------------------------------
        // HELPERS DE UI — panel simple de Opciones.
        // ------------------------------------------------------------------

        private void CrearTituloDePanel(Transform padre, string texto, Vector2 posicion)
        {
            var go = new GameObject("TituloPanel");
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.text = texto;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 32;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = ColorAcentoHover;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(500, 50);
            rect.anchoredPosition = posicion;
        }

        private void CrearEtiquetaSeccion(Transform padre, string texto, Vector2 posicion)
        {
            var go = new GameObject($"Seccion_{texto}");
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.text = texto;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 16;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = ColorTextoInactivo;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(480, 30);
            rect.anchoredPosition = posicion;
        }

        // Botón de texto simple (sin marco dorado), usado para "VOLVER" dentro de
        // Opciones — el marco dorado se reserva a propósito para los 4 botones del
        // menú principal.
        private void CrearBotonTextoSimple(Transform padre, string texto, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject($"Boton_{texto}");
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();
            imagen.color = new Color(1f, 1f, 1f, 0f);

            var boton = go.AddComponent<Button>();
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = ColorAcentoHover;
            boton.colors = colores;
            boton.targetGraphic = imagen;
            boton.onClick.AddListener(ReproducirClick);
            boton.onClick.AddListener(accion);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(360, 60);
            rect.anchoredPosition = posicion;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var textoUI = textoGo.AddComponent<Text>();
            textoUI.text = texto;
            textoUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoUI.fontSize = 24;
            textoUI.fontStyle = FontStyle.Bold;
            textoUI.alignment = TextAnchor.MiddleCenter;
            textoUI.color = ColorTextoActivo;
            textoUI.raycastTarget = false;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;
        }

        // Slider nativo de Unity, con etiqueta a la izquierda.
        private void CrearSlider(Transform padre, string etiqueta, Vector2 posicion, float valorInicial,
            UnityEngine.Events.UnityAction<float> alCambiar)
        {
            var filaGo = new GameObject($"Fila_{etiqueta}");
            filaGo.transform.SetParent(padre, false);
            var filaRect = filaGo.AddComponent<RectTransform>();
            filaRect.anchorMin = new Vector2(0.5f, 1f);
            filaRect.anchorMax = new Vector2(0.5f, 1f);
            filaRect.pivot = new Vector2(0.5f, 1f);
            filaRect.sizeDelta = new Vector2(480, 40);
            filaRect.anchoredPosition = posicion;

            CrearEtiquetaDeFila(filaRect, etiqueta);

            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(filaRect, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(1f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(1f, 0.5f);
            sliderRect.sizeDelta = new Vector2(220, 20);
            sliderRect.anchoredPosition = Vector2.zero;
            var slider = sliderGo.AddComponent<Slider>();

            var fondoGo = new GameObject("Fondo");
            fondoGo.transform.SetParent(sliderGo.transform, false);
            var fondoImg = fondoGo.AddComponent<Image>();
            fondoImg.color = new Color(1f, 1f, 1f, 0.15f);
            var fondoRect2 = fondoGo.GetComponent<RectTransform>();
            fondoRect2.anchorMin = Vector2.zero;
            fondoRect2.anchorMax = Vector2.one;
            fondoRect2.offsetMin = Vector2.zero;
            fondoRect2.offsetMax = Vector2.zero;

            var areaGo = new GameObject("Fill Area");
            areaGo.transform.SetParent(sliderGo.transform, false);
            var areaRect = areaGo.AddComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0f, 0.25f);
            areaRect.anchorMax = new Vector2(1f, 0.75f);
            areaRect.offsetMin = new Vector2(5, 0);
            areaRect.offsetMax = new Vector2(-5, 0);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(areaGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = ColorAcentoRojo;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16, 24);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = valorInicial;
            slider.onValueChanged.AddListener(alCambiar);
        }

        // Selector "‹ valor ›": etiqueta + flecha izquierda + texto de valor + flecha derecha.
        private Text CrearSelectorCiclo(Transform padre, string etiqueta, Vector2 posicion, string valorInicial,
            UnityEngine.Events.UnityAction<int> alCambiar)
        {
            var filaGo = new GameObject($"Fila_{etiqueta}");
            filaGo.transform.SetParent(padre, false);
            var filaRect = filaGo.AddComponent<RectTransform>();
            filaRect.anchorMin = new Vector2(0.5f, 1f);
            filaRect.anchorMax = new Vector2(0.5f, 1f);
            filaRect.pivot = new Vector2(0.5f, 1f);
            filaRect.sizeDelta = new Vector2(480, 40);
            filaRect.anchoredPosition = posicion;

            CrearEtiquetaDeFila(filaRect, etiqueta);

            var valorGo = new GameObject("Valor");
            valorGo.transform.SetParent(filaRect, false);
            var valorTexto = valorGo.AddComponent<Text>();
            valorTexto.text = valorInicial;
            valorTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valorTexto.fontSize = 18;
            valorTexto.alignment = TextAnchor.MiddleCenter;
            valorTexto.color = ColorAcentoHover;
            valorTexto.raycastTarget = false;
            var valorRect = valorGo.GetComponent<RectTransform>();
            valorRect.anchorMin = new Vector2(1f, 0.5f);
            valorRect.anchorMax = new Vector2(1f, 0.5f);
            valorRect.pivot = new Vector2(1f, 0.5f);
            valorRect.sizeDelta = new Vector2(140, 30);
            valorRect.anchoredPosition = new Vector2(-40, 0);

            CrearFlechaSelector(filaRect, "<", new Vector2(-180, 0), () => alCambiar(-1));
            CrearFlechaSelector(filaRect, ">", new Vector2(0, 0), () => alCambiar(1));

            return valorTexto;
        }

        private void CrearEtiquetaDeFila(Transform padre, string texto)
        {
            var go = new GameObject("Etiqueta");
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.text = texto;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 20;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = ColorTextoActivo;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(200, 30);
            rect.anchoredPosition = Vector2.zero;
        }

        // Flecha "‹"/"›" del selector: reproduce el sonido de cambio al presionarla.
        private void CrearFlechaSelector(Transform padre, string simbolo, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject($"Flecha_{simbolo}_{posicion.x}");
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();
            imagen.color = new Color(1f, 1f, 1f, 0f);
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = imagen;
            var colores = boton.colors;
            colores.highlightedColor = ColorAcentoHover;
            boton.colors = colores;
            boton.onClick.AddListener(ReproducirCambio);
            boton.onClick.AddListener(accion);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(30, 30);
            rect.anchoredPosition = posicion;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var textoUI = textoGo.AddComponent<Text>();
            textoUI.text = simbolo;
            textoUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoUI.fontSize = 22;
            textoUI.fontStyle = FontStyle.Bold;
            textoUI.alignment = TextAnchor.MiddleCenter;
            textoUI.color = ColorTextoActivo;
            textoUI.raycastTarget = false;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------
        // HELPERS DE UI — ventana grande de Créditos (sin cambios).
        // ------------------------------------------------------------------

        // "fondoPersonalizado" es opcional: si se asigna, reemplaza el color plano +
        // degradado rojo por esa imagen (llenando la ventana, no toda la pantalla).
        private GameObject CrearVentanaGrande(Transform padre, string nombre, string tituloMenu, Sprite fondoPersonalizado = null)
        {
            var ventana = new GameObject(nombre);
            ventana.transform.SetParent(padre, false);
            var fondo = ventana.AddComponent<Image>();

            var rect = ventana.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(AnchoPanelGrande, AltoPanelGrande);
            rect.anchoredPosition = Vector2.zero;

            if (fondoPersonalizado != null)
            {
                // Con imagen propia no se agrega el degradado rojo generado por código:
                // se vería encima de tu arte y lo taparía/desentonaría.
                fondo.sprite = fondoPersonalizado;
                fondo.type = Image.Type.Simple;
                fondo.preserveAspect = false;
                fondo.color = Color.white;
            }
            else
            {
                fondo.color = ColorFondoPanel;

                var degradadoGo = new GameObject("Degradado");
                degradadoGo.transform.SetParent(ventana.transform, false);
                var degradadoImg = degradadoGo.AddComponent<Image>();
                degradadoImg.sprite = ObtenerSpriteDegradado();
                degradadoImg.raycastTarget = false;
                var degradadoRect = degradadoGo.GetComponent<RectTransform>();
                degradadoRect.anchorMin = Vector2.zero;
                degradadoRect.anchorMax = Vector2.one;
                degradadoRect.offsetMin = Vector2.zero;
                degradadoRect.offsetMax = Vector2.zero;
            }

            var encabezadoGo = new GameObject("Encabezado");
            encabezadoGo.transform.SetParent(ventana.transform, false);
            var encabezadoTexto = encabezadoGo.AddComponent<Text>();
            encabezadoTexto.text = tituloMenu;
            encabezadoTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            encabezadoTexto.fontSize = 38;
            encabezadoTexto.fontStyle = FontStyle.Bold;
            encabezadoTexto.alignment = TextAnchor.MiddleLeft;
            encabezadoTexto.color = ColorAcentoRojo;
            encabezadoTexto.raycastTarget = false;
            var encabezadoRect = encabezadoGo.GetComponent<RectTransform>();
            encabezadoRect.anchorMin = new Vector2(0f, 1f);
            encabezadoRect.anchorMax = new Vector2(0f, 1f);
            encabezadoRect.pivot = new Vector2(0f, 1f);
            encabezadoRect.sizeDelta = new Vector2(600, 55);
            encabezadoRect.anchoredPosition = new Vector2(50, -40);

            var lineaGo = new GameObject("Linea");
            lineaGo.transform.SetParent(ventana.transform, false);
            var lineaImg = lineaGo.AddComponent<Image>();
            lineaImg.color = ColorLinea;
            lineaImg.raycastTarget = false;
            var lineaRect = lineaGo.GetComponent<RectTransform>();
            lineaRect.anchorMin = new Vector2(0f, 1f);
            lineaRect.anchorMax = new Vector2(0f, 1f);
            lineaRect.pivot = new Vector2(0f, 1f);
            lineaRect.sizeDelta = new Vector2(AnchoPanelGrande - 100, 2);
            lineaRect.anchoredPosition = new Vector2(50, -95);

            CrearBotonVolver(ventana.transform, () => ventana.SetActive(false));
            CrearPistaTeclado(ventana.transform, "↑ ↓    Navegar", 110);
            CrearPistaTeclado(ventana.transform, "ENTER    Confirmar", 75);
            CrearPistaTeclado(ventana.transform, "ESC    Volver", 40);

            return ventana;
        }

        private void CrearBotonVolver(Transform padre, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject("BotonVolver");
            go.transform.SetParent(padre, false);
            var texto = go.AddComponent<Text>();
            texto.text = "✕";
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 30;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = ColorTextoInactivo;

            var boton = go.AddComponent<Button>();
            boton.transition = Selectable.Transition.None;
            boton.targetGraphic = texto;
            boton.onClick.AddListener(ReproducirClick);
            boton.onClick.AddListener(accion);

            var trigger = go.AddComponent<EventTrigger>();
            AgregarEventoPuntero(trigger, EventTriggerType.PointerEnter, () => texto.color = ColorAcentoHover);
            AgregarEventoPuntero(trigger, EventTriggerType.PointerExit, () => texto.color = ColorTextoInactivo);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(50, 50);
            rect.anchoredPosition = new Vector2(-30, -25);
        }

        private void CrearPistaTeclado(Transform padre, string texto, float y)
        {
            var go = new GameObject("Pista");
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.text = texto;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleRight;
            t.color = ColorTextoInactivo;
            t.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(320, 26);
            rect.anchoredPosition = new Vector2(-50, y);
        }

        private void AgregarEventoPuntero(EventTrigger trigger, EventTriggerType tipo, UnityEngine.Events.UnityAction accion)
        {
            var entrada = new EventTrigger.Entry { eventID = tipo };
            entrada.callback.AddListener(_ => accion());
            trigger.triggers.Add(entrada);
        }

        // ------------------------------------------------------------------
        // DEGRADADO ROJO DIAGONAL (generado en runtime, usado solo por Créditos).
        // ------------------------------------------------------------------

        private static Sprite spriteDegradadoCache;

        private static Sprite ObtenerSpriteDegradado()
        {
            if (spriteDegradadoCache != null) return spriteDegradadoCache;

            const int n = 128;
            var textura = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var colorBorde = new Color(0.55f, 0.05f, 0.05f, 0.9f);
            for (int y = 0; y < n; y++)
            {
                float v = y / (float)(n - 1);
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1);
                    float diagonal = u - v;
                    float intensidad = Mathf.Pow(Mathf.Abs(diagonal), 1.4f);
                    float alfa = intensidad * colorBorde.a;
                    textura.SetPixel(x, y, new Color(colorBorde.r, colorBorde.g, colorBorde.b, alfa));
                }
            }
            textura.Apply();

            spriteDegradadoCache = Sprite.Create(textura, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return spriteDegradadoCache;
        }

        // ------------------------------------------------------------------
        // ACCIONES DE LOS BOTONES PRINCIPALES
        // ------------------------------------------------------------------

        private void OnIniciarJuego()
        {
            SceneManager.LoadScene(nombreEscenaJuego);
        }

        private void OnSalir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
