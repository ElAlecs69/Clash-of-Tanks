using System.Collections.Generic;
using System.Linq;
using TanksGame.Core;
using TanksGame.Language;
using UnityEngine;

namespace TanksGame.Gameplay
{
    public enum GameResult { Ongoing, Draw, PlayerWins }

    // Un disparo (AMT o MISIL) ocurrido durante la ronda que se acaba de
    // ejecutar, con los datos que la vista necesita para animarlo: de dónde
    // sale, hasta dónde llega (la celda del tanque golpeado, o la última
    // celda libre/borde si no impactó a nadie) y si impactó de verdad.
    public class ShotEvent
    {
        public int ShooterId;
        public Vector2Int Origin;
        public Vector2Int ImpactCell;
        public bool EsMisil;   // true = MISIL, false = AMT
        public bool Impacto;   // true = golpeó a un tanque
    }

    // Orquesta una ronda de juego siguiendo el orden general de ejecución
    // descrito en la sección 22 del documento:
    // leer -> validar -> ejecutar -> actualizar posición/vida -> minas ->
    // choques -> escudo -> comprobar ganador -> siguiente turno.
    public class TurnManager
    {
        public GridBoard Board;
        public List<TankAgent> Agents;
        public List<string> LastRoundLog = new List<string>();
        public List<ShotEvent> LastRoundShots = new List<ShotEvent>();
        public int RondasAntesDeDesgaste = 2;
        public float DanioPorDesgaste = 2f;

        // Cuántas rondas SEGUIDAS lleva cada tanque con un MOV inválido (fuera del
        // tablero o bloqueado por un obstáculo). Es POR TANQUE a propósito: antes
        // este desgaste era global (si nadie se movía ni hacía daño en toda la
        // ronda, TODOS perdían vida, incluso un tanque que eligió ESPERAR a
        // propósito) — ahora solo se castiga al que realmente está atascado contra
        // algo, nunca al que decide esperar.
        private readonly Dictionary<TankAgent, int> rondasAtascadoPorAgente = new Dictionary<TankAgent, int>();

        // Posición de cada tanque AL EMPEZAR la ronda actual, antes de
        // ejecutar ninguna instrucción. CheckMines la usa para saber si el
        // tanque se movió o no durante la ronda: una mina armada solo hace
        // daño si el tanque termina la ronda en la MISMA casilla en la que la
        // empezó (antes se comparaba solo la posición final contra la mina,
        // así que un tanque que se movía y terminaba pisando una mina
        // recibía daño igual; ahora moverse basta para esquivarla).
        private readonly Dictionary<TankAgent, Vector2Int> posicionInicioRonda = new Dictionary<TankAgent, Vector2Int>();

        public TurnManager(GridBoard board, List<TankAgent> agents)
        {
            Board = board;
            Agents = agents;
        }

        public GameResult ExecuteRound()
        {
            LastRoundLog.Clear();
            LastRoundShots.Clear();
            bool huboDanio = false;

            // El escudo protege solo durante el turno en que se activa (sección 13).
            foreach (var agent in Agents)
                agent.Tank.ShieldActive = false;

            // Se captura ANTES de ejecutar nada y se arman las minas que
            // vienen de rondas anteriores. Las minas colocadas en ESTA ronda
            // (por la instrucción Mina, más abajo) quedan desarmadas y no
            // podrán hacer daño hasta la ronda siguiente.
            posicionInicioRonda.Clear();
            foreach (var agent in Agents)
                if (agent.Tank.IsAlive)
                    posicionInicioRonda[agent] = agent.Tank.Position;

            Board.ArmarMinasPendientes();

            foreach (var agent in Agents)
            {
                if (!agent.Tank.IsAlive) continue;

                var step = agent.GetNextStep();
                if (step == null)
                {
                    LastRoundLog.Add($"Jugador {agent.Tank.PlayerId}: sin instrucciones, ESPERA.");
                    RegistrarTurnoNoAtascado(agent);
                    continue;
                }

                bool shouldExecute = step.Condition == null ||
                    step.Condition.Evaluate(agent.Tank, Radar);

                if (!shouldExecute)
                {
                    LastRoundLog.Add($"Jugador {agent.Tank.PlayerId}: condición IF falsa, ESPERA.");
                    RegistrarTurnoNoAtascado(agent);
                    continue;
                }

                Execute(agent, step.Instruction, ref huboDanio);
            }

            CheckMines(ref huboDanio);
            CheckCollisions(ref huboDanio);
            AplicarDesgastePorEstancamiento();

            return CheckWinner();
        }

