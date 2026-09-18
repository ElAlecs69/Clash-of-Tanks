using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TanksGame.Core;
using TanksGame.Gameplay;

namespace TanksGame.UI
{
    // Genera en tiempo de ejecución una UI mínima: un botón "Siguiente turno", un
    // panel por cada tanque (vida, bombas restantes, escudo y su parte del log) y
    // un panel con el log completo de la última ronda.
    // Es un placeholder funcional; la versión final debería reemplazarse por el panel
    // de instrucciones descrito en DESIGN_GUIDE.md.
    //
    // Uso: agrega este componente a un GameObject vacío y arrastra el GameManager
    // de tu escena al campo "Game Manager".
    public class GameplayUI : MonoBehaviour
    {
        public GameManager gameManager;

        [Header("Paneles de tanques")]
        public Vector2 tamanoPanelTanque = new Vector2(300, 200);
        public float espacioEntrePaneles = 20f;
        public float margenBorde = 20f;

        private readonly List<PanelTanqueHud> panelesTanque = new List<PanelTanqueHud>();

        // Referencias vivas de un recuadro de tanque estilo "arcade pixel": barra
        // de vida segmentada, pips de bombas, ícono de escudo y el trocito de log
        // que le corresponde. Se arma una vez en ConstruirPanelesDeTanques() y
        // ActualizarHud() solo actualiza colores/textos/estados sobre esto.
        private class PanelTanqueHud
        {
            public GameObject raiz;
            public Text encabezado;
            public Image[] segmentosVida;
            public Text textoPorcentajeVida;
            public Image[] pipsBomba;
            public Text textoBombas;
            public Image iconoEscudo;
            public Text textoEscudo;
            public Text textoLog;
            public GameObject overlayDestruido;
            public Color colorJugador;
            public int capacidadBombas;
        }

        [Header("Ejecución automática")]
        [Tooltip("Segundos entre turno y turno a velocidad normal (x1).")]
        public float segundosPorTurno = 1.5f;

        [Header("Pausa")]
        [Tooltip("Nombre de la escena del menú principal, para el botón 'Regresar al menú'.")]
        public string nombreEscenaMenu = "MenuPrincipal";
        public string nombreEscenaProgramacion = "PantallaProgramacionTanques";

        [Header("Sonidos (opcionales, se usan en el panel de pausa)")]
        public AudioClip sonidoClick;
        public AudioClip sonidoCambio;
        [Tooltip("Se reproduce al abrir el panel de pausa (además del sonido de click del botón).")]
        public AudioClip sonidoPausa;

        [Header("Imagen de fondo de Pausa (opcional)")]
        [Tooltip("Asigna aquí el mismo sprite que usas en 'Fondo Opciones' de MenuPrincipal para que el panel de pausa se vea igual que Opciones.")]
        public Sprite fondoPausa;

        [Header("Música de fondo (solo esta pantalla)")]
        [Tooltip("Se reproduce en bucle mientras estás en la partida. Se detiene sola al volver al menú (no usa DontDestroyOnLoad).")]
        public AudioClip musicaFondo;
        [Range(0f, 1f)]
        public float volumenMusica = 0.5f;

        private const int MAX_TANQUES_POR_LADO = 3; // 6 tanques en total como máximo.

        // Misma paleta que MenuPrincipal, para que el panel de pausa se vea
        // consistente con el de Opciones del menú de inicio.
        private static readonly Color ColorFondoPanel = new Color(0.03f, 0.02f, 0.02f, 1f);
        private static readonly Color ColorAcentoRojo = new Color(0.75f, 0.10f, 0.10f, 1f);
        private static readonly Color ColorAcentoHover = new Color(0.87f, 0.68f, 0.20f, 1f);
        private static readonly Color ColorTextoActivo = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color ColorTextoInactivo = new Color(0.55f, 0.55f, 0.55f, 1f);

        // Paleta de acento por jugador para los recuadros arcade del HUD —
        // colores saturados tipo consola retro, uno distinto por tanque.
        private static readonly Color[] PaletaAcentoJugador = new[]
        {
            new Color(0.30f, 0.78f, 0.98f), // J1 - cian
            new Color(0.98f, 0.58f, 0.16f), // J2 - naranja
            new Color(0.42f, 0.90f, 0.45f), // J3 - verde
            new Color(0.93f, 0.32f, 0.78f), // J4 - magenta
            new Color(0.96f, 0.86f, 0.22f), // J5 - amarillo
            new Color(0.68f, 0.40f, 0.98f), // J6 - violeta
        };

        private static Color ColorDeJugador(int playerId) =>
            PaletaAcentoJugador[Mathf.Max(0, playerId - 1) % PaletaAcentoJugador.Length];
        // Más clara que ColorFondoPanel a propósito: es la ventana emergente de
        // confirmación, que necesita contrastar sobre la imagen de fondo de pausa.
        private static readonly Color ColorFondoVentanaEmergente = new Color(0.16f, 0.13f, 0.12f, 0.97f);
        private static readonly string[] NombresCalidad = { "BAJO", "MEDIO", "ALTO" };

        private float multiplicadorVelocidad = 1f;
        private float temporizadorTurno = 0f;
        private bool partidaTerminada = false;
        private Text textoVelocidad;
        private Text textoResultadoPartida;
        private GameObject panelFinDePartida;
        private Button botonJugarDeNuevo;
        private Button botonRepetir;
        private AudioSource audioSource;
        private AudioSource audioSourceMusica;

        // Estado del panel de pausa.
        private GameObject panelPausa;
        private GameObject panelConfirmarSalir;
        private bool juegoPausado = false;

        // Estado de las opciones de video, igual que en MenuPrincipal.
        private List<Resolution> resolucionesDisponibles;
        private int indiceResolucion;
        private int indiceCalidad;
        private bool estadoPantallaCompleta;
        private Text textoResolucion;
        private Text textoCalidad;
        private Text textoPantallaCompleta;

