using System;
using UnityEngine;

namespace SprunkiRacing
{
    [Serializable]
    public struct RacePlaceReward
    {
        [SerializeField] private int trophies;
        [SerializeField] private int coins;

        public RacePlaceReward(int trophies, int coins)
        {
            this.trophies = trophies;
            this.coins = coins;
        }

        public int Trophies => trophies;
        public int Coins => coins;
    }

    public readonly struct RacePayout
    {
        public RacePayout(int trophies, int coins)
        {
            Trophies = trophies;
            Coins = coins;
        }

        public int Trophies { get; }
        public int Coins { get; }
    }

    public readonly struct RaceRewardBreakdown
    {
        public RaceRewardBreakdown(int placeTrophies, int killTrophies, int placeCoins, int killCoins)
        {
            PlaceTrophies = placeTrophies;
            KillTrophies = killTrophies;
            PlaceCoins = placeCoins;
            KillCoins = killCoins;
        }

        public int PlaceTrophies { get; }
        public int KillTrophies { get; }
        public int PlaceCoins { get; }
        public int KillCoins { get; }
        public int Trophies => PlaceTrophies + KillTrophies;
        public int Coins => PlaceCoins + KillCoins;
    }

    [CreateAssetMenu(fileName = "RaceRewardConfig", menuName = "Sprunki Racing/Race Reward")]
    public class RaceRewardConfig : ScriptableObject
    {
        [SerializeField] private RacePlaceReward[] places =
        {
            new RacePlaceReward(20, 100),
            new RacePlaceReward(15, 75),
            new RacePlaceReward(10, 50),
            new RacePlaceReward(5, 40),
            new RacePlaceReward(0, 30),
            new RacePlaceReward(-5, 20),
            new RacePlaceReward(-10, 10),
            new RacePlaceReward(-15, 5)
        };

        [SerializeField] private int trophiesPerKill = 1;
        [SerializeField] private int coinsPerKill = 10;

        public int TrophiesPerKill => trophiesPerKill;
        public int CoinsPerKill => coinsPerKill;

        public bool ContainsPlace(int place)
        {
            return place > 0 && places != null && place <= places.Length;
        }

        public RaceRewardBreakdown EvaluateBreakdown(int place, int kills)
        {
            int safeKills = Mathf.Max(0, kills);
            return new RaceRewardBreakdown(
                PlaceTrophies(place),
                trophiesPerKill * safeKills,
                PlaceCoins(place),
                coinsPerKill * safeKills);
        }

        public RacePayout Evaluate(int place, int kills)
        {
            RaceRewardBreakdown breakdown = EvaluateBreakdown(place, kills);
            return new RacePayout(breakdown.Trophies, breakdown.Coins);
        }

        private int PlaceTrophies(int place)
        {
            if (!ContainsPlace(place))
                return 0;

            return places[place - 1].Trophies;
        }

        private int PlaceCoins(int place)
        {
            if (!ContainsPlace(place))
                return 0;

            return places[place - 1].Coins;
        }
    }
}
