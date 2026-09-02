using System.Collections.Generic;
using System.Linq;
using TanksGame.Core;
using TanksGame.Language;

namespace TanksGame.Gameplay
{
    public enum GameResult { Ongoing, Draw, PlayerWins }

    // Orquesta una ronda de juego siguiendo el orden general de ejecución
    // descrito en la sección 22 del documento:
    // leer -> validar -> ejecutar -> actualizar posición/vida -> minas ->
    // choques -> escudo -> comprobar ganador -> siguiente turno.
    public class TurnManager
    {
        public GridBoard Board;
        public List<TankAgent> Agents;
        public List<string> LastRoundLog = new List<string>();

        public TurnManager(GridBoard board, List<TankAgent> agents)
        {
            Board = board;
            Agents = agents;
        }

        public GameResult ExecuteRound()
        {
            LastRoundLog.Clear();

            // El escudo protege solo durante el turno en que se activa (sección 13).
            foreach (var agent in Agents)
                agent.Tank.ShieldActive = false;

            foreach (var agent in Agents)
            {
                if (!agent.Tank.IsAlive) continue;

                var step = agent.GetNextStep();
                if (step == null)
                {
                    LastRoundLog.Add($"Jugador {agent.Tank.PlayerId}: sin instrucciones, ESPERA.");
                    continue;
                }

                bool shouldExecute = step.Condition == null ||
                    step.Condition.Evaluate(agent.Tank, Radar);

                if (!shouldExecute)
                {
                    LastRoundLog.Add($"Jugador {agent.Tank.PlayerId}: condición IF falsa, ESPERA.");
                    continue;
                }

                Execute(agent, step.Instruction);
            }

            CheckMines();
            CheckCollisions();

            return CheckWinner();
        }

        private void Execute(TankAgent agent, Instruction instruction)
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
                        break;
                    }
                    if (Board.IsObstacle(newPos))
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MOV({dir}) bloqueado por un obstáculo.");
                        break;
                    }

                    tank.Position = newPos;
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: se mueve a {newPos}.");
                    break;
                }
                case InstructionType.Amt:
                {
                    var dir = instruction.Dir ?? tank.Facing;
                    tank.Facing = dir;
                    var (hit, _, _) = CombatResolver.Trace(Board, AliveTanks(), tank.Position, dir);

                    if (hit != null)
                    {
                        hit.TakeDamage(25f);
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: AMT({dir}) impacta a Jugador {hit.PlayerId} (-25%).");
                        HandleIfDestroyed(hit);
                    }
                    else
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: AMT({dir}) no impacta a nadie.");
                    }
                    break;
                }
                case InstructionType.Mina:
                    Board.PlaceMine(tank.Position);
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: coloca una mina en {tank.Position}.");
                    break;

                case InstructionType.Misil:
                {
                    if (tank.Missiles <= 0)
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MISIL inválido, sin munición.");
                        break;
                    }

                    tank.Missiles--;
                    var (hit, _, _) = CombatResolver.Trace(Board, AliveTanks(), tank.Position, tank.Facing);

                    if (hit != null)
                    {
                        hit.TakeDamage(25f);
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MISIL impacta a Jugador {hit.PlayerId} (-25%).");
                        HandleIfDestroyed(hit);
                    }
                    else
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MISIL no impacta a nadie.");
                    }
                    break;
                }
                case InstructionType.Radar:
                {
                    var dir = instruction.Dir ?? tank.Facing;
                    var value = Radar(tank, dir);
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: RADAR({dir}) = {value}.");
                    break;
                }
                case InstructionType.Escudo:
                    tank.ShieldActive = true;
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: activa ESCUDO.");
                    break;

                case InstructionType.Esperar:
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: ESPERA.");
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
                    break;
            }
        }

        private int Radar(Tank tank, Direction dir)
        {
            var (hit, dist, _) = CombatResolver.Trace(Board, AliveTanks(), tank.Position, dir);
            return hit != null ? dist : -dist;
        }

        private void CheckMines()
        {
            foreach (var agent in Agents)
            {
                var tank = agent.Tank;
                if (!tank.IsAlive) continue;

                if (Board.TryConsumeMine(tank.Position))
                {
                    tank.TakeDamage(20f);
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: pisa una mina (-20%).");
                    HandleIfDestroyed(tank);
                }
            }
        }

        private void CheckCollisions()
        {
            var alive = AliveTanks().ToList();
            for (int i = 0; i < alive.Count; i++)
            {
                for (int j = i + 1; j < alive.Count; j++)
                {
                    if (alive[i].Position == alive[j].Position)
                    {
                        alive[i].TakeDamage(25f);
                        alive[j].TakeDamage(25f);
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