        private void Start()
        {
            if (gameManager == null)
            {
                Debug.LogError("GameplayUI: falta asignar 'Game Manager' en el Inspector.");
                enabled = false;
                return;
            }

            if (FindObjectOfType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // AudioSource separado (en bucle) para la música, igual que en
            // MenuPrincipal y PantallaProgramacionTanques: así los sonidos de
            // click/cambio (PlayOneShot) no la interrumpen.
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
            ActualizarHud();
        }

        private void ReproducirClick()
        {
            if (sonidoClick != null) audioSource.PlayOneShot(sonidoClick);
        }

        private void ReproducirCambio()
        {
            if (sonidoCambio != null) audioSource.PlayOneShot(sonidoCambio);
        }

        private void Update()
        {
            // Con ESC se abre/cierra la pausa igual que en el menú de inicio.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !partidaTerminada)
            {
                if (panelConfirmarSalir != null && panelConfirmarSalir.activeSelf) OnCancelarSalir();
                else if (juegoPausado) OnContinuar();
                else OnPausa();
            }

            if (partidaTerminada || gameManager == null || juegoPausado) return;

            // Si la ronda anterior todavía se está animando (movimientos y
            // disparos en orden), no se avanza el temporizador. Antes esto no
            // se comprobaba: con velocidades altas o animaciones largas, el
            // siguiente turno podía arrancar mientras BoardView todavía
            // estaba reproduciendo la ronda anterior, y eso terminaba
            // rompiendo la animación en curso (ver comentario en
            // GameManager.EjecutarSiguienteTurno sobre "Collection was
            // modified").
            if (gameManager.vistaTablero != null && gameManager.vistaTablero.RondaEnAnimacion) return;

            temporizadorTurno += Time.deltaTime * multiplicadorVelocidad;
            if (temporizadorTurno >= segundosPorTurno)
            {
                temporizadorTurno = 0f;
                EjecutarTurnoAutomatico();
            }
        }

        private void EjecutarTurnoAutomatico()
        {
            var resultado = gameManager.EjecutarSiguienteTurno();
            ActualizarHud();
            RevisarFinDePartida(resultado);
        }

        // Usa el GameResult real que ya calcula TurnManager (PlayerWins / Draw /
        // en progreso) en vez de aproximarlo contando tanques vivos nosotros mismos
        // — así respeta las reglas de desempate por daño que ya tiene tu lógica de
        // turnos, en vez de reinventarlas aquí.
        private void RevisarFinDePartida(GameResult resultado)
        {
            if (resultado == GameResult.PlayerWins)
            {
                var ganador = gameManager.Turno.AliveTanks().First();
                partidaTerminada = true;
                MostrarResultadoPartida($"GANADOR: JUGADOR {ganador.PlayerId}");
            }
            else if (resultado == GameResult.Draw)
            {
                partidaTerminada = true;
                MostrarResultadoPartida("EMPATE");
            }
        }

        private void ConstruirUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("Canvas_TanksGame");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            // --- Botón de velocidad x1/x2 (abajo, centrado) — reemplaza al viejo
            // "SIGUIENTE TURNO": los turnos ahora se ejecutan solos (ver Update()),
            // este botón solo acelera/desacelera el ritmo. ---
            var botonGo = new GameObject("BotonVelocidad");
            botonGo.transform.SetParent(canvasGo.transform, false);
            var botonImagen = botonGo.AddComponent<Image>();
            botonImagen.color = new Color(0.85f, 0.65f, 0.15f);
            var boton = botonGo.AddComponent<Button>();

            var botonRect = botonGo.GetComponent<RectTransform>();
            botonRect.sizeDelta = new Vector2(260, 70);
            botonRect.anchorMin = new Vector2(0.5f, 0f);
            botonRect.anchorMax = new Vector2(0.5f, 0f);
            botonRect.anchoredPosition = new Vector2(0, 60);

            var botonTextoGo = new GameObject("Texto");
            botonTextoGo.transform.SetParent(botonGo.transform, false);
            textoVelocidad = botonTextoGo.AddComponent<Text>();
            textoVelocidad.text = "VELOCIDAD x1";
            textoVelocidad.alignment = TextAnchor.MiddleCenter;
            textoVelocidad.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoVelocidad.fontSize = 22;
            textoVelocidad.fontStyle = FontStyle.Bold;
            textoVelocidad.color = Color.black;
            var botonTextoRect = botonTextoGo.GetComponent<RectTransform>();
            botonTextoRect.anchorMin = Vector2.zero;
            botonTextoRect.anchorMax = Vector2.one;
            botonTextoRect.offsetMin = Vector2.zero;
            botonTextoRect.offsetMax = Vector2.zero;

            boton.onClick.AddListener(OnCambiarVelocidad);

            // --- Panel de fin de partida (oculto hasta que termine la
            // partida): fondo oscuro para que resalte sobre el tablero,
            // título con contorno/sombra en vez de texto plano, y el botón
            // "JUGAR DE NUEVO" justo debajo. Todo agrupado bajo un mismo
            // GameObject para mostrarlo/ocultarlo de una sola vez. Centrado
            // en pantalla y un poco más abajo del centro, en vez de pegado
            // arriba como antes. ---
            var panelFinGo = new GameObject("PanelFinDePartida");
            panelFinGo.transform.SetParent(canvasGo.transform, false);
            panelFinDePartida = panelFinGo;
            var panelFinRect = panelFinGo.AddComponent<RectTransform>();
            panelFinRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelFinRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelFinRect.pivot = new Vector2(0.5f, 0.5f);
            panelFinRect.sizeDelta = new Vector2(760, 280);
            panelFinRect.anchoredPosition = new Vector2(0, 20);

            var fondoFinGo = new GameObject("Fondo");
            fondoFinGo.transform.SetParent(panelFinGo.transform, false);
            var fondoFinImagen = fondoFinGo.AddComponent<Image>();
            fondoFinImagen.color = new Color(0.03f, 0.02f, 0.02f, 0.88f);
            var fondoFinRect = fondoFinGo.GetComponent<RectTransform>();
            fondoFinRect.anchorMin = Vector2.zero;
            fondoFinRect.anchorMax = Vector2.one;
            fondoFinRect.offsetMin = Vector2.zero;
            fondoFinRect.offsetMax = Vector2.zero;

            var resultadoGo = new GameObject("TextoResultado");
            resultadoGo.transform.SetParent(panelFinGo.transform, false);
            textoResultadoPartida = resultadoGo.AddComponent<Text>();
            textoResultadoPartida.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoResultadoPartida.fontSize = 64;
            textoResultadoPartida.fontStyle = FontStyle.Bold;
            textoResultadoPartida.alignment = TextAnchor.MiddleCenter;
            textoResultadoPartida.color = new Color(1f, 0.83f, 0.28f); // dorado
            textoResultadoPartida.horizontalOverflow = HorizontalWrapMode.Overflow;
            var resultadoRect = resultadoGo.GetComponent<RectTransform>();
            resultadoRect.anchorMin = new Vector2(0f, 0.4f);
            resultadoRect.anchorMax = new Vector2(1f, 1f);
            resultadoRect.offsetMin = Vector2.zero;
            resultadoRect.offsetMax = Vector2.zero;

            // Doble contorno (uno oscuro grueso hacia abajo-derecha, otro
            // sutil hacia arriba-izquierda) más una sombra proyectada: es lo
            // que le da al texto dorado el relieve "épico" en vez del look
            // plano de un Text de UI sin nada encima.
            var contornoOscuro = resultadoGo.AddComponent<Outline>();
            contornoOscuro.effectColor = new Color(0.35f, 0.05f, 0.02f, 1f);
            contornoOscuro.effectDistance = new Vector2(3f, -3f);
            var contornoSutil = resultadoGo.AddComponent<Outline>();
            contornoSutil.effectColor = new Color(0f, 0f, 0f, 0.65f);
            contornoSutil.effectDistance = new Vector2(-2f, 2f);
            var sombraResultado = resultadoGo.AddComponent<Shadow>();
            sombraResultado.effectColor = new Color(0f, 0f, 0f, 0.75f);
            sombraResultado.effectDistance = new Vector2(0f, -8f);

            // Dos botones lado a lado: REPETIR (misma configuración, directo a
            // la revancha) a la izquierda, y JUGAR DE NUEVO (vuelve a la
            // pantalla de programación para armar una partida nueva) a la
            // derecha.
            botonRepetir = CrearBotonFinDePartida(panelFinGo.transform, "BotonRepetir",
                "REPETIR", new Color(0.16f, 0.42f, 0.2f), new Vector2(-160, 26), OnRepetir);

            botonJugarDeNuevo = CrearBotonFinDePartida(panelFinGo.transform, "BotonJugarDeNuevo",
                "JUGAR DE NUEVO", ColorAcentoRojo, new Vector2(160, 26), OnJugarDeNuevo);

            panelFinGo.SetActive(false);

            // --- Un panel por cada tanque: mitad en el borde izquierdo, mitad en el derecho ---
            ConstruirPanelesDeTanques(canvasGo.transform);

            // --- Botón de pausa (esquina superior, centrado) ---
            CrearBotonPausa(canvasGo.transform);
            ConstruirPanelPausa(canvasGo.transform);
            ConstruirPanelConfirmarSalir(canvasGo.transform);
        }

