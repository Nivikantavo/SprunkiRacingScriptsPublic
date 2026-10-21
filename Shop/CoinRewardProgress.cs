using System;
using System.Globalization;
using UnityEngine;

namespace SprunkiRacing
{
    public class CoinRewardProgress : MonoBehaviour
    {
        public const string StorageKey = "coin_reward_views";

        private const char Separator = ':';

        private readonly StoredValue _storage = new StoredValue(StorageKey, "Coin reward views could not be saved");
        private Action _changed;
        private string _day = string.Empty;
        private int _used;

        public static CoinRewardProgress Instance { get; private set; }

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

            var progressObject = new GameObject(nameof(CoinRewardProgress));
            progressObject.AddComponent<CoinRewardProgress>();
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

        public int GetRemaining(int limit)
        {
            limit = Mathf.Max(0, limit);
            if (!IsReady)
                return 0;

            return Mathf.Max(0, limit - UsedToday());
        }

        public bool TryConsumeView(int limit)
        {
            limit = Mathf.Max(0, limit);
            if (!IsReady || UsedToday() >= limit)
                return false;

            _used++;
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
                _day = Today();
                _used = 0;
                Debug.LogWarning("Coin reward views could not be loaded from storage.", this);
            }

            IsReady = true;
            _changed?.Invoke();
        }

        private int UsedToday()
        {
            string today = Today();
            if (_day == today)
                return _used;

            _day = today;
            _used = 0;
            return 0;
        }

        private void Parse(string data)
        {
            _day = Today();
            _used = 0;
            if (string.IsNullOrEmpty(data))
                return;

            int split = data.IndexOf(Separator);
            if (split <= 0 || split >= data.Length - 1)
                return;

            if (data.Substring(0, split) != _day)
                return;

            if (!int.TryParse(data.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int used))
                return;

            _used = Mathf.Max(0, used);
        }

        private string Serialize()
        {
            UsedToday();
            return _day + Separator + _used.ToString(CultureInfo.InvariantCulture);
        }

        private static string Today()
        {
            return DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }
    }
}
