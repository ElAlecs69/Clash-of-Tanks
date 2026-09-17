using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TanksGame.Core;
using TanksGame.Language;
using TanksGame.Visual;
using TanksGame.CameraControl;

namespace TanksGame.Gameplay
{
    // Configuración de un tanque: nombre, misiles iniciales y su programa
    // (en el mini-lenguaje propio: IF RADAR, MISIL, MOV, REPARAR, ESPERAR, etc.)
    [System.Serializable]
    public class ConfiguracionTanque
    {
        public string nombre = "Tanque";
        [Min(1)] public int misilesIniciales = 5;
        public TanqueSkinDatos skin;

        [TextArea(5, 15)]
        public string programa = "ESPERAR";
    }

    // Componente principal: arma el tablero (tamaño configurable NxM), entre 2 y 8
    // tanques según "configuracionTanques", y ejecuta una ronda cada vez que se llama
    // a EjecutarSiguienteTurno() (conéctalo a un botón "Siguiente turno" en tu UI).
    public class GameManager : MonoBehaviour
    {
        [Header("Tamaño del tablero (NxM)")]
        [Min(2)] public int anchoTablero = 8;
        [Min(2)] public int altoTablero = 8;

        [Header("Vista visual del tablero (opcional)")]
        public BoardView vistaTablero;

        [Header("Configuración de jugadores")]
        [Tooltip("Cuántos de los tanques definidos abajo se usan en esta partida (2 a 8).")]
        [Range(2, 8)] public int cantidadTanques = 2;

        [Tooltip("Define hasta 8 tanques. Solo se usan los primeros 'cantidadTanques'.")]
        public ConfiguracionTanque[] configuracionTanques = new ConfiguracionTanque[]
        {
            new ConfiguracionTanque
            {
                nombre = "Jugador 1",
                programa = "IF RADAR(E) > 0\nMISIL\nIF RADAR(E) < 0\nMOV(E)\nESPERAR"
            },
            new ConfiguracionTanque
            {
                nombre = "Jugador 2",
                programa = "IF VIDA < 40\nREPARAR\nIF RADAR(O) > 0\nAMT(O)\nMOV(O)"
            }
        };

        private GridBoard board;
        private TurnManager turnManager;
        private List<TankAgent> agents;

        // Acceso de solo lectura para scripts de UI/HUD.
        public IReadOnlyList<TankAgent> Agentes => agents;
        public TurnManager Turno => turnManager;

        // Se usa Awake() en vez de Start() para garantizar que los tanques ya
        // existan (gameManager.Agentes con datos) antes de que GameplayUI.Start()
        // intente construir un panel por cada uno — Unity ejecuta todos los
        // Awake() de la escena antes que cualquier Start().
        private void Awake()
        {
            AplicarConfiguracionDesdeProgramacionSiCorresponde();

            board = new GridBoard(anchoTablero, altoTablero);
            board.SetHospital(new Vector2Int(0, 0));

            agents = new List<TankAgent>();

            // No se puede usar más tanques de los que hay configurados ni menos de 2.
            int total = Mathf.Clamp(cantidadTanques, 2, 8);
            total = Mathf.Min(total, configuracionTanques.Length);

            var posiciones = CalcularPosicionesIniciales(total);

            for (int i = 0; i < total; i++)
            {
                var config = configuracionTanques[i];
                var (posicion, direccion) = posiciones[i];

                var tank = new Tank(i + 1, posicion, missiles: config.misilesIniciales)
                {
                    Facing = direccion
                };

                var agent = new TankAgent(tank)
                {
                    Program = TankProgramParser.Parse(config.programa),
                    Skin = config.skin
                };

                agents.Add(agent);
            }

            turnManager = new TurnManager(board, agents);

            if (vistaTablero != null)
            {
                vistaTablero.Construir(board.Width, board.Height);
                RefrescarVista();
                CentrarCamaraEnTablero();
            }
        }

