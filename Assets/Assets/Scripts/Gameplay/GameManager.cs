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
            var result = turnManager.ExecuteRound();

            foreach (var line in turnManager.LastRoundLog)
                Debug.Log(line);

            RefrescarVista();

            if (vistaTablero != null)
            {
                vistaTablero.ActualizarMinas(board.MinePositions());
                vistaTablero.ReproducirDisparos(turnManager.LastRoundShots);
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

        private void RefrescarVista()
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

            vistaTablero.ActualizarTanques(datos);
        }
        // El multiplicador fijo (x1.6) que había antes no tenía en cuenta el
        // campo de visión real de la cámara ni el aspecto de pantalla, así que
        // acertaba más o menos para un tamaño de tablero concreto y se quedaba
        // corto en otros (por ejemplo 20x20, que quedaba cortado por los
        // bordes). Ahora se calcula la distancia mínima real, con trigonometría,
        // para que el círculo que envuelve todo el tablero entre en el cono de
        // visión de la cámara, tanto en vertical como en horizontal según su
        // relación de aspecto.
        private void CentrarCamaraEnTablero()
        {
            var camaraOrbit = FindFirstObjectByType<OrbitZoomCamera>();
            if (camaraOrbit == null) return;

            float lado = vistaTablero.tamanoCelda;
            var centro = new Vector3((board.Width - 1) * lado * 0.5f, 0f,
                (board.Height - 1) * lado * 0.5f);
            camaraOrbit.PanTo(centro);

            // Radio de la circunferencia que envuelve el tablero completo,
            // con un 15% extra de margen para que no quede pegado al borde
            // de la pantalla.
            float radioTablero = 0.5f * lado *
                Mathf.Sqrt(board.Width * board.Width + board.Height * board.Height) * 1.15f;

            var camaraUnity = camaraOrbit.GetComponent<Camera>();
            float distanciaNecesaria;

            if (camaraUnity != null && !camaraUnity.orthographic)
            {
                // Distancia mínima para que el radio entre en el FOV vertical...
                float mitadFovVerticalRad = camaraUnity.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distanciaVertical = radioTablero / Mathf.Tan(mitadFovVerticalRad);

                // ...y distancia mínima para que entre en el FOV horizontal
                // (que depende del aspecto de pantalla: en una ventana ancha
                // sobra horizontal, pero en una angosta puede ser el límite).
                float mitadFovHorizontalRad = Mathf.Atan(Mathf.Tan(mitadFovVerticalRad) * camaraUnity.aspect);
                float distanciaHorizontal = radioTablero / Mathf.Tan(mitadFovHorizontalRad);

                distanciaNecesaria = Mathf.Max(distanciaVertical, distanciaHorizontal);
            }
            else
            {
                // Cámara ortográfica u OrbitZoomCamera sin Camera propia:
                // se conserva el cálculo anterior como respaldo razonable.
                distanciaNecesaria = Mathf.Max(board.Width, board.Height) * lado * 1.6f;
            }

            // Si el tablero pedido (ej. 20x20) necesita alejarse más de lo que la
            // cámara tiene configurado como límite, hay que subir ese límite --
            // si no, un tablero grande queda cortado aunque el cálculo de arriba
            // esté bien, porque el Clamp de abajo lo topa antes de tiempo.
            if (distanciaNecesaria > camaraOrbit.maxDistance)
                camaraOrbit.maxDistance = distanciaNecesaria;

            camaraOrbit.distance = Mathf.Clamp(distanciaNecesaria, camaraOrbit.minDistance, camaraOrbit.maxDistance);
        }
    }
}