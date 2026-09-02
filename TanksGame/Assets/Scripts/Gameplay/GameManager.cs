using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TanksGame.Core;
using TanksGame.Language;
using TanksGame.Visual;

namespace TanksGame.Gameplay
{
    // Componente de ejemplo: arma el tablero (tamaño configurable NxM), dos tanques
    // con un programa de prueba, y ejecuta una ronda cada vez que se llama a
    // EjecutarSiguienteTurno() (conéctalo a un botón "Siguiente turno" en tu UI).
    public class GameManager : MonoBehaviour
    {
        [Header("Tamaño del tablero (NxM)")]
        [Min(2)] public int anchoTablero = 8;
        [Min(2)] public int altoTablero = 8;

        [Header("Vista visual del tablero (opcional)")]
        public BoardView vistaTablero;

        [TextArea(5, 15)]
        public string programaJugador1 =
            "IF RADAR(E) > 0\nMISIL\nIF RADAR(E) < 0\nMOV(E)\nESPERAR";

        [TextArea(5, 15)]
        public string programaJugador2 =
            "IF VIDA < 40\nREPARAR\nIF RADAR(O) > 0\nAMT(O)\nMOV(O)";

        private GridBoard board;
        private TurnManager turnManager;
        private List<TankAgent> agents;

        // Acceso de solo lectura para scripts de UI/HUD.
        public IReadOnlyList<TankAgent> Agentes => agents;
        public TurnManager Turno => turnManager;

        private void Start()
        {
            board = new GridBoard(anchoTablero, altoTablero);
            board.SetHospital(new Vector2Int(0, 0));

            // Las posiciones iniciales se calculan a partir del tamaño del tablero
            // para que sigan teniendo sentido con cualquier ancho/alto.
            var posInicial1 = new Vector2Int(0, altoTablero - 1);
            var posInicial2 = new Vector2Int(anchoTablero - 1, 0);

            var tank1 = new Tank(1, posInicial1, missiles: 5) { Facing = Direction.E };
            var tank2 = new Tank(2, posInicial2, missiles: 5) { Facing = Direction.O };

            var agent1 = new TankAgent(tank1) { Program = TankProgramParser.Parse(programaJugador1) };
            var agent2 = new TankAgent(tank2) { Program = TankProgramParser.Parse(programaJugador2) };

            agents = new List<TankAgent> { agent1, agent2 };
            turnManager = new TurnManager(board, agents);

            if (vistaTablero != null)
            {
                vistaTablero.Construir(board.Width, board.Height);
                RefrescarVista();
            }
        }

        public void EjecutarSiguienteTurno()
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
                Debug.Log("Empate: ambos tanques fueron destruidos.");
            }
        }

        private void RefrescarVista()
        {
            if (vistaTablero == null) return;

            var datos = agents.Select(a =>
                (playerId: a.Tank.PlayerId, posicion: a.Tank.Position, vivo: a.Tank.IsAlive));

            vistaTablero.ActualizarTanques(datos);
        }
    }
}