        // Si el jugador acaba de programar tanques en PantallaProgramacionTanques,
        // usa esos scripts y ese tamaño de tablero en vez de la configuración fija
        // del Inspector de más arriba. Si no hay nada pendiente (por ejemplo, si
        // esta escena se abrió directo para probarla), sigue usando esa
        // configuración normal sin tocar nada.
        private void AplicarConfiguracionDesdeProgramacionSiCorresponde()
        {
            if (!ConfiguracionPartidaPendiente.Hay) return;

            var scripts = ConfiguracionPartidaPendiente.ScriptsPorTanque;
            var skins = ConfiguracionPartidaPendiente.SkinsPorTanque;

            configuracionTanques = new ConfiguracionTanque[scripts.Count];
            for (int i = 0; i < scripts.Count; i++)
            {
                configuracionTanques[i] = new ConfiguracionTanque
                {
                    nombre = $"Tanque {i + 1}",
                    programa = scripts[i],
                    skin = (skins != null && i < skins.Count) ? skins[i] : default
                };
            }

            cantidadTanques = scripts.Count;

            int tamano = Mathf.Max(2, ConfiguracionPartidaPendiente.TamanoTablero);
            anchoTablero = tamano;
            altoTablero = tamano;

            ConfiguracionPartidaPendiente.Limpiar();
        }

        // Reparte las posiciones iniciales: primero las 4 esquinas del tablero y,
        // si hacen falta más tanques, los puntos medios de los 4 bordes. Cada tanque
        // queda orientado hacia el centro del tablero. Ajusta este método si quieres
        // otra distribución (por ejemplo, en círculo).
        private List<(Vector2Int posicion, Direction direccion)> CalcularPosicionesIniciales(int cantidad)
        {
            int maxX = anchoTablero - 1;
            int maxY = altoTablero - 1;
            int midX = anchoTablero / 2;
            int midY = altoTablero / 2;

            var candidatos = new List<(Vector2Int posicion, Direction direccion)>
            {
                (new Vector2Int(0, maxY), Direction.E),     // arriba-izquierda
                (new Vector2Int(maxX, 0), Direction.O),     // abajo-derecha
                (new Vector2Int(0, 0), Direction.N),        // abajo-izquierda
                (new Vector2Int(maxX, maxY), Direction.S),  // arriba-derecha
                (new Vector2Int(midX, maxY), Direction.S),  // medio-arriba
                (new Vector2Int(midX, 0), Direction.N),     // medio-abajo
                (new Vector2Int(0, midY), Direction.E),     // medio-izquierda
                (new Vector2Int(maxX, midY), Direction.O),  // medio-derecha
            };

            return candidatos.Take(cantidad).ToList();
        }

