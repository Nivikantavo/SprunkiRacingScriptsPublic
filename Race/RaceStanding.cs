using UnityEngine;

namespace SprunkiRacing
{
    public class RaceStanding
    {
        public RaceStanding(RaceRacer racer)
        {
            Racer = racer;
            Preview = racer != null ? racer.Preview : null;
            LowHealthPreview = racer != null ? racer.LowHealthPreview : null;
            IsPlayer = racer != null && racer.IsPlayer;
        }

        public RaceRacer Racer { get; }
        public Sprite Preview { get; }
        public Sprite LowHealthPreview { get; }
        public bool IsPlayer { get; }
        public int DeathOrder { get; private set; } = -1;
        public bool IsEliminated => DeathOrder >= 0;

        public void Eliminate(int deathOrder)
        {
            if (DeathOrder < 0)
                DeathOrder = deathOrder;
        }
    }
}
