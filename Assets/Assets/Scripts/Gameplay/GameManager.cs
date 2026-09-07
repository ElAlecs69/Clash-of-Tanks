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
                    Program = TankProgramParser.Parse(config.programa)
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

            var datos = agents.Select(a =>
                (playerId: a.Tank.PlayerId, posicion: a.Tank.Position, vivo: a.Tank.IsAlive));

            vistaTablero.ActualizarTanques(datos);
        }
        private void CentrarCamaraEnTablero()
        {
            var camara = FindFirstObjectByType<OrbitZoomCamera>();
            if (camara == null) return;

            float lado = vistaTablero.tamanoCelda;
            var centro = new Vector3((board.Width - 1) * lado * 0.5f, 0f,
                (board.Height - 1) * lado * 0.5f);
            camara.PanTo(centro);

            float distanciaNecesaria = Mathf.Max(board.Width, board.Height) * lado * 1.6f;
            camara.distance = Mathf.Clamp(distanciaNecesaria, camara.minDistance, camara.maxDistance);
        }
    }
}