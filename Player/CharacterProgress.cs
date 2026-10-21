using System;
using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public class CharacterProgress : MonoBehaviour
    {
        public const string UnlocksStorageKey = "character_unlocks";
        public const string SelectedStorageKey = "character_selected";

        private const char UnlockSeparator = '|';

        private readonly HashSet<string> _purchasedIds = new HashSet<string>();
        private readonly StoredValue _unlocks = new StoredValue(UnlocksStorageKey, "Character progress could not be saved");
        private readonly StoredValue _selected = new StoredValue(SelectedStorageKey, "Character progress could not be saved");
        private Action _changed;
        private int _pendingLoads;

        public static CharacterProgress Instance { get; private set; }

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

            var progressObject = new GameObject(nameof(CharacterProgress));
            progressObject.AddComponent<CharacterProgress>();
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

        public bool IsUnlocked(CharacterConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Id))
                return false;

            return config.UnlockedByDefault || _purchasedIds.Contains(config.Id);
        }

        public bool TryPurchase(CharacterConfig config)
        {
            if (!IsReady || config == null || string.IsNullOrEmpty(config.Id) || IsUnlocked(config))
                return false;

            Player player = Player.Instance;
            if (player == null || !player.TrySpend(config.Price))
                return false;

            _purchasedIds.Add(config.Id);
            _unlocks.Set(SerializeUnlocks());
            _changed?.Invoke();
            return true;
        }

        public bool TrySelect(CharacterConfig config)
        {
            if (!IsReady || !IsUnlocked(config))
                return false;

            if (SelectedId == config.Id)
                return true;

            SelectedId = config.Id;
            _selected.Set(SelectedId);
            _changed?.Invoke();
            return true;
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
                Debug.LogWarning("Character unlocks could not be loaded from storage.", this);
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
                Debug.LogWarning("Selected character could not be loaded from storage.", this);
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
