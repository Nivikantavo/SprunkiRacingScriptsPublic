using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SprunkiRacing
{
    public class CarUpgradeProgress : MonoBehaviour
    {
        public const string StorageKey = "car_upgrades";

        private const char EntrySeparator = '|';
        private const char LevelSeparator = ':';

        private readonly Dictionary<CarStatId, int> _levels = new Dictionary<CarStatId, int>();
        private readonly StoredValue _storage = new StoredValue(StorageKey, "Car upgrades could not be saved");
        private Action _changed;

        public static CarUpgradeProgress Instance { get; private set; }

        public bool IsReady { get; private set; }

        public event Action Changed
        {
            add
            {
                _changed += value;
                if (IsReady)
                    value?.Invoke();
            }
            remove => _changed -= value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var progressObject = new GameObject(nameof(CarUpgradeProgress));
            progressObject.AddComponent<CarUpgradeProgress>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _storage.Load(OnLoaded);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public int GetLevel(CarStatId id)
        {
            if (_levels.TryGetValue(id, out int level))
                return Mathf.Max(0, level);

            return 0;
        }

        public int GetTotalLevel()
        {
            int total = 0;
            foreach (KeyValuePair<CarStatId, int> pair in _levels)
                total += Mathf.Max(0, pair.Value);

            return total;
        }

        public bool TryUpgrade(CarStatsConfig config, CarStatId id)
        {
            if (!IsReady || config == null || config.IsMaxLevel(id))
                return false;

            Player player = Player.Instance;
            if (player == null || !player.TrySpend(config.GetUpgradePrice(id)))
                return false;

            _levels[id] = GetLevel(id) + 1;
            _storage.Set(Serialize());
            _changed?.Invoke();
            return true;
        }

        private void OnLoaded(bool success, string data)
        {
            if (success)
            {
                Parse(data);
                _storage.Remember(data);
            }
            else
            {
                Debug.LogWarning("Car upgrades could not be loaded from storage.", this);
            }

            IsReady = true;
            _changed?.Invoke();
        }

        private void Parse(string data)
        {
            _levels.Clear();
            if (string.IsNullOrEmpty(data))
                return;

            string[] parts = data.Split(EntrySeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part))
                    continue;

                int split = part.IndexOf(LevelSeparator);
                if (split <= 0 || split >= part.Length - 1)
                    continue;

                if (!int.TryParse(part.Substring(0, split), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idValue))
                    continue;

                if (!int.TryParse(part.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
                    continue;

                if (!Enum.IsDefined(typeof(CarStatId), idValue) || level <= 0)
                    continue;

                _levels[(CarStatId)idValue] = level;
            }
        }

        private string Serialize()
        {
            var parts = new List<string>(_levels.Count);
            foreach (KeyValuePair<CarStatId, int> pair in _levels)
            {
                if (pair.Value <= 0)
                    continue;

                parts.Add(((int)pair.Key).ToString(CultureInfo.InvariantCulture)
                    + LevelSeparator
                    + pair.Value.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(EntrySeparator.ToString(), parts);
        }
    }
}