        // ------------------------------------------------------------------
        // BOTÓN DE PAUSA
        // ------------------------------------------------------------------

        private void CrearBotonPausa(Transform padre)
        {
            var go = new GameObject("BotonPausa");
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();
            imagen.color = new Color(0.08f, 0.06f, 0.06f, 0.85f);

            var boton = go.AddComponent<Button>();
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1f, 0.9f, 0.55f);
            colores.pressedColor = new Color(0.6f, 0.45f, 0.15f);
            boton.colors = colores;
            boton.targetGraphic = imagen;
            boton.onClick.AddListener(ReproducirClick);
            boton.onClick.AddListener(OnPausa);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(56, 56);
            rect.anchoredPosition = new Vector2(0, -20);

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.text = "II";
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 26;
            texto.fontStyle = FontStyle.Bold;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = ColorTextoActivo;
            texto.raycastTarget = false;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;
        }

        private void OnPausa()
        {
            juegoPausado = true;
            Time.timeScale = 0f;
            panelPausa.SetActive(true);
            if (sonidoPausa != null) audioSource.PlayOneShot(sonidoPausa);
        }

        private void OnContinuar()
        {
            panelPausa.SetActive(false);
            Time.timeScale = 1f;
            juegoPausado = false;
        }

        // ------------------------------------------------------------------
        // PANEL DE PAUSA — mismas opciones que "OPCIONES" del menú de inicio
        // (audio y video), más "CONTINUAR" y "REGRESAR AL MENÚ".
        // ------------------------------------------------------------------

