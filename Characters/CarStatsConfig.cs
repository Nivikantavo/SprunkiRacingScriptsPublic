using System;
using System.Collections.Generic;
using CarControllerwithShooting;
using UnityEngine;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    public enum CarStatId
    {
        BulletDamage,
        StartingBullets,
        BulletsPerPack,
        RocketDamage,
        StartingRockets,
        RocketsPerPack,
        Health,
        Speed,
        StartingNitro,
        NitroPerPack
    }

    [Serializable]
    public struct CarStatEntry
    {
        [SerializeField] private CarStatId id;
        [SerializeField] private LocalizedString title;
        [SerializeField] private Sprite icon;
        [SerializeField] private float step;
        [SerializeField] private int basePrice;
        [SerializeField] private int priceGrowth;
        [SerializeField] private int maxLevel;

        public CarStatId Id => id;
        public LocalizedString Title => title;
        public Sprite Icon => icon;
        public float Step => step > 0f ? step : 1f;
        public int BasePrice => Mathf.Max(0, basePrice);
        public int PriceGrowth => Mathf.Max(0, priceGrowth);
        public int MaxLevel => maxLevel > 0 ? maxLevel : 10;
    }

    [CreateAssetMenu(fileName = "CarStatsConfig", menuName = "Sprunki Racing/Car Stats")]
    public class CarStatsConfig : ScriptableObject
    {
        [SerializeField] private int bulletDamage = 5;
        [SerializeField] private int startingBullets = 240;
        [SerializeField] private int bulletsPerAmmoPack = 100;
        [SerializeField] private int rocketDamage = 25;
        [SerializeField] private int startingRockets = 20;
        [SerializeField] private int rocketsPerAmmoPack = 2;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float topSpeed = 150f;
        [SerializeField] private float fullTorque = 2000f;
        private const float TorquePerSpeedUpgrade = 100f;
        [SerializeField] private float startingNitro;
        [SerializeField] private float nitroPerPack = 25f;
        [SerializeField] private CarStatCatalog catalog;

        public int BulletDamage => bulletDamage;
        public int StartingBullets => startingBullets;
        public int BulletsPerAmmoPack => bulletsPerAmmoPack;
        public int RocketDamage => rocketDamage;
        public int StartingRockets => startingRockets;
        public int RocketsPerAmmoPack => rocketsPerAmmoPack;
        public int MaxHealth => maxHealth;
        public float TopSpeed => topSpeed;
        public float FullTorque => fullTorque;
        public float StartingNitro => startingNitro;
        public float NitroPerPack => nitroPerPack;
        public CarStatCatalog Catalog => catalog;
        public IReadOnlyList<CarStatEntry> Entries => catalog != null ? catalog.Entries : Array.Empty<CarStatEntry>();

        public bool TryGetEntry(CarStatId id, out CarStatEntry entry)
        {
            if (catalog != null)
                return catalog.TryGetEntry(id, out entry);

            entry = default;
            return false;
        }

        public int GetLevel(CarStatId id)
        {
            CarUpgradeProgress progress = CarUpgradeProgress.Instance;
            if (progress == null)
                return 0;

            return progress.GetLevel(id);
        }

        public bool IsMaxLevel(CarStatId id)
        {
            if (!TryGetEntry(id, out CarStatEntry entry))
                return true;

            return GetLevel(id) >= entry.MaxLevel;
        }

        public int GetUpgradePrice(CarStatId id)
        {
            if (!TryGetEntry(id, out CarStatEntry entry))
                return 0;

            return entry.BasePrice + entry.PriceGrowth * GetLevel(id);
        }

        public float GetDisplayValue(CarStatId id)
        {
            return Value(id, null);
        }

        public float GetNextDisplayValue(CarStatId id)
        {
            if (IsMaxLevel(id))
                return GetDisplayValue(id);

            return GetValue(id, GetLevel(id) + 1);
        }

        private int LevelOf(CarStatId id, IReadOnlyDictionary<CarStatId, int> levels)
        {
            if (levels == null)
                return GetLevel(id);

            if (levels.TryGetValue(id, out int level))
                return Mathf.Max(0, level);

            return 0;
        }

        private float Value(CarStatId id, IReadOnlyDictionary<CarStatId, int> levels)
        {
            return GetValue(id, LevelOf(id, levels));
        }

        private int Round(CarStatId id, IReadOnlyDictionary<CarStatId, int> levels)
        {
            return Mathf.Max(0, Mathf.RoundToInt(Value(id, levels)));
        }

        public void ApplyTo(GameObject player)
        {
            ApplyLoadout(player, null);
        }

        public void ApplyBotLoadout(GameObject bot, int spread)
        {
            int playerTotal = 0;
            CarUpgradeProgress progress = CarUpgradeProgress.Instance;
            if (progress != null)
                playerTotal = progress.GetTotalLevel();

            int count = RollUpgradeCount(playerTotal, spread);
            Dictionary<CarStatId, int> levels = RollLevels(count);
            KeepUpWithPlayerSpeed(levels);
            ApplyLoadout(bot, levels);
        }

        private void ApplyLoadout(GameObject car, IReadOnlyDictionary<CarStatId, int> levels)
        {
            if (car == null)
                return;

            CarController controller = car.GetComponentInChildren<CarController>(true);
            if (controller != null)
                controller.ApplyLoadout(Round(CarStatId.Health, levels), Torque(levels), Value(CarStatId.Speed, levels));

            GunController gun = car.GetComponentInChildren<GunController>(true);
            if (gun != null)
            {
                gun.ApplyLoadout(
                    Round(CarStatId.StartingBullets, levels),
                    Round(CarStatId.StartingRockets, levels),
                    Round(CarStatId.BulletDamage, levels),
                    Round(CarStatId.RocketDamage, levels),
                    Round(CarStatId.BulletsPerPack, levels),
                    Round(CarStatId.RocketsPerPack, levels));
            }

            Gasoline fuel = car.GetComponentInChildren<Gasoline>(true);
            if (fuel != null)
                fuel.ApplyLoadout(Value(CarStatId.StartingNitro, levels), Value(CarStatId.NitroPerPack, levels));
        }

        private void KeepUpWithPlayerSpeed(Dictionary<CarStatId, int> levels)
        {
            if (levels == null || !TryGetEntry(CarStatId.Speed, out CarStatEntry entry))
                return;

            int playerSpeed = 0;
            CarUpgradeProgress progress = CarUpgradeProgress.Instance;
            if (progress != null)
                playerSpeed = progress.GetLevel(CarStatId.Speed);

            int pace = Mathf.Clamp(playerSpeed, 0, entry.MaxLevel);
            levels.TryGetValue(CarStatId.Speed, out int rolled);
            if (rolled < pace)
                levels[CarStatId.Speed] = pace;
        }

        private int RollUpgradeCount(int playerTotal, int spread)
        {
            int window = Mathf.Max(0, spread);
            int min = Mathf.Max(0, playerTotal - window);
            int max = Mathf.Min(playerTotal + window, UpgradeCapacity());
            if (max < min)
                max = min;

            return UnityEngine.Random.Range(min, max + 1);
        }

        private Dictionary<CarStatId, int> RollLevels(int count)
        {
            var levels = new Dictionary<CarStatId, int>();
            IReadOnlyList<CarStatEntry> table = Entries;
            if (count <= 0 || table.Count == 0)
                return levels;

            var open = new List<int>(table.Count);
            for (int i = 0; i < table.Count; i++)
                open.Add(i);

            for (int placed = 0; placed < count && open.Count > 0; placed++)
            {
                int pick = UnityEngine.Random.Range(0, open.Count);
                CarStatEntry entry = table[open[pick]];
                int level = 0;
                levels.TryGetValue(entry.Id, out level);
                level++;
                levels[entry.Id] = level;
                if (level >= entry.MaxLevel)
                    open.RemoveAt(pick);
            }

            return levels;
        }

        private int UpgradeCapacity()
        {
            IReadOnlyList<CarStatEntry> table = Entries;
            int capacity = 0;
            for (int i = 0; i < table.Count; i++)
                capacity += table[i].MaxLevel;

            return capacity;
        }

        private float Torque(IReadOnlyDictionary<CarStatId, int> levels)
        {
            return fullTorque + TorquePerSpeedUpgrade * LevelOf(CarStatId.Speed, levels);
        }

        private float GetValue(CarStatId id, int level)
        {
            float step = 0f;
            if (TryGetEntry(id, out CarStatEntry entry))
                step = entry.Step;

            return BaseValue(id) + step * Mathf.Max(0, level);
        }

        private float BaseValue(CarStatId id)
        {
            switch (id)
            {
                case CarStatId.BulletDamage:
                    return bulletDamage;
                case CarStatId.StartingBullets:
                    return startingBullets;
                case CarStatId.BulletsPerPack:
                    return bulletsPerAmmoPack;
                case CarStatId.RocketDamage:
                    return rocketDamage;
                case CarStatId.StartingRockets:
                    return startingRockets;
                case CarStatId.RocketsPerPack:
                    return rocketsPerAmmoPack;
                case CarStatId.Health:
                    return maxHealth;
                case CarStatId.Speed:
                    return topSpeed;
                case CarStatId.StartingNitro:
                    return startingNitro;
                case CarStatId.NitroPerPack:
                    return nitroPerPack;
                default:
                    return 0f;
            }
        }
    }
}