        public GameResult EjecutarSiguienteTurno()
        {
            // Posición de cada tanque ANTES de ejecutar la ronda, para saber
            // después cuáles se movieron y animar solo esos.
            var posicionesAntes = agents.ToDictionary(a => a.Tank.PlayerId, a => a.Tank.Position);
            // Vida ANTES de la ronda: a los tanques que reciban un disparo
            // esta ronda se les sigue mostrando esta vida (sin daño) hasta
            // que su explosión se reproduzca en pantalla; ver más abajo.
            var vidaAntes = agents.ToDictionary(a => a.Tank.PlayerId, a => Mathf.RoundToInt(a.Tank.Health));

            var result = turnManager.ExecuteRound();

            foreach (var line in turnManager.LastRoundLog)
                Debug.Log(line);

            // IDs de los tanques que fueron golpeados por un disparo, mina,
            // choque o desgaste esta ronda: su apariencia dañada (chapa
            // quemada/humo/volcado) no se debe aplicar todavía -- BoardView
            // la aplicará en el momento exacto en que se reproduzca la
            // animación de ESE evento en particular (impacto de disparo,
            // detonación de mina, choque o sacudida por desgaste).
            var golpeadosPorDisparo = new HashSet<int>(
                turnManager.LastRoundShots.Where(s => s.Impacto).Select(s => s.TargetId));
            var golpeadosPorOtrosEventos = new HashSet<int>(
                turnManager.LastRoundDamageEvents.SelectMany(e => e.TargetIds));
            var todosLosGolpeados = new HashSet<int>(golpeadosPorDisparo.Concat(golpeadosPorOtrosEventos));

            // Vida, aparición y muerte se actualizan ya mismo para todos los
            // tanques EXCEPTO los golpeados esta ronda (esos muestran su
            // vida/estado de ANTES hasta que se reproduzca su evento).
            // Tampoco se anima movimiento aquí (animarMovimiento: false): el
            // movimiento, los disparos y los demás eventos los reproduce,
            // en orden, ReproducirRondaSecuencial más abajo.
            var datosVisuales = agents.Select(a =>
                (playerId: a.Tank.PlayerId,
                 posicion: a.Tank.Position,
                 vivo: todosLosGolpeados.Contains(a.Tank.PlayerId) ? true : a.Tank.IsAlive,
                 vidaPorcentaje: todosLosGolpeados.Contains(a.Tank.PlayerId)
                     ? vidaAntes[a.Tank.PlayerId]
                     : Mathf.RoundToInt(a.Tank.Health),
                 skin: a.Skin));

            if (vistaTablero != null)
            {
                vistaTablero.ActualizarTanques(datosVisuales, animarMovimiento: false);
                vistaTablero.ActualizarMinas(board.MinePositions());

                // Se copia cada disparo a un diccionario propio de esta
                // llamada: turnManager.LastRoundShots es la MISMA lista que
                // TurnManager vacía al empezar la próxima ronda. Si se la
                // pasáramos tal cual a una corrutina de animación, un turno
                // automático que arranca antes de que esa corrutina termine
                // la vacía a mitad de camino y la corrutina explota con
                // "Collection was modified", perdiendo el disparo pendiente
                // (por eso el jugador 3 se quedaba sin animar su MISIL).
                var disparosPorJugador = turnManager.LastRoundShots
                    .GroupBy(s => s.ShooterId)
                    .ToDictionary(g => g.Key, g => g.First());

                // Mismo criterio que con los disparos: se copia a un
                // diccionario propio de esta llamada porque LastRoundRadars
                // es la MISMA lista que TurnManager vacía al empezar la
                // próxima ronda.
                var radaresPorJugador = turnManager.LastRoundRadars
                    .GroupBy(r => r.ShooterId)
                    .ToDictionary(g => g.Key, g => g.First());

                var pasos = agents.Select(a => new BoardView.PasoRonda
                {
                    PlayerId = a.Tank.PlayerId,
                    SeMovio = posicionesAntes.TryGetValue(a.Tank.PlayerId, out var antes)
                              && antes != a.Tank.Position,
                    Destino = a.Tank.Position,
                    Disparo = disparosPorJugador.TryGetValue(a.Tank.PlayerId, out var disparo) ? disparo : null,
                    Radar = radaresPorJugador.TryGetValue(a.Tank.PlayerId, out var radarEv) ? radarEv : null
                }).ToList();

                // Igual que con los disparos: se copian los eventos a listas
                // propias de esta llamada, porque LastRoundDamageEvents es
                // la MISMA lista que TurnManager reutiliza y vacía en la
                // próxima ronda.
                var eventos = turnManager.LastRoundDamageEvents
                    .Select(e => new BoardView.EventoDanoVisual
                    {
                        Tipo = e.Tipo,
                        Celda = e.Celda,
                        CeldaB = e.CeldaB,
                        TargetIds = new List<int>(e.TargetIds),
                        VidaPorcentajeDespuesPorId = new Dictionary<int, int>(e.VidaPorcentajeDespuesPorId),
                        DestruidosIds = new HashSet<int>(e.DestruidosIds)
                    }).ToList();

                vistaTablero.ReproducirRondaSecuencial(pasos, eventos);
            }

            if (result == GameResult.PlayerWins)
            {
                var ganador = turnManager.AliveTanks().First();
                Debug.Log($"¡GANADOR: Jugador {ganador.PlayerId}!");
            }
            else if (result == GameResult.Draw)
            {
                Debug.Log("Empate: todos los tanques restantes fueron destruidos.");
            }

            return result;
        }