        private void ConstruirPanelPausa(Transform padre)
        {
            panelPausa = new GameObject("PanelPausa");
            panelPausa.transform.SetParent(padre, false);

            var fondo = panelPausa.AddComponent<Image>();
            var rectFondo = panelPausa.GetComponent<RectTransform>();
            rectFondo.anchorMin = Vector2.zero;
            rectFondo.anchorMax = Vector2.one;
            rectFondo.offsetMin = Vector2.zero;
            rectFondo.offsetMax = Vector2.zero;

            if (fondoPausa != null)
            {
                // Mismo criterio que MenuPrincipal.ConstruirPanelOpciones: con imagen
                // propia no se agrega color plano encima, para no taparla.
                fondo.sprite = fondoPausa;
                fondo.type = Image.Type.Simple;
                fondo.preserveAspect = false;
                fondo.color = Color.white;
            }
            else
            {
                fondo.color = ColorFondoPanel;
            }

            var contenedorGo = new GameObject("ContenidoPausa");
            contenedorGo.transform.SetParent(panelPausa.transform, false);
            var contenedorRect = contenedorGo.AddComponent<RectTransform>();
            contenedorRect.anchorMin = new Vector2(0.5f, 0.5f);
            contenedorRect.anchorMax = new Vector2(0.5f, 0.5f);
            contenedorRect.pivot = new Vector2(0.5f, 0.5f);
            contenedorRect.sizeDelta = new Vector2(700, 700);
            contenedorRect.anchoredPosition = new Vector2(0, 40);

            CrearTituloDePanel(contenedorRect, "PAUSA", new Vector2(0, -40));

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

            CrearEtiquetaSeccion(contenedorRect, "PARTIDA", new Vector2(0, -490));
            CrearBotonTextoSimple(contenedorRect, "REGRESAR AL MENÚ", new Vector2(0, -550), OnAbrirConfirmarSalir);
            CrearBotonTextoSimple(contenedorRect, "CONTINUAR", new Vector2(0, -610), OnContinuar);

            panelPausa.SetActive(false);
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

        // ------------------------------------------------------------------
        // PANEL DE CONFIRMACIÓN — advertencia al regresar al menú, con
        // "CONFIRMAR" (se pierde la partida) y "CANCELAR" (vuelve a pausa).
        // ------------------------------------------------------------------

        private void ConstruirPanelConfirmarSalir(Transform padre)
        {
            panelConfirmarSalir = new GameObject("PanelConfirmarSalir");
            panelConfirmarSalir.transform.SetParent(padre, false);
            var fondo = panelConfirmarSalir.AddComponent<Image>();
            fondo.color = new Color(0f, 0f, 0f, 0.75f);
            var rectFondo = panelConfirmarSalir.GetComponent<RectTransform>();
            rectFondo.anchorMin = Vector2.zero;
            rectFondo.anchorMax = Vector2.one;
            rectFondo.offsetMin = Vector2.zero;
            rectFondo.offsetMax = Vector2.zero;

            var cuadroGo = new GameObject("Cuadro");
            cuadroGo.transform.SetParent(panelConfirmarSalir.transform, false);
            var cuadroImg = cuadroGo.AddComponent<Image>();
            cuadroImg.color = ColorFondoVentanaEmergente;
            var cuadroRect = cuadroGo.GetComponent<RectTransform>();
            cuadroRect.anchorMin = new Vector2(0.5f, 0.5f);
            cuadroRect.anchorMax = new Vector2(0.5f, 0.5f);
            cuadroRect.pivot = new Vector2(0.5f, 0.5f);
            cuadroRect.sizeDelta = new Vector2(560, 300);
            cuadroRect.anchoredPosition = Vector2.zero;

            var bordeGo = new GameObject("Borde");
            bordeGo.transform.SetParent(cuadroGo.transform, false);
            var bordeImg = bordeGo.AddComponent<Image>();
            bordeImg.color = ColorAcentoRojo;
            bordeImg.raycastTarget = false;
            var bordeRect = bordeGo.GetComponent<RectTransform>();
            bordeRect.anchorMin = new Vector2(0.5f, 1f);
            bordeRect.anchorMax = new Vector2(0.5f, 1f);
            bordeRect.pivot = new Vector2(0.5f, 1f);
            bordeRect.sizeDelta = new Vector2(560, 3);
            bordeRect.anchoredPosition = Vector2.zero;

            var tituloGo = new GameObject("Titulo");
            tituloGo.transform.SetParent(cuadroGo.transform, false);
            var titulo = tituloGo.AddComponent<Text>();
            titulo.text = "¿REGRESAR AL MENÚ?";
            titulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titulo.fontSize = 26;
            titulo.fontStyle = FontStyle.Bold;
            titulo.alignment = TextAnchor.MiddleCenter;
            titulo.color = ColorAcentoHover;
            var tituloRect = tituloGo.GetComponent<RectTransform>();
            tituloRect.anchorMin = new Vector2(0.5f, 1f);
            tituloRect.anchorMax = new Vector2(0.5f, 1f);
            tituloRect.pivot = new Vector2(0.5f, 1f);
            tituloRect.sizeDelta = new Vector2(500, 40);
            tituloRect.anchoredPosition = new Vector2(0, -40);

            var mensajeGo = new GameObject("Mensaje");
            mensajeGo.transform.SetParent(cuadroGo.transform, false);
            var mensaje = mensajeGo.AddComponent<Text>();
            mensaje.text = "Se perderá el progreso de la partida actual.";
            mensaje.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mensaje.fontSize = 18;
            mensaje.alignment = TextAnchor.MiddleCenter;
            mensaje.color = ColorTextoActivo;
            var mensajeRect = mensajeGo.GetComponent<RectTransform>();
            mensajeRect.anchorMin = new Vector2(0.5f, 1f);
            mensajeRect.anchorMax = new Vector2(0.5f, 1f);
            mensajeRect.pivot = new Vector2(0.5f, 1f);
            mensajeRect.sizeDelta = new Vector2(500, 60);
            mensajeRect.anchoredPosition = new Vector2(0, -100);

            // anchorMin/anchorMax/pivot = (x, 0) en CrearBotonConfirmacion: el punto de
            // referencia es el BORDE INFERIOR del cuadro, y la posición se cuenta hacia
            // arriba desde ahí (no desde el centro) — por eso va en positivo, no en -195.
            CrearBotonConfirmacion(cuadroGo.transform, "CANCELAR", new Vector2(-140, 45), OnCancelarSalir, ColorTextoActivo);
            CrearBotonConfirmacion(cuadroGo.transform, "CONFIRMAR", new Vector2(140, 45), OnConfirmarSalir, ColorAcentoRojo);

            panelConfirmarSalir.SetActive(false);
        }

        private void CrearBotonConfirmacion(Transform padre, string texto, Vector2 posicion,
            UnityEngine.Events.UnityAction accion, Color colorTexto)
        {
            var go = new GameObject($"Boton_{texto}");
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();
            imagen.color = new Color(1f, 1f, 1f, 0.06f);

            var boton = go.AddComponent<Button>();
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1f, 0.9f, 0.55f);
            boton.colors = colores;
            boton.targetGraphic = imagen;
            boton.onClick.AddListener(ReproducirClick);
            boton.onClick.AddListener(accion);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(220, 55);
            rect.anchoredPosition = posicion;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(go.transform, false);
            var textoUI = textoGo.AddComponent<Text>();
            textoUI.text = texto;
            textoUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoUI.fontSize = 20;
            textoUI.fontStyle = FontStyle.Bold;
            textoUI.alignment = TextAnchor.MiddleCenter;
            textoUI.color = colorTexto;
            textoUI.raycastTarget = false;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;
        }

