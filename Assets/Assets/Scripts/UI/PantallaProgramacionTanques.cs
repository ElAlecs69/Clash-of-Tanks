using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TanksGame.Language;

namespace TanksGame.UI
{
    // Pantalla "Programación de Tanques" (barra superior + historial + terminal/editor +
    // vista previa del tanque + personalización de skin).
    //
    // A propósito sigue usando colores planos y rectángulos simples: la idea es que
    // reemplaces cada pieza por tu arte final (fondo, iconos, fuente) directo en el
    // Inspector después, sin tener que rearmar el layout. Los 5 contenedores principales
    // (barra superior + 4 paneles) usan anclajes elásticos, así que la pantalla siempre
    // cubre toda la ventana sin importar la resolución — el espacio extra se lo queda el
    // panel de la terminal (más ancho/alto) y el de personalizar skin (más alto).
    public class PantallaProgramacionTanques : MonoBehaviour
    {
        [Header("Colores placeholder (se usan solo si no asignas un sprite abajo)")]
        public Color colorFondoPantalla = new Color(0.05f, 0.05f, 0.05f);
        public Color colorPanel = new Color(0.11f, 0.11f, 0.12f);
        public Color colorBoton = new Color(0.2f, 0.2f, 0.21f);
        public Color colorBotonAccentoRojo = new Color(0.55f, 0.14f, 0.14f);
        public Color colorBotonAccentoVerde = new Color(0.16f, 0.45f, 0.2f);
        public Color colorTexto = new Color(0.92f, 0.92f, 0.92f);
        public Color colorTextoSecundario = new Color(0.6f, 0.6f, 0.62f);
        public Color colorSwatch = new Color(0.3f, 0.3f, 0.32f);
        public Color colorSwatchSeleccionado = new Color(0.75f, 0.6f, 0.15f);

        [Header("Decoración estilo militar (nativa, sin imágenes)")]
        [Tooltip("Color de los brackets tipo HUD en las esquinas de cada panel y de las franjas de acento bajo los títulos. Mismo tono dorado que MenuPrincipal, para que combinen.")]
        public Color colorAcentoMilitar = new Color(0.85f, 0.65f, 0.15f);
        [Tooltip("Si está activo, se agrega una viñeta sutil (oscurece los bordes) sobre el fondo.")]
        public bool usarVinetaDeFondo = true;

        [Header("Arte real (opcional). Arrastra tus PNG acá; si dejas un campo vacío, se usa el color de arriba.")]
        public Sprite spriteFondoPantalla;
        public Sprite spriteMarcoPanel;      // Se reutiliza en la barra superior y los 4 paneles.
        public Sprite spriteFondoBoton;      // Se reutiliza en los 5 botones de acción.
        public Sprite spriteIconoHistorial;  // Se reutiliza en cada fila del historial.
        public Sprite spriteVistaPreviaTanque;

        [Header("Guardado en disco")]
        [Tooltip("Subcarpeta dentro de Application.persistentDataPath donde se guardan los .txt.")]
        public string carpetaScripts = "ScriptsTanques";

        [Header("Volver al menú")]
        [Tooltip("Nombre de la escena del menú principal (la de MenuPrincipal.cs). Debe estar agregada en File > Build Profiles > Scene List.")]
        public string nombreEscenaMenuPrincipal = "MenuPrincipal";

        [Header("Rosa de los vientos (dirección)")]
        [Tooltip("Nombres de instrucciones que requieren una dirección como argumento, ej. \"MOV\" para MOV(). Se detecta cuando aparecen escritas con paréntesis vacíos: MOV()")]
        public string[] comandosQueRequierenDireccion = { "MOV", "AMT", "RADAR", "MISIL" };

        [Header("Música de fondo (solo esta pantalla)")]
        [Tooltip("Se reproduce en bucle mientras estás en esta pantalla. Se detiene sola al volver al menú (no usa DontDestroyOnLoad).")]
        public AudioClip musicaFondo;
        [Range(0f, 1f)]
        public float volumenMusica = 0.5f;

        // --- Referencias vivas, creadas en tiempo de ejecución ---
        private InputField campoEditor;
        private Text textoResaltado;       // Overlay con colores de sintaxis, superpuesto al InputField (que queda con texto invisible).
        private Text textoNumerosLinea;    // Columna de números de línea, a la izquierda del código.
        private RectTransform contenedorEditorRect; // El que realmente cambia de tamaño y el que mueve el ScrollRect.
        private ScrollRect scrollTerminal;
        private Text textoEstado;
        private Transform contenedorListaHistorial;
        private RectTransform panelHistorialRect;
        private GameObject overlayConfirmarBorrado;
        private AudioSource audioSourceMusica;

        // Overlay "Guardar como" / "Abrir": reemplaza al viejo campo de texto fijo en la
        // barra superior. El nombre del archivo ahora se pide recién al presionar
        // GUARDAR SCRIPT o CARGAR SCRIPT, como el diálogo "Guardar como" de un SO.
        private GameObject overlayNombreArchivo;
        private InputField campoNombreOverlay;
        private Text tituloOverlayNombre;
        private Text textoBotonConfirmarOverlay;
        private Action<string> accionConfirmarNombreArchivo;
        private string nombreArchivoActual = ""; // Recordado internamente para reabrir/re-guardar rápido; no se muestra en pantalla.

        // Rosa de los vientos: vive DENTRO del panel de la Terminal, abajo a la derecha,
        // de forma permanente. Si hay una instrucción de las de
        // 'comandosQueRequierenDireccion' escrita con paréntesis vacíos (ej. "MOV()"),
        // tocar N/S/E/O la completa; si no hay ninguna pendiente, los botones
        // simplemente no hacen nada (y el subtítulo lo indica).
        private GameObject panelRosaVientos;
        private Text textoInstruccionPendienteDireccion;

        // Palabras clave del lenguaje, para el resaltado de sintaxis (ver ColorearLinea).
        private static readonly string[] PalabrasClave =
        {
            "INICIO", "IF", "FIN", "MOV", "AMT", "MINA", "MISIL", "RADAR",
            "ESCUDO", "ESPERAR", "DAÑAR", "DAÑO", "BUCLE"
        };

        // --- Datos de personalización del tanque (nativos, sin imágenes) ---
        private static readonly Color[] ColoresPrincipales =
        {
            new Color(0.30f, 0.36f, 0.22f), // Verde militar
            new Color(0.55f, 0.47f, 0.33f), // Arena
            new Color(0.35f, 0.36f, 0.38f), // Gris urbano
            new Color(0.20f, 0.28f, 0.34f), // Azul marino
        };

        private static readonly string[] SimbolosCalcomania = { "★", "✖", "●", "▲" };
        private static readonly string[] NumerosTanque = { "01", "07", "13", "99" };

        private static readonly Color[] ColoresBandera =
        {
            new Color(0.75f, 0.15f, 0.15f),
            new Color(0.15f, 0.35f, 0.65f),
            new Color(0.20f, 0.55f, 0.25f),
            new Color(0.80f, 0.65f, 0.15f),
            new Color(0.85f, 0.85f, 0.85f),
        };

        private readonly List<EntradaHistorial> historial = new List<EntradaHistorial>();
        // swatchesPorGrupo = el "marco" que se resalta al seleccionar (siempre gris/dorado).
        // contenidoSwatchesPorGrupo = el contenido real de cada swatch (color/patrón/texto).
        private readonly Dictionary<string, List<Image>> swatchesPorGrupo = new Dictionary<string, List<Image>>();
        private readonly Dictionary<string, List<Image>> contenidoSwatchesPorGrupo = new Dictionary<string, List<Image>>();
        private readonly Dictionary<string, int> seleccionActual = new Dictionary<string, int>();

        // Vista previa del tanque: se actualiza en vivo según las selecciones de arriba.
        private Image imagenVistaPreviaTanque;
        private Text textoDecalPreview;
        private Text textoNumeroPreview;
        private Image imagenBanderaPreview;

        private class EntradaHistorial
        {
            public string nombre;
            public string fechaHora;
            public string contenido;
        }

        // Punto de enganche para que GameManager (u otro script) recoja el último script
        // validado con éxito. Es un campo estático simple a propósito: conectar esto con
        // qué tanque/jugador específico recibe el programa depende de cómo termines de
        // armar el flujo entre escenas (selección de jugador, etc.), así que se deja como
        // el siguiente paso una vez definas eso.
        public static string UltimoScriptValidado { get; private set; } = "";

