using UnityEngine;

namespace SprunkiRacing
{
    public enum BotGoal
    {
        WinRace,
        Destroy,
        Ram
    }

    [CreateAssetMenu(fileName = "BotProfile", menuName = "Sprunki Racing/Bot Profile")]
    public class BotProfile : ScriptableObject
    {
        [SerializeField] private BotGoal goal = BotGoal.WinRace;
        [SerializeField, Range(0f, 1f)] private float aggression = 0.5f;
        [SerializeField, Range(0f, 1f)] private float intelligence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float accuracy = 0.5f;
        [SerializeField] private float reactionDelay = 0.2f;
        [SerializeField] private float lookahead = 18f;
        [SerializeField] private float ammoThreshold = 40f;
        [SerializeField] private float missileThreshold = 4f;
        [SerializeField] private float fuelThreshold = 20f;
        [SerializeField] private float healthThreshold = 50f;
        [SerializeField] private float maxDetour = 20f;
        [SerializeField] private float fireRange = 40f;
        [SerializeField] private float fireCone = 14f;
        [SerializeField] private float shotDelay = 0.9f;
        [SerializeField] private int machineGunBurst = 6;
        [SerializeField] private float steerNoise;
        [SerializeField, Range(0f, 0.45f)] private float chaseWeight = 0.3f;
        [SerializeField] private float ramBias;
        [SerializeField] private bool useNitroOnStraight = true;
        [SerializeField] private bool leadShots;
        [SerializeField] private float nitroChance;

        private static BotProfile _fallback;

        public BotGoal Goal => goal;
        public float Aggression => aggression;
        public float Intelligence => intelligence;
        public float Accuracy => accuracy;
        public float ReactionDelay => reactionDelay;
        public float Lookahead => lookahead;
        public float AmmoThreshold => ammoThreshold;
        public float MissileThreshold => missileThreshold;
        public float FuelThreshold => fuelThreshold;
        public float HealthThreshold => healthThreshold;
        public float MaxDetour => maxDetour;
        public float FireRange => fireRange;
        public float FireCone => fireCone;
        public float ShotDelay => shotDelay;
        public int MachineGunBurst => machineGunBurst > 0 ? machineGunBurst : 1;
        public float SteerNoise => steerNoise;
        public float ChaseWeight => chaseWeight;
        public float RamBias => ramBias;
        public bool UseNitroOnStraight => useNitroOnStraight;
        public bool LeadShots => leadShots;
        public float NitroChance => nitroChance;

        public static BotProfile Fallback
        {
            get
            {
                if (_fallback != null)
                    return _fallback;

                _fallback = CreateInstance<BotProfile>();
                _fallback.name = "Fallback";
                _fallback.hideFlags = HideFlags.HideAndDontSave;
                return _fallback;
            }
        }
    }
}