        private void OnAbrirConfirmarSalir() => panelConfirmarSalir.SetActive(true);
        private void OnCancelarSalir() => panelConfirmarSalir.SetActive(false);

        private void OnConfirmarSalir()
        {
            // Restaurar timeScale antes de cambiar de escena: si no, el menú
            // principal (y su música/animaciones) arrancarían congelados.
            Time.timeScale = 1f;
            SceneManager.LoadScene(nombreEscenaMenu);
        }

        // ------------------------------------------------------------------
        // HELPERS DE UI — copiados de MenuPrincipal para que el panel de
        // pausa comparta el mismo estilo visual que "OPCIONES".
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

        private void OnCambiarVelocidad()
        {
            multiplicadorVelocidad = multiplicadorVelocidad >= 2f ? 1f : 2f;
            textoVelocidad.text = multiplicadorVelocidad >= 2f ? "VELOCIDAD x2" : "VELOCIDAD x1";
        }

        private void MostrarResultadoPartida(string mensaje)
        {
            textoResultadoPartida.text = mensaje;
            panelFinDePartida.SetActive(true);
            StartCoroutine(AnimarEntradaResultado());
        }

        // Entrada "con fuerza" del panel de resultado: escala desde 0 hasta
        // el tamaño normal con un pequeño rebote (back-out), para que el
        // anuncio del ganador/empate se sienta épico en vez de aparecer de
        // golpe y plano.
        private System.Collections.IEnumerator AnimarEntradaResultado()
        {
            var rect = panelFinDePartida.GetComponent<RectTransform>();
            const float duracion = 0.45f;
            const float overshoot = 1.7f;
            float tiempo = 0f;

            while (tiempo < duracion)
            {
                tiempo += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(tiempo / duracion) - 1f;
                float easeOut = p * p * ((overshoot + 1f) * p + overshoot) + 1f;
                rect.localScale = Vector3.one * Mathf.Max(easeOut, 0f);
                yield return null;
            }

            rect.localScale = Vector3.one;
        }

        // Crea uno de los dos botones del panel de fin de partida (REPETIR /
        // JUGAR DE NUEVO), ambos con el mismo tamaño y estilo, solo cambiando
        // color, texto, posición y acción.
        private Button CrearBotonFinDePartida(Transform padre, string nombre, string texto, Color color,
            Vector2 posicionAnclada, UnityEngine.Events.UnityAction accion)
        {
            var botonGo = new GameObject(nombre);
            botonGo.transform.SetParent(padre, false);
            var botonImagen = botonGo.AddComponent<Image>();
            botonImagen.color = color;
            var boton = botonGo.AddComponent<Button>();
            var botonRect = botonGo.GetComponent<RectTransform>();
            botonRect.anchorMin = new Vector2(0.5f, 0f);
            botonRect.anchorMax = new Vector2(0.5f, 0f);
            botonRect.pivot = new Vector2(0.5f, 0f);
            botonRect.sizeDelta = new Vector2(300, 64);
            botonRect.anchoredPosition = posicionAnclada;

            var botonTextoGo = new GameObject("Texto");
            botonTextoGo.transform.SetParent(botonGo.transform, false);
            var botonTexto = botonTextoGo.AddComponent<Text>();
            botonTexto.text = texto;
            botonTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            botonTexto.fontSize = 22;
            botonTexto.fontStyle = FontStyle.Bold;
            botonTexto.alignment = TextAnchor.MiddleCenter;
            botonTexto.color = ColorTextoActivo;
            var botonTextoRect = botonTextoGo.GetComponent<RectTransform>();
            botonTextoRect.anchorMin = Vector2.zero;
            botonTextoRect.anchorMax = Vector2.one;
            botonTextoRect.offsetMin = Vector2.zero;
            botonTextoRect.offsetMax = Vector2.zero;

            boton.onClick.AddListener(accion);
            return boton;
        }

        // "JUGAR DE NUEVO": vuelve a la pantalla de programación para armar
        // una partida nueva desde cero (nuevos tanques, nuevos scripts).
        private void OnJugarDeNuevo()
        {
            ReproducirClick();
            SceneManager.LoadScene(nombreEscenaProgramacion);
        }