        private void Start()
        {
            // Si la escena no tiene ninguna Cámara (y por lo tanto ningún AudioListener),
            // la música no se reproduce. Nos aseguramos de que exista al menos uno.
            if (FindObjectOfType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

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

        private void ConstruirUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("Canvas_ProgramacionTanques");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1536, 1024); // mismo tamaño que tu mockup.
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            var fondo = CrearRect(canvasGo.transform, "Fondo", Vector2.zero, Vector2.zero, colorFondoPantalla,
                spriteFondoPantalla, sliced: false);
            var fondoRect = fondo.GetComponent<RectTransform>();
            fondoRect.anchorMin = Vector2.zero;
            fondoRect.anchorMax = Vector2.one;
            fondoRect.offsetMin = Vector2.zero;
            fondoRect.offsetMax = Vector2.zero;

            if (usarVinetaDeFondo)
            {
                var vinetaGo = new GameObject("Vineta");
                vinetaGo.transform.SetParent(canvasGo.transform, false);
                var vinetaImg = vinetaGo.AddComponent<Image>();
                vinetaImg.sprite = ObtenerSpriteVineta();
                vinetaImg.raycastTarget = false;
                var vinetaRect = vinetaGo.GetComponent<RectTransform>();
                vinetaRect.anchorMin = Vector2.zero;
                vinetaRect.anchorMax = Vector2.one;
                vinetaRect.offsetMin = Vector2.zero;
                vinetaRect.offsetMax = Vector2.zero;
            }

            ConstruirBarraSuperior(canvasGo.transform);
            ConstruirPanelHistorial(canvasGo.transform);
            ConstruirPanelInstrucciones(canvasGo.transform);
            ConstruirPanelTerminal(canvasGo.transform);
            ConstruirPanelVistaPrevia(canvasGo.transform);
            ConstruirPanelPersonalizarSkin(canvasGo.transform);
            ConstruirOverlayConfirmarBorrado(canvasGo.transform);
            ConstruirOverlayNombreArchivo(canvasGo.transform);

            ReconstruirListaHistorial();
            ActualizarTextoEstado("LISTO", esError: false);
        }

        // ------------------------------------------------------------------
        // BARRA SUPERIOR: título + 5 botones de acción.
        // Ancla elástica: fija al borde superior, estirada a todo el ancho.
        // ------------------------------------------------------------------

        private const float ALTURA_BARRA = 215f;
        private const float MARGEN = 15f;
        private const float ANCHO_HISTORIAL = 325f;
        private const float ANCHO_INSTRUCCIONES = 325f; // Mismo ancho que el panel de historial, como pediste.
        private const float ANCHO_COLUMNA_DERECHA = 466f;
        private const float ALTO_VISTA_PREVIA = 320f;
        private const float GAP = 10f;
        private const float GAP_COLUMNA_DERECHA = 20f;

        private void ConstruirBarraSuperior(Transform padre)
        {
            var barra = CrearRectElastico(padre, "BarraSuperior",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -ALTURA_BARRA), new Vector2(0, 0),
                colorPanel, spriteMarcoPanel);

            CrearTexto(barra.transform, "TituloPantalla", "PROGRAMACIÓN DE TANQUES",
                new Vector2(15, 10), new Vector2(700, 60), 32, FontStyle.Bold, TextAnchor.MiddleLeft, colorTexto);

            AgregarFranjaAcento(barra.transform, 70f);
            AgregarBracketsDeEsquina(barra.transform, colorAcentoMilitar);

            float anchoBoton = 289f, alto = 95f, y = 105f;
            float[] xBotones = { 15, 319, 623, 927, 1231 };

            CrearBotonAccion(barra.transform, "BotonGuardarScript", "GUARDAR SCRIPT", ".TXT",
                new Vector2(xBotones[0], y), new Vector2(anchoBoton, alto), colorBoton, OnGuardarScript);
            CrearBotonAccion(barra.transform, "BotonCargarScript", "CARGAR SCRIPT", ".TXT",
                new Vector2(xBotones[1], y), new Vector2(anchoBoton, alto), colorBoton, OnCargarScript);
            CrearBotonAccion(barra.transform, "BotonVolverMenu", "MENÚ DE INICIO", "VOLVER",
                new Vector2(xBotones[2], y), new Vector2(anchoBoton, alto), colorBotonAccentoRojo, OnVolverAlMenu, colorEsAccento: true);
            CrearBotonAccion(barra.transform, "BotonBorrarScript", "BORRAR SCRIPT", "",
                new Vector2(xBotones[3], y), new Vector2(anchoBoton, alto), colorBoton, OnBorrarScript);
            CrearBotonAccion(barra.transform, "BotonIniciarEjecucion", "INICIAR", "EJECUCIÓN",
                new Vector2(xBotones[4], y), new Vector2(anchoBoton, alto), colorBotonAccentoVerde, OnIniciarEjecucion, colorEsAccento: true);
        }

        // ------------------------------------------------------------------
        // PANEL IZQUIERDO: historial de scripts ejecutados.
        // Ancla elástica: fijo al borde izquierdo, ancho fijo, estirado en alto.
        // ------------------------------------------------------------------

