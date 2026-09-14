using System;
using System.Collections;
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
    // Flujo de programación de tanques (resumen):
    //  1. Escribís el script del tanque en el editor.
    //  2. Tocás "GUARDAR" (pie de la Terminal): valida la sintaxis, lo agrega al
    //     Historial como "Tanque N" y cuenta ese tanque como programado. El editor se
    //     limpia solo después de cada guardado, así podés seguir directo con el
    //     próximo tanque (mientras no se llegue al mínimo jugable, esto es obligatorio).
    //  3. "ELIMINAR SCRIPT DE TANQUE" (arriba a la derecha de la Terminal) alterna un
    //     modo donde aparece un "-" junto a cada tanque del Historial, para borrar el
    //     que quieras.
    //  4. "INICIAR" (barra superior) arranca la partida — exige que ya se haya
    //     alcanzado el mínimo de tanques programados.
    //  El botón "GUARDAR SCRIPT" (.TXT, en la barra superior) es independiente de todo
    //  esto: sirve para exportar el contenido actual del editor a un archivo con
    //  cualquier nombre que el jugador quiera, para reutilizarlo más adelante.
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
        [Tooltip("Color de los brackets tipo HUD en las esquinas de cada panel y de las franjas de acento bajo los títulos.")]
        public Color colorAcentoMilitar = new Color(0.85f, 0.65f, 0.15f);
        [Tooltip("Si está activo, se agrega una viñeta sutil (oscurece los bordes) sobre el fondo.")]
        public bool usarVinetaDeFondo = true;

        [Header("Arte real (opcional). Arrastra tus PNG acá; si dejas un campo vacío, se usa el color de arriba.")]
        public Sprite spriteFondoPantalla;
        public Sprite spriteMarcoPanel;      // Se reutiliza en la barra superior y los 4 paneles.
        public Sprite spriteFondoBoton;      // Se reutiliza en los 5 botones de acción.
        public Sprite spriteIconoHistorial;  // Se reutiliza en cada fila del historial.

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

        [Header("Vista previa 3D del tanque (opcional)")]
        [Tooltip("Si asignás un prefab acá (ej. el Tank_006 del asset pack), se muestra el modelo 3D real (renderizado por una cámara aparte a una textura) en vez de la silueta dibujada por código.")]
        public GameObject prefabTanquePreview;
        [Tooltip("Rotación inicial del modelo dentro del 'escenario' de la vista previa, para elegir un buen ángulo de cámara.")]
        public Vector3 rotacionInicialTanque = new Vector3(10f, 200f, 0f);
        public bool rotarTanquePreview = true;
        public float velocidadRotacionPreview = 20f;
        [Tooltip("Nombres (o parte del nombre) de las mallas del modelo que NO deben pintarse con la skin — ej. orugas, ruedas, vidrios. Sin distinguir mayúsculas/minúsculas.")]
        public string[] partesExcluidasDeSkin = { "track", "wheel", "glass", "rueda", "oruga", "vidrio" };

        [Header("Control de cámara con mouse (arrastrar = orbitar, rueda = zoom)")]
        public float distanciaInicialCamara = 4.5f;
        public float elevacionInicialCamara = 15f;
        public float distanciaMinCamara = 1.8f;
        public float distanciaMaxCamara = 9f;
        public float velocidadOrbita = 0.3f;
        public float velocidadZoomCamara = 0.6f;

        [Header("Programación de tanques (mínimo/máximo)")]
        [Tooltip("No se puede jugar con menos tanques que este número; se fuerza a seguir programando hasta llegar acá.")]
        public int cantidadMinimaTanquesParaJugar = 2;
        [Tooltip("Tope de tanques que se pueden programar en total.")]
        public int cantidadMaximaTanques = 6;

        [Header("Tamaño del tablero")]
        [Tooltip("Tamaño NxN sugerido al entrar a la pantalla (se ajusta solo hacia arriba si queda por debajo del mínimo permitido).")]
        public int tamanoTableroInicial = 8;
        private const int TAMANO_TABLERO_MAXIMO = 20;

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

        // Overlay "Guardar como" / "Abrir": para EXPORTAR el script actual del editor a
        // un .txt con cualquier nombre. Independiente del Historial (que usa nombres
        // automáticos "Tanque N").
        private GameObject overlayNombreArchivo;
        private InputField campoNombreOverlay;
        private Text tituloOverlayNombre;
        private Text textoBotonConfirmarOverlay;
        private Action<string> accionConfirmarNombreArchivo;
        private string nombreArchivoActual = "";

        // Rosa de los vientos: vive DENTRO del panel de la Terminal, abajo a la derecha,
        // de forma permanente.
        private GameObject panelRosaVientos;
        private Text textoInstruccionPendienteDireccion;

        // Botón "Eliminar script de tanque" (ahora vive como uno de los 5 botones
        // principales de la barra superior, en lugar de "Borrar script" — ver
        // ConstruirBarraSuperior). Alterna un modo donde aparece un "-" junto a cada
        // tanque del Historial para poder borrarlo individualmente. Ver
        // ActualizarBotonEliminarScript() / OnAlternarModoEliminar().
        private Button botonEliminarScriptRef;
        private Text textoBotonEliminarScript;
        private bool modoEliminarActivo;

        // Un script por tanque ya programado y validado, en el orden en que se programaron.
        private readonly List<string> scriptsPorTanque = new List<string>();

        // Control de tamaño de tablero (NxN): ahora vive como un "slot" más dentro de
        // la fila de botones de acción (ver ConstruirControlTamanoTableroCompacto).
        private int tamanoTablero;
        private Text textoTamanoTablero;

        private int ultimaPosicionCursorConocida;

        private static readonly string[] PalabrasClave =
        {
            "INICIO", "IF", "FIN", "MOV", "AMT", "MINA", "MISIL", "RADAR",
            "ESCUDO", "ESPERAR", "DAÑAR", "DAÑO", "BUCLE"
        };

        private static readonly Color[] ColoresPrincipales =
        {
            new Color(0.30f, 0.36f, 0.22f),
            new Color(0.55f, 0.47f, 0.33f),
            new Color(0.35f, 0.36f, 0.38f),
            new Color(0.20f, 0.28f, 0.34f),
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
        private readonly Dictionary<string, List<Image>> swatchesPorGrupo = new Dictionary<string, List<Image>>();
        private readonly Dictionary<string, List<Image>> contenidoSwatchesPorGrupo = new Dictionary<string, List<Image>>();
        private readonly Dictionary<string, int> seleccionActual = new Dictionary<string, int>();

        private Image imagenCascoTanque;
        private Image imagenTorretaTanque;
        private Image imagenCanonTanque;
        private Text textoDecalPreview;
        private Text textoNumeroPreview;
        private Image imagenBanderaPreview;

        private Transform tanquePreviewInstancia;
        private RenderTexture renderTexturaPreview;

        private class EntradaHistorial
        {
            public string nombre;
            public string fechaHora;
            public string contenido;
        }

        public static List<string> ScriptsTanquesPartida { get; private set; } = new List<string>();

        private void Start()
        {
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

        private void Update()
        {
            if (campoEditor != null && campoEditor.isFocused)
                ultimaPosicionCursorConocida = campoEditor.caretPosition;

            if (rotarTanquePreview && tanquePreviewInstancia != null)
                tanquePreviewInstancia.Rotate(Vector3.up, velocidadRotacionPreview * Time.deltaTime, Space.World);
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
            scaler.referenceResolution = new Vector2(1536, 1024);
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

            tamanoTablero = tamanoTableroInicial;
            ActualizarTamanoMinimoTablero(scriptsPorTanque.Count);

            ConstruirPanelHistorial(canvasGo.transform);
            ConstruirPanelInstrucciones(canvasGo.transform);
            ConstruirPanelTerminal(canvasGo.transform);
            ConstruirPanelVistaPrevia(canvasGo.transform);
            ConstruirPanelPersonalizarSkin(canvasGo.transform);
            ConstruirOverlayConfirmarBorrado(canvasGo.transform);
            ConstruirOverlayNombreArchivo(canvasGo.transform);

            ReconstruirListaHistorial();
            ActualizarBotonEliminarScript();
            ActualizarTextoEstado("LISTO", esError: false);
        }

        // ------------------------------------------------------------------
        // BARRA SUPERIOR: título + 5 botones de acción + control de tablero (6to slot).
        // Ancla elástica: fija al borde superior, estirada a todo el ancho.
        //
        // Los 6 slots (5 botones + el control de tablero) se reparten por FRACCIÓN
        // de ancho (no en píxeles fijos), así la fila siempre ocupa TODO el ancho
        // disponible de la barra sin importar la resolución de pantalla.
        // ------------------------------------------------------------------

        private const float ALTURA_BARRA = 235f;
        private const float MARGEN = 15f;
        private const float ANCHO_HISTORIAL = 325f;
        private const float ANCHO_INSTRUCCIONES = 325f;
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

            const int totalSlots = 6;
            float alto = 115f, y = 100f;

            CrearBotonAccionSlot(barra.transform, "BotonGuardarScript", "GUARDAR SCRIPT", ".TXT",
                0, totalSlots, y, alto, colorBoton, OnGuardarScript);
            CrearBotonAccionSlot(barra.transform, "BotonCargarScript", "CARGAR SCRIPT", ".TXT",
                1, totalSlots, y, alto, colorBoton, OnCargarScript);
            CrearBotonAccionSlot(barra.transform, "BotonVolverMenu", "MENÚ DE INICIO", "VOLVER",
                2, totalSlots, y, alto, colorBotonAccentoRojo, OnVolverAlMenu, colorEsAccento: true);

            // Antes acá vivía "BORRAR SCRIPT" y "ELIMINAR SCRIPT DE TANQUE" era un
            // botón chico arriba a la derecha de la Terminal. Se intercambiaron de
            // lugar: ahora "ELIMINAR SCRIPT DE TANQUE" es uno de los 5 botones
            // principales, y "BORRAR SCRIPT" pasó al rincón de la Terminal (ver
            // ConstruirPanelTerminal).
            var botonEliminarScript = CrearBotonAccionSlot(barra.transform, "BotonEliminarScriptTanque",
                "ELIMINAR SCRIPT DE TANQUE", "",
                3, totalSlots, y, alto, colorBotonAccentoRojo, OnAlternarModoEliminar, colorEsAccento: true);
            botonEliminarScriptRef = botonEliminarScript.GetComponent<Button>();
            textoBotonEliminarScript = botonEliminarScript.transform.Find("Titulo").GetComponent<Text>();

            CrearBotonAccionSlot(barra.transform, "BotonIniciarPartida", "INICIAR", "PARTIDA",
                4, totalSlots, y, alto, colorBotonAccentoVerde, OnIniciarPartida, colorEsAccento: true);

            ConstruirControlTamanoTableroCompacto(barra.transform, 5, totalSlots, y, alto);
        }

        // Control de tamaño de tablero: mismo "slot" que un botón más de la fila de
        // arriba (6to lugar, a la derecha de INICIAR PARTIDA), con una flecha "-" a la
        // izquierda, un ícono de grilla 3x3 (representa el tablero) en el centro, el
        // "N x N" arriba del ícono (con texto más grande), y una flecha "+" a la derecha.
        private void ConstruirControlTamanoTableroCompacto(Transform barra, int indice, int totalSlots, float y, float alto)
        {
            float anchorMinX = (float)indice / totalSlots;
            float anchorMaxX = (float)(indice + 1) / totalSlots;
            float offsetIzq = indice == 0 ? MARGEN : GAP / 2f;
            float offsetDer = indice == totalSlots - 1 ? -MARGEN : -GAP / 2f;

            var contenedor = CrearRectElastico(barra, "ControlTamanoTablero",
                new Vector2(anchorMinX, 1), new Vector2(anchorMaxX, 1),
                new Vector2(offsetIzq, -(y + alto)), new Vector2(offsetDer, -y),
                colorPanel, null);

            const float ladoFlecha = 34f;

            var botonMenos = CrearRect(contenedor.transform, "BotonMenos",
                new Vector2(4, (alto - ladoFlecha) / 2f), new Vector2(ladoFlecha, ladoFlecha), colorBoton);
            var btnMenos = botonMenos.AddComponent<Button>();
            btnMenos.targetGraphic = botonMenos.GetComponent<Image>();
            btnMenos.onClick.AddListener(() => CambiarTamanoTablero(-1));
            CrearTexto(botonMenos.transform, "Texto", "-", Vector2.zero, new Vector2(ladoFlecha, ladoFlecha),
                20, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            // Anclado a la esquina superior derecha del propio slot (en vez de
            // depender de un "ancho" fijo), así funciona con cualquier ancho de slot.
            var botonMas = CrearRectAncladoEsquina(contenedor.transform, "BotonMas",
                new Vector2(1, 1), 4, (alto - ladoFlecha) / 2f, ladoFlecha, ladoFlecha, colorBoton, null);
            var btnMas = botonMas.AddComponent<Button>();
            btnMas.targetGraphic = botonMas.GetComponent<Image>();
            btnMas.onClick.AddListener(() => CambiarTamanoTablero(1));
            CrearTexto(botonMas.transform, "Texto", "+", Vector2.zero, new Vector2(ladoFlecha, ladoFlecha),
                20, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            // Texto "N x N": ahora más grande (16, antes 13) y estirado/centrado a
            // todo el ancho del slot en vez de un ancho fijo.
            textoTamanoTablero = CrearTextoElastico(contenedor.transform, "TextoTamano", "7 x 7",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -(8 + 26)), new Vector2(0, -8),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            const float ladoIcono = 48f;
            CrearIconoGrid3x3Centrado(contenedor.transform, 38f, ladoIcono);
        }

        // Ícono de grilla 3x3 (representa visualmente "el tablero"), centrado
        // horizontalmente dentro de su padre (sin depender de un ancho fijo).
        private void CrearIconoGrid3x3Centrado(Transform padre, float yDesdeArriba, float lado)
        {
            const int celdas = 3;
            const float gap = 3f;
            float tamanoCelda = (lado - gap * (celdas - 1)) / celdas;

            for (int fila = 0; fila < celdas; fila++)
            {
                for (int columna = 0; columna < celdas; columna++)
                {
                    float offsetXDesdeCentro = (columna - 1) * (tamanoCelda + gap);
                    float offsetYDesdeArriba = yDesdeArriba + fila * (tamanoCelda + gap);

                    var go = new GameObject($"CeldaIcono_{fila}_{columna}");
                    go.transform.SetParent(padre, false);
                    var img = go.AddComponent<Image>();
                    img.color = colorAcentoMilitar;

                    var rect = go.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(tamanoCelda, tamanoCelda);
                    rect.anchoredPosition = new Vector2(offsetXDesdeCentro, -offsetYDesdeArriba);
                }
            }
        }

        // ------------------------------------------------------------------
        // PANEL IZQUIERDO: historial de scripts ejecutados.
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
            float anchoTexto = modoEliminarActivo ? 205 : 240;

            var fila = CrearRect(padre, $"Item_{entrada.nombre}", posicion, new Vector2(295, 55), new Color(0, 0, 0, 0));

            CrearRect(fila.transform, "Icono", new Vector2(0, 5), new Vector2(30, 30), colorTextoSecundario,
                spriteIconoHistorial, sliced: false);
            CrearTexto(fila.transform, "NombreArchivo", entrada.nombre, new Vector2(42, 0), new Vector2(anchoTexto, 26),
                16, FontStyle.Normal, TextAnchor.MiddleLeft, colorTexto);
            CrearTexto(fila.transform, "Fecha", entrada.fechaHora, new Vector2(42, 26), new Vector2(anchoTexto, 22),
                12, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);

            var boton = fila.AddComponent<Button>();
            boton.targetGraphic = fila.GetComponent<Image>();
            boton.onClick.AddListener(() => OnSeleccionarHistorial(entrada));

            if (modoEliminarActivo)
            {
                var botonEliminar = CrearRect(fila.transform, "BotonEliminar", new Vector2(295 - 34, 12), new Vector2(30, 30), colorBotonAccentoRojo);
                var btnEliminar = botonEliminar.AddComponent<Button>();
                btnEliminar.targetGraphic = botonEliminar.GetComponent<Image>();
                btnEliminar.onClick.AddListener(() => OnEliminarTanque(entrada));
                CrearTexto(botonEliminar.transform, "Texto", "-", Vector2.zero, new Vector2(30, 30),
                    20, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
            }
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
            boton.onClick.AddListener(() => InsertarEnEditor(fragmentoAInsertar));
            CrearTexto(go.transform, "Texto", etiqueta, Vector2.zero, new Vector2(295, 40),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
        }

        private void InsertarEnEditor(string fragmento)
        {
            string texto = campoEditor.text;
            int posicion = Mathf.Clamp(ultimaPosicionCursorConocida, 0, texto.Length);

            string nuevoTexto = texto.Insert(posicion, fragmento);
            campoEditor.text = nuevoTexto;
            int nuevaPosicion = posicion + fragmento.Length;

            ActualizarEditorSinMoverCursor();

            ultimaPosicionCursorConocida = nuevaPosicion;
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

            // "BORRAR SCRIPT": antes era uno de los 5 botones principales de la barra
            // superior; ahora vive acá, arriba a la derecha de la Terminal. Se
            // intercambió de lugar con "ELIMINAR SCRIPT DE TANQUE", que ahora es un
            // botón principal en la barra superior (ver ConstruirBarraSuperior).
            var botonBorrarScript = CrearRectAncladoEsquina(panel.transform, "BotonBorrarScript",
                new Vector2(1, 1), 15, 15, 240, 30, colorBotonAccentoRojo, null);
            var btnBorrarScript = botonBorrarScript.AddComponent<Button>();
            btnBorrarScript.targetGraphic = botonBorrarScript.GetComponent<Image>();
            btnBorrarScript.onClick.AddListener(OnBorrarScript);
            CrearTexto(botonBorrarScript.transform, "Texto", "BORRAR SCRIPT",
                Vector2.zero, new Vector2(240, 30), 12, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

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

            var botonGuardarTanque = CrearRectElastico(panel.transform, "BotonGuardarScriptTanque",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(15, 46), new Vector2(-175, 85),
                colorBoton, null);
            var btnGuardarTanque = botonGuardarTanque.AddComponent<Button>();
            btnGuardarTanque.targetGraphic = botonGuardarTanque.GetComponent<Image>();
            btnGuardarTanque.onClick.AddListener(OnGuardarScriptDelTanque);

            var textoGuardarTanqueGo = new GameObject("Texto");
            textoGuardarTanqueGo.transform.SetParent(botonGuardarTanque.transform, false);
            var textoGuardarTanque = textoGuardarTanqueGo.AddComponent<Text>();
            textoGuardarTanque.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoGuardarTanque.fontSize = 14;
            textoGuardarTanque.fontStyle = FontStyle.Bold;
            textoGuardarTanque.alignment = TextAnchor.MiddleCenter;
            textoGuardarTanque.color = colorTexto;
            textoGuardarTanque.text = "GUARDAR";
            var textoGuardarTanqueRect = textoGuardarTanqueGo.GetComponent<RectTransform>();
            textoGuardarTanqueRect.anchorMin = Vector2.zero;
            textoGuardarTanqueRect.anchorMax = Vector2.one;
            textoGuardarTanqueRect.offsetMin = Vector2.zero;
            textoGuardarTanqueRect.offsetMax = Vector2.zero;

            ConstruirPanelRosaVientos(panel.transform);

            campoEditor.onValueChanged.AddListener(_ =>
            {
                ActualizarDeteccionDireccion();
                ActualizarNumerosDeLinea();
                ActualizarResaltadoSintaxis();
            });

            ActualizarEditorTrasCambio();
        }

        private void ActualizarEditorTrasCambio()
        {
            campoEditor.caretPosition = campoEditor.text.Length;
            ultimaPosicionCursorConocida = campoEditor.caretPosition;
            ActualizarEditorSinMoverCursor();

            if (scrollTerminal != null)
                scrollTerminal.verticalNormalizedPosition = 0f;
        }

        private void ActualizarEditorSinMoverCursor()
        {
            float alturaPreferida = campoEditor.textComponent.preferredHeight + 20f;
            contenedorEditorRect.sizeDelta = new Vector2(contenedorEditorRect.sizeDelta.x, Mathf.Max(200f, alturaPreferida));

            Canvas.ForceUpdateCanvases();

            ActualizarDeteccionDireccion();
            ActualizarNumerosDeLinea();
            ActualizarResaltadoSintaxis();
        }

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

            codigo = Regex.Replace(codigo, PatronPalabrasClave, "<color=#4FC3F7>$1</color>");
            codigo = Regex.Replace(codigo, @"\(([NSEO])\)", "(<color=#FFB74D>$1</color>)");
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

            var areaGo = CrearRect(panel.transform, "AreaVistaPreviaTanque", new Vector2(15, 60), new Vector2(436, 220), colorFondoPantalla);

            if (prefabTanquePreview != null)
                ConstruirVistaPrevia3D(areaGo.transform);
            else
                ConstruirSiluetaTanque(areaGo.transform);

            textoDecalPreview = CrearTexto(areaGo.transform, "DecalPreview", SimbolosCalcomania[0],
                new Vector2(10, 10), new Vector2(50, 40), 26, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
            textoNumeroPreview = CrearTexto(areaGo.transform, "NumeroPreview", NumerosTanque[0],
                new Vector2(356, 190), new Vector2(70, 24), 18, FontStyle.Bold, TextAnchor.MiddleRight, colorTexto);
            var banderaGo = CrearRect(areaGo.transform, "BanderaPreview", new Vector2(396, 10), new Vector2(30, 20), ColoresBandera[0]);
            imagenBanderaPreview = banderaGo.GetComponent<Image>();

            CrearFlecha(panel.transform, "BotonSkinAnterior", "<", new Vector2(25, 150), () => OnCambiarSkin(-1));
            CrearFlecha(panel.transform, "BotonSkinSiguiente", ">", new Vector2(436 - 25, 150), () => OnCambiarSkin(1));
        }

        private void ConstruirVistaPrevia3D(Transform contenedorUI)
        {
            var escenario = new GameObject("EscenarioPreviaTanque");
            escenario.transform.position = new Vector3(500f, 0f, 500f);

            var instancia = Instantiate(prefabTanquePreview, escenario.transform);
            instancia.transform.localPosition = Vector3.zero;
            instancia.transform.localRotation = Quaternion.Euler(rotacionInicialTanque);
            tanquePreviewInstancia = instancia.transform;

            var luzGo = new GameObject("LuzPreviaTanque");
            luzGo.transform.SetParent(escenario.transform, false);
            luzGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.2f;

            var camaraGo = new GameObject("CamaraPreviaTanque");
            camaraGo.transform.SetParent(escenario.transform, false);
            var camara = camaraGo.AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = colorFondoPantalla;
            camara.fieldOfView = 30f;

            renderTexturaPreview = new RenderTexture(512, 512, 16);
            camara.targetTexture = renderTexturaPreview;

            var rawGo = new GameObject("RenderVistaPreviaTanque");
            rawGo.transform.SetParent(contenedorUI, false);
            var rawImg = rawGo.AddComponent<RawImage>();
            rawImg.texture = renderTexturaPreview;
            var rawRect = rawGo.GetComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            var controlador = rawGo.AddComponent<ControladorOrbitaCamara>();
            controlador.camara = camaraGo.transform;
            controlador.puntoMira = escenario.transform.position + Vector3.up * 0.5f;
            controlador.distanciaMin = distanciaMinCamara;
            controlador.distanciaMax = distanciaMaxCamara;
            controlador.velocidadRotacion = velocidadOrbita;
            controlador.velocidadZoom = velocidadZoomCamara;
            controlador.alInteractuar = () => rotarTanquePreview = false;
            controlador.alSoltar = () => rotarTanquePreview = true;
            controlador.Inicializar(0f, elevacionInicialCamara, distanciaInicialCamara);
        }

        private class ControladorOrbitaCamara : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
        {
            public Transform camara;
            public Vector3 puntoMira;
            public float distanciaMin = 1.5f;
            public float distanciaMax = 10f;
            public float velocidadRotacion = 0.3f;
            public float velocidadZoom = 0.5f;
            public System.Action alInteractuar;
            public System.Action alSoltar;

            private float azimut;
            private float elevacion;
            private float distancia;

            public void Inicializar(float azimutInicial, float elevacionInicial, float distanciaInicial)
            {
                azimut = azimutInicial;
                elevacion = elevacionInicial;
                distancia = Mathf.Clamp(distanciaInicial, distanciaMin, distanciaMax);
                AplicarTransform();
            }

            public void OnBeginDrag(PointerEventData eventData) => alInteractuar?.Invoke();

            public void OnDrag(PointerEventData eventData)
            {
                azimut += eventData.delta.x * velocidadRotacion;
                elevacion = Mathf.Clamp(elevacion - eventData.delta.y * velocidadRotacion, -85f, 85f);
                AplicarTransform();
            }

            public void OnEndDrag(PointerEventData eventData) => alSoltar?.Invoke();

            public void OnScroll(PointerEventData eventData)
            {
                distancia = Mathf.Clamp(distancia - eventData.scrollDelta.y * velocidadZoom, distanciaMin, distanciaMax);
                AplicarTransform();
            }

            private void AplicarTransform()
            {
                var rotacion = Quaternion.Euler(elevacion, azimut, 0f);
                var offset = rotacion * new Vector3(0f, 0f, -distancia);
                camara.position = puntoMira + offset;
                camara.LookAt(puntoMira);
            }
        }

        private void ConstruirSiluetaTanque(Transform contenedor)
        {
            const float centroX = 218f, centroY = 110f;
            const float anchoCasco = 260f, altoCasco = 90f;
            const float anchoOruga = 260f, altoOruga = 18f;
            const float ladoTorreta = 78f;
            const float anchoCanon = 130f, altoCanon = 16f;

            Color colorOrugas = new Color(0.16f, 0.16f, 0.17f);

            CrearRect(contenedor, "OrugaSuperior",
                new Vector2(centroX - anchoOruga / 2f, centroY - altoCasco / 2f - altoOruga),
                new Vector2(anchoOruga, altoOruga), colorOrugas);
            CrearRect(contenedor, "OrugaInferior",
                new Vector2(centroX - anchoOruga / 2f, centroY + altoCasco / 2f),
                new Vector2(anchoOruga, altoOruga), colorOrugas);

            var cascoGo = CrearRect(contenedor, "Casco",
                new Vector2(centroX - anchoCasco / 2f, centroY - altoCasco / 2f),
                new Vector2(anchoCasco, altoCasco), colorSwatch);
            imagenCascoTanque = cascoGo.GetComponent<Image>();

            var canonGo = CrearRect(contenedor, "Canon",
                new Vector2(centroX, centroY - altoCanon / 2f),
                new Vector2(anchoCanon, altoCanon), colorSwatch);
            imagenCanonTanque = canonGo.GetComponent<Image>();

            var torretaGo = CrearRect(contenedor, "Torreta",
                new Vector2(centroX - ladoTorreta / 2f, centroY - ladoTorreta / 2f),
                new Vector2(ladoTorreta, ladoTorreta), colorSwatch);
            imagenTorretaTanque = torretaGo.GetComponent<Image>();

            CrearTexto(torretaGo.transform, "DecalTorreta", SimbolosCalcomania[0],
                Vector2.zero, new Vector2(ladoTorreta, ladoTorreta), 24, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
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

        private void AplicarColoresReales(string grupo, Color[] colores)
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue(grupo, out var lista)) return;
            for (int i = 0; i < lista.Count && i < colores.Length; i++)
                lista[i].color = colores[i];
        }

        private void AplicarEtiquetas(string grupo, string[] etiquetas)
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue(grupo, out var lista)) return;
            for (int i = 0; i < lista.Count && i < etiquetas.Length; i++)
            {
                CrearTexto(lista[i].transform, "Etiqueta", etiquetas[i], Vector2.zero, new Vector2(45, 45),
                    18, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);
            }
        }

        private void RegenerarVisualesPatron()
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue("Patron", out var lista)) return;
            int iColor = seleccionActual.TryGetValue("Color", out var c) ? c : 0;
            Color colorBase = ColoresPrincipales[iColor];

            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].sprite = GenerarSpritePatron(i, colorBase);
                lista[i].type = Image.Type.Simple;
                lista[i].color = Color.white;
            }
        }

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

            return Sprite.Create(textura, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private void ActualizarVistaPreviaTanque()
        {
            int iPatron = seleccionActual.TryGetValue("Patron", out var p) ? p : 0;
            int iColor = seleccionActual.TryGetValue("Color", out var c) ? c : 0;
            int iCalcomania = seleccionActual.TryGetValue("Calcomania", out var d) ? d : 0;
            int iNumero = seleccionActual.TryGetValue("Numero", out var num) ? num : 0;
            int iBandera = seleccionActual.TryGetValue("Bandera", out var b) ? b : 0;

            if (imagenCascoTanque != null)
            {
                var spritePatron = GenerarSpritePatron(iPatron, ColoresPrincipales[iColor]);
                foreach (var img in new[] { imagenCascoTanque, imagenTorretaTanque, imagenCanonTanque })
                {
                    img.sprite = spritePatron;
                    img.type = Image.Type.Simple;
                    img.color = Color.white;
                }
            }
            else if (tanquePreviewInstancia != null)
            {
                AplicarSkinAlModelo3D(iPatron, iColor);
            }

            if (textoDecalPreview != null) textoDecalPreview.text = SimbolosCalcomania[iCalcomania];
            if (textoNumeroPreview != null) textoNumeroPreview.text = NumerosTanque[iNumero];
            if (imagenBanderaPreview != null) imagenBanderaPreview.color = ColoresBandera[iBandera];
        }

        private void AplicarSkinAlModelo3D(int iPatron, int iColor)
        {
            var colorBase = ColoresPrincipales[iColor];
            var texturaPatron = GenerarSpritePatron(iPatron, colorBase).texture;

            foreach (var renderer in tanquePreviewInstancia.GetComponentsInChildren<Renderer>())
            {
                if (DebeExcluirseDeSkin(renderer.gameObject.name)) continue;

                foreach (var material in renderer.materials)
                {
                    material.mainTexture = texturaPatron;
                    material.color = Color.white;
                }
            }
        }

        private bool DebeExcluirseDeSkin(string nombreObjeto)
        {
            string nombreMin = nombreObjeto.ToLowerInvariant();
            foreach (var parte in partesExcluidasDeSkin)
            {
                if (!string.IsNullOrWhiteSpace(parte) && nombreMin.Contains(parte.ToLowerInvariant()))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // OVERLAY DE CONFIRMACIÓN GENÉRICO (usado por "Borrar script" en la
        // Terminal y por "Limpiar historial" en el panel de Historial).
        // ------------------------------------------------------------------

        private Text textoMensajeConfirmarBorrado;
        private Action accionConfirmarBorrado;

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

            textoMensajeConfirmarBorrado = CrearTexto(caja.transform, "Mensaje", "¿Borrar todo el contenido del editor?",
                new Vector2(20, 20), new Vector2(380, 60), 18, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            var botonSi = CrearRect(caja.transform, "BotonSi", new Vector2(30, 110), new Vector2(170, 50), colorBotonAccentoRojo);
            var siBtn = botonSi.AddComponent<Button>();
            siBtn.targetGraphic = botonSi.GetComponent<Image>();
            siBtn.onClick.AddListener(() =>
            {
                accionConfirmarBorrado?.Invoke();
                overlayConfirmarBorrado.SetActive(false);
            });
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

        // Abre el overlay de confirmación genérico con un mensaje y una acción a
        // ejecutar si el usuario confirma tocando "SÍ, BORRAR".
        private void AbrirOverlayConfirmarBorrado(string mensaje, Action alConfirmar)
        {
            textoMensajeConfirmarBorrado.text = mensaje;
            accionConfirmarBorrado = alConfirmar;
            overlayConfirmarBorrado.SetActive(true);
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

            int nuevaPosicion = indice + reemplazo.Length;
            campoEditor.caretPosition = nuevaPosicion;
            ultimaPosicionCursorConocida = nuevaPosicion;

            ActualizarEditorSinMoverCursor();
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
        // DECORACIÓN ESTILO MILITAR — nativa, sin imágenes.
        // ------------------------------------------------------------------

        private void AgregarFranjaAcento(Transform panel, float yDesdeArriba, float grosor = 3f, float margenLateral = 15f)
        {
            CrearRectElastico(panel, "FranjaAcento",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(margenLateral, -(yDesdeArriba + grosor)), new Vector2(-margenLateral, -yDesdeArriba),
                colorAcentoMilitar, null);
        }

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
                float v = (y / (float)(n - 1)) * 2f - 1f;
                for (int x = 0; x < n; x++)
                {
                    float u = (x / (float)(n - 1)) * 2f - 1f;
                    float distancia = Mathf.Sqrt(u * u + v * v) / 1.41421f;
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

        // Variante "elástica" de CrearTexto: en vez de un tamaño fijo en píxeles, se
        // ancla con fracciones de sus padre (anchorMin/anchorMax) más un offset en
        // píxeles, para que el texto se estire y quede centrado sin importar el
        // ancho real de su contenedor (usado en los botones y controles de la barra
        // superior, cuyo ancho ahora es una fracción del ancho de pantalla).
        private Text CrearTextoElastico(Transform padre, string nombre, string contenido,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
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
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            return texto;
        }

        private const float ANCHO_NUMEROS_LINEA = 45f;

        private InputField CrearCampoTextoMultilineaConScroll(Transform padre, string nombre, string textoInicial, ScrollRect scrollRect)
        {
            var contenedorGo = new GameObject($"{nombre}_Contenedor");
            contenedorGo.transform.SetParent(padre, false);
            contenedorEditorRect = contenedorGo.AddComponent<RectTransform>();
            contenedorEditorRect.anchorMin = new Vector2(0f, 1f);
            contenedorEditorRect.anchorMax = new Vector2(1f, 1f);
            contenedorEditorRect.pivot = new Vector2(0.5f, 1f);
            contenedorEditorRect.anchoredPosition = Vector2.zero;
            contenedorEditorRect.sizeDelta = new Vector2(0f, 200f);

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

            var go = new GameObject(nombre);
            go.transform.SetParent(contenedorGo.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(ANCHO_NUMEROS_LINEA + 5f, 0f);
            rect.offsetMax = Vector2.zero;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 16;
            texto.color = new Color(colorTexto.r, colorTexto.g, colorTexto.b, 0f);
            texto.alignment = TextAnchor.UpperLeft;
            texto.supportRichText = false;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = new Vector2(10, 5);
            textoRect.offsetMax = new Vector2(-10, -5);

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

            campo.customCaretColor = true;
            campo.caretColor = Color.white;
            campo.caretWidth = 2;
            campo.selectionColor = new Color(0.3f, 0.55f, 0.9f, 0.5f);

            scrollRect.content = contenedorEditorRect;

            return campo;
        }

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

        // Botón de acción "slot" de la barra superior: en vez de un ancho fijo en
        // píxeles, ocupa una fracción (1/totalSlots) del ancho total de la barra, así
        // los 5 botones + el control de tablero siempre abarcan TODO el ancho
        // disponible, sea cual sea la resolución de pantalla.
        private GameObject CrearBotonAccionSlot(Transform barra, string nombre, string titulo, string subtitulo,
            int indice, int totalSlots, float y, float alto, Color color, UnityEngine.Events.UnityAction accion,
            bool colorEsAccento = false)
        {
            float anchorMinX = (float)indice / totalSlots;
            float anchorMaxX = (float)(indice + 1) / totalSlots;
            float offsetIzq = indice == 0 ? MARGEN : GAP / 2f;
            float offsetDer = indice == totalSlots - 1 ? -MARGEN : -GAP / 2f;

            var go = colorEsAccento
                ? CrearRectElastico(barra, nombre, new Vector2(anchorMinX, 1), new Vector2(anchorMaxX, 1),
                    new Vector2(offsetIzq, -(y + alto)), new Vector2(offsetDer, -y), color, null)
                : CrearRectElastico(barra, nombre, new Vector2(anchorMinX, 1), new Vector2(anchorMaxX, 1),
                    new Vector2(offsetIzq, -(y + alto)), new Vector2(offsetDer, -y), color, spriteFondoBoton);

            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(accion);

            CrearTextoElastico(go.transform, "Titulo", titulo,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(15, -(10 + 30)), new Vector2(-15, -10),
                18, FontStyle.Bold, TextAnchor.MiddleLeft, colorTexto);

            if (!string.IsNullOrEmpty(subtitulo))
            {
                CrearTextoElastico(go.transform, "Subtitulo", subtitulo,
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(15, -(42 + 24)), new Vector2(-15, -42),
                    13, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);
            }

            return go;
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
            AbrirOverlayConfirmarBorrado("¿Borrar todo el contenido del editor?", ConfirmarBorrado);
        }

        private void ConfirmarBorrado()
        {
            campoEditor.text = "";
            ActualizarEditorTrasCambio();
            ActualizarTextoEstado("LISTO", esError: false);
        }

        // Valida el script del tanque actual y lo agrega al Historial como
        // "Tanque N". El editor se limpia SIEMPRE después de un guardado exitoso,
        // para seguir directo con el próximo tanque sin necesitar un botón aparte.
        private void OnGuardarScriptDelTanque()
        {
            if (scriptsPorTanque.Count >= cantidadMaximaTanques)
            {
                ActualizarTextoEstado($"Ya programaste el máximo de {cantidadMaximaTanques} tanques", esError: true);
                return;
            }

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

            int tanquesProgramados = scriptsPorTanque.Count + 1;
            string nombreTanque = $"Tanque {tanquesProgramados}";

            var entrada = new EntradaHistorial
            {
                nombre = nombreTanque,
                fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                contenido = texto
            };
            historial.Add(entrada);
            ReconstruirListaHistorial();

            scriptsPorTanque.Add(texto);
            ActualizarTamanoMinimoTablero(tanquesProgramados);

            campoEditor.text = "";
            ActualizarEditorTrasCambio();

            if (tanquesProgramados < cantidadMinimaTanquesParaJugar)
            {
                int siguienteTanque = tanquesProgramados + 1;
                ActualizarTextoEstado(
                    $"{nombreTanque} guardado. Debes programar el Tanque {siguienteTanque} (mínimo {cantidadMinimaTanquesParaJugar} tanques para jugar)",
                    esError: false);
            }
            else if (tanquesProgramados >= cantidadMaximaTanques)
            {
                ActualizarTextoEstado($"{nombreTanque} guardado. Alcanzaste el máximo de {cantidadMaximaTanques} tanques.", esError: false);
            }
            else
            {
                ActualizarTextoEstado($"{nombreTanque} guardado. Podés seguir programando otro tanque o iniciar la partida.", esError: false);
            }

            ActualizarBotonEliminarScript();
        }

        private void OnIniciarPartida()
        {
            if (scriptsPorTanque.Count < cantidadMinimaTanquesParaJugar)
            {
                ActualizarTextoEstado(
                    $"Debes programar al menos {cantidadMinimaTanquesParaJugar} tanques antes de iniciar la partida",
                    esError: true);
                return;
            }

            ScriptsTanquesPartida = new List<string>(scriptsPorTanque);

            ActualizarTextoEstado(
                $"Partida iniciada con {scriptsPorTanque.Count} tanques (tablero {tamanoTablero}x{tamanoTablero})",
                esError: false);
        }

        private void OnLimpiarHistorial()
        {
            AbrirOverlayConfirmarBorrado("¿Borrar todo el historial de scripts programados?", ConfirmarLimpiarHistorial);
        }

        private void ConfirmarLimpiarHistorial()
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

        // ------------------------------------------------------------------
        // ELIMINAR SCRIPT DE TANQUE / TAMAÑO DE TABLERO.
        // ------------------------------------------------------------------

        // Ahora que "ELIMINAR SCRIPT DE TANQUE" es uno de los botones principales de
        // la barra superior (no un botón que aparece/desaparece), en vez de
        // ocultarlo se lo deshabilita (interactable) mientras no haya tanques
        // programados, para no dejar un hueco vacío en la fila de botones.
        private void ActualizarBotonEliminarScript()
        {
            if (botonEliminarScriptRef == null) return;

            bool hayTanques = scriptsPorTanque.Count > 0;
            botonEliminarScriptRef.interactable = hayTanques;

            if (!hayTanques) modoEliminarActivo = false;

            if (textoBotonEliminarScript != null)
                textoBotonEliminarScript.text = modoEliminarActivo ? "LISTO" : "ELIMINAR SCRIPT DE TANQUE";
        }

        private void OnAlternarModoEliminar()
        {
            modoEliminarActivo = !modoEliminarActivo;
            ActualizarBotonEliminarScript();
            ReconstruirListaHistorial();
        }

        // Borra un tanque puntual: lo saca del Historial Y de la lista de scripts que
        // realmente cuentan como "programados" (scriptsPorTanque), buscándolo por el
        // contenido exacto del script.
        private void OnEliminarTanque(EntradaHistorial entrada)
        {
            historial.Remove(entrada);
            scriptsPorTanque.Remove(entrada.contenido);

            ReconstruirListaHistorial();
            ActualizarBotonEliminarScript();
            ActualizarTamanoMinimoTablero(scriptsPorTanque.Count);
            ActualizarTextoEstado($"\"{entrada.nombre}\" eliminado ({scriptsPorTanque.Count} tanques programados)", esError: false);
        }

        private int ObtenerTamanoMinimoTablero()
        {
            int tanquesDeReferencia = Mathf.Max(scriptsPorTanque.Count, cantidadMinimaTanquesParaJugar);
            return Mathf.Min(tanquesDeReferencia + 1, TAMANO_TABLERO_MAXIMO);
        }

        private void ActualizarTamanoMinimoTablero(int tanquesProgramados)
        {
            int minimo = ObtenerTamanoMinimoTablero();
            if (tamanoTablero < minimo)
                tamanoTablero = minimo;
            ActualizarTextoTamanoTablero();
        }

        private void ActualizarTextoTamanoTablero()
        {
            if (textoTamanoTablero != null)
                textoTamanoTablero.text = $"{tamanoTablero} x {tamanoTablero}";
        }

        private void CambiarTamanoTablero(int delta)
        {
            int minimo = ObtenerTamanoMinimoTablero();
            tamanoTablero = Mathf.Clamp(tamanoTablero + delta, minimo, TAMANO_TABLERO_MAXIMO);
            ActualizarTextoTamanoTablero();
        }

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

            if (grupo == "Color") RegenerarVisualesPatron();

            ActualizarVistaPreviaTanque();
        }
    }
}