        // "REPETIR": relanza la MISMA partida (mismos tanques, mismos
        // scripts, mismo tablero) sin pasar por la pantalla de programación.
        private void OnRepetir()
        {
            ReproducirClick();
            ConfiguracionPartidaPendiente.PrepararRepeticion();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ConstruirPanelesDeTanques(Transform padre)
        {
            panelesTanque.Clear();

            var agentes = gameManager.Agentes;
            if (agentes == null) return;

            // Máximo 6 recuadros (3 por lado), aunque el GameManager tenga
            // configurados más — el resto simplemente no muestra recuadro (la
            // cantidad real de tanques la define la programación).
            int total = Mathf.Min(agentes.Count, MAX_TANQUES_POR_LADO * 2);

            for (int i = 0; i < total; i++)
            {
                var (esIzquierda, indiceEnLado) = OrdenPaneles[i];
                var (anchor, posicion) = CalcularPosicionPanel(esIzquierda, indiceEnLado);

                var tanque = agentes[i].Tank;
                var panel = CrearPanelTanqueArcade(padre, $"PanelTanque_{i + 1}",
                    tanque, anchor, posicion, tamanoPanelTanque);

                panelesTanque.Add(panel);
            }
        }

        // Orden en el que se van llenando las 6 posiciones posibles a medida que
        // hay más tanques en la partida: primero las dos esquinas superiores
        // (izquierda y derecha), después los dos recuadros que quedan pegados a
        // esas esquinas (hacia el centro), y por último los dos de abajo. Con 2
        // tanques quedan uno en cada esquina superior (sin pares); con 6, las 4
        // posiciones de arriba (2 por lado) más las 2 de abajo.
        private static readonly (bool esIzquierda, int indiceEnLado)[] OrdenPaneles =
        {
            (true, 0),   // 1: arriba-izquierda (esquina)
            (false, 0),  // 2: arriba-derecha (esquina)
            (true, 1),   // 3: arriba-izquierda (junto al 1)
            (false, 1),  // 4: arriba-derecha (junto al 2)
            (true, 2),   // 5: abajo-izquierda
            (false, 2),  // 6: abajo-derecha
        };

        // Reparte hasta 3 tanques por lado SIN apilarlos en una columna vertical
        // (lo que antes hacía que el tercer recuadro invadiera el centro de la
        // pantalla, tapando el tablero). Ahora: los dos primeros de cada lado
        // van arriba, uno al lado del otro en fila horizontal ("los primeros",
        // pegados a su esquina superior); el tercero (si lo hay) va solo, abajo,
        // en la esquina inferior del mismo lado. Con 6 tanques en total quedan
        // 4 recuadros arriba (2 por esquina) y 2 abajo (1 por esquina), y ningún
        // recuadro queda nunca hacia el centro de la pantalla.
        private (Vector2 anchor, Vector2 posicion) CalcularPosicionPanel(bool esIzquierda, int indiceEnLado)
        {
            float signo = esIzquierda ? 1f : -1f;
            var anchorArriba = esIzquierda ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            var anchorAbajo = esIzquierda ? new Vector2(0f, 0f) : new Vector2(1f, 0f);

            if (indiceEnLado < 2)
            {
                // Fila horizontal arriba: el primero pegado a la esquina, el
                // segundo a su lado (hacia el centro), separados por el mismo
                // espacio que antes se usaba entre paneles apilados.
                float offsetX = margenBorde + indiceEnLado * (tamanoPanelTanque.x + espacioEntrePaneles);
                return (anchorArriba, new Vector2(signo * offsetX, -margenBorde));
            }

            // Tercer tanque del lado (si lo hay): solo, en la esquina de abajo.
            return (anchorAbajo, new Vector2(signo * margenBorde, margenBorde));
        }

        // ------------------------------------------------------------------
        // RECUADRO DE TANQUE ESTILO ARCADE PIXEL — HUD retro pero prolijo:
        // marco de dos capas con esquinas pixel brillantes, franja de
        // encabezado con el color del jugador, barra de vida segmentada tipo
        // "energía" de 8 bits, pips de bombas, ícono de escudo, y un sello
        // dramático de "DESTRUIDO" cuando el tanque cae.
        // ------------------------------------------------------------------

        private const int SEGMENTOS_BARRA_VIDA = 10;

        private PanelTanqueHud CrearPanelTanqueArcade(Transform padre, string nombre, Tank tanque,
            Vector2 anchor, Vector2 posicion, Vector2 tamano)
        {
            var color = ColorDeJugador(tanque.PlayerId);
            var hud = new PanelTanqueHud { colorJugador = color, capacidadBombas = Mathf.Max(1, tanque.Missiles) };

            // Marco exterior: el propio borde de color de acento, grueso, detrás
            // de un fondo casi negro con un margen de 3px -- el clásico "borde
            // pixel de dos tonos" de los HUD de consola retro.
            var marcoGo = new GameObject(nombre);
            marcoGo.transform.SetParent(padre, false);
            var marcoImg = marcoGo.AddComponent<Image>();
            marcoImg.color = color;
            var marcoRect = marcoGo.GetComponent<RectTransform>();
            marcoRect.anchorMin = anchor;
            marcoRect.anchorMax = anchor;
            marcoRect.pivot = anchor;
            marcoRect.sizeDelta = tamano;
            marcoRect.anchoredPosition = posicion;
            hud.raiz = marcoGo;

            const float borde = 3f;
            var fondoGo = new GameObject("Fondo");
            fondoGo.transform.SetParent(marcoGo.transform, false);
            var fondoImg = fondoGo.AddComponent<Image>();
            fondoImg.color = new Color(0.04f, 0.04f, 0.05f, 0.93f);
            var fondoRect = fondoGo.GetComponent<RectTransform>();
            fondoRect.anchorMin = Vector2.zero;
            fondoRect.anchorMax = Vector2.one;
            fondoRect.offsetMin = new Vector2(borde, borde);
            fondoRect.offsetMax = new Vector2(-borde, -borde);
            var contenido = fondoGo.transform;

            AgregarEsquinasPixel(contenido, color);

            // Encabezado: franja sólida del color del jugador con su nombre en
            // mayúsculas, alto contraste, look de placa metálica de tanque.
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(contenido, false);
            var headerImg = headerGo.AddComponent<Image>();
            headerImg.color = color;
            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 26);
            headerRect.anchoredPosition = Vector2.zero;

            var encabezadoGo = new GameObject("Texto");
            encabezadoGo.transform.SetParent(headerGo.transform, false);
            hud.encabezado = encabezadoGo.AddComponent<Text>();
            hud.encabezado.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.encabezado.fontSize = 17;
            hud.encabezado.fontStyle = FontStyle.Bold;
            hud.encabezado.alignment = TextAnchor.MiddleCenter;
            hud.encabezado.color = new Color(0.05f, 0.05f, 0.06f);
            hud.encabezado.text = $"JUGADOR {tanque.PlayerId}";
            var encRect = hud.encabezado.rectTransform;
            encRect.anchorMin = Vector2.zero;
            encRect.anchorMax = Vector2.one;
            encRect.offsetMin = Vector2.zero;
            encRect.offsetMax = Vector2.zero;
            var encSombra = encabezadoGo.AddComponent<Shadow>();
            encSombra.effectColor = new Color(1f, 1f, 1f, 0.4f);
            encSombra.effectDistance = new Vector2(1f, -1f);

            // Fila VIDA: etiqueta + barra segmentada de 10 bloques + porcentaje.
            CrearEtiquetaFilaHud(contenido, "VIDA", 36f, color);
            hud.segmentosVida = CrearBarraVidaSegmentada(contenido, 50f, SEGMENTOS_BARRA_VIDA);
            hud.textoPorcentajeVida = CrearTextoValorHud(contenido, 36f);

            // Fila BOMBAS: etiqueta + pips cuadrados + número.
            CrearEtiquetaFilaHud(contenido, "BOMBAS", 74f, color);
            hud.pipsBomba = CrearPipsHud(contenido, 88f, Mathf.Min(hud.capacidadBombas, 8), new Color(1f, 0.72f, 0.18f));
            hud.textoBombas = CrearTextoValorHud(contenido, 74f);

            // Fila ESCUDO: etiqueta + ícono que se enciende + estado.
            CrearEtiquetaFilaHud(contenido, "ESCUDO", 108f, color);
            var pipsEscudo = CrearPipsHud(contenido, 122f, 1, new Color(0.35f, 0.75f, 1f));
            hud.iconoEscudo = pipsEscudo[0];
            hud.textoEscudo = CrearTextoValorHud(contenido, 108f);

            // Log del turno: monoespaciado chico, look "consola de radio militar".
            var logGo = new GameObject("Log");
            logGo.transform.SetParent(contenido, false);
            hud.textoLog = logGo.AddComponent<Text>();
            hud.textoLog.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.textoLog.fontSize = 11;
            hud.textoLog.color = new Color(0.72f, 0.76f, 0.78f);
            hud.textoLog.alignment = TextAnchor.UpperLeft;
            hud.textoLog.horizontalOverflow = HorizontalWrapMode.Wrap;
            hud.textoLog.verticalOverflow = VerticalWrapMode.Truncate;
            var logRect = hud.textoLog.rectTransform;
            logRect.anchorMin = new Vector2(0, 0);
            logRect.anchorMax = new Vector2(1, 1);
            logRect.offsetMin = new Vector2(9, 6);
            logRect.offsetMax = new Vector2(-9, -146f);

            hud.overlayDestruido = CrearOverlayDestruido(contenido, tanque.PlayerId);

            return hud;
        }