        private void Execute(TankAgent agent, Instruction instruction, ref bool huboDanio)
        {
            var tank = agent.Tank;

            switch (instruction.Type)
            {
                case InstructionType.Mov:
                {
                    var dir = instruction.Dir ?? tank.Facing;
                    var newPos = tank.Position + dir.ToOffset();
                    tank.Facing = dir;

                    if (!Board.IsInside(newPos))
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MOV({dir}) inválido, fuera del tablero.");
                        RegistrarTurnoAtascado(agent);
                        break;
                    }
                    if (Board.IsObstacle(newPos))
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MOV({dir}) bloqueado por un obstáculo.");
                        RegistrarTurnoAtascado(agent);
                        break;
                    }

                    tank.Position = newPos;
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: se mueve a {newPos}.");
                    RegistrarTurnoNoAtascado(agent);
                    break;
                }
                case InstructionType.Amt:
                {
                    var dir = instruction.Dir ?? tank.Facing;
                    tank.Facing = dir;
                    var (hit, distanciaAmt, bloqueadoAmt) = CombatResolver.Trace(Board, AliveTanks(), tank.Position, dir);
                    LastRoundShots.Add(new ShotEvent
                    {
                        ShooterId = tank.PlayerId,
                        Origin = tank.Position,
                        ImpactCell = tank.Position + dir.ToOffset() * CeldaVisibleDelTrazo(distanciaAmt, hit != null, bloqueadoAmt),
                        EsMisil = false,
                        Impacto = hit != null
                    });

                    if (hit != null)
                    {
                        if (AplicarDanio(hit, 25f)) huboDanio = true;
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: AMT({dir}) impacta a Jugador {hit.PlayerId} (-25%).");
                        HandleIfDestroyed(hit);
                    }
                    else
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: AMT({dir}) no impacta a nadie.");
                    }
                    RegistrarTurnoNoAtascado(agent);
                    break;
                }
                case InstructionType.Mina:
                    Board.PlaceMine(tank.Position);
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: coloca una mina en {tank.Position}.");
                    RegistrarTurnoNoAtascado(agent);
                    break;

                case InstructionType.Misil:
                {
                    if (tank.Missiles <= 0)
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MISIL inválido, sin munición.");
                        RegistrarTurnoNoAtascado(agent); // se quedó sin balas, no "atascado contra algo".
                        break;
                    }

                    tank.Missiles--;
                    // El MISIL admite una dirección propia (MISIL(N), etc.,
                    // ya la reconocía el parser) que antes se ignoraba por
                    // completo aquí: siempre disparaba hacia tank.Facing sin
                    // actualizarlo, así que un MISIL(N) con el tanque mirando
                    // al Este disparaba igual hacia el Este. Ahora, igual que
                    // AMT, usa esa dirección si viene, y de paso orienta al
                    // tanque hacia ella.
                    var dirMisil = instruction.Dir ?? tank.Facing;
                    tank.Facing = dirMisil;
                    var (hit, distanciaMisil, bloqueadoMisil) = CombatResolver.Trace(Board, AliveTanks(), tank.Position, dirMisil);
                    LastRoundShots.Add(new ShotEvent
                    {
                        ShooterId = tank.PlayerId,
                        Origin = tank.Position,
                        ImpactCell = tank.Position + dirMisil.ToOffset() * CeldaVisibleDelTrazo(distanciaMisil, hit != null, bloqueadoMisil),
                        EsMisil = true,
                        Impacto = hit != null
                    });

                    if (hit != null)
                    {
                        if (AplicarDanio(hit, 25f)) huboDanio = true;
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MISIL impacta a Jugador {hit.PlayerId} (-25%).");
                        HandleIfDestroyed(hit);
                    }
                    else
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MISIL no impacta a nadie.");
                    }
                    RegistrarTurnoNoAtascado(agent);
                    break;
                }
                case InstructionType.Radar:
                {
                    var dir = instruction.Dir ?? tank.Facing;
                    var value = Radar(tank, dir);
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: RADAR({dir}) = {value}.");
                    RegistrarTurnoNoAtascado(agent);
                    break;
                }
                case InstructionType.Escudo:
                    tank.ShieldActive = true;
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: activa ESCUDO.");
                    RegistrarTurnoNoAtascado(agent);
                    break;

                case InstructionType.Esperar:
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: ESPERA.");
                    RegistrarTurnoNoAtascado(agent);
                    break;

                case InstructionType.Reparar:
                    if (Board.IsHospital(tank.Position))
                    {
                        tank.Heal(20f);
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: repara, ahora tiene {tank.Health}% de vida.");
                    }
                    else
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: REPARAR inválido, no está en el Hospital.");
                    }
                    RegistrarTurnoNoAtascado(agent);
                    break;
            }
        }

        // CombatResolver.Trace() cuenta la distancia incluyendo el paso que
        // lo hace fallar: si el trazo se sale del tablero, "distance" ya
        // corresponde a la casilla INVÁLIDA de fuera del tablero (por eso la
        // explosión se veía disparada una casilla de más, fuera del borde
        // verde, en el bosque). Si impactó un tanque o chocó contra un
        // obstáculo, "distance" sí es una casilla válida dentro del tablero
        // y hay que usarla tal cual. Solo cuando no impactó nada y no fue un
        // obstáculo (se salió del tablero) hay que quedarse una casilla
        // antes (distance - 1) para que la explosión se vea en el borde real
        // del tablero en vez de flotando fuera de él.
        private static int CeldaVisibleDelTrazo(int distancia, bool impacto, bool bloqueadoPorObstaculo)
        {
            if (impacto || bloqueadoPorObstaculo) return distancia;
            return Mathf.Max(distancia - 1, 0);
        }

        private int Radar(Tank tank, Direction dir)
        {
            var (hit, dist, _) = CombatResolver.Trace(Board, AliveTanks(), tank.Position, dir);
            return hit != null ? dist : -dist;
        }

        private void CheckMines(ref bool huboDanio)
        {
            foreach (var agent in Agents)
            {
                var tank = agent.Tank;
                if (!tank.IsAlive) continue;

                // Solo detona si el tanque terminó la ronda EN LA MISMA
                // casilla en la que la empezó (no se movió). Un tanque que se
                // desplaza esta ronda -- se aleje de una mina o pise una
                // nueva -- nunca recibe daño de mina en esa misma ronda.
                if (!posicionInicioRonda.TryGetValue(agent, out var posicionInicial)) continue;
                if (tank.Position != posicionInicial) continue;

                if (Board.TryConsumeMine(tank.Position))
                {
                    if (AplicarDanio(tank, 20f)) huboDanio = true;
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: pisa una mina (-20%).");
                    HandleIfDestroyed(tank);
                }
            }
        }

        private void CheckCollisions(ref bool huboDanio)
        {
            var alive = AliveTanks().ToList();
            for (int i = 0; i < alive.Count; i++)
            {
                for (int j = i + 1; j < alive.Count; j++)
                {
                    if (alive[i].Position == alive[j].Position)
                    {
                        if (AplicarDanio(alive[i], 25f)) huboDanio = true;
                        if (AplicarDanio(alive[j], 25f)) huboDanio = true;
                        LastRoundLog.Add(
                            $"Choque entre Jugador {alive[i].PlayerId} y Jugador {alive[j].PlayerId} (-25% cada uno).");
                        HandleIfDestroyed(alive[i]);
                        HandleIfDestroyed(alive[j]);
                    }
                }
            }
        }

        private void HandleIfDestroyed(Tank tank)
        {
            if (!tank.IsAlive)
            {
                Board.SetObstacle(tank.Position);
                LastRoundLog.Add($"Jugador {tank.PlayerId}: ¡tanque destruido! Su casilla ahora es un obstáculo.");
            }
        }

        private static bool AplicarDanio(Tank tank, float cantidad)
        {
            float vidaAnterior = tank.Health;
            tank.TakeDamage(cantidad);
            return tank.Health < vidaAnterior;
        }

        private void RegistrarTurnoAtascado(TankAgent agent)
        {
            rondasAtascadoPorAgente.TryGetValue(agent, out int actual);
            rondasAtascadoPorAgente[agent] = actual + 1;
        }

        private void RegistrarTurnoNoAtascado(TankAgent agent)
        {
            rondasAtascadoPorAgente[agent] = 0;
        }

        // Ahora es POR TANQUE (antes era una sola cuenta global de la ronda). Solo
        // castiga al tanque que lleva 'RondasAntesDeDesgaste' turnos SEGUIDOS con un
        // MOV inválido (fuera del tablero o bloqueado por un obstáculo) — es decir,
        // realmente atascado contra algo. ESPERAR (explícito, por falta de
        // instrucciones, o por un IF falso) nunca cuenta como estancamiento: es una
        // decisión válida del jugador, no un bloqueo.
        private void AplicarDesgastePorEstancamiento()
        {
            foreach (var agent in Agents)
            {
                if (!agent.Tank.IsAlive) continue;
                if (!rondasAtascadoPorAgente.TryGetValue(agent, out int rondas)) continue;
                if (rondas < RondasAntesDeDesgaste) continue;

                if (!AplicarDanio(agent.Tank, DanioPorDesgaste)) continue;
                LastRoundLog.Add($"Jugador {agent.Tank.PlayerId}: desgaste por estancamiento (-{DanioPorDesgaste}%).");
                HandleIfDestroyed(agent.Tank);
            }
        }

        private GameResult CheckWinner()
        {
            var alive = AliveTanks().ToList();
            if (alive.Count == 1) return GameResult.PlayerWins;
            if (alive.Count == 0) return GameResult.Draw;
            return GameResult.Ongoing;
        }

        public IEnumerable<Tank> AliveTanks() => Agents.Select(a => a.Tank).Where(t => t.IsAlive);
    }
}