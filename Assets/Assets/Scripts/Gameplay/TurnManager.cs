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

        // Datos del tanque golpeado, para que la vista pueda aplicar su
        // apariencia de daño (chapa quemada, humo, volcado) justo en el
        // instante en que la explosión se reproduce en pantalla, en vez de
        // aplicarla de golpe al principio de la ronda, antes de que el
        // disparo siquiera haya salido.
        public int TargetId = -1;
        public int TargetVidaPorcentajeDespues;
        public bool TargetDestruido;
    }

    // Un escaneo de RADAR ocurrido durante la ronda, con los datos que la
    // vista necesita para animar el barrido en la dirección consultada.
    public class RadarEvent
    {
        public int ShooterId;
        public Vector2Int Origin;
        public Direction Dir;
        public int Valor;
    }

    public enum DamageEventType { Mina, Choque, Desgaste }

    // Un daño ocurrido durante la ronda que NO viene de un disparo (mina,
    // choque entre tanques, o desgaste por estancamiento contra un
    // obstáculo/borde). Igual que ShotEvent, existe para que la vista pueda
    // reproducir su animación y recién ENTONCES aplicar la apariencia de
    // daño de cada tanque afectado, en vez de aplicarla de golpe al
    // principio de la ronda.
    public class DamageEvent
    {
        public DamageEventType Tipo;
        public Vector2Int Celda;
        // Solo para Choque ENTRE DOS TANQUES: la celda del segundo tanque
        // involucrado, para que la vista pueda ubicar la explosión a mitad
        // de camino entre ambos en vez de encima de uno solo. Null en
        // choque contra obstáculo (ahí sí hay una única celda real) y en
        // mina/desgaste.
        public Vector2Int? CeldaB;
        public List<int> TargetIds = new List<int>();
        public Dictionary<int, int> VidaPorcentajeDespuesPorId = new Dictionary<int, int>();
        public HashSet<int> DestruidosIds = new HashSet<int>();
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
        public List<RadarEvent> LastRoundRadars = new List<RadarEvent>();
        // Eventos de daño de esta ronda que NO son disparos, EN ORDEN
        // (minas primero, luego choques, luego desgaste -- el mismo orden
        // en que ExecuteRound los procesa).
        public List<DamageEvent> LastRoundDamageEvents = new List<DamageEvent>();
        public int RondasAntesDeDesgaste = 2;
        public float DanioPorDesgaste = 2f;

        // Cuántas rondas SEGUIDAS lleva cada tanque con un MOV inválido (fuera del
        // tablero o bloqueado por un obstáculo). Es POR TANQUE a propósito: antes
        // este desgaste era global (si nadie se movía ni hacía daño en toda la
        // ronda, TODOS perdían vida, incluso un tanque que eligió ESPERAR a
        // propósito) — ahora solo se castiga al que realmente está atascado contra
        // algo, nunca al que decide esperar.
        private readonly Dictionary<TankAgent, int> rondasAtascadoPorAgente = new Dictionary<TankAgent, int>();

        public TurnManager(GridBoard board, List<TankAgent> agents)
        {
            Board = board;
            Agents = agents;
        }

        public GameResult ExecuteRound()
        {
            LastRoundLog.Clear();
            LastRoundShots.Clear();
            LastRoundRadars.Clear();
            LastRoundDamageEvents.Clear();
            bool huboDanio = false;

            // El escudo protege solo durante el turno en que se activa (sección 13).
            foreach (var agent in Agents)
                agent.Tank.ShieldActive = false;

            // Avanza el contador de ronda del tablero. Ya NO depende de que
            // esto se llame antes de procesar las instrucciones (ver el
            // comentario de GridBoard.AvanzarRonda): una mina colocada más
            // abajo, en esta misma ronda, queda sellada con el número de
            // ronda actual pase lo que pase, y solo se arma sola a partir de
            // la ronda siguiente.
            Board.AvanzarRonda();

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
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MOV({dir}) bloqueado, choca contra un obstáculo.");
                        RegistrarTurnoAtascado(agent);

                        // Chocar contra un obstáculo (montaña, o la chatarra de un
                        // tanque ya destruido) hace el mismo daño que un choque
                        // entre dos tanques (sección de choques): -12%.
                        if (AplicarDanio(tank, 12f)) huboDanio = true;
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: choque contra el obstáculo (-12%).");
                        HandleIfDestroyed(tank);

                        LastRoundDamageEvents.Add(new DamageEvent
                        {
                            Tipo = DamageEventType.Choque,
                            Celda = newPos,
                            TargetIds = { tank.PlayerId },
                            VidaPorcentajeDespuesPorId = { [tank.PlayerId] = Mathf.RoundToInt(tank.Health) },
                            DestruidosIds = tank.IsAlive ? new HashSet<int>() : new HashSet<int> { tank.PlayerId }
                        });

                        break;
                    }

                    // Otro tanque, todavía vivo, ya ocupa esa casilla -- porque no
                    // se movió esta ronda, o porque le tocó antes en el orden de
                    // Agents y ya "ganó" la casilla. Para el que llega segundo esa
                    // casilla es una pared: se queda donde está, y AMBOS se hacen
                    // el mismo daño que un choque (-12% cada uno) -- el que ya
                    // estaba ahí no tiene la culpa, pero el golpe lo siente igual.
                    var ocupante = AliveTanks().FirstOrDefault(t => t != tank && t.Position == newPos);
                    if (ocupante != null)
                    {
                        LastRoundLog.Add($"Jugador {tank.PlayerId}: MOV({dir}) bloqueado, choca contra Jugador {ocupante.PlayerId}.");
                        RegistrarTurnoAtascado(agent);

                        if (AplicarDanio(tank, 12f)) huboDanio = true;
                        if (AplicarDanio(ocupante, 12f)) huboDanio = true;
                        LastRoundLog.Add(
                            $"Choque entre Jugador {tank.PlayerId} y Jugador {ocupante.PlayerId} (-12% cada uno).");
                        HandleIfDestroyed(tank);
                        HandleIfDestroyed(ocupante);

                        LastRoundDamageEvents.Add(new DamageEvent
                        {
                            Tipo = DamageEventType.Choque,
                            Celda = tank.Position,
                            CeldaB = newPos,
                            TargetIds = { tank.PlayerId, ocupante.PlayerId },
                            VidaPorcentajeDespuesPorId =
                            {
                                [tank.PlayerId] = Mathf.RoundToInt(tank.Health),
                                [ocupante.PlayerId] = Mathf.RoundToInt(ocupante.Health)
                            },
                            DestruidosIds = new HashSet<int>(
                                new[] { tank, ocupante }.Where(t => !t.IsAlive).Select(t => t.PlayerId))
                        });

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
                        LastRoundShots[LastRoundShots.Count - 1].TargetId = hit.PlayerId;
                        LastRoundShots[LastRoundShots.Count - 1].TargetVidaPorcentajeDespues = Mathf.RoundToInt(hit.Health);
                        LastRoundShots[LastRoundShots.Count - 1].TargetDestruido = !hit.IsAlive;
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
                        LastRoundShots[LastRoundShots.Count - 1].TargetId = hit.PlayerId;
                        LastRoundShots[LastRoundShots.Count - 1].TargetVidaPorcentajeDespues = Mathf.RoundToInt(hit.Health);
                        LastRoundShots[LastRoundShots.Count - 1].TargetDestruido = !hit.IsAlive;
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
                    tank.Facing = dir;
                    var value = Radar(tank, dir);
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: RADAR({dir}) = {value}.");
                    LastRoundRadars.Add(new RadarEvent
                    {
                        ShooterId = tank.PlayerId,
                        Origin = tank.Position,
                        Dir = dir,
                        Valor = value
                    });
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

                // Detona si el tanque TERMINA la ronda sobre una mina armada,
                // sin importar si llegó ahí moviéndose esta misma ronda o si
                // ya estaba parado encima -- "pisar una mina" es justamente
                // moverse hacia su casilla, así que exigir que NO se haya
                // movido (como se hacía antes) hacía que nunca detonara con
                // un MOV hacia ella, solo quedándose quieto encima.
                if (Board.TryConsumeMine(tank.Position))
                {
                    if (AplicarDanio(tank, 20f)) huboDanio = true;
                    LastRoundLog.Add($"Jugador {tank.PlayerId}: pisa una mina (-20%).");
                    HandleIfDestroyed(tank);

                    LastRoundDamageEvents.Add(new DamageEvent
                    {
                        Tipo = DamageEventType.Mina,
                        Celda = tank.Position,
                        TargetIds = { tank.PlayerId },
                        VidaPorcentajeDespuesPorId = { [tank.PlayerId] = Mathf.RoundToInt(tank.Health) },
                        DestruidosIds = tank.IsAlive ? new HashSet<int>() : new HashSet<int> { tank.PlayerId }
                    });
                }
            }
        }

        // Con el chequeo de "casilla ocupada" agregado a MOV, un tanque ya no
        // puede terminar la ronda superpuesto con otro que siga vivo (el que
        // llega segundo se frena antes). Esto queda solo como red de
        // seguridad ante algún otro camino no contemplado hacia el mismo
        // resultado.
        private void CheckCollisions(ref bool huboDanio)
        {
            var alive = AliveTanks().ToList();
            for (int i = 0; i < alive.Count; i++)
            {
                for (int j = i + 1; j < alive.Count; j++)
                {
                    if (alive[i].Position == alive[j].Position)
                    {
                        if (AplicarDanio(alive[i], 12f)) huboDanio = true;
                        if (AplicarDanio(alive[j], 12f)) huboDanio = true;
                        LastRoundLog.Add(
                            $"Choque entre Jugador {alive[i].PlayerId} y Jugador {alive[j].PlayerId} (-12% cada uno).");
                        HandleIfDestroyed(alive[i]);
                        HandleIfDestroyed(alive[j]);

                        LastRoundDamageEvents.Add(new DamageEvent
                        {
                            Tipo = DamageEventType.Choque,
                            Celda = alive[i].Position,
                            CeldaB = alive[j].Position,
                            TargetIds = { alive[i].PlayerId, alive[j].PlayerId },
                            VidaPorcentajeDespuesPorId =
                            {
                                [alive[i].PlayerId] = Mathf.RoundToInt(alive[i].Health),
                                [alive[j].PlayerId] = Mathf.RoundToInt(alive[j].Health)
                            },
                            DestruidosIds = new HashSet<int>(
                                new[] { alive[i], alive[j] }.Where(t => !t.IsAlive).Select(t => t.PlayerId))
                        });
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

                LastRoundDamageEvents.Add(new DamageEvent
                {
                    Tipo = DamageEventType.Desgaste,
                    Celda = agent.Tank.Position,
                    TargetIds = { agent.Tank.PlayerId },
                    VidaPorcentajeDespuesPorId = { [agent.Tank.PlayerId] = Mathf.RoundToInt(agent.Tank.Health) },
                    DestruidosIds = agent.Tank.IsAlive ? new HashSet<int>() : new HashSet<int> { agent.Tank.PlayerId }
                });
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