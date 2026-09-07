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
        public bool ShieldActive;
        public bool IsAlive => Health > 0f;

        public Tank(int playerId, Vector2Int startPos, int missiles)
        {
            PlayerId = playerId;
            Position = startPos;
            Missiles = missiles;
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