        private void ConstruirPanelHistorial(Transform padre)
        {
            var panel = CrearRectElastico(padre, "PanelHistorial",
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(MARGEN, MARGEN), new Vector2(MARGEN + ANCHO_HISTORIAL, -(ALTURA_BARRA + GAP)),
                colorPanel, spriteMarcoPanel);
            panelHistorialRect = panel.GetComponent<RectTransform>();

            CrearTexto(panel.transform, "TituloHistorial", "HISTORIAL DE SCRIPTS",
                new Vector2(15, 15), new Vector2(295, 30), 20, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            AgregarFranjaAcento(panel.transform, 50f);
            AgregarBracketsDeEsquina(panel.transform, colorAcentoMilitar);

            contenedorListaHistorial = CrearAreaConScroll(panel.transform, "ScrollHistorial",
                new Vector2(0, 75), new Vector2(0, 55), 1f);

            var botonLimpiar = CrearRectElastico(panel.transform, "BotonLimpiarHistorial",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(15, 15), new Vector2(-15, 65),
                colorBoton, null);
            var boton = botonLimpiar.AddComponent<Button>();
            boton.targetGraphic = botonLimpiar.GetComponent<Image>();
            boton.onClick.AddListener(OnLimpiarHistorial);
            CrearTexto(botonLimpiar.transform, "Texto", "LIMPIAR HISTORIAL", Vector2.zero, new Vector2(295, 50),
                14, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
        }

        private void ReconstruirListaHistorial()
        {
            for (int i = contenedorListaHistorial.childCount - 1; i >= 0; i--)
                Destroy(contenedorListaHistorial.GetChild(i).gameObject);

            float y = 0f;
            for (int i = historial.Count - 1; i >= 0; i--)
            {
                var entrada = historial[i];
                CrearItemHistorial(contenedorListaHistorial, entrada, new Vector2(0, y));
                y += 62f;
            }

            if (contenedorListaHistorial is RectTransform contenidoRect)
                contenidoRect.sizeDelta = new Vector2(0f, Mathf.Max(1f, y));
        }

        private void CrearItemHistorial(Transform padre, EntradaHistorial entrada, Vector2 posicion)
        {
            var fila = CrearRect(padre, $"Item_{entrada.nombre}", posicion, new Vector2(295, 55), new Color(0, 0, 0, 0));

            CrearRect(fila.transform, "Icono", new Vector2(0, 5), new Vector2(30, 30), colorTextoSecundario,
                spriteIconoHistorial, sliced: false);
            CrearTexto(fila.transform, "NombreArchivo", entrada.nombre, new Vector2(42, 0), new Vector2(240, 26),
                16, FontStyle.Normal, TextAnchor.MiddleLeft, colorTexto);
            CrearTexto(fila.transform, "Fecha", entrada.fechaHora, new Vector2(42, 26), new Vector2(240, 22),
                12, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);

            var boton = fila.AddComponent<Button>();
            boton.targetGraphic = fila.GetComponent<Image>();
            boton.onClick.AddListener(() => OnSeleccionarHistorial(entrada));
        }

        // ------------------------------------------------------------------
        // PANEL: lista de instrucciones clicables.
        // ------------------------------------------------------------------

        private void ConstruirPanelInstrucciones(Transform padre)
        {
            float izquierda = MARGEN + ANCHO_HISTORIAL + GAP;

            var panel = CrearRectElastico(padre, "PanelInstrucciones",
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(izquierda, MARGEN), new Vector2(izquierda + ANCHO_INSTRUCCIONES, -(ALTURA_BARRA + GAP)),
                colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloInstrucciones", "INSTRUCCIONES",
                new Vector2(15, 15), new Vector2(295, 30), 20, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            AgregarFranjaAcento(panel.transform, 50f);
            AgregarBracketsDeEsquina(panel.transform, colorAcentoMilitar);

            // etiqueta = lo que se ve en el botón; fragmento = lo que se inserta en el
            // editor. Los que llevan "()" vacíos (MOV, AMT, RADAR, MISIL) son
            // justamente los que después completa la rosa de los vientos.
            // "IF" ahora inserta el bloque real IF (condición) { instrucción } en vez
            // del viejo "IF CONDICIÓN THEN INSTRUCCIÓN".
            var instrucciones = new (string etiqueta, string fragmento)[]
            {
                ("MOV (dir)", "MOV()"),
                ("AMT (dir)", "AMT()"),
                ("MINA", "MINA"),
                ("MISIL (dir)", "MISIL()"),
                ("RADAR (dir)", "RADAR()"),
                ("IF ( ) { }", "IF ( ) {\n    \n}"),
                ("ESCUDO", "ESCUDO"),
                ("ESPERAR", "ESPERAR"),
                ("DAÑAR", "DAÑAR"),
                ("DAÑO", "DAÑO"),
                ("BUCLE", "BUCLE\n    \nFIN"),
            };

            float altoContenido = instrucciones.Length * 48f + 20f;
            var contenido = CrearAreaConScroll(panel.transform, "ScrollInstrucciones",
                new Vector2(0, 15), new Vector2(0, 55), altoContenido);

            float y = 10f;
            foreach (var (etiqueta, fragmento) in instrucciones)
            {
                CrearBotonInstruccion(contenido, etiqueta, y, fragmento);
                y += 48f;
            }
        }

        private Transform CrearAreaConScroll(Transform padre, string nombre, Vector2 margenIzqAbajo, Vector2 margenDerArriba, float altoContenido)
        {
            var viewportGo = CrearRectElasticoLocal(padre, nombre, new Vector2(0, 0), new Vector2(1, 1),
                margenIzqAbajo, margenDerArriba, new Color(0, 0, 0, 0), null);
            viewportGo.AddComponent<RectMask2D>();

            var scrollRect = viewportGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25f;
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();

            var contenidoGo = new GameObject("Contenido");
            contenidoGo.transform.SetParent(viewportGo.transform, false);
            var contenidoRect = contenidoGo.AddComponent<RectTransform>();
            contenidoRect.anchorMin = new Vector2(0f, 1f);
            contenidoRect.anchorMax = new Vector2(1f, 1f);
            contenidoRect.pivot = new Vector2(0.5f, 1f);
            contenidoRect.sizeDelta = new Vector2(0f, altoContenido);
            contenidoRect.anchoredPosition = Vector2.zero;

            scrollRect.content = contenidoRect;
            return contenidoRect;
        }

        private void CrearBotonInstruccion(Transform padre, string etiqueta, float y, string fragmentoAInsertar)
        {
            var go = CrearRect(padre, $"Instruccion_{etiqueta}", new Vector2(15, y), new Vector2(295, 40), colorBoton);
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(() => InsertarEnEditor(fragmentoAInsertar + "\n"));
            CrearTexto(go.transform, "Texto", etiqueta, Vector2.zero, new Vector2(295, 40),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
        }

        private void InsertarEnEditor(string fragmento)
        {
            string texto = campoEditor.text;
            if (texto.Length > 0 && !texto.EndsWith("\n"))
                texto += "\n";
            texto += fragmento;

            campoEditor.text = texto;
            ActualizarEditorTrasCambio();
        }

        // ------------------------------------------------------------------
        // PANEL CENTRAL: terminal / editor de script.
        // ------------------------------------------------------------------

        private void ConstruirPanelTerminal(Transform padre)
        {
            float izquierda = MARGEN + ANCHO_HISTORIAL + GAP + ANCHO_INSTRUCCIONES + GAP;
            float derecha = MARGEN + ANCHO_COLUMNA_DERECHA + GAP;

            var panel = CrearRectElastico(padre, "PanelTerminal",
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(izquierda, MARGEN), new Vector2(-derecha, -(ALTURA_BARRA + GAP)),
                colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloTerminal", "TERMINAL / EDITOR DE SCRIPT",
                new Vector2(15, 15), new Vector2(400, 30), 20, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            AgregarFranjaAcento(panel.transform, 50f);
            AgregarBracketsDeEsquina(panel.transform, colorAcentoMilitar);

            var botonBorrarLinea = CrearRectAncladoEsquina(panel.transform, "BotonBorrarUltimaLinea",
                new Vector2(1, 1), 15, 15, 210, 30, colorBoton, null);
            var btnBorrarLinea = botonBorrarLinea.AddComponent<Button>();
            btnBorrarLinea.targetGraphic = botonBorrarLinea.GetComponent<Image>();
            btnBorrarLinea.onClick.AddListener(OnBorrarUltimaInstruccion);
            CrearTexto(botonBorrarLinea.transform, "Texto", "⌫ BORRAR ÚLTIMA LÍNEA", Vector2.zero, new Vector2(210, 30),
                12, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            var areaCodigo = CrearRectElasticoLocal(panel.transform, "AreaCodigo",
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(15, 85), new Vector2(15, 65),
                colorFondoPantalla, null);

            areaCodigo.AddComponent<RectMask2D>();
            scrollTerminal = areaCodigo.AddComponent<ScrollRect>();
            scrollTerminal.horizontal = false;
            scrollTerminal.vertical = true;
            scrollTerminal.movementType = ScrollRect.MovementType.Clamped;
            scrollTerminal.scrollSensitivity = 25f;
            scrollTerminal.viewport = areaCodigo.GetComponent<RectTransform>();

            campoEditor = CrearCampoTextoMultilineaConScroll(areaCodigo.transform, "CampoEditor",
                "# Escribe aquí tu script\nINICIO:\n    ESPERAR\n", scrollTerminal);

            textoEstado = CrearRectElasticoTextoInferior(panel.transform);

            ConstruirPanelRosaVientos(panel.transform);

            campoEditor.onValueChanged.AddListener(_ =>
            {
                // Actualizaciones "ligeras": se llaman en CADA tecla que se escribe.
                // A propósito NO llaman a ActualizarEditorTrasCambio() acá (esa mueve
                // el cursor al final del texto), porque eso rompería la edición normal
                // — el cursor saltaría al final cada vez que escribís una letra.
                ActualizarDeteccionDireccion();
                ActualizarNumerosDeLinea();
                ActualizarResaltadoSintaxis();
            });

            ActualizarEditorTrasCambio();
        }

        private void ActualizarEditorTrasCambio()
        {
            float alturaPreferida = campoEditor.textComponent.preferredHeight + 20f;
            contenedorEditorRect.sizeDelta = new Vector2(contenedorEditorRect.sizeDelta.x, Mathf.Max(200f, alturaPreferida));
            campoEditor.caretPosition = campoEditor.text.Length;

            Canvas.ForceUpdateCanvases();
            if (scrollTerminal != null)
                scrollTerminal.verticalNormalizedPosition = 0f;

            ActualizarDeteccionDireccion();
            ActualizarNumerosDeLinea();
            ActualizarResaltadoSintaxis();
        }

        // Actualiza la columna de números de línea (1, 2, 3...) a la izquierda del
        // código, para que se vea como un editor de verdad.
        private void ActualizarNumerosDeLinea()
        {
            if (textoNumerosLinea == null) return;
            int cantidadLineas = campoEditor.text.Split('\n').Length;
            var numeros = new System.Text.StringBuilder();
            for (int i = 1; i <= cantidadLineas; i++)
            {
                numeros.Append(i);
                if (i < cantidadLineas) numeros.Append('\n');
            }
            textoNumerosLinea.text = numeros.ToString();
        }

        // Recolorea el texto: como el InputField real queda con su propio texto
        // invisible (ver CrearCampoTextoMultilineaConScroll), lo que el jugador ve es
        // este overlay con rich text. Como las etiquetas <color=...> no cambian el
        // ancho de los caracteres, el overlay queda pixel a pixel alineado con el
        // InputField real (y con el cursor, que pertenece al InputField real).
        private void ActualizarResaltadoSintaxis()
        {
            if (textoResaltado == null) return;
            textoResaltado.text = ColorearSintaxis(campoEditor.text);
        }

        private static readonly string PatronPalabrasClave = @"\b(" + string.Join("|", PalabrasClave) + @")\b";

        private string ColorearSintaxis(string texto)
        {
            var lineas = texto.Split('\n');
            for (int i = 0; i < lineas.Length; i++)
                lineas[i] = ColorearLinea(lineas[i]);
            return string.Join("\n", lineas);
        }

        private string ColorearLinea(string linea)
        {
            int indiceComentario = linea.IndexOf('#');
            string codigo = indiceComentario >= 0 ? linea.Substring(0, indiceComentario) : linea;
            string comentario = indiceComentario >= 0 ? linea.Substring(indiceComentario) : "";

            // Palabras clave (INICIO, IF, MOV, ESPERAR, etc.) en celeste.
            codigo = Regex.Replace(codigo, PatronPalabrasClave, "<color=#4FC3F7>$1</color>");
            // Direcciones dentro de paréntesis: (N), (S), (E), (O), en naranja.
            codigo = Regex.Replace(codigo, @"\(([NSEO])\)", "(<color=#FFB74D>$1</color>)");
            // Números sueltos, en verde.
            codigo = Regex.Replace(codigo, @"\b(\d+)\b", "<color=#C3E88D>$1</color>");

            if (comentario.Length > 0)
                codigo += $"<color=#777777><i>{comentario}</i></color>";

            return codigo;
        }

        private void OnBorrarUltimaInstruccion()
        {
            string texto = campoEditor.text;
            if (string.IsNullOrEmpty(texto)) return;

            string sinSaltoFinal = texto.EndsWith("\n") ? texto.Substring(0, texto.Length - 1) : texto;
            int ultimoSalto = sinSaltoFinal.LastIndexOf('\n');
            string nuevoTexto = ultimoSalto >= 0 ? sinSaltoFinal.Substring(0, ultimoSalto + 1) : "";

            campoEditor.text = nuevoTexto;
            ActualizarEditorTrasCambio();
        }

        // Al presionar Enter dentro del editor: en vez del salto de línea "a secas" que
        // pondría un InputField normal, copia la indentación (espacios iniciales) de la
        // línea actual, y le suma un nivel más (4 espacios) si esa línea termina en "{"
        // — así el bloque de un IF/BUCLE recién abierto ya aparece indentado solo.
        private void InsertarNuevaLineaConIndentacion()
        {
            string texto = campoEditor.text;
            int caret = Mathf.Clamp(campoEditor.caretPosition, 0, texto.Length);

            int inicioLinea = texto.LastIndexOf('\n', Mathf.Max(0, caret - 1)) + 1;
            string lineaActual = texto.Substring(inicioLinea, caret - inicioLinea);

            int cantidadEspacios = 0;
            while (cantidadEspacios < lineaActual.Length && lineaActual[cantidadEspacios] == ' ')
                cantidadEspacios++;
            string indentacion = new string(' ', cantidadEspacios);

            if (lineaActual.TrimEnd().EndsWith("{"))
                indentacion += "    ";

            string nuevoTexto = texto.Insert(caret, "\n" + indentacion);
            campoEditor.text = nuevoTexto;
            campoEditor.caretPosition = caret + 1 + indentacion.Length;

            ActualizarEditorTrasCambio();
        }

        private Text CrearRectElasticoTextoInferior(Transform padre)
        {
            var go = CrearRectElastico(padre, "TextoEstadoTerminal",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(15, 15), new Vector2(-15, 45),
                new Color(0, 0, 0, 0), null);

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 14;
            texto.alignment = TextAnchor.MiddleLeft;
            texto.color = colorTextoSecundario;

            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;

            return texto;
        }

        private void ActualizarTextoEstado(string mensaje, bool esError)
        {
            if (textoEstado == null) return;
            textoEstado.text = esError ? $"ERROR: {mensaje}" : $"ESTADO: {mensaje}";
            textoEstado.color = esError ? new Color(0.85f, 0.3f, 0.25f) : colorTextoSecundario;
        }

        // ------------------------------------------------------------------
        // PANEL DERECHO (arriba): vista previa del tanque.
        // ------------------------------------------------------------------

        private void ConstruirPanelVistaPrevia(Transform padre)
        {
            var panel = CrearRectAncladoEsquina(padre, "PanelVistaPrevia",
                new Vector2(1, 1), MARGEN, ALTURA_BARRA + GAP, ANCHO_COLUMNA_DERECHA, ALTO_VISTA_PREVIA,
                colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloVistaPrevia", "VISTA PREVIA DEL TANQUE",
                new Vector2(15, 15), new Vector2(400, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            AgregarFranjaAcento(panel.transform, 48f);
            AgregarBracketsDeEsquina(panel.transform, colorAcentoMilitar);

            var imagenPreviaGo = CrearRect(panel.transform, "ImagenVistaPreviaTanque", new Vector2(15, 60), new Vector2(436, 220),
                colorFondoPantalla, spriteVistaPreviaTanque, sliced: false);
            imagenVistaPreviaTanque = imagenPreviaGo.GetComponent<Image>();

            // Overlays: decal grande al centro, número abajo a la derecha, bandera
            // chica arriba a la izquierda. Todo se actualiza en ActualizarVistaPreviaTanque().
            textoDecalPreview = CrearTexto(imagenPreviaGo.transform, "DecalPreview", SimbolosCalcomania[0],
                new Vector2(178, 70), new Vector2(80, 80), 40, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            textoNumeroPreview = CrearTexto(imagenPreviaGo.transform, "NumeroPreview", NumerosTanque[0],
                new Vector2(340, 178), new Vector2(80, 30), 22, FontStyle.Bold, TextAnchor.MiddleRight, colorTexto);

            var banderaGo = CrearRect(imagenPreviaGo.transform, "BanderaPreview", new Vector2(10, 10), new Vector2(34, 22), ColoresBandera[0]);
            imagenBanderaPreview = banderaGo.GetComponent<Image>();

            CrearFlecha(panel.transform, "BotonSkinAnterior", "<", new Vector2(25, 150), () => OnCambiarSkin(-1));
            CrearFlecha(panel.transform, "BotonSkinSiguiente", ">", new Vector2(436 - 25, 150), () => OnCambiarSkin(1));
        }

        // ------------------------------------------------------------------
        // PANEL DERECHO (abajo): personalizar skin.
        // ------------------------------------------------------------------

        private void ConstruirPanelPersonalizarSkin(Transform padre)
        {
            float arriba = ALTURA_BARRA + GAP + ALTO_VISTA_PREVIA + GAP_COLUMNA_DERECHA;

            var panel = CrearRectElastico(padre, "PanelPersonalizarSkin",
                new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-(MARGEN + ANCHO_COLUMNA_DERECHA), MARGEN), new Vector2(-MARGEN, -arriba),
                colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloPersonalizarSkin", "PERSONALIZAR SKIN",
                new Vector2(15, 15), new Vector2(400, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            AgregarFranjaAcento(panel.transform, 48f);
            AgregarBracketsDeEsquina(panel.transform, colorAcentoMilitar);

            var contenido = CrearAreaConScroll(panel.transform, "ScrollPersonalizarSkin",
                new Vector2(0, 15), new Vector2(0, 55), 320f);

            CrearTexto(contenido, "TituloPatron", "PATRÓN", new Vector2(15, 5), new Vector2(180, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearTexto(contenido, "TituloColorPrincipal", "COLOR PRINCIPAL", new Vector2(245, 5), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);

            // El orden importa: "Color" debe crearse antes de llamar a
            // RegenerarVisualesPatron() (los patrones de camuflaje se pintan con el
            // color principal actualmente seleccionado).
            CrearFilaSwatches(contenido, "Patron", 4, new Vector2(15, 35));
            CrearFilaSwatches(contenido, "Color", 4, new Vector2(245, 35));
            AplicarColoresReales("Color", ColoresPrincipales);
            RegenerarVisualesPatron();

            CrearTexto(contenido, "TituloCalcomanias", "CALCOMANÍAS", new Vector2(15, 110), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearTexto(contenido, "TituloNumero", "NÚMERO", new Vector2(245, 110), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);

            CrearFilaSwatches(contenido, "Calcomania", 4, new Vector2(15, 140));
            AplicarEtiquetas("Calcomania", SimbolosCalcomania);
            CrearFilaSwatches(contenido, "Numero", 4, new Vector2(245, 140));
            AplicarEtiquetas("Numero", NumerosTanque);

            CrearTexto(contenido, "TituloBandera", "BANDERA", new Vector2(15, 215), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearFilaSwatches(contenido, "Bandera", 5, new Vector2(15, 245));
            AplicarColoresReales("Bandera", ColoresBandera);

            ActualizarVistaPreviaTanque();
        }

        // Cada swatch ahora son 2 capas: un "marco" (lo que se resalta al
        // seleccionar, siempre transparente u dorado) y, adentro, el "contenido" real
        // (color/patrón/texto) que NUNCA cambia por selección — así un swatch puede
        // mostrar su color/patrón/símbolo verdadero sin que el resaltado se lo tape.
        private void CrearFilaSwatches(Transform padre, string grupo, int cantidad, Vector2 posicion)
        {
            const float tamano = 45f, gap = 10f, margenMarco = 4f;
            var listaMarcos = new List<Image>();
            swatchesPorGrupo[grupo] = listaMarcos;
            var listaContenido = new List<Image>();
            contenidoSwatchesPorGrupo[grupo] = listaContenido;
            if (!seleccionActual.ContainsKey(grupo)) seleccionActual[grupo] = 0;

            for (int i = 0; i < cantidad; i++)
            {
                var posMarco = new Vector2(posicion.x + i * (tamano + gap) - margenMarco, posicion.y - margenMarco);
                var marcoGo = CrearRect(padre, $"Marco{grupo}_{i}", posMarco,
                    new Vector2(tamano + margenMarco * 2, tamano + margenMarco * 2), new Color(0, 0, 0, 0));
                var marcoImagen = marcoGo.GetComponent<Image>();
                listaMarcos.Add(marcoImagen);

                var swatchGo = CrearRect(marcoGo.transform, $"Swatch{grupo}_{i}",
                    new Vector2(margenMarco, margenMarco), new Vector2(tamano, tamano), colorSwatch);
                listaContenido.Add(swatchGo.GetComponent<Image>());

                var boton = marcoGo.AddComponent<Button>();
                boton.targetGraphic = marcoImagen;
                int indice = i;
                boton.onClick.AddListener(() => OnSeleccionarSwatch(grupo, indice));
            }

            ResaltarSeleccion(grupo);
        }

        private void ResaltarSeleccion(string grupo)
        {
            if (!swatchesPorGrupo.TryGetValue(grupo, out var lista)) return;
            int seleccionado = seleccionActual[grupo];
            for (int i = 0; i < lista.Count; i++)
                lista[i].color = (i == seleccionado) ? colorSwatchSeleccionado : new Color(0, 0, 0, 0);
        }

        // Pinta cada swatch de un grupo con su color real (usado por "Color" y "Bandera").
        private void AplicarColoresReales(string grupo, Color[] colores)
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue(grupo, out var lista)) return;
            for (int i = 0; i < lista.Count && i < colores.Length; i++)
                lista[i].color = colores[i];
        }

        // Agrega un texto centrado a cada swatch de un grupo (usado por "Calcomania" y "Numero").
        private void AplicarEtiquetas(string grupo, string[] etiquetas)
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue(grupo, out var lista)) return;
            for (int i = 0; i < lista.Count && i < etiquetas.Length; i++)
            {
                CrearTexto(lista[i].transform, "Etiqueta", etiquetas[i], Vector2.zero, new Vector2(45, 45),
                    18, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
            }
        }

        // Regenera los 4 swatches de "Patron" con el color principal actualmente
        // seleccionado (los patrones son bicolor: una variante oscura y una clara del
        // mismo color base, no un color fijo aparte).
        private void RegenerarVisualesPatron()
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue("Patron", out var lista)) return;
            int iColor = seleccionActual.TryGetValue("Color", out var c) ? c : 0;
            Color colorBase = ColoresPrincipales[iColor];

            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].sprite = GenerarSpritePatron(i, colorBase);
                lista[i].type = Image.Type.Simple;
                lista[i].color = Color.white; // El color ya está horneado en la textura.
            }
        }

        // Genera (en runtime, sin ninguna imagen externa) una textura de camuflaje de
        // 32x32: 0 = sólido, 1 = rayas diagonales, 2 = puntos, 3 = manchas (Perlin noise).
        // Siempre en 2 tonos derivados de 'colorBase' (uno más oscuro, uno más claro).
        private Sprite GenerarSpritePatron(int patronIndice, Color colorBase)
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
                        case 0: // Sólido.
                            esClaro = false;
                            break;
                        case 1: // Rayas diagonales.
                            esClaro = ((x + y) / 4) % 2 == 0;
                            break;
                        case 2: // Puntos.
                            int cx = (x % 8) - 4, cy = (y % 8) - 4;
                            esClaro = (cx * cx + cy * cy) < 6;
                            break;
                        default: // Manchas tipo camuflaje.
                            esClaro = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) > 0.55f;
                            break;
                    }
                    textura.SetPixel(x, y, esClaro ? claro : oscuro);
                }
            }
            textura.Apply();

            return Sprite.Create(textura, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        // Recalcula la vista previa completa (patrón+color, decal, número, bandera) a
        // partir de 'seleccionActual'. Se llama después de cualquier cambio de swatch.
        private void ActualizarVistaPreviaTanque()
        {
            if (imagenVistaPreviaTanque == null) return;

            int iPatron = seleccionActual.TryGetValue("Patron", out var p) ? p : 0;
            int iColor = seleccionActual.TryGetValue("Color", out var c) ? c : 0;
            int iCalcomania = seleccionActual.TryGetValue("Calcomania", out var d) ? d : 0;
            int iNumero = seleccionActual.TryGetValue("Numero", out var num) ? num : 0;
            int iBandera = seleccionActual.TryGetValue("Bandera", out var b) ? b : 0;

            // Si asignaste un sprite real de vista previa en el Inspector, no lo pisamos
            // con el patrón generado — se respeta tu arte final por sobre el placeholder.
            if (spriteVistaPreviaTanque == null)
            {
                imagenVistaPreviaTanque.sprite = GenerarSpritePatron(iPatron, ColoresPrincipales[iColor]);
                imagenVistaPreviaTanque.type = Image.Type.Simple;
                imagenVistaPreviaTanque.color = Color.white;
            }

            if (textoDecalPreview != null) textoDecalPreview.text = SimbolosCalcomania[iCalcomania];
            if (textoNumeroPreview != null) textoNumeroPreview.text = NumerosTanque[iNumero];
            if (imagenBanderaPreview != null) imagenBanderaPreview.color = ColoresBandera[iBandera];
        }

        // ------------------------------------------------------------------
        // OVERLAY DE CONFIRMACIÓN (usado por "Borrar script").
        // ------------------------------------------------------------------

        private void ConstruirOverlayConfirmarBorrado(Transform padre)
        {
            overlayConfirmarBorrado = CrearRectElastico(padre, "OverlayConfirmarBorrado",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, 0.75f), null);

            var caja = CrearRect(overlayConfirmarBorrado.transform, "Caja", Vector2.zero, new Vector2(420, 180), colorPanel);
            var cajaRect = caja.GetComponent<RectTransform>();
            cajaRect.anchorMin = new Vector2(0.5f, 0.5f);
            cajaRect.anchorMax = new Vector2(0.5f, 0.5f);
            cajaRect.pivot = new Vector2(0.5f, 0.5f);
            cajaRect.anchoredPosition = Vector2.zero;

            CrearTexto(caja.transform, "Mensaje", "¿Borrar todo el contenido del editor?",
                new Vector2(20, 20), new Vector2(380, 60), 18, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            var botonSi = CrearRect(caja.transform, "BotonSi", new Vector2(30, 110), new Vector2(170, 50), colorBotonAccentoRojo);
            var siBtn = botonSi.AddComponent<Button>();
            siBtn.targetGraphic = botonSi.GetComponent<Image>();
            siBtn.onClick.AddListener(ConfirmarBorrado);
            CrearTexto(botonSi.transform, "Texto", "SÍ, BORRAR", Vector2.zero, new Vector2(170, 50),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            var botonNo = CrearRect(caja.transform, "BotonNo", new Vector2(220, 110), new Vector2(170, 50), colorBoton);
            var noBtn = botonNo.AddComponent<Button>();
            noBtn.targetGraphic = botonNo.GetComponent<Image>();
            noBtn.onClick.AddListener(() => overlayConfirmarBorrado.SetActive(false));
            CrearTexto(botonNo.transform, "Texto", "CANCELAR", Vector2.zero, new Vector2(170, 50),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            overlayConfirmarBorrado.SetActive(false);
        }

        // ------------------------------------------------------------------
        // OVERLAY "GUARDAR COMO" / "ABRIR" (usado por GUARDAR SCRIPT y CARGAR SCRIPT).
        // ------------------------------------------------------------------

        private void ConstruirOverlayNombreArchivo(Transform padre)
        {
            overlayNombreArchivo = CrearRectElastico(padre, "OverlayNombreArchivo",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, 0.75f), null);

            var caja = CrearRect(overlayNombreArchivo.transform, "Caja", Vector2.zero, new Vector2(460, 230), colorPanel);
            var cajaRect = caja.GetComponent<RectTransform>();
            cajaRect.anchorMin = new Vector2(0.5f, 0.5f);
            cajaRect.anchorMax = new Vector2(0.5f, 0.5f);
            cajaRect.pivot = new Vector2(0.5f, 0.5f);
            cajaRect.anchoredPosition = Vector2.zero;

            tituloOverlayNombre = CrearTexto(caja.transform, "Titulo", "GUARDAR SCRIPT COMO",
                new Vector2(20, 20), new Vector2(420, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            campoNombreOverlay = CrearCampoTextoUnaLinea(caja.transform, "CampoNombre", "nombre_del_script",
                new Vector2(20, 65), new Vector2(420, 42));

            var botonConfirmar = CrearRect(caja.transform, "BotonConfirmar", new Vector2(30, 155), new Vector2(200, 50), colorBotonAccentoVerde);
            var confirmarBtn = botonConfirmar.AddComponent<Button>();
            confirmarBtn.targetGraphic = botonConfirmar.GetComponent<Image>();
            confirmarBtn.onClick.AddListener(() => accionConfirmarNombreArchivo?.Invoke(campoNombreOverlay.text));
            textoBotonConfirmarOverlay = CrearTexto(botonConfirmar.transform, "Texto", "GUARDAR", Vector2.zero, new Vector2(200, 50),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            var botonCancelar = CrearRect(caja.transform, "BotonCancelar", new Vector2(250, 155), new Vector2(200, 50), colorBoton);
            var cancelarBtn = botonCancelar.AddComponent<Button>();
            cancelarBtn.targetGraphic = botonCancelar.GetComponent<Image>();
            cancelarBtn.onClick.AddListener(() => overlayNombreArchivo.SetActive(false));
            CrearTexto(botonCancelar.transform, "Texto", "CANCELAR", Vector2.zero, new Vector2(200, 50),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            overlayNombreArchivo.SetActive(false);
        }

        private void AbrirOverlayNombreArchivo(string titulo, string textoBoton, string nombrePrellenado, Action<string> alConfirmar)
        {
            tituloOverlayNombre.text = titulo;
            textoBotonConfirmarOverlay.text = textoBoton;
            campoNombreOverlay.text = nombrePrellenado ?? "";
            accionConfirmarNombreArchivo = alConfirmar;
            overlayNombreArchivo.SetActive(true);
            campoNombreOverlay.Select();
            campoNombreOverlay.ActivateInputField();
        }

        // ------------------------------------------------------------------
        // ROSA DE LOS VIENTOS.
        // ------------------------------------------------------------------

        private void ConstruirPanelRosaVientos(Transform padre)
        {
            panelRosaVientos = CrearRectAncladoEsquina(padre, "PanelRosaVientos",
                new Vector2(1, 0), 15, 55, 150, 185, colorPanel, spriteMarcoPanel);

            CrearTexto(panelRosaVientos.transform, "Titulo", "DIRECCIÓN",
                new Vector2(10, 10), new Vector2(130, 22), 14, FontStyle.Bold, TextAnchor.MiddleCenter, colorBotonAccentoRojo);

            AgregarBracketsDeEsquina(panelRosaVientos.transform, colorAcentoMilitar, tamano: 14f, grosor: 2f);

            textoInstruccionPendienteDireccion = CrearTexto(panelRosaVientos.transform, "Pendiente", "—",
                new Vector2(10, 32), new Vector2(130, 18), 11, FontStyle.Normal, TextAnchor.MiddleCenter, colorTextoSecundario);

            CrearBotonDireccion(panelRosaVientos.transform, "N", new Vector2(55, 55), () => OnSeleccionarDireccion('N'));
            CrearBotonDireccion(panelRosaVientos.transform, "O", new Vector2(15, 95), () => OnSeleccionarDireccion('O'));
            CrearBotonDireccion(panelRosaVientos.transform, "E", new Vector2(95, 95), () => OnSeleccionarDireccion('E'));
            CrearBotonDireccion(panelRosaVientos.transform, "S", new Vector2(55, 135), () => OnSeleccionarDireccion('S'));
        }

        private void CrearBotonDireccion(Transform padre, string letra, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            var go = CrearRect(padre, $"BotonDireccion_{letra}", posicion, new Vector2(40, 40), colorBoton);
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(accion);
            CrearTexto(go.transform, "Texto", letra, Vector2.zero, new Vector2(40, 40),
                18, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
        }

        private bool BuscarInstruccionPendiente(string texto, out string comandoEncontrado, out int indice, out int longitud)
        {
            comandoEncontrado = null;
            indice = -1;
            longitud = 0;

            foreach (var comando in comandosQueRequierenDireccion)
            {
                if (string.IsNullOrWhiteSpace(comando)) continue;

                foreach (Match match in Regex.Matches(texto, $@"{Regex.Escape(comando)}\([NSEO]?\)"))
                {
                    if (match.Index > indice)
                    {
                        comandoEncontrado = comando;
                        indice = match.Index;
                        longitud = match.Length;
                    }
                }
            }

            return comandoEncontrado != null;
        }

        private void ActualizarDeteccionDireccion()
        {
            bool hayInstruccion = BuscarInstruccionPendiente(campoEditor.text, out _, out int indice, out int longitud);
            textoInstruccionPendienteDireccion.text = hayInstruccion
                ? $"Editando: {campoEditor.text.Substring(indice, longitud)}"
                : "—";
        }

        private void OnSeleccionarDireccion(char letra)
        {
            string texto = campoEditor.text;
            if (!BuscarInstruccionPendiente(texto, out string comando, out int indice, out int longitud))
                return;

            string reemplazo = $"{comando}({letra})";
            texto = texto.Remove(indice, longitud).Insert(indice, reemplazo);
            campoEditor.text = texto;
            ActualizarEditorTrasCambio();
        }

        // ------------------------------------------------------------------
        // HELPERS DE UI.
        // ------------------------------------------------------------------

        private GameObject CrearRect(Transform padre, string nombre, Vector2 posicion, Vector2 tamano, Color color,
            Sprite sprite = null, bool sliced = true)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();

            if (sprite != null)
            {
                imagen.sprite = sprite;
                imagen.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                imagen.color = Color.white;
            }
            else
            {
                imagen.color = color;
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = tamano;
            rect.anchoredPosition = new Vector2(posicion.x, -posicion.y);

            return go;
        }

        private GameObject CrearRectElastico(Transform padre, string nombre, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 margenDesdeInicio, Vector2 margenDesdeFin, Color color, Sprite sprite, bool sliced = true)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();

            if (sprite != null)
            {
                imagen.sprite = sprite;
                imagen.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                imagen.color = Color.white;
            }
            else
            {
                imagen.color = color;
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(margenDesdeInicio.x, margenDesdeInicio.y);
            rect.offsetMax = new Vector2(margenDesdeFin.x, margenDesdeFin.y);

            return go;
        }

        private GameObject CrearRectElasticoLocal(Transform padre, string nombre, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 margenIzqAbajo, Vector2 margenDerArriba, Color color, Sprite sprite, bool sliced = true)
        {
            return CrearRectElastico(padre, nombre, anchorMin, anchorMax,
                new Vector2(margenIzqAbajo.x, margenIzqAbajo.y),
                new Vector2(-margenDerArriba.x, -margenDerArriba.y),
                color, sprite, sliced);
        }

        private GameObject CrearRectAncladoEsquina(Transform padre, string nombre, Vector2 esquina,
            float margenX, float margenY, float ancho, float alto, Color color, Sprite sprite, bool sliced = true)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();
            if (sprite != null) { imagen.sprite = sprite; imagen.type = sliced ? Image.Type.Sliced : Image.Type.Simple; imagen.color = Color.white; }
            else imagen.color = color;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = esquina;
            rect.anchorMax = esquina;

            float offInicioX = esquina.x >= 1f ? -(margenX + ancho) : margenX;
            float offFinX = esquina.x >= 1f ? -margenX : margenX + ancho;
            float offInicioY = esquina.y >= 1f ? -(margenY + alto) : margenY;
            float offFinY = esquina.y >= 1f ? -margenY : margenY + alto;

            rect.offsetMin = new Vector2(offInicioX, offInicioY);
            rect.offsetMax = new Vector2(offFinX, offFinY);

            return go;
        }

        // ------------------------------------------------------------------
        // DECORACIÓN ESTILO MILITAR — nativa, sin imágenes (brackets tipo HUD,
        // franjas de acento, viñeta de fondo).
        // ------------------------------------------------------------------

        // Franja fina horizontal, pegada bajo el título de un panel (estilo "galón").
        // 'yDesdeArriba' es la distancia en unidades desde arriba del panel (mismo
        // sistema de coordenadas que CrearTexto/CrearRect).
        private void AgregarFranjaAcento(Transform panel, float yDesdeArriba, float grosor = 3f, float margenLateral = 15f)
        {
            CrearRectElastico(panel, "FranjaAcento",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(margenLateral, -(yDesdeArriba + grosor)), new Vector2(-margenLateral, -yDesdeArriba),
                colorAcentoMilitar, null);
        }

        // Agrega 4 "brackets" tipo mira táctica en las esquinas del panel (2 barras
        // finas en L por esquina). Funciona sin importar el tamaño real del panel,
        // porque cada bracket se ancla directamente a su propia esquina.
        private void AgregarBracketsDeEsquina(Transform panel, Color color, float tamano = 22f, float grosor = 3f, float margen = 8f)
        {
            AgregarBracketEsquina(panel, new Vector2(0, 0), tamano, grosor, margen, color);
            AgregarBracketEsquina(panel, new Vector2(1, 0), tamano, grosor, margen, color);
            AgregarBracketEsquina(panel, new Vector2(0, 1), tamano, grosor, margen, color);
            AgregarBracketEsquina(panel, new Vector2(1, 1), tamano, grosor, margen, color);
        }

        private void AgregarBracketEsquina(Transform panel, Vector2 esquina, float tamano, float grosor, float margen, Color color)
        {
            float signoX = esquina.x >= 1f ? -1f : 1f;
            float signoY = esquina.y >= 1f ? -1f : 1f;
            var posicion = new Vector2(signoX * margen, signoY * margen);

            CrearSegmentoBracket(panel, "BracketH", esquina, new Vector2(tamano, grosor), posicion, color);
            CrearSegmentoBracket(panel, "BracketV", esquina, new Vector2(grosor, tamano), posicion, color);
        }

        private void CrearSegmentoBracket(Transform panel, string nombre, Vector2 esquina, Vector2 tamano, Vector2 posicion, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(panel, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = esquina;
            rect.anchorMax = esquina;
            rect.pivot = esquina;
            rect.sizeDelta = tamano;
            rect.anchoredPosition = posicion;
        }

        // Viñeta sutil (oscurece las esquinas/bordes de la pantalla) generada en
        // runtime, como la del degradado de MenuPrincipal — le da un poco de
        // profundidad al fondo plano sin necesitar ninguna imagen.
        private static Sprite spriteVinetaCache;

        private static Sprite ObtenerSpriteVineta()
        {
            if (spriteVinetaCache != null) return spriteVinetaCache;

            const int n = 128;
            var textura = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < n; y++)
            {
                float v = (y / (float)(n - 1)) * 2f - 1f; // -1..1
                for (int x = 0; x < n; x++)
                {
                    float u = (x / (float)(n - 1)) * 2f - 1f; // -1..1
                    float distancia = Mathf.Sqrt(u * u + v * v) / 1.41421f; // 0 en el centro, 1 en la esquina.
                    float alfa = Mathf.Clamp01(Mathf.Pow(distancia, 2.2f)) * 0.55f;
                    textura.SetPixel(x, y, new Color(0f, 0f, 0f, alfa));
                }
            }
            textura.Apply();

            spriteVinetaCache = Sprite.Create(textura, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return spriteVinetaCache;
        }

        private Text CrearTexto(Transform padre, string nombre, string contenido, Vector2 posicion, Vector2 tamano,
            int fontSize, FontStyle estilo, TextAnchor alineacion, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var texto = go.AddComponent<Text>();
            texto.text = contenido;
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = fontSize;
            texto.fontStyle = estilo;
            texto.alignment = alineacion;
            texto.color = color;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = tamano;
            rect.anchoredPosition = new Vector2(posicion.x, -posicion.y);

            return texto;
        }

        // Editor con: gutter de números de línea a la izquierda, InputField real
        // (texto invisible, solo para input/cursor) + overlay con colores de sintaxis
        // encima. Todo dentro de un contenedor que es el que realmente cambia de alto
        // y el que mueve el ScrollRect — así el gutter y el overlay scrollean en
        // sincronía con el código.
        private const float ANCHO_NUMEROS_LINEA = 45f;

        private InputField CrearCampoTextoMultilineaConScroll(Transform padre, string nombre, string textoInicial, ScrollRect scrollRect)
        {
            // Contenedor: esto es lo que el ScrollRect mueve. Ancho = 100% del padre,
            // alto inicial 200 (se recalcula en ActualizarEditorTrasCambio).
            var contenedorGo = new GameObject($"{nombre}_Contenedor");
            contenedorGo.transform.SetParent(padre, false);
            contenedorEditorRect = contenedorGo.AddComponent<RectTransform>();
            contenedorEditorRect.anchorMin = new Vector2(0f, 1f);
            contenedorEditorRect.anchorMax = new Vector2(1f, 1f);
            contenedorEditorRect.pivot = new Vector2(0.5f, 1f);
            contenedorEditorRect.anchoredPosition = Vector2.zero;
            contenedorEditorRect.sizeDelta = new Vector2(0f, 200f);

            // --- Columna de números de línea ---
            var numerosGo = new GameObject("NumerosLinea");
            numerosGo.transform.SetParent(contenedorGo.transform, false);
            textoNumerosLinea = numerosGo.AddComponent<Text>();
            textoNumerosLinea.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoNumerosLinea.fontSize = 16;
            textoNumerosLinea.alignment = TextAnchor.UpperRight;
            textoNumerosLinea.color = colorTextoSecundario;
            textoNumerosLinea.horizontalOverflow = HorizontalWrapMode.Overflow;
            textoNumerosLinea.verticalOverflow = VerticalWrapMode.Overflow;
            textoNumerosLinea.raycastTarget = false;
            textoNumerosLinea.text = "1";
            var numerosRect = numerosGo.GetComponent<RectTransform>();
            numerosRect.anchorMin = new Vector2(0f, 1f);
            numerosRect.anchorMax = new Vector2(0f, 1f);
            numerosRect.pivot = new Vector2(0f, 1f);
            numerosRect.sizeDelta = new Vector2(ANCHO_NUMEROS_LINEA - 10f, 50f);
            numerosRect.anchoredPosition = new Vector2(5f, -5f);

            // Línea vertical fina separando los números del código (como en cualquier IDE).
            var separadorGo = new GameObject("Separador");
            separadorGo.transform.SetParent(contenedorGo.transform, false);
            var separadorImg = separadorGo.AddComponent<Image>();
            separadorImg.color = new Color(1f, 1f, 1f, 0.08f);
            separadorImg.raycastTarget = false;
            var separadorRect = separadorGo.GetComponent<RectTransform>();
            separadorRect.anchorMin = new Vector2(0f, 0f);
            separadorRect.anchorMax = new Vector2(0f, 1f);
            separadorRect.pivot = new Vector2(0f, 0f);
            separadorRect.sizeDelta = new Vector2(1f, 0f);
            separadorRect.anchoredPosition = new Vector2(ANCHO_NUMEROS_LINEA, 0f);

            // --- InputField real: estirado dentro del contenedor, inset a la derecha
            // del gutter de números. Al llenar TODO el alto del contenedor (stretch
            // vertical), no hace falta reajustar su tamaño por separado: crece solo
            // cuando ActualizarEditorTrasCambio cambia el alto del contenedor. ---
            var go = new GameObject(nombre);
            go.transform.SetParent(contenedorGo.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(ANCHO_NUMEROS_LINEA + 5f, 0f);
            rect.offsetMax = Vector2.zero;

            // Texto real del InputField: queda con ALPHA 0 (invisible). Sigue existiendo
            // y sigue siendo lo que define dónde está el cursor y qué carácter hay en
            // cada posición — solo que lo que el jugador VE es el overlay de al lado
            // (textoResaltado), que tiene los colores de sintaxis.
            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 16;
            texto.color = new Color(colorTexto.r, colorTexto.g, colorTexto.b, 0f); // Invisible a propósito.
            texto.alignment = TextAnchor.UpperLeft;
            texto.supportRichText = false;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = new Vector2(10, 5);
            textoRect.offsetMax = new Vector2(-10, -5);

            // Overlay con resaltado de sintaxis: mismo tamaño/posición exacta que el
            // texto real de arriba (mismos offsets), para quedar pixel-alineado. Con
            // rich text activado y sin capturar clics (raycastTarget=false), así los
            // clics le siguen llegando al InputField que está debajo.
            var overlayGo = new GameObject("TextoResaltado");
            overlayGo.transform.SetParent(go.transform, false);
            textoResaltado = overlayGo.AddComponent<Text>();
            textoResaltado.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoResaltado.fontSize = 16;
            textoResaltado.color = colorTexto;
            textoResaltado.alignment = TextAnchor.UpperLeft;
            textoResaltado.supportRichText = true;
            textoResaltado.horizontalOverflow = HorizontalWrapMode.Wrap;
            textoResaltado.verticalOverflow = VerticalWrapMode.Overflow;
            textoResaltado.raycastTarget = false;
            var overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = new Vector2(10, 5);
            overlayRect.offsetMax = new Vector2(-10, -5);

            var campo = go.AddComponent<InputFieldPorLinea>();
            campo.textComponent = texto;
            campo.lineType = InputField.LineType.MultiLineNewline;
            campo.text = textoInicial;
            campo.onBackspacePorLinea = OnBorrarUltimaInstruccion;
            campo.onEnterPorLinea = InsertarNuevaLineaConIndentacion;

            // Cursor bien visible: blanco brillante y un poco más grueso que el default
            // (antes, sin 'customCaretColor', el cursor podía heredar un color casi
            // invisible sobre fondo oscuro, o directamente el color transparente del
            // texto real de arriba).
            campo.customCaretColor = true;
            campo.caretColor = Color.white;
            campo.caretWidth = 2;
            campo.selectionColor = new Color(0.3f, 0.55f, 0.9f, 0.5f);

            scrollRect.content = contenedorEditorRect;

            return campo;
        }

        // Subclase de InputField que intercepta Retroceso (Backspace, borra la línea
        // completa) y Enter (inserta salto de línea CON indentación automática), en
        // vez del comportamiento normal carácter-por-carácter de un InputField.
        private class InputFieldPorLinea : InputField
        {
            public System.Action onBackspacePorLinea;
            public System.Action onEnterPorLinea;

            public override void OnUpdateSelected(BaseEventData eventData)
            {
                if (!isFocused) return;

                bool huboEvento = false;
                var evt = new Event();
                while (Event.PopEvent(evt))
                {
                    if (evt.rawType == EventType.KeyDown && evt.keyCode == KeyCode.Backspace)
                    {
                        huboEvento = true;
                        onBackspacePorLinea?.Invoke();
                        continue;
                    }

                    if (evt.rawType == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                    {
                        huboEvento = true;
                        onEnterPorLinea?.Invoke();
                        continue;
                    }

                    if (evt.rawType == EventType.KeyDown)
                    {
                        huboEvento = true;
                        var estado = KeyPressed(evt);
                        if (estado == EditState.Finish)
                        {
                            DeactivateInputField();
                            break;
                        }
                    }
                }

                if (huboEvento)
                    UpdateLabel();

                eventData.Use();
            }
        }

        private InputField CrearCampoTextoUnaLinea(Transform padre, string nombre, string textoPlaceholder,
            Vector2 posicion, Vector2 tamano)
        {
            var go = CrearRect(padre, nombre, posicion, tamano, new Color(1f, 1f, 1f, 0.06f));

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 15;
            texto.color = colorTexto;
            texto.alignment = TextAnchor.MiddleLeft;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = new Vector2(8, 0);
            textoRect.offsetMax = new Vector2(-8, 0);

            var campo = go.AddComponent<InputField>();
            campo.textComponent = texto;
            campo.lineType = InputField.LineType.SingleLine;
            campo.text = textoPlaceholder;
            campo.customCaretColor = true;
            campo.caretColor = Color.white;
            campo.caretWidth = 2;

            return campo;
        }

        private void CrearBotonAccion(Transform padre, string nombre, string titulo, string subtitulo,
            Vector2 posicion, Vector2 tamano, Color color, UnityEngine.Events.UnityAction accion, bool colorEsAccento = false)
        {
            var go = colorEsAccento
                ? CrearRect(padre, nombre, posicion, tamano, color)
                : CrearRect(padre, nombre, posicion, tamano, color, spriteFondoBoton);
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(accion);

            CrearTexto(go.transform, "Titulo", titulo, new Vector2(15, 10), new Vector2(tamano.x - 30, 30),
                18, FontStyle.Bold, TextAnchor.MiddleLeft, colorTexto);

            if (!string.IsNullOrEmpty(subtitulo))
            {
                CrearTexto(go.transform, "Subtitulo", subtitulo, new Vector2(15, 42), new Vector2(tamano.x - 30, 24),
                    13, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);
            }
        }

        private void CrearFlecha(Transform padre, string nombre, string simbolo, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            var go = CrearRect(padre, nombre, posicion, new Vector2(40, 40), new Color(0, 0, 0, 0));
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(accion);

            CrearTexto(go.transform, "Texto", simbolo, Vector2.zero, new Vector2(40, 40),
                26, FontStyle.Bold, TextAnchor.MiddleCenter, colorBotonAccentoRojo);
        }

        // ------------------------------------------------------------------
        // ACCIONES
        // ------------------------------------------------------------------

        private string RutaCarpetaScripts()
        {
            string ruta = Path.Combine(Application.persistentDataPath, carpetaScripts);
            Directory.CreateDirectory(ruta);
            return ruta;
        }

        private string SanearNombreArchivo(string nombreCrudo)
        {
            string nombre = string.IsNullOrWhiteSpace(nombreCrudo) ? "script_sin_nombre" : nombreCrudo.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }

        private void OnGuardarScript()
        {
            AbrirOverlayNombreArchivo("GUARDAR SCRIPT COMO", "GUARDAR", nombreArchivoActual, GuardarArchivoComo);
        }

        private void OnCargarScript()
        {
            AbrirOverlayNombreArchivo("ABRIR SCRIPT", "ABRIR", nombreArchivoActual, CargarArchivoDesde);
        }

        private void GuardarArchivoComo(string nombreCrudo)
        {
            try
            {
                string nombre = SanearNombreArchivo(nombreCrudo);
                string ruta = Path.Combine(RutaCarpetaScripts(), nombre + ".txt");
                File.WriteAllText(ruta, campoEditor.text);
                nombreArchivoActual = nombre;
                overlayNombreArchivo.SetActive(false);
                ActualizarTextoEstado($"Guardado como \"{nombre}.txt\"", esError: false);
            }
            catch (Exception e)
            {
                ActualizarTextoEstado($"No se pudo guardar ({e.Message})", esError: true);
            }
        }

        private void CargarArchivoDesde(string nombreCrudo)
        {
            try
            {
                string nombre = SanearNombreArchivo(nombreCrudo);
                string ruta = Path.Combine(RutaCarpetaScripts(), nombre + ".txt");
                if (!File.Exists(ruta))
                {
                    ActualizarTextoEstado($"No existe \"{nombre}.txt\"", esError: true);
                    return;
                }
                campoEditor.text = File.ReadAllText(ruta);
                ActualizarEditorTrasCambio();
                nombreArchivoActual = nombre;
                overlayNombreArchivo.SetActive(false);
                ActualizarTextoEstado($"Cargado \"{nombre}.txt\"", esError: false);
            }
            catch (Exception e)
            {
                ActualizarTextoEstado($"No se pudo cargar ({e.Message})", esError: true);
            }
        }

        private void OnVolverAlMenu()
        {
            SceneManager.LoadScene(nombreEscenaMenuPrincipal);
        }

        private void OnBorrarScript()
        {
            overlayConfirmarBorrado.SetActive(true);
        }

        private void ConfirmarBorrado()
        {
            campoEditor.text = "";
            ActualizarEditorTrasCambio();
            overlayConfirmarBorrado.SetActive(false);
            ActualizarTextoEstado("LISTO", esError: false);
        }

        private void OnIniciarEjecucion()
        {
            string texto = campoEditor.text;

            if (string.IsNullOrWhiteSpace(texto))
            {
                ActualizarTextoEstado("El script está vacío", esError: true);
                return;
            }

            try
            {
                _ = TankProgramParser.Parse(texto);
            }
            catch (Exception e)
            {
                ActualizarTextoEstado($"Sintaxis inválida — {e.Message}", esError: true);
                return;
            }

            UltimoScriptValidado = texto;

            string nombre = string.IsNullOrEmpty(nombreArchivoActual)
                ? $"script_{DateTime.Now:HHmmss}"
                : nombreArchivoActual;
            var entrada = new EntradaHistorial
            {
                nombre = nombre,
                fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                contenido = texto
            };
            historial.Add(entrada);
            ReconstruirListaHistorial();

            ActualizarTextoEstado($"Ejecutado \"{nombre}\" correctamente", esError: false);
        }

        private void OnLimpiarHistorial()
        {
            historial.Clear();
            ReconstruirListaHistorial();
        }

        private void OnSeleccionarHistorial(EntradaHistorial entrada)
        {
            campoEditor.text = entrada.contenido;
            ActualizarEditorTrasCambio();
            nombreArchivoActual = entrada.nombre;
            ActualizarTextoEstado($"Cargado del historial: \"{entrada.nombre}\"", esError: false);
        }

        // Las flechas < > de Vista Previa avanzan las 5 categorías a la vez (un
        // "siguiente conjunto" rápido), en vez de una sola. Sirve para recorrer
        // combinaciones rápido sin tener que tocar cada swatch por separado.
        private static readonly (string grupo, int cantidad)[] CategoriasSkin =
        {
            ("Patron", 4), ("Color", 4), ("Calcomania", 4), ("Numero", 4), ("Bandera", 5)
        };

        private void OnCambiarSkin(int direccion)
        {
            foreach (var (grupo, cantidad) in CategoriasSkin)
            {
                int actual = seleccionActual.TryGetValue(grupo, out var v) ? v : 0;
                int nuevo = ((actual + direccion) % cantidad + cantidad) % cantidad;
                seleccionActual[grupo] = nuevo;
                ResaltarSeleccion(grupo);
            }

            RegenerarVisualesPatron();
            ActualizarVistaPreviaTanque();
        }

        private void OnSeleccionarSwatch(string grupo, int indice)
        {
            seleccionActual[grupo] = indice;
            ResaltarSeleccion(grupo);

            // El patrón se pinta con el color principal, así que si lo que cambió fue
            // justo el color, hay que regenerar las 4 texturas de patrón también.
            if (grupo == "Color") RegenerarVisualesPatron();

            ActualizarVistaPreviaTanque();
        }
    }
}