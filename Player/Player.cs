using System;
using System.Globalization;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
#endif

namespace SprunkiRacing
{
    public class Player : MonoBehaviour
    {
        public const string BalanceStorageKey = "player_balance";

        private Action<int> _balanceChanged;
        private int _queuedDelta;
        private bool _saveInFlight;
        private bool _savePending;

        public static Player Instance { get; private set; }

        public int Balance { get; private set; }

        public bool IsReady { get; private set; }

        public event Action<int> BalanceChanged
        {
            add
            {
                _balanceChanged += value;
                if (IsReady)
                    value?.Invoke(Balance);
            }
            remove => _balanceChanged -= value;
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

            var playerObject = new GameObject(nameof(Player));
            playerObject.AddComponent<Player>();
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
            LoadBalance();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void AddBalance(int amount)
        {
            if (amount > 0)
                ApplyBalance(amount);
        }

        public void ApplyBalance(int delta)
        {
            if (delta == 0)
                return;

            if (!IsReady)
            {
                _queuedDelta = ClampAdd(_queuedDelta, delta);
                return;
            }

            SetBalance(ClampAdd(Balance, delta));
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || !IsReady || Balance < amount)
                return false;

            SetBalance(Balance - amount);
            return true;
        }

        private void LoadBalance()
        {
#if UNITY_WEBGL
            Bridge.storage.Get(BalanceStorageKey, OnBalanceLoaded);
#else
            string stored = PlayerPrefs.GetString(BalanceStorageKey, null);
            OnBalanceLoaded(true, string.IsNullOrEmpty(stored) ? null : stored);
#endif
        }

        private void OnBalanceLoaded(bool success, string data)
        {
            int balance = 0;
            if (success && TryParseBalance(data, out int stored))
                balance = stored;
            else if (!success)
                Debug.LogWarning("Player balance could not be loaded from Playgama storage.", this);

            IsReady = true;
            Balance = balance;
            _balanceChanged?.Invoke(Balance);

            if (_queuedDelta == 0)
                return;

            int queuedDelta = _queuedDelta;
            _queuedDelta = 0;
            ApplyBalance(queuedDelta);
        }

        private void SetBalance(int value)
        {
            value = Mathf.Max(0, value);
            if (Balance == value)
                return;

            Balance = value;
            _balanceChanged?.Invoke(Balance);
            SaveBalance();
        }

        private void SaveBalance()
        {
            if (_saveInFlight)
            {
                _savePending = true;
                return;
            }

            _saveInFlight = true;
#if UNITY_WEBGL
            Bridge.storage.Set(BalanceStorageKey, Balance, OnBalanceSaved);
#else
            PlayerPrefs.SetString(BalanceStorageKey, Balance.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            OnBalanceSaved(true);
#endif
        }

        private void OnBalanceSaved(bool success)
        {
            _saveInFlight = false;

            if (!success)
                Debug.LogWarning("Player balance could not be saved to Playgama storage.", this);

            if (!_savePending)
                return;

            _savePending = false;
            SaveBalance();
        }

        private static bool TryParseBalance(string data, out int balance)
        {
            if (string.IsNullOrEmpty(data))
            {
                balance = 0;
                return false;
            }

            if (!int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out balance))
                return false;

            balance = Mathf.Max(0, balance);
            return true;
        }

        private static int ClampAdd(int balance, int amount)
        {
            long sum = (long)balance + amount;
            if (sum >= int.MaxValue)
                return int.MaxValue;

            if (sum <= int.MinValue)
                return int.MinValue;

            return (int)sum;
        }
    }
}
