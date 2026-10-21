using System;
using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public class TrailProgress : MonoBehaviour
    {
        public const string UnlocksStorageKey = "trail_unlocks";
        public const string SelectedStorageKey = "trail_selected";

        private const char UnlockSeparator = '|';

        private readonly HashSet<string> _purchasedIds = new HashSet<string>();
        private readonly StoredValue _unlocks = new StoredValue(UnlocksStorageKey, "Trail progress could not be saved");
        private readonly StoredValue _selected = new StoredValue(SelectedStorageKey, "Trail progress could not be saved");
        private Action _changed;
        private int _pendingLoads;

        public static TrailProgress Instance { get; private set; }

        public bool IsReady { get; private set; }

        public string SelectedId { get; private set; }

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

            var progressObject = new GameObject(nameof(TrailProgress));
            progressObject.AddComponent<TrailProgress>();
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
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsUnlocked(TrailConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Id))
                return false;

            return config.UnlockedByDefault || _purchasedIds.Contains(config.Id);
        }

        public bool TryPurchase(TrailConfig config)
        {
            if (!IsReady || config == null || string.IsNullOrEmpty(config.Id) || IsUnlocked(config))
                return false;

            Player player = Player.Instance;
            if (player == null || !player.TrySpend(config.Price))
                return false;

            _purchasedIds.Add(config.Id);
            _unlocks.Set(SerializeUnlocks());
            Select(config);
            _changed?.Invoke();
            return true;
        }

        public bool TrySelect(TrailConfig config)
        {
            if (!IsReady || !IsUnlocked(config))
                return false;

            if (SelectedId == config.Id)
                return true;

            Select(config);
            _changed?.Invoke();
            return true;
        }

        private void Select(TrailConfig config)
        {
            SelectedId = config.Id;
            _selected.Set(SelectedId);
        }

        private void Load()
        {
            _pendingLoads = 2;
            _unlocks.Load(OnUnlocksLoaded);
            _selected.Load(OnSelectedLoaded);
        }

        private void OnUnlocksLoaded(bool success, string data)
        {
            if (success)
            {
                ParseUnlocks(data);
                _unlocks.Remember(data);
            }
            else
            {
                Debug.LogWarning("Trail unlocks could not be loaded from storage.", this);
            }

            CompleteLoad();
        }

        private void OnSelectedLoaded(bool success, string data)
        {
            if (success)
            {
                SelectedId = string.IsNullOrEmpty(data) ? null : data;
                _selected.Remember(SelectedId);
            }
            else
            {
                Debug.LogWarning("Selected trail could not be loaded from storage.", this);
            }

            CompleteLoad();
        }

        private void CompleteLoad()
        {
            _pendingLoads--;
            if (_pendingLoads > 0)
                return;

            IsReady = true;
            _changed?.Invoke();
        }

        private void ParseUnlocks(string data)
        {
            _purchasedIds.Clear();
            if (string.IsNullOrEmpty(data))
                return;

            string[] ids = data.Split(UnlockSeparator);
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                    _purchasedIds.Add(ids[i]);
            }
        }

        private string SerializeUnlocks()
        {
            return string.Join(UnlockSeparator.ToString(), _purchasedIds);
        }
    }
}