        // 4 cuadraditos brillantes en las esquinas del recuadro: el detalle
        // "HUD de 8 bits" que distingue esto de un panel plano.
        private void AgregarEsquinasPixel(Transform padre, Color color)
        {
            void Esquina(Vector2 anclaEsquina, Vector2 signo)
            {
                var go = new GameObject("EsquinaPixel");
                go.transform.SetParent(padre, false);
                var img = go.AddComponent<Image>();
                img.color = color;
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = anclaEsquina;
                rect.anchorMax = anclaEsquina;
                rect.pivot = anclaEsquina;
                rect.sizeDelta = new Vector2(7, 7);
                rect.anchoredPosition = new Vector2(signo.x * 3f, signo.y * 3f);
            }

            Esquina(new Vector2(0, 0), new Vector2(1, 1));
            Esquina(new Vector2(1, 0), new Vector2(-1, 1));
            Esquina(new Vector2(0, 1), new Vector2(1, -1));
            Esquina(new Vector2(1, 1), new Vector2(-1, -1));
        }

        // Etiqueta corta en mayúsculas ("VIDA", "BOMBAS", "ESCUDO"), teñida con
        // el color de acento del jugador para que las tres filas se lean como
        // parte del mismo panel.
        private void CrearEtiquetaFilaHud(Transform padre, string texto, float yDesdeArriba, Color color)
        {
            var go = new GameObject($"Etiqueta_{texto}");
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 11;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.UpperLeft;
            t.color = color;
            t.text = texto;
            var rect = t.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(70, 14);
            rect.anchoredPosition = new Vector2(9, -yDesdeArriba);
        }

        // Número/estado alineado a la derecha de una fila (porcentaje de vida,
        // cantidad de bombas, o el estado del escudo).
        private Text CrearTextoValorHud(Transform padre, float yDesdeArriba)
        {
            var go = new GameObject("Valor");
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.UpperRight;
            t.color = ColorTextoActivo;
            var rect = t.rectTransform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(60, 14);
            rect.anchoredPosition = new Vector2(-9, -yDesdeArriba);
            return t;
        }

        // Barra de vida "de 8 bits": una fila de bloques con un hueco oscuro
        // entre cada uno, en vez de una barra continua -- así se lee como
        // energía de arcade y no como un simple slider.
        private Image[] CrearBarraVidaSegmentada(Transform padre, float yDesdeArriba, int cantidad)
        {
            var contenedorGo = new GameObject("BarraVida");
            contenedorGo.transform.SetParent(padre, false);
            var contRect = contenedorGo.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0, 1);
            contRect.anchorMax = new Vector2(1, 1);
            contRect.pivot = new Vector2(0, 1);
            contRect.offsetMin = new Vector2(9, 0);
            contRect.offsetMax = new Vector2(-60, 0);
            contRect.anchoredPosition = new Vector2(0, -yDesdeArriba);
            contRect.sizeDelta = new Vector2(0, 12);

            var fondoBarra = contenedorGo.AddComponent<Image>();
            fondoBarra.color = new Color(0f, 0f, 0f, 0.55f);