        private void RefrescarVista(bool animarMovimiento = true)
        {
            if (vistaTablero == null) return;

            // Se usa la sobrecarga con vida (Mathf.RoundToInt(a.Tank.Health))
            // en vez de la de 4 elementos: así BoardView puede mostrar el
            // estado de daño real de cada tanque (chapa quemada a partir de
            // 50% y volcado/incinerado al llegar a 0%) en vez de asumir
            // siempre 100% de vida.
            var datos = agents.Select(a =>
                (playerId: a.Tank.PlayerId, posicion: a.Tank.Position, vivo: a.Tank.IsAlive,
                 vidaPorcentaje: Mathf.RoundToInt(a.Tank.Health), skin: a.Skin));

            vistaTablero.ActualizarTanques(datos, animarMovimiento);
        }
        // El multiplicador fijo (x1.6) que había antes no tenía en cuenta el
        // tamaño real de la cámara ortográfica ni el aspecto de pantalla, así
        // que acertaba más o menos para un tamaño de tablero concreto y se
        // quedaba corto en otros (por ejemplo 20x20, que quedaba cortado por
        // los bordes). Ahora se calcula el tamaño ortográfico mínimo real,
        // con trigonometría simple (sin FOV: en Orthographic no hay
        // perspectiva), para que el círculo que envuelve todo el tablero
        // entre en el encuadre, tanto en vertical como en horizontal según
        // el aspecto de pantalla.
        // IMPORTANTE: este método asume que la Camera es Orthographic (así
        // está configurada la Main Camera del proyecto). Antes había también
        // una rama para cámara en perspectiva basada en fieldOfView; se quitó
        // por completo -- nunca se usaba con la cámara real de la escena.
        //
        // Estos valores DEBEN coincidir con los que usa GameplayUI para armar
        // los recuadros de estadísticas (tamanoPanelTanque, margenBorde,
        // espacioEntrePaneles y el máximo de 3 recuadros por lado, repartidos
        // en fila arriba + uno solo abajo -- ver
        // GameplayUI.CalcularPosicionPanel()). Se duplican acá (en vez de
        // referenciar GameplayUI directamente) porque CentrarCamaraEnTablero()
        // corre en Awake(), antes de que GameplayUI.Start() arme esos recuadros.
        private const int MAX_TANQUES_POR_LADO_HUD = 3;
        private const float ANCHO_PANEL_HUD = 300f;
        private const float ALTO_PANEL_HUD = 200f;
        private const float ESPACIO_PANEL_HUD = 20f;
        private const float MARGEN_BORDE_HUD = 20f;

        [Header("Ajuste fino de encuadre de cámara")]
        [Tooltip("Cuántas 'casillas' (tamanoCelda) se sube el punto que mira la cámara. Sube el punto de mira -> el tablero se ve más abajo en pantalla.")]
        public float desplazamientoAbajoCeldas = 1f;
        [Tooltip("Cuántas 'casillas' se corre el punto de mira hacia la izquierda en pantalla -> el tablero se ve desplazado hacia la derecha.")]
        public float desplazamientoDerechaCeldas = 3f;

        private void CentrarCamaraEnTablero()
        {
            var camaraOrbit = FindFirstObjectByType<OrbitZoomCamera>();
            if (camaraOrbit == null)
            {
                Debug.LogWarning("CentrarCamaraEnTablero: no se encontró ningún OrbitZoomCamera en la escena -- el encuadre NO se aplicó.");
                return;
            }

            var camaraUnity = camaraOrbit.GetComponent<Camera>();
            if (camaraUnity == null || !camaraUnity.orthographic)
            {
                Debug.LogWarning("CentrarCamaraEnTablero: la Camera no está en modo Orthographic -- este método fue simplificado para trabajar solo en ese modo, el encuadre NO se aplicó.");
                return;
            }

            float lado = vistaTablero.tamanoCelda;
            var centro = new Vector3((board.Width - 1) * lado * 0.5f, 0f,
                (board.Height - 1) * lado * 0.5f);

            // "Derecha en pantalla" según el yaw inicial (45°) de la cámara
            // isométrica -- ver OrbitZoomCamera.yaw. Se usa este valor fijo (en
            // vez de leer el yaw actual del componente) porque este método
            // corre en Awake(), antes de que el jugador haya podido orbitar
            // la cámara con el botón derecho.
            var derechaEnPantalla = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;

            var centroAjustado = centro
                + Vector3.up * (desplazamientoAbajoCeldas * lado)
                - derechaEnPantalla * (desplazamientoDerechaCeldas * lado);

            camaraOrbit.PanTo(centroAjustado);

            // Radio de la circunferencia que envuelve el tablero completo,
            // con un 15% extra de margen para que no quede pegado al borde
            // de la pantalla.
            float radioTablero = 0.5f * lado *
                Mathf.Sqrt(board.Width * board.Width + board.Height * board.Height) * 1.15f;

            // Los recuadros de estadísticas ya NO se apilan en columna: hasta
            // dos por lado van arriba, uno al lado del otro (fila horizontal),
            // y un tercero (si lo hay) va solo, abajo -- ver
            // GameplayUI.CalcularPosicionPanel(). Acá se calcula qué fracción
            // de la pantalla les queda libre al tablero, en cada eje, y se
            // exige que el tablero entre en esa fracción en vez de en la
            // pantalla completa.
            int totalTanques = Mathf.Min(agents?.Count ?? 0, MAX_TANQUES_POR_LADO_HUD * 2);
            int porLado = Mathf.Min(MAX_TANQUES_POR_LADO_HUD, Mathf.CeilToInt(totalTanques / 2f));
            bool hayPanelAbajo = porLado >= 3; // el tercer tanque de algún lado cae solo, abajo.

            // Horizontal: cuando un lado tiene 2 o más tanques, esos dos
            // primeros quedan uno junto al otro (fila), así que el ancho que
            // ocupan en ese borde es el de DOS recuadros, no de uno solo.
            int panelesEnFila = Mathf.Min(porLado, 2);
            float margenHorizontalPx = MARGEN_BORDE_HUD
                + panelesEnFila * ANCHO_PANEL_HUD
                + Mathf.Max(0, panelesEnFila - 1) * ESPACIO_PANEL_HUD
                + 20f; // + un respiro extra

            // Vertical: como ya no hay columnas apiladas, cada borde (arriba y,
            // si corresponde, abajo) ocupa como mucho la altura de UN recuadro.
            float margenVerticalUnaFilaPx = MARGEN_BORDE_HUD + ALTO_PANEL_HUD + 20f;

            float anchoPantalla = Mathf.Max(1, Screen.width);
            float altoPantalla = Mathf.Max(1, Screen.height);

            // Los recuadros ocupan ambos bordes IZQUIERDO/DERECHO a la vez, así
            // que se descuenta el doble del margen horizontal. En vertical se
            // descuenta el margen de arriba siempre, y el de abajo solo si
            // efectivamente hay un tercer recuadro colgando del borde inferior.
            float margenVerticalTotalPx = margenVerticalUnaFilaPx * (hayPanelAbajo ? 2f : 1f);

            float fraccionHorizontalLibre = Mathf.Clamp((anchoPantalla - 2f * margenHorizontalPx) / anchoPantalla, 0.35f, 1f);
            float fraccionVerticalLibre = Mathf.Clamp((altoPantalla - margenVerticalTotalPx) / altoPantalla, 0.35f, 1f);

            // En Orthographic, "orthographicSize" es la MITAD de lo que se ve
            // verticalmente, en unidades de mundo; lo que se ve horizontalmente
            // es orthographicSize * aspect. Para que el círculo del tablero
            // (radioTablero) entre en cada eje, respetando además la fracción
            // libre que dejan los recuadros de estadísticas:
            float sizeVertical = radioTablero / fraccionVerticalLibre;
            float sizeHorizontal = (radioTablero / Mathf.Max(0.01f, camaraUnity.aspect)) / fraccionHorizontalLibre;
            float sizeNecesario = Mathf.Max(sizeVertical, sizeHorizontal);

            // SetOrthographicSize ya sube maxOrthographicSize si hace falta
            // (para que un tablero grande, como 20x20, no quede topado por el
            // límite configurado en el Inspector) y clampea contra
            // minOrthographicSize/maxOrthographicSize.
            camaraOrbit.SetOrthographicSize(sizeNecesario);
        }
    }
}