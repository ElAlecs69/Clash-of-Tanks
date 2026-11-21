using UnityEngine;

namespace TanksGame.Core
{
    // Sección 5, 16 y 19: estado de un tanque.
    public class Tank
    {
        public int PlayerId;
        public Vector2Int Position;
        public Direction Facing = Direction.N;
        public float Health = 100f;
        public int Missiles;
        // Reparaciones (REPARAR) que le quedan; se llena al armar la partida con ReglasPartida.reparaciones.
        public int RepairsLeft;
        public bool ShieldActive;
        // Turnos que le quedan al escudo (incluida la ronda en curso).
        public int ShieldTurnsLeft;
        public bool IsAlive => Health > 0f;

        public Tank(int playerId, Vector2Int startPos, int missiles)
        {
            PlayerId = playerId;
            Position = startPos;
            Missiles = missiles;
        }

        // Activa (o renueva) el escudo por 'turns' turnos, contando la ronda actual.
        public void ActivateShield(int turns)
        {
            ShieldTurnsLeft = Mathf.Max(1, turns);
            ShieldActive = true;
        }

        // Se llama una vez al empezar cada ronda: descuenta un turno al escudo y lo
        // apaga cuando se acaba.
        public void TickShield()
        {
            if (ShieldTurnsLeft > 0) ShieldTurnsLeft--;
            ShieldActive = ShieldTurnsLeft > 0;
        }

        public void TakeDamage(float amount)
        {
            if (ShieldActive) return; // sección 13/21: el escudo bloquea el daño
            Health = Mathf.Max(0f, Health - amount);
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(100f, Health + amount);
        }
    }
}