            var segmentos = new Image[cantidad];
            float espacio = 2f;
            for (int i = 0; i < cantidad; i++)
            {
                var segGo = new GameObject($"Segmento_{i}");
                segGo.transform.SetParent(contenedorGo.transform, false);
                var img = segGo.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.22f, 1f);
                var rect = img.rectTransform;
                rect.anchorMin = new Vector2((float)i / cantidad, 0f);
                rect.anchorMax = new Vector2((float)(i + 1) / cantidad, 1f);
                rect.offsetMin = new Vector2(espacio * 0.5f, 1f);
                rect.offsetMax = new Vector2(-espacio * 0.5f, -1f);
                segmentos[i] = img;
            }

            return segmentos;
        }

        // Fila de "pips" cuadrados (para bombas y para el escudo): cada uno
        // prende o se apaga según corresponda, look de vidas/ítems de arcade.
        private Image[] CrearPipsHud(Transform padre, float yDesdeArriba, int cantidad, Color colorEncendido)
        {
            var pips = new Image[Mathf.Max(1, cantidad)];
            for (int i = 0; i < pips.Length; i++)
            {
                var go = new GameObject($"Pip_{i}");
                go.transform.SetParent(padre, false);
                var img = go.AddComponent<Image>();
                img.color = colorEncendido;
                var rect = img.rectTransform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(10, 10);
                rect.anchoredPosition = new Vector2(9 + i * 14f, -yDesdeArriba);
                pips[i] = img;
            }

            return pips;
        }

        // Sello "DESTRUIDO": tinte oscuro sobre todo el recuadro, una gran cruz
        // en aspa (como un tanque quemado, marcado con una X) y el texto con
        // doble contorno y sombra para que se sienta dramático, no un simple
        // cartel plano.
        private GameObject CrearOverlayDestruido(Transform padre, int playerId)
        {
            var overlayGo = new GameObject("OverlayDestruido");
            overlayGo.transform.SetParent(padre, false);
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(0.08f, 0.02f, 0.02f, 0.82f);
            var overlayRect = overlayImg.rectTransform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            void Aspa(float rotacion)
            {
                var go = new GameObject("Aspa");
                go.transform.SetParent(overlayGo.transform, false);
                var img = go.AddComponent<Image>();
                img.color = new Color(0.55f, 0.08f, 0.08f, 0.55f);
                var rect = img.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(260, 6);
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0, 0, rotacion);
            }

            Aspa(28f);
            Aspa(-28f);

            var selloGo = new GameObject("Texto");
            selloGo.transform.SetParent(overlayGo.transform, false);
            var sello = selloGo.AddComponent<Text>();
            sello.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sello.fontSize = 22;
            sello.fontStyle = FontStyle.Bold;
            sello.alignment = TextAnchor.MiddleCenter;
            sello.color = new Color(0.95f, 0.2f, 0.15f);
            sello.text = "DESTRUIDO";
            var selloRect = sello.rectTransform;
            selloRect.anchorMin = new Vector2(0, 0.32f);
            selloRect.anchorMax = new Vector2(1, 0.62f);
            selloRect.offsetMin = Vector2.zero;
            selloRect.offsetMax = Vector2.zero;
            selloRect.localRotation = Quaternion.Euler(0, 0, -6f);

            var contornoOscuro = selloGo.AddComponent<Outline>();
            contornoOscuro.effectColor = new Color(0f, 0f, 0f, 0.9f);
            contornoOscuro.effectDistance = new Vector2(2f, -2f);
            var sombraSello = selloGo.AddComponent<Shadow>();
            sombraSello.effectColor = new Color(0f, 0f, 0f, 0.7f);
            sombraSello.effectDistance = new Vector2(0f, -3f);

            overlayGo.SetActive(false);
            return overlayGo;
        }

        private void ActualizarHud()
        {
            var agentes = gameManager.Agentes;
            if (agentes == null) return;

            // ToList() para no re-enumerar el log varias veces por cada panel.
            var log = gameManager.Turno?.LastRoundLog?.ToList() ?? new List<string>();

            for (int i = 0; i < agentes.Count && i < panelesTanque.Count; i++)
            {
                var tanque = agentes[i].Tank;
                ActualizarPanelTanqueArcade(panelesTanque[i], tanque, log);
            }
        }

        // Actualiza un recuadro ya construido: enciende/apaga segmentos de vida
        // con el color según el nivel (verde / amarillo / rojo, como cualquier
        // barra de energía de arcade), prende los pips de bombas y de escudo, y
        // arma el trocito de log de este tanque. Si el tanque cayó, en vez de
        // tocar todo lo anterior simplemente se muestra el sello "DESTRUIDO".
        private void ActualizarPanelTanqueArcade(PanelTanqueHud hud, Tank tanque, List<string> log)
        {
            hud.overlayDestruido.SetActive(!tanque.IsAlive);
            if (!tanque.IsAlive) return;

            float porcentaje = Mathf.Clamp(tanque.Health, 0f, 100f);
            Color colorVida = porcentaje > 60f
                ? new Color(0.35f, 0.9f, 0.35f)
                : porcentaje > 30f
                    ? new Color(0.98f, 0.82f, 0.2f)
                    : new Color(0.95f, 0.25f, 0.2f);

            int segmentosLlenos = Mathf.RoundToInt(porcentaje / 100f * hud.segmentosVida.Length);
            for (int i = 0; i < hud.segmentosVida.Length; i++)
                hud.segmentosVida[i].color = i < segmentosLlenos ? colorVida : new Color(0.2f, 0.2f, 0.22f, 1f);

            hud.textoPorcentajeVida.text = $"{Mathf.RoundToInt(porcentaje)}%";
            hud.textoPorcentajeVida.color = colorVida;

            int bombasEncendidas = Mathf.Clamp(tanque.Missiles, 0, hud.pipsBomba.Length);
            for (int i = 0; i < hud.pipsBomba.Length; i++)
            {
                var pip = hud.pipsBomba[i];
                bool encendido = i < bombasEncendidas;
                pip.color = encendido
                    ? new Color(1f, 0.72f, 0.18f)
                    : new Color(0.22f, 0.2f, 0.18f, 1f);
            }
            hud.textoBombas.text = tanque.Missiles.ToString();

            bool escudoActivo = tanque.ShieldActive;
            hud.iconoEscudo.color = escudoActivo
                ? new Color(0.35f, 0.8f, 1f)
                : new Color(0.2f, 0.22f, 0.24f, 1f);
            hud.textoEscudo.text = escudoActivo ? "ACTIVO" : "—";
            hud.textoEscudo.color = escudoActivo ? new Color(0.35f, 0.8f, 1f) : ColorTextoInactivo;

            var lineasDelTanque = log
                .Where(l => l.StartsWith($"Jugador {tanque.PlayerId}") && !l.Contains("IF") && !l.Contains("ESPERA"))
                .ToList();

            hud.textoLog.text = lineasDelTanque.Count == 0
                ? ""
                : string.Join("\n", lineasDelTanque.Skip(Mathf.Max(0, lineasDelTanque.Count - 3)));
        }
    }
}