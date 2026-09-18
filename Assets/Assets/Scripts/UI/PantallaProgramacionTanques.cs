using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TanksGame.Core;
using TanksGame.Language;

namespace TanksGame.UI
{
    // Pantalla "Programación de Tanques" (barra superior + historial + terminal/editor +
    // vista previa del tanque + personalización de skin).
    //
    // Flujo de programación de tanques (resumen):
    //  1. Escribes el script del tanque en el editor.
    //  2. Tocas "GUARDAR" (pie de la Terminal): valida la sintaxis, lo agrega al
    //     Historial como "Tanque N" y cuenta ese tanque como programado. El editor se
    //     limpia solo después de cada guardado, así puedes seguir directo con el
    //     próximo tanque (mientras no se llegue al mínimo jugable, esto es obligatorio).
    //  3. "ELIMINAR SCRIPT DE TANQUE" (arriba a la derecha de la Terminal) alterna un
    //     modo donde aparece un "-" junto a cada tanque del Historial, para borrar el
    //     que quieras.
    //  4. "INICIAR" (barra superior) arranca la partida — exige que ya se haya
    //     alcanzado el mínimo de tanques programados.
    //  Los botones "GUARDAR SCRIPT" y "CARGAR SCRIPT" (.TXT, barra superior) son
    //  independientes de todo esto y usan el explorador de archivos NATIVO del
    //  sistema operativo (Windows, Linux, macOS) vía el plugin SFB
    //  (StandaloneFileBrowser): "GUARDAR SCRIPT" deja elegir cuál script del
    //  Historial exportar y dónde guardarlo en la máquina del jugador; "CARGAR
    //  SCRIPT" abre cualquier .txt de la máquina y lo vuelca en la Terminal para
    //  modificarlo y volver a guardarlo. El Historial en sí (panel izquierdo) es
    //  permanente: se guarda como JSON en persistentDataPath y sobrevive a cerrar
    //  el juego o a volver a darle Play en el editor de Unity.
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

        [Header("Arte real (opcional). Arrastra tus PNG aquí; si dejas un campo vacío, se usa el color de arriba.")]
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

        [Header("Iniciar partida")]
        [Tooltip("Nombre de la escena de juego (la de GameManager). Debe estar agregada en File > Build Profiles > Scene List, igual que la del menú.")]
        public string nombreEscenaJuego = "Juego";

        [Header("Rosa de los vientos (dirección)")]
        [Tooltip("Nombres de instrucciones que requieren una dirección como argumento, ej. \"MOV\" para MOV(). Se detecta cuando aparecen escritas con paréntesis vacíos: MOV()")]
        public string[] comandosQueRequierenDireccion = { "MOV", "AMT", "RADAR", "MISIL" };

        [Header("Música de fondo (solo esta pantalla)")]
        [Tooltip("Se reproduce en bucle mientras estás en esta pantalla. Se detiene sola al volver al menú (no usa DontDestroyOnLoad).")]
        public AudioClip musicaFondo;
        [Range(0f, 1f)]
        public float volumenMusica = 0.5f;

        [Header("Vista previa 3D del tanque (opcional)")]
        [Tooltip("Si asignas un prefab aquí (ej. el Tank_006 del asset pack), se muestra el modelo 3D real (renderizado por una cámara aparte a una textura) en vez de la silueta dibujada por código.")]
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
        [Tooltip("No se puede jugar con menos tanques que este número; se fuerza a seguir programando hasta llegar aquí.")]
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
        private GameObject overlayConfirmarBorrado;
        private AudioSource audioSourceMusica;

        // Overlay "Guardar como" / "Abrir": para EXPORTAR el script actual del editor a
        // un .txt con cualquier nombre. Independiente del Historial (que usa nombres
        // automáticos "Tanque N").
        private GameObject overlayNombreArchivo;
        private Text tituloOverlayNombre;
        private string nombreArchivoActual = "";

        // Si no es null, significa que el contenido del editor vino de tocar un
        // tanque en el Historial (ver OnSeleccionarHistorial): la próxima vez que
        // se toque GUARDAR, se ACTUALIZA ese tanque en vez de crear uno nuevo (ver
        // OnGuardarScriptDelTanque / ActualizarTanqueExistente).
        private EntradaHistorial entradaEnEdicion;

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
        private Text textoBotonGuardarTanque;
        private RectTransform rectBotonGuardarTanque;
        private Button botonCancelarEdicionRef;
        private Text textoBotonCancelarEdicion;
        private string textoEditorAntesDeEditarHistorial = "";
        private string nombreArchivoAntesDeEditarHistorial = "";
        private bool modoEliminarActivo;

        // Un script por tanque ya programado y validado, en el orden en que se programaron.
        private readonly List<string> scriptsPorTanque = new List<string>();

        // En paralelo a scriptsPorTanque (mismo índice = mismo tanque): la
        // personalización (patrón/color/calcomanía/número/bandera) que estaba
        // elegida en "PERSONALIZAR SKIN" en el momento de guardar ese tanque. Antes
        // esto nunca se guardaba en ningún lado, así que la escena de Juego no
        // tenía cómo pintar cada tanque distinto.
        private readonly List<TanqueSkinDatos> skinsPorTanque = new List<TanqueSkinDatos>();

        // Control de tamaño de tablero (NxN): ahora vive como un "slot" más dentro de
        // la fila de botones de acción (ver ConstruirControlTamanoTableroCompacto).
        private int tamanoTablero;
        private Text textoTamanoTablero;

        private int ultimaPosicionCursorConocida;
        private bool sincronizandoCursorProgramaticamente;

        // Estado anterior del editor. Se usa para interceptar Backspace sin
        // intervenir en OnUpdateSelected(), dejando que InputField gestione
        // normalmente flechas, Enter, Home/End, selección, etc.
        private string textoAnteriorEditor = string.Empty;
        private int caretAnteriorEditor;
        private int anclaAnteriorEditor;
        private int focoAnteriorEditor;
        private bool procesandoCambioBackspace;

        private static readonly string[] PalabrasClave =
        {
            "INICIO", "IF", "FIN", "MOV", "AMT", "MINA", "MISIL", "RADAR",
            "ESCUDO", "ESPERAR", /*"DAÑAR", "DAÑO",*/ "BUCLE"
        };

        private static readonly Color[] ColoresPrincipales =
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

        private static readonly string[] SimbolosCalcomania = { "★", "✖", "●", "▲", "☠", "⚔", "✦", "⚡" };

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
        private GameObject escenarioPreview;
        private RenderTexture renderTexturaPreview;
        private readonly Dictionary<string, Sprite> spritesPatronCache = new Dictionary<string, Sprite>();

        [Serializable]
        private class EntradaHistorial
        {
            public string nombre;
            public string fechaHora;
            public string contenido;

            // Personalización del tanque en el momento en que se guardó este
            // script (mismos índices que seleccionActual/ObtenerSkinActual). Viaja
            // con la entrada -- así, al tocarla en el Historial, se puede
            // reconstruir cómo se ve ese tanque en vez de perder la
            // personalización. Entradas viejas (guardadas antes de este campo)
            // simplemente quedan en 0 (primer patrón/color/calcomanía/bandera).
            public int patron;
            public int color;
            public int calcomania;
            public int bandera;
        }

        // Envoltorio porque JsonUtility no serializa listas en la raíz.
        [Serializable]
        private class HistorialGuardadoEnDisco
        {
            public List<EntradaHistorial> entradas = new List<EntradaHistorial>();
        }

        // El historial ahora es PERMANENTE: sobrevive a cerrar el juego y a volver a
        // darle Play en el editor de Unity. Se guarda como JSON en persistentDataPath
        // (misma carpeta que ScriptsTanques, pero un archivo aparte) y se recarga
        // completo en Start(). Cada cambio al historial (agregar, actualizar, borrar,
        // limpiar) llama a GuardarHistorialEnDisco() para que quede sincronizado.
        private const string NOMBRE_ARCHIVO_HISTORIAL = "historial_scripts.json";

        private string RutaArchivoHistorial() =>
            Path.Combine(RutaCarpetaScripts(), NOMBRE_ARCHIVO_HISTORIAL);

        private void GuardarHistorialEnDisco()
        {
            try
            {
                var datos = new HistorialGuardadoEnDisco { entradas = historial };
                string json = JsonUtility.ToJson(datos, prettyPrint: true);
                File.WriteAllText(RutaArchivoHistorial(), json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo guardar el historial en disco: {e.Message}");
            }
        }

        private void CargarHistorialDesdeDisco()
        {
            try
            {
                string ruta = RutaArchivoHistorial();
                if (!File.Exists(ruta)) return;

                string json = File.ReadAllText(ruta);
                var datos = JsonUtility.FromJson<HistorialGuardadoEnDisco>(json);
                if (datos?.entradas == null) return;

                historial.Clear();
                historial.AddRange(datos.entradas);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo cargar el historial guardado: {e.Message}");
            }
        }

        // Nombre único para una entrada nueva del historial: fecha y hora hasta
        // segundos + número de tanque. Antes se usaba solo "Tanque N", que se repetía
        // entre partidas -- ahora que el historial es permanente, dos sesiones
        // distintas programando un "Tanque 1" chocarían con el mismo nombre.
        private string GenerarNombreUnicoHistorial(int numeroTanque) =>
            $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_Tanque{numeroTanque}";

        private void OnDestroy()
        {
            if (renderTexturaPreview != null)
            {
                renderTexturaPreview.Release();
                Destroy(renderTexturaPreview);
                renderTexturaPreview = null;
            }

            if (escenarioPreview != null)
            {
                Destroy(escenarioPreview);
                escenarioPreview = null;
            }

            foreach (var sprite in spritesPatronCache.Values)
            {
                if (sprite != null)
                {
                    var texture = sprite.texture;
                    Destroy(sprite);
                    if (texture != null)
                        Destroy(texture);
                }
            }

            spritesPatronCache.Clear();
        }

        private void Start()
        {
            CargarHistorialDesdeDisco();

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
            if (!sincronizandoCursorProgramaticamente && campoEditor != null && campoEditor.isFocused)
            {
                int caretActual = campoEditor.caretPosition;

                // El EventSystem procesa las teclas después de Update(). Por eso aquí
                // vemos el resultado de las flechas del frame anterior y actualizamos
                // el ScrollRect solo cuando el caret realmente cambió.
                if (caretActual != ultimaPosicionCursorConocida)
                {
                    ultimaPosicionCursorConocida = caretActual;
                    AsegurarCursorVisible();
                }

                // Guardamos el estado anterior para el Backspace personalizado.
                caretAnteriorEditor = caretActual;
                anclaAnteriorEditor = campoEditor.selectionAnchorPosition;
                focoAnteriorEditor = campoEditor.selectionFocusPosition;
            }

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
            ActualizarEtiquetaBotonGuardar();
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

            const float ladoFlecha = 42f;

            var botonMenos = CrearRect(contenedor.transform, "BotonMenos",
                new Vector2(4, (alto - ladoFlecha) / 2f), new Vector2(ladoFlecha, ladoFlecha), colorBoton);
            var btnMenos = botonMenos.AddComponent<Button>();
            btnMenos.targetGraphic = botonMenos.GetComponent<Image>();
            btnMenos.onClick.AddListener(() => CambiarTamanoTablero(-1));
            CrearTexto(botonMenos.transform, "Texto", "-", Vector2.zero, new Vector2(ladoFlecha, ladoFlecha),
                26, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            // Anclado a la esquina superior derecha del propio slot (en vez de
            // depender de un "ancho" fijo), así funciona con cualquier ancho de slot.
            var botonMas = CrearRectAncladoEsquina(contenedor.transform, "BotonMas",
                new Vector2(1, 1), 4, (alto - ladoFlecha) / 2f, ladoFlecha, ladoFlecha, colorBoton, null);
            var btnMas = botonMas.AddComponent<Button>();
            btnMas.targetGraphic = botonMas.GetComponent<Image>();
            btnMas.onClick.AddListener(() => CambiarTamanoTablero(1));
            CrearTexto(botonMas.transform, "Texto", "+", Vector2.zero, new Vector2(ladoFlecha, ladoFlecha),
                26, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            // Texto "N x N": bien grande (26, antes 16) y estirado/centrado a todo el
            // ancho del slot en vez de un ancho fijo.
            textoTamanoTablero = CrearTextoElastico(contenedor.transform, "TextoTamano", "7 x 7",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -(6 + 38)), new Vector2(0, -6),
                26, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            const float ladoIcono = 62f;
            CrearIconoGrid3x3Centrado(contenedor.transform, 48f, ladoIcono);
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
                //("DAÑAR", "DAÑAR"),
                //("DAÑO", "DAÑO"),
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
    if (campoEditor == null || string.IsNullOrEmpty(fragmento)) return;

    string texto = campoEditor.text;
    int posicion = Mathf.Clamp(ObtenerCursorActual(), 0, texto.Length);

    // Regla permanente: una instrucción insertada con un botón siempre arranca su propia
    // línea. Solo se omite el salto en dos casos posicionales (no de estado):
    //  - la línea actual está vacía hasta el cursor (incluye el cuerpo indentado de un IF
    //    y el documento vacío): ya estamos en una línea libre, no hace falta otra.
    //  - el cursor está justo antes de un ")": estamos dentro de la condición de un IF,
    //    donde un salto de línea la partiría al medio.
    int inicioLinea = posicion > 0 ? texto.LastIndexOf('\n', posicion - 1) + 1 : 0;
    string antesEnLinea = texto.Substring(inicioLinea, posicion - inicioLinea);

    bool lineaLibre = string.IsNullOrWhiteSpace(antesEnLinea);
    bool dentroDeCondicion = posicion < texto.Length && texto[posicion] == ')';

    string prefijo = (lineaLibre || dentroDeCondicion) ? "" : "\n";

    campoEditor.text = texto.Insert(posicion, prefijo + fragmento);

    int indiceParentesis = fragmento.IndexOf("( )", StringComparison.Ordinal);
    int nuevaPosicion = indiceParentesis >= 0
        ? posicion + prefijo.Length + indiceParentesis + 2
        : posicion + prefijo.Length + fragmento.Length;

    ActualizarEditorSinMoverCursor();
    ultimaPosicionCursorConocida = nuevaPosicion;

    FijarCursorEnEditor(nuevaPosicion);
}

// Punto de entrada único para reposicionar el cursor tras cualquier edición nuestra
// (botón de instrucción, dirección, retroceso, Enter con indentación).
private void FijarCursorEnEditor(int posicion)
{
    if (campoEditor.isFocused)
    {
        // Ya estaba enfocado (típicamente: venimos de una tecla, no de un botón). Reactivar
        // igual con ActivateInputField() dispara la misma reactivación diferida que salta el
        // cursor al final del texto por un instante — así que si ya está enfocado, alcanza
        // con fijar la posición directamente, sin corrutina ni espera.
        int posicionClamp = Mathf.Clamp(posicion, 0, campoEditor.text.Length);
        campoEditor.selectionAnchorPosition = posicionClamp;
        campoEditor.selectionFocusPosition = posicionClamp;
        campoEditor.caretPosition = posicionClamp;
        campoEditor.ForceLabelUpdate();
        ultimaPosicionCursorConocida = posicionClamp;
        AsegurarCursorVisible();
        return;
    }

    StopAllCoroutines();
    StartCoroutine(EnfocarEditorYFijarCursor(posicion));
}

// Hace scroll dentro de la terminal lo mínimo necesario para que la línea del cursor
// quede visible — un InputField normal no hace esto solo dentro de un ScrollRect.
private void AsegurarCursorVisible()
        {
            if (scrollTerminal == null || campoEditor == null || contenedorEditorRect == null) return;

            string texto = campoEditor.text ?? string.Empty;
            int caret = Mathf.Clamp(campoEditor.caretPosition, 0, texto.Length);

            int lineaCaret = 0;
            for (int i = 0; i < caret; i++)
            {
                if (texto[i] == '\n')
                    lineaCaret++;
            }

            float alturaViewport = scrollTerminal.viewport != null
                ? scrollTerminal.viewport.rect.height
                : 0f;
            float alturaContenido = contenedorEditorRect.rect.height;
            float maxScrollPx = Mathf.Max(0f, alturaContenido - alturaViewport);
            if (maxScrollPx <= 0f) return;

            float topLinea = lineaCaret * ALTURA_LINEA_EDITOR;
            float bottomLinea = topLinea + ALTURA_LINEA_EDITOR;
            float scrollActualPx = (1f - scrollTerminal.verticalNormalizedPosition) * maxScrollPx;

            float nuevoScrollPx = scrollActualPx;
            if (topLinea < scrollActualPx)
                nuevoScrollPx = topLinea;
            else if (bottomLinea > scrollActualPx + alturaViewport)
                nuevoScrollPx = bottomLinea - alturaViewport;

            nuevoScrollPx = Mathf.Clamp(nuevoScrollPx, 0f, maxScrollPx);
            scrollTerminal.verticalNormalizedPosition = 1f - (nuevoScrollPx / maxScrollPx);
        }

        private IEnumerator EnfocarEditorYFijarCursor(int posicion)
{
    sincronizandoCursorProgramaticamente = true;
    try
    {
        campoEditor.ActivateInputField();

        int intentos = 0;
        while (!campoEditor.isFocused && intentos < 6)
        {
            yield return null;
            intentos++;
        }
        yield return null;

        int posicionClamp = Mathf.Clamp(posicion, 0, campoEditor.text.Length);
        campoEditor.selectionAnchorPosition = posicionClamp;
        campoEditor.selectionFocusPosition = posicionClamp;
        campoEditor.caretPosition = posicionClamp;
        campoEditor.ForceLabelUpdate();

        ultimaPosicionCursorConocida = posicionClamp;
    }
    finally
    {
        sincronizandoCursorProgramaticamente = false;
    }
    AsegurarCursorVisible();
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

            // GUARDAR/ACTUALIZAR y CANCELAR se reparten en dos mitades iguales el
            // espacio que va desde el borde izquierdo de la terminal (x=15) hasta
            // el borde izquierdo de la rosa de los vientos (panelRight - 165, ver
            // ConstruirPanelRosaVientos). El punto medio de ese tramo es
            // 0.5*ancho - 75; cada botón usa la mitad con un pequeño respiro de
            // 10px entre ambos, y CANCELAR termina justo donde empieza la rosa
            // (sin taparse ni superponerse).
            var botonGuardarTanque = CrearRectElastico(panel.transform, "BotonGuardarScriptTanque",
                new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(15, 46), new Vector2(-90, 85),
                colorBoton, null);
            rectBotonGuardarTanque = botonGuardarTanque.GetComponent<RectTransform>();
            var btnGuardarTanque = botonGuardarTanque.AddComponent<Button>();
            btnGuardarTanque.targetGraphic = botonGuardarTanque.GetComponent<Image>();
            btnGuardarTanque.onClick.AddListener(OnGuardarScriptDelTanque);

            textoBotonGuardarTanque = CrearTexto(
                botonGuardarTanque.transform, "Texto", "GUARDAR",
                Vector2.zero, Vector2.zero, 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, colorTexto);

            var textoRect = textoBotonGuardarTanque.rectTransform;
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;

            var botonCancelarEdicion = CrearRectElastico(panel.transform, "BotonCancelarEdicion",
                new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(-70, 46), new Vector2(-175, 85),
                colorBotonAccentoRojo, null);
            botonCancelarEdicionRef = botonCancelarEdicion.AddComponent<Button>();
            botonCancelarEdicionRef.targetGraphic = botonCancelarEdicion.GetComponent<Image>();
            botonCancelarEdicionRef.onClick.AddListener(OnCancelarEdicion);
            textoBotonCancelarEdicion = CrearTexto(
                botonCancelarEdicion.transform, "Texto", "CANCELAR",
                Vector2.zero, Vector2.zero, 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, colorTexto);
            botonCancelarEdicionRef.gameObject.SetActive(false);

            var textoCancelarRect = textoBotonCancelarEdicion.rectTransform;
            textoCancelarRect.anchorMin = Vector2.zero;
            textoCancelarRect.anchorMax = Vector2.one;
            textoCancelarRect.offsetMin = Vector2.zero;
            textoCancelarRect.offsetMax = Vector2.zero;

            ConstruirPanelRosaVientos(panel.transform);

            campoEditor.onValueChanged.AddListener(AlCambiarTextoEditor);

            textoAnteriorEditor = campoEditor.text;
            caretAnteriorEditor = campoEditor.caretPosition;
            anclaAnteriorEditor = campoEditor.selectionAnchorPosition;
            focoAnteriorEditor = campoEditor.selectionFocusPosition;

            ActualizarEditorTrasCambio();
        }

        private void AlCambiarTextoEditor(string nuevoTexto)
        {
            // InputField ya procesó el Backspace. Solo después de ese procesamiento
            // reemplazamos el borrado de UN carácter por el borrado de la instrucción
            // completa. No tocamos OnUpdateSelected(), que es quien mantiene la
            // navegación vertical/horizontal del InputField.
            bool backspacePresionado = Keyboard.current != null &&
                                       Keyboard.current.backspaceKey.isPressed;

            bool debeBorrarInstruccion = !procesandoCambioBackspace &&
                                         backspacePresionado &&
                                         campoEditor != null &&
                                         campoEditor.isFocused &&
                                         anclaAnteriorEditor == focoAnteriorEditor &&
                                         !string.IsNullOrEmpty(textoAnteriorEditor) &&
                                         nuevoTexto.Length == textoAnteriorEditor.Length - 1;

            if (debeBorrarInstruccion)
            {
                int caret = Mathf.Clamp(caretAnteriorEditor, 0, textoAnteriorEditor.Length);

                if (caret > 0)
                {
                    int inicioLinea = textoAnteriorEditor.LastIndexOf('\n', Mathf.Max(0, caret - 1)) + 1;
                    int inicioABorrar;

                    if (caret == inicioLinea)
                    {
                        inicioABorrar = inicioLinea - 1;
                    }
                    else
                    {
                        string antesEnLinea = textoAnteriorEditor.Substring(inicioLinea, caret - inicioLinea);
                        inicioABorrar = inicioLinea + EncontrarInicioTokenABorrar(antesEnLinea);
                    }

                    inicioABorrar = Mathf.Clamp(inicioABorrar, 0, caret);
                    string textoPersonalizado = textoAnteriorEditor.Remove(
                        inicioABorrar, caret - inicioABorrar);

                    procesandoCambioBackspace = true;
                    try
                    {
                        campoEditor.text = textoPersonalizado;
                    }
                    finally
                    {
                        procesandoCambioBackspace = false;
                    }

                    nuevoTexto = textoPersonalizado;
                    FijarCursorEnEditor(inicioABorrar);
                }
            }

            textoAnteriorEditor = campoEditor.text;
            caretAnteriorEditor = campoEditor.caretPosition;
            anclaAnteriorEditor = campoEditor.selectionAnchorPosition;
            focoAnteriorEditor = campoEditor.selectionFocusPosition;
            ultimaPosicionCursorConocida = caretAnteriorEditor;

            // Cada cambio de texto debe recalcular también la altura del contenido.
            // Este era el punto que hacía que, después de ~11 líneas, Enter siguiera
            // agregando texto pero el ScrollRect no tuviera contenido adicional al
            // cual desplazarse.
            ActualizarEditorSinMoverCursor();

            if (campoEditor.isFocused && !sincronizandoCursorProgramaticamente)
                AsegurarCursorVisible();
        }

        private void ActualizarEditorTrasCambio()
{
    campoEditor.caretPosition = campoEditor.text.Length;
    campoEditor.selectionAnchorPosition = campoEditor.caretPosition;
    campoEditor.selectionFocusPosition = campoEditor.caretPosition;
    campoEditor.ForceLabelUpdate();
    textoAnteriorEditor = campoEditor.text;
    caretAnteriorEditor = campoEditor.caretPosition;
    anclaAnteriorEditor = campoEditor.selectionAnchorPosition;
    focoAnteriorEditor = campoEditor.selectionFocusPosition;
    ultimaPosicionCursorConocida = campoEditor.caretPosition;
    ActualizarEditorSinMoverCursor();

    if (scrollTerminal != null)
        scrollTerminal.verticalNormalizedPosition = 1f; // antes era 0f — con la convención real de Unity, 1f es el tope
}

        private void ActualizarEditorSinMoverCursor()
        {
            if (campoEditor == null || contenedorEditorRect == null) return;

            // No dependemos de Text.preferredHeight para dimensionar el contenido.
            // Con un InputField multilinea dentro de un ScrollRect, ese valor puede
            // quedarse limitado por la altura actual del propio campo (en este caso
            // ~200 px, unas 11 líneas), haciendo que el caret visual parezca quedar
            // atrapado aunque el texto siga creciendo.
            int cantidadLineas = 1;
            string texto = campoEditor.text ?? string.Empty;
            for (int i = 0; i < texto.Length; i++)
            {
                if (texto[i] == '\n')
                    cantidadLineas++;
            }

            float alturaViewport = scrollTerminal != null && scrollTerminal.viewport != null
                ? scrollTerminal.viewport.rect.height
                : 200f;
            float alturaNecesaria = cantidadLineas * ALTURA_LINEA_EDITOR + 10f;
            float alturaContenido = Mathf.Max(alturaViewport, alturaNecesaria);

            contenedorEditorRect.sizeDelta = new Vector2(0f, alturaContenido);

            // El InputField y sus Text necesitan tener la nueva geometría antes de
            // recalcular el scroll.
            Canvas.ForceUpdateCanvases();
            campoEditor.ForceLabelUpdate();

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

        private string PatronInstruccionConParametro
        {
            get
            {
                var comandos = comandosQueRequierenDireccion == null
                    ? Array.Empty<string>()
                    : comandosQueRequierenDireccion
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(Regex.Escape)
                        .ToArray();

                return comandos.Length == 0
                    ? @"(?!x)x"
                    : @"(?<!\w)(" + string.Join("|", comandos) + @")\([NSEO]?\)$";
            }
        }

        private static readonly string PatronPalabraClaveFinal =
            @"\b(" + string.Join("|", PalabrasClave.Select(Regex.Escape)) + @")$";

        private int EncontrarInicioTokenABorrar(string antesEnLinea)
        {
            var match = Regex.Match(antesEnLinea, PatronInstruccionConParametro);
            if (match.Success)
                return match.Index;

            match = Regex.Match(antesEnLinea, PatronPalabraClaveFinal);
            return match.Success ? match.Index : antesEnLinea.Length - 1;
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
            textoNumeroPreview = CrearTexto(areaGo.transform, "NumeroPreview", "01",
                new Vector2(356, 190), new Vector2(70, 24), 18, FontStyle.Bold, TextAnchor.MiddleRight, colorTexto);
            var banderaGo = CrearRect(areaGo.transform, "BanderaPreview", new Vector2(396, 10), new Vector2(30, 20), colorSwatch);
            imagenBanderaPreview = banderaGo.GetComponent<Image>();

            CrearFlecha(panel.transform, "BotonSkinAnterior", "<", new Vector2(25, 150), () => OnCambiarSkin(-1));
            CrearFlecha(panel.transform, "BotonSkinSiguiente", ">", new Vector2(436 - 25, 150), () => OnCambiarSkin(1));
        }

        private void ConstruirVistaPrevia3D(Transform contenedorUI)
        {
            escenarioPreview = new GameObject("EscenarioPreviaTanque");
            var escenario = escenarioPreview;
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
                new Vector2(0, 15), new Vector2(0, 55), 530f);

            CrearTexto(contenido, "TituloPatron", "PATRÓN", new Vector2(15, 5), new Vector2(180, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearTexto(contenido, "TituloColorPrincipal", "COLOR PRINCIPAL", new Vector2(245, 5), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);

            // PATRÓN: 6 diseños en 2 filas de 3 -- incluye "Llamas" y "Camuflaje
            // digital" como opciones épicas adicionales a las 4 originales.
            CrearFilaSwatches(contenido, "Patron", 3, new Vector2(15, 35), 0);
            CrearFilaSwatches(contenido, "Patron", 3, new Vector2(15, 90), 3);
            // COLOR PRINCIPAL: 8 colores en 2 filas de 4 (los 4 originales + Rojo
            // sangre, Negro ónix, Dorado y Púrpura real).
            CrearFilaSwatches(contenido, "Color", 4, new Vector2(245, 35), 0);
            CrearFilaSwatches(contenido, "Color", 4, new Vector2(245, 90), 4);
            AplicarColoresReales("Color", ColoresPrincipales);
            RegenerarVisualesPatron();

            // CALCOMANÍAS: 8 diseños en 2 filas de 4 (el espacio que antes ocupaba
            // el selector de NÚMERO, ahora automático -- ver comentario en
            // TanqueSkinDatos.cs). Incluye diseños "épicos": ☠ ⚔ ✦ ⚡.
            CrearTexto(contenido, "TituloCalcomanias", "CALCOMANÍAS", new Vector2(15, 165), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);

            CrearFilaSwatches(contenido, "Calcomania", 4, new Vector2(15, 195), 0);
            CrearFilaSwatches(contenido, "Calcomania", 4, new Vector2(15, 250), 4);
            AplicarEtiquetas("Calcomania", SimbolosCalcomania);

            // BANDERAS: países de la 1ra/2da Guerra Mundial, dibujadas por código
            // (PaletaSkins.GenerarTexturaBandera) -- sin plugin externo. 18 banderas
            // en 3 filas de 6.
            CrearTexto(contenido, "TituloBandera", "BANDERA", new Vector2(15, 315), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearFilaSwatches(contenido, "Bandera", 6, new Vector2(15, 345), 0);
            CrearFilaSwatches(contenido, "Bandera", 6, new Vector2(15, 400), 6);
            CrearFilaSwatches(contenido, "Bandera", 6, new Vector2(15, 455), 12);
            RegenerarVisualesBandera();

            ActualizarVistaPreviaTanque();
        }

        // Pinta cada swatch de "Bandera" con la textura real generada por
        // PaletaSkins.GenerarTexturaBandera (en vez de un simple color plano).
        private void RegenerarVisualesBandera()
        {
            if (!contenidoSwatchesPorGrupo.TryGetValue("Bandera", out var lista)) return;
            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].sprite = GenerarSpriteBandera(i);
                lista[i].type = Image.Type.Simple;
                lista[i].color = Color.white;
            }
        }

        private Sprite GenerarSpriteBandera(int indiceBandera)
        {
            string clave = $"bandera_{indiceBandera}";
            if (spritesPatronCache.TryGetValue(clave, out var spriteExistente))
                return spriteExistente;

            var textura = PaletaSkins.GenerarTexturaBandera(indiceBandera);
            var sprite = Sprite.Create(textura, new Rect(0, 0, textura.width, textura.height), new Vector2(0.5f, 0.5f), 100f);
            spritesPatronCache[clave] = sprite;
            return sprite;
        }

        // indiceInicial permite construir un grupo en varias filas (ej. 8
        // calcomanías en 2 filas de 4, o 18 banderas en 3 filas de 6): cada llamada
        // agrega swatches al MISMO grupo en vez de reemplazar los anteriores.
        private void CrearFilaSwatches(Transform padre, string grupo, int cantidad, Vector2 posicion, int indiceInicial = 0)
        {
            const float tamano = 45f, gap = 10f, margenMarco = 4f;
            if (!swatchesPorGrupo.TryGetValue(grupo, out var listaMarcos))
            {
                listaMarcos = new List<Image>();
                swatchesPorGrupo[grupo] = listaMarcos;
            }
            if (!contenidoSwatchesPorGrupo.TryGetValue(grupo, out var listaContenido))
            {
                listaContenido = new List<Image>();
                contenidoSwatchesPorGrupo[grupo] = listaContenido;
            }
            if (!seleccionActual.ContainsKey(grupo)) seleccionActual[grupo] = 0;

            for (int i = 0; i < cantidad; i++)
            {
                var posMarco = new Vector2(posicion.x + i * (tamano + gap) - margenMarco, posicion.y - margenMarco);
                var marcoGo = CrearRect(padre, $"Marco{grupo}_{indiceInicial + i}", posMarco,
                    new Vector2(tamano + margenMarco * 2, tamano + margenMarco * 2), new Color(0, 0, 0, 0));
                var marcoImagen = marcoGo.GetComponent<Image>();
                listaMarcos.Add(marcoImagen);

                var swatchGo = CrearRect(marcoGo.transform, $"Swatch{grupo}_{indiceInicial + i}",
                    new Vector2(margenMarco, margenMarco), new Vector2(tamano, tamano), colorSwatch);
                listaContenido.Add(swatchGo.GetComponent<Image>());

                var boton = marcoGo.AddComponent<Button>();
                boton.targetGraphic = marcoImagen;
                int indiceGlobal = indiceInicial + i;
                boton.onClick.AddListener(() => OnSeleccionarSwatch(grupo, indiceGlobal));
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
            string clave = $"{patronIndice}_{colorBase.r:F4}_{colorBase.g:F4}_{colorBase.b:F4}";
            if (spritesPatronCache.TryGetValue(clave, out var spriteExistente))
                return spriteExistente;

            const int n = 32;
            var textura = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            Color oscuro = new Color(colorBase.r * 0.55f, colorBase.g * 0.55f, colorBase.b * 0.55f, 1f);
            Color claro = Color.Lerp(colorBase, Color.white, 0.4f);

            // Colores de fuego para "Llamas" (índice 4): fijos, no dependen del
            // color principal, para que el patrón se vea igual de épico siempre.
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
                        case 4: // Llamas (patrón épico de fuego)
                        {
                            float alturaNormalizada = y / (float)(n - 1);
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

            var sprite = Sprite.Create(textura, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            spritesPatronCache[clave] = sprite;
            return sprite;
        }

        // Lee la personalización actualmente elegida en "PERSONALIZAR SKIN" (la
        // misma que se ve en "Vista previa del tanque"), como snapshot para
        // guardarla junto con el script del tanque. "Numero" ya no se elige: se
        // asigna automático según el orden de programación (1, 2, 3...) y se pinta
        // en el casco durante la partida.
        private TanqueSkinDatos ObtenerSkinActual()
        {
            return new TanqueSkinDatos
            {
                Patron = seleccionActual.TryGetValue("Patron", out var p) ? p : 0,
                Color = seleccionActual.TryGetValue("Color", out var c) ? c : 0,
                Calcomania = seleccionActual.TryGetValue("Calcomania", out var d) ? d : 0,
                Numero = scriptsPorTanque.Count + 1,
                Bandera = seleccionActual.TryGetValue("Bandera", out var b) ? b : 0
            };
        }

        private void ActualizarVistaPreviaTanque()
        {
            int iPatron = seleccionActual.TryGetValue("Patron", out var p) ? p : 0;
            int iColor = seleccionActual.TryGetValue("Color", out var c) ? c : 0;
            int iCalcomania = seleccionActual.TryGetValue("Calcomania", out var d) ? d : 0;
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
            // El número que se muestra en la vista previa es el que le tocará al
            // PRÓXIMO tanque a programar (automático, no elegible).
            if (textoNumeroPreview != null) textoNumeroPreview.text = (scriptsPorTanque.Count + 1).ToString("D2");
            if (imagenBanderaPreview != null)
            {
                imagenBanderaPreview.sprite = GenerarSpriteBandera(iBandera);
                imagenBanderaPreview.type = Image.Type.Simple;
                imagenBanderaPreview.color = Color.white;
            }
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
                if (!string.IsNullOrWhiteSpace(parte) && nombreMin.Contains(parte.ToLowerInvariant()))
                    return true;

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

        // Este overlay ahora solo se usa para "GUARDAR SCRIPT" (barra superior),
        // como selector de CUÁL script del Historial exportar a la máquina del
        // usuario. "CARGAR SCRIPT" ya no lo usa: abre directo el explorador de
        // archivos nativo del sistema operativo (ver OnCargarScript). Tras elegir
        // una fila aquí, se abre el diálogo nativo "Guardar como" para elegir dónde
        // en el sistema de archivos (Windows, Linux, macOS) se guarda el .txt.
        private Transform contenedorListaOverlay;

        private void ConstruirOverlayNombreArchivo(Transform padre)
        {
            overlayNombreArchivo = CrearRectElastico(padre, "OverlayNombreArchivo",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, 0.75f), null);

            var caja = CrearRect(overlayNombreArchivo.transform, "Caja", Vector2.zero, new Vector2(460, 420), colorPanel);
            var cajaRect = caja.GetComponent<RectTransform>();
            cajaRect.anchorMin = new Vector2(0.5f, 0.5f);
            cajaRect.anchorMax = new Vector2(0.5f, 0.5f);
            cajaRect.pivot = new Vector2(0.5f, 0.5f);
            cajaRect.anchoredPosition = Vector2.zero;

            tituloOverlayNombre = CrearTexto(caja.transform, "Titulo", "GUARDAR SCRIPT COMO",
                new Vector2(20, 20), new Vector2(420, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            CrearTexto(caja.transform, "Subtitulo", "Elige qué script del historial exportar como .txt:",
                new Vector2(20, 55), new Vector2(420, 22), 12, FontStyle.Italic, TextAnchor.MiddleLeft, colorTextoSecundario);

            contenedorListaOverlay = CrearAreaConScroll(caja.transform, "ListaOverlay",
                new Vector2(20, 90), new Vector2(20, 90), 0f);

            // Antes este botón quedaba en (130, 25) -- casi arriba de la caja --
            // así que se dibujaba ENCIMA del título/subtítulo y, al tener Image
            // (que bloquea raycasts), robaba los clicks de la lista de historial
            // que hay debajo. Por eso el cuadro se veía "raro" y no dejaba elegir
            // ningún script: nunca llegaba el click a la fila. Va abajo de todo,
            // dentro del margen que ya reservaba CrearAreaConScroll (90px).
            var botonCancelar = CrearRect(caja.transform, "BotonCancelar", new Vector2(130, 355), new Vector2(200, 50), colorBoton);
            var cancelarBtn = botonCancelar.AddComponent<Button>();
            cancelarBtn.targetGraphic = botonCancelar.GetComponent<Image>();
            cancelarBtn.onClick.AddListener(() => overlayNombreArchivo.SetActive(false));
            CrearTexto(botonCancelar.transform, "Texto", "CANCELAR", Vector2.zero, new Vector2(200, 50),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, colorTexto);

            overlayNombreArchivo.SetActive(false);
        }

        // Reconstruye la lista de scripts del Historial dentro del overlay, uno por
        // fila; tocar una fila dispara el diálogo nativo "Guardar como" con ese
        // contenido y ese nombre sugerido.
        //
        // Antes las filas se creaban con CrearRectElasticoLocal (anclado y con
        // offsets), pero con el signo invertido: el borde superior de cada fila
        // quedaba en offsetMax.y = +y en vez de -y, así que a medida que "y"
        // crecía la fila se iba ARRIBA del contenedor (fuera del área visible que
        // recorta el RectMask2D del scroll) en vez de abajo. Por eso la lista se
        // veía vacía y no había nada que tocar. Ahora usa el mismo patrón simple
        // (CrearRect, posición absoluta hacia abajo) que ya funciona en
        // CrearItemHistorial/ReconstruirListaHistorial.
        private void AbrirOverlaySeleccionarParaExportar()
        {
            for (int i = contenedorListaOverlay.childCount - 1; i >= 0; i--)
                Destroy(contenedorListaOverlay.GetChild(i).gameObject);

            if (historial.Count == 0)
            {
                CrearTexto(contenedorListaOverlay, "Vacio", "(sin scripts guardados todavía)",
                    new Vector2(0, 0), new Vector2(400, 26), 13, FontStyle.Italic, TextAnchor.MiddleLeft, colorTextoSecundario);
            }
            else
            {
                float y = 0f;
                const float altoFila = 34f;
                for (int i = historial.Count - 1; i >= 0; i--)
                {
                    var entrada = historial[i];
                    var fila = CrearRect(contenedorListaOverlay, $"Fila_{i}", new Vector2(0, y),
                        new Vector2(400, altoFila), colorBoton);
                    var boton = fila.AddComponent<Button>();
                    boton.targetGraphic = fila.GetComponent<Image>();
                    boton.onClick.AddListener(() => ExportarEntradaHistorialAMaquina(entrada));
                    CrearTexto(fila.transform, "Texto", $"{entrada.nombre}  ({entrada.fechaHora})",
                        new Vector2(10, 0), new Vector2(380, altoFila), 13, FontStyle.Normal, TextAnchor.MiddleLeft, colorTexto);
                    y += altoFila + 4f;
                }

                if (contenedorListaOverlay is RectTransform contenidoRect)
                    contenidoRect.sizeDelta = new Vector2(0, y);
            }

            tituloOverlayNombre.text = "GUARDAR SCRIPT COMO";
            overlayNombreArchivo.SetActive(true);
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

        private bool BuscarInstruccionPendiente(string texto, int cursor, out string comandoEncontrado, out int indice, out int longitud)
        {
            comandoEncontrado = null;
            indice = -1;
            longitud = 0;
            int mejorDistancia = int.MaxValue;

            foreach (var comando in comandosQueRequierenDireccion)
            {
                if (string.IsNullOrWhiteSpace(comando)) continue;

                foreach (Match match in Regex.Matches(texto, $@"{Regex.Escape(comando)}\([NSEO]?\)"))
                {
                    int inicioMatch = match.Index;
                    int finMatch = match.Index + match.Length;

                    // Si el cursor está dentro (o justo pegado a) la instrucción, es la más
                    // relevante posible. Si no, nos quedamos con la que esté más cerca del
                    // cursor, en vez de siempre la última del texto.
                    int distancia = (cursor >= inicioMatch && cursor <= finMatch)
                        ? 0
                        : Mathf.Min(Mathf.Abs(cursor - inicioMatch), Mathf.Abs(cursor - finMatch));

                    if (distancia < mejorDistancia)
                    {
                        mejorDistancia = distancia;
                        comandoEncontrado = comando;
                        indice = inicioMatch;
                        longitud = match.Length;
                    }
                }
            }

            return comandoEncontrado != null;
        }

        private int ObtenerCursorActual()
        {
            int longitudTexto = campoEditor.text.Length;
            int cursor = campoEditor.isFocused ? campoEditor.caretPosition : ultimaPosicionCursorConocida;
            return Mathf.Clamp(cursor, 0, longitudTexto);
        }

        private void ActualizarDeteccionDireccion()
        {
            bool hayInstruccion = BuscarInstruccionPendiente(campoEditor.text, ObtenerCursorActual(), out _, out int indice, out int longitud);
            textoInstruccionPendienteDireccion.text = hayInstruccion
                ? $"Editando: {campoEditor.text.Substring(indice, longitud)}"
                : "—";
        }

        private void OnSeleccionarDireccion(char letra)
        {
            string texto = campoEditor.text;
            if (!BuscarInstruccionPendiente(texto, ObtenerCursorActual(), out string comando, out int indice, out int longitud))
                return;

            string reemplazo = $"{comando}({letra})";
            texto = texto.Remove(indice, longitud).Insert(indice, reemplazo);
            campoEditor.text = texto;

            int nuevaPosicion = indice + reemplazo.Length;
            ActualizarEditorSinMoverCursor();
            FijarCursorEnEditor(nuevaPosicion);
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
        private const float ALTURA_LINEA_EDITOR = 20f;

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
            texto.resizeTextForBestFit = false;
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

            // InputField estándar: dejamos que Unity gestione directamente la cola de
            // eventos. Esto es importante para ↑/↓ porque mantiene internamente
            // m_CaretPosition y m_CaretSelectPosition sin que nuestro código los
            // desincronice. El Backspace especial se reconstruye en AlCambiarTextoEditor().
            var campo = go.AddComponent<InputField>();
            campo.textComponent = texto;
            campo.lineType = InputField.LineType.MultiLineNewline;
            campo.text = textoInicial;

            campo.customCaretColor = true;
            campo.caretColor = Color.white;
            campo.caretWidth = 2;
            campo.selectionColor = new Color(0.3f, 0.55f, 0.9f, 0.5f);

            scrollRect.content = contenedorEditorRect;

            return campo;
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

        // "GUARDAR SCRIPT" (barra superior): abre el selector interno para elegir
        // CUÁL script del Historial exportar (ver AbrirOverlaySeleccionarParaExportar),
        // y desde ahí se dispara el diálogo nativo "Guardar como" del sistema
        // operativo del jugador (Windows, Linux o macOS), vía SFB.
        private void OnGuardarScript()
        {
            AbrirOverlaySeleccionarParaExportar();
        }

        // "CARGAR SCRIPT" (barra superior): abre directo el explorador de archivos
        // nativo del sistema operativo para elegir un .txt de donde sea en la
        // máquina del usuario, y lo vuelca en la terminal para poder modificarlo.
        private void OnCargarScript()
        {
            var rutas = SFB.StandaloneFileBrowser.OpenFilePanel("Abrir script de tanque", "", "txt", false);
            if (rutas == null || rutas.Length == 0 || string.IsNullOrEmpty(rutas[0])) return;

            try
            {
                string ruta = rutas[0];
                campoEditor.text = File.ReadAllText(ruta);
                ActualizarEditorTrasCambio();
                nombreArchivoActual = Path.GetFileNameWithoutExtension(ruta);
                ActualizarTextoEstado($"Cargado \"{Path.GetFileName(ruta)}\" — ya puedes modificarlo y guardarlo.", esError: false);
            }
            catch (Exception e)
            {
                ActualizarTextoEstado($"No se pudo cargar ({e.Message})", esError: true);
            }
        }

        // Dispara el diálogo nativo "Guardar como" con el contenido de una entrada
        // del historial, para que el jugador elija dónde en su sistema de archivos
        // (Windows/Linux/macOS) guardar ese .txt para futuras partidas.
        private void ExportarEntradaHistorialAMaquina(EntradaHistorial entrada)
        {
            overlayNombreArchivo.SetActive(false);

            string sugerido = SanearNombreArchivo(entrada.nombre);
            string ruta = SFB.StandaloneFileBrowser.SaveFilePanel("Guardar script como", "", sugerido, "txt");
            if (string.IsNullOrEmpty(ruta)) return;

            try
            {
                File.WriteAllText(ruta, entrada.contenido);
                ActualizarTextoEstado($"Guardado en \"{ruta}\"", esError: false);
            }
            catch (Exception e)
            {
                ActualizarTextoEstado($"No se pudo guardar ({e.Message})", esError: true);
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
            entradaEnEdicion = null;
            textoEditorAntesDeEditarHistorial = "";
            nombreArchivoAntesDeEditarHistorial = "";
            ActualizarEtiquetaBotonGuardar();
            ActualizarTextoEstado("LISTO", esError: false);
        }

        // Valida el script del tanque actual. Si el contenido vino de tocar un
        // tanque en el Historial (entradaEnEdicion != null), lo ACTUALIZA en vez de
        // crear uno nuevo — antes, editar y volver a guardar generaba un tanque
        // adicional por error. El editor se limpia SIEMPRE después de un guardado
        // nuevo exitoso, para seguir directo con el próximo tanque.
        private void OnGuardarScriptDelTanque()
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

            if (entradaEnEdicion != null)
            {
                ActualizarTanqueExistente(texto);
                return;
            }

            if (scriptsPorTanque.Count >= cantidadMaximaTanques)
            {
                ActualizarTextoEstado($"Ya programaste el máximo de {cantidadMaximaTanques} tanques", esError: true);
                return;
            }

            int tanquesProgramados = scriptsPorTanque.Count + 1;
            string nombreTanque = $"Tanque {tanquesProgramados}";

            var skinElegida = ObtenerSkinActual();
            var entrada = new EntradaHistorial
            {
                nombre = GenerarNombreUnicoHistorial(tanquesProgramados),
                fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                contenido = texto,
                patron = skinElegida.Patron,
                color = skinElegida.Color,
                calcomania = skinElegida.Calcomania,
                bandera = skinElegida.Bandera
            };
            historial.Add(entrada);
            GuardarHistorialEnDisco();
            ReconstruirListaHistorial();

            scriptsPorTanque.Add(texto);
            skinsPorTanque.Add(skinElegida);
            ActualizarTamanoMinimoTablero(tanquesProgramados);

            campoEditor.text = "";
            ActualizarEditorTrasCambio();
            textoEditorAntesDeEditarHistorial = "";
            nombreArchivoAntesDeEditarHistorial = "";
            ActualizarEtiquetaBotonGuardar();

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
                ActualizarTextoEstado($"{nombreTanque} guardado. Puedes seguir programando otro tanque o iniciar la partida.", esError: false);
            }

            ActualizarBotonEliminarScript();
        }

        // Actualiza el script de un tanque que ya estaba guardado (el que se cargó
        // con un click en el Historial) en vez de agregar uno nuevo. Igual que al
        // guardar un tanque nuevo, se limpia el editor después: dejar el código
        // ahí confundía, porque parecía que seguías editando ese mismo tanque.
        private void ActualizarTanqueExistente(string texto)
        {
            int indice = historial.IndexOf(entradaEnEdicion);
            if (indice < 0)
            {
                // La entrada ya no existe (por ejemplo, se borró mientras se
                // editaba): se trata como un tanque nuevo, para no perder lo escrito.
                entradaEnEdicion = null;
                ActualizarEtiquetaBotonGuardar();
                OnGuardarScriptDelTanque();
                return;
            }

            var skinActualizada = ObtenerSkinActual();
            entradaEnEdicion.contenido = texto;
            entradaEnEdicion.fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            entradaEnEdicion.patron = skinActualizada.Patron;
            entradaEnEdicion.color = skinActualizada.Color;
            entradaEnEdicion.calcomania = skinActualizada.Calcomania;
            entradaEnEdicion.bandera = skinActualizada.Bandera;
            if (indice < scriptsPorTanque.Count)
                scriptsPorTanque[indice] = texto;
            if (indice < skinsPorTanque.Count)
                skinsPorTanque[indice] = skinActualizada;

            string nombreTanque = entradaEnEdicion.nombre;
            entradaEnEdicion = null;
            textoEditorAntesDeEditarHistorial = "";
            nombreArchivoAntesDeEditarHistorial = "";

            campoEditor.text = "";
            ActualizarEditorTrasCambio();
            ActualizarEtiquetaBotonGuardar();

            GuardarHistorialEnDisco();
            ReconstruirListaHistorial();
            ActualizarTextoEstado($"{nombreTanque} actualizado correctamente.", esError: false);
        }

        private void ActualizarEtiquetaBotonGuardar()
        {
            bool editandoHistorial = entradaEnEdicion != null;

            if (textoBotonGuardarTanque != null)
                textoBotonGuardarTanque.text = editandoHistorial ? "ACTUALIZAR" : "GUARDAR";

            if (botonCancelarEdicionRef != null)
            {
                botonCancelarEdicionRef.gameObject.SetActive(editandoHistorial);
                botonCancelarEdicionRef.interactable = editandoHistorial;
            }

            // Sin CANCELAR a la vista (modo normal, no editando), GUARDAR vuelve a
            // ocupar todo el ancho original de la terminal (hasta antes de la rosa
            // de los vientos). Editando un tanque del Historial, CANCELAR aparece
            // y GUARDAR/ACTUALIZAR se achica a su mitad para no superponerse.
            if (rectBotonGuardarTanque != null)
            {
                rectBotonGuardarTanque.anchorMax = editandoHistorial
                    ? new Vector2(0.5f, 0f)
                    : new Vector2(1f, 0f);
                rectBotonGuardarTanque.offsetMax = editandoHistorial
                    ? new Vector2(-90, 85)
                    : new Vector2(-175, 85);
            }

            if (textoBotonCancelarEdicion != null)
                textoBotonCancelarEdicion.text = "CANCELAR";
        }

        // Arranca la partida de verdad: le deja los tanques programados y el tamaño
        // de tablero al GameManager (a través de ConfiguracionPartidaPendiente, en
        // TanksGame.Core) y recién ahí carga la escena de juego. El GameManager los
        // lee solo en su propio Awake() (ver GameManager.cs) — aquí no hace falta
        // ninguna otra referencia a la escena de juego.
        private void OnIniciarPartida()
        {
            if (scriptsPorTanque.Count < cantidadMinimaTanquesParaJugar)
            {
                ActualizarTextoEstado(
                    $"Debes programar al menos {cantidadMinimaTanquesParaJugar} tanques antes de iniciar la partida",
                    esError: true);
                return;
            }

            ConfiguracionPartidaPendiente.Establecer(new List<string>(scriptsPorTanque), new List<TanqueSkinDatos>(skinsPorTanque), tamanoTablero);
            SceneManager.LoadScene(nombreEscenaJuego);
        }

        private void OnLimpiarHistorial()
        {
            AbrirOverlayConfirmarBorrado("¿Borrar todo el historial de scripts programados?", ConfirmarLimpiarHistorial);
        }

        private void ConfirmarLimpiarHistorial()
        {
            historial.Clear();
            scriptsPorTanque.Clear(); // antes esto quedaba desincronizado: el Historial se vaciaba pero los tanques seguían "programados".
            skinsPorTanque.Clear();
            entradaEnEdicion = null;
            textoEditorAntesDeEditarHistorial = "";
            nombreArchivoAntesDeEditarHistorial = "";
            modoEliminarActivo = false;

            GuardarHistorialEnDisco();
            ReconstruirListaHistorial();
            ActualizarBotonEliminarScript();
            ActualizarEtiquetaBotonGuardar();
            ActualizarTextoEstado("Historial y tanques programados reiniciados.", esError: false);
        }

        private void OnSeleccionarHistorial(EntradaHistorial entrada)
        {
            if (entradaEnEdicion == null)
            {
                // Conservamos lo que hubiera en el editor antes de entrar al modo
                // edición. Si el usuario se arrepiente, CANCELAR lo restaura.
                textoEditorAntesDeEditarHistorial = campoEditor != null ? campoEditor.text : "";
                nombreArchivoAntesDeEditarHistorial = nombreArchivoActual;
            }

            campoEditor.text = entrada.contenido;
            ActualizarEditorTrasCambio();
            nombreArchivoActual = entrada.nombre;
            entradaEnEdicion = entrada;
            AplicarSkinDesdeEntrada(entrada);
            ActualizarEtiquetaBotonGuardar();
            ActualizarTextoEstado($"Editando \"{entrada.nombre}\" — puedes ACTUALIZAR o CANCELAR la edición.", esError: false);
        }

        // Empuja la personalización guardada en la entrada hacia los controles de
        // "PERSONALIZAR SKIN" (swatches resaltados + vista previa), para que al
        // tocar un script del Historial se vea de inmediato cómo estaba vestido
        // ese tanque, en vez de mostrar la última personalización que quedó
        // seleccionada en pantalla.
        private void AplicarSkinDesdeEntrada(EntradaHistorial entrada)
        {
            seleccionActual["Patron"] = entrada.patron;
            seleccionActual["Color"] = entrada.color;
            seleccionActual["Calcomania"] = entrada.calcomania;
            seleccionActual["Bandera"] = entrada.bandera;

            ResaltarSeleccion("Patron");
            ResaltarSeleccion("Color");
            ResaltarSeleccion("Calcomania");
            ResaltarSeleccion("Bandera");

            RegenerarVisualesPatron();
            ActualizarVistaPreviaTanque();
        }

        private void OnCancelarEdicion()
        {
            if (entradaEnEdicion == null)
                return;

            string nombreCancelado = entradaEnEdicion.nombre;

            entradaEnEdicion = null;
            campoEditor.text = textoEditorAntesDeEditarHistorial;
            ActualizarEditorTrasCambio();
            nombreArchivoActual = nombreArchivoAntesDeEditarHistorial;
            textoEditorAntesDeEditarHistorial = "";
            nombreArchivoAntesDeEditarHistorial = "";
            ActualizarEtiquetaBotonGuardar();
            ActualizarTextoEstado($"Edición de \"{nombreCancelado}\" cancelada. El script original no fue modificado.", esError: false);
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

        // Borra un tanque puntual: lo saca del Historial y, por índice, de
        // scriptsPorTanque/skinsPorTanque (antes se buscaba por el contenido
        // exacto del script, lo que podía borrar el tanque equivocado si dos
        // tanques tenían el mismo programa).
        private void OnEliminarTanque(EntradaHistorial entrada)
        {
            int indice = historial.IndexOf(entrada);

            historial.Remove(entrada);
            if (indice >= 0 && indice < scriptsPorTanque.Count)
                scriptsPorTanque.RemoveAt(indice);
            if (indice >= 0 && indice < skinsPorTanque.Count)
                skinsPorTanque.RemoveAt(indice);

            if (entradaEnEdicion == entrada)
            {
                entradaEnEdicion = null;
                textoEditorAntesDeEditarHistorial = "";
                nombreArchivoAntesDeEditarHistorial = "";
                ActualizarEtiquetaBotonGuardar();
            }

            GuardarHistorialEnDisco();
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
            ("Patron", 6), ("Color", 8), ("Calcomania", 8), ("Bandera", 18)
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