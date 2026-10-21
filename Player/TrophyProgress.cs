using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
#endif

namespace SprunkiRacing
{
    public class TrophyProgress : MonoBehaviour
    {
        public const string StorageKey = "player_trophies";
        public const string ProbeStorageKey = "player_trophies_probe";

        private const string GuestStorageKey = "player_trophies_guest";
        private const string GuestProbeKey = "player_trophies_guest_probe";
        private const string MergedProbeKey = "player_trophies_merged_probe";

        private Action<int> _changed;
        private int _queuedDelta;
        private bool _saveInFlight;
        private bool _savePending;
        private bool _guestSession;
        private bool _clearGuestOnSave;
        private bool _submitOnSave;
        private bool _mergeInFlight;
        private bool _mergeAfterSave;
        private bool _deferAccountSave;
        private bool _leaderboardVisible;
        private int _mergeGuest = -1;
        private string _mergeProbe;
        private string _accountProbe;

        public static TrophyProgress Instance { get; private set; }

        public int Trophies { get; private set; }

        public bool IsReady { get; private set; }

        public event Action<int> Changed
        {
            add
            {
                _changed += value;
                if (IsReady)
                    value?.Invoke(Trophies);
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

            var progressObject = new GameObject(nameof(TrophyProgress));
            progressObject.AddComponent<TrophyProgress>();
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
            _leaderboardVisible = LeaderboardBoard.CanUse;
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

#if UNITY_WEBGL
        private void Update()
        {
            if (Application.isEditor)
                return;

            bool visible = LeaderboardBoard.CanUse;
            if (visible == _leaderboardVisible)
                return;

            _leaderboardVisible = visible;
            LeaderboardBoard.NotifyAvailabilityChanged();

            if (!visible || !IsReady || _mergeInFlight || !_guestSession || !IsPlayerAuthorized())
                return;

            BeginAuthorizationMerge();
        }
#endif

        public void Apply(int delta)
        {
            if (delta == 0)
                return;

            if (!IsReady || _mergeInFlight)
            {
                _queuedDelta = ClampAdd(_queuedDelta, delta);
                return;
            }

            SetTrophies(ClampAdd(Trophies, delta));
        }

        private void Load()
        {
#if UNITY_WEBGL
            Bridge.storage.Get(new List<string> { StorageKey, ProbeStorageKey }, OnLoaded);
#else
            string stored = PlayerPrefs.GetString(StorageKey, null);
            OnLoaded(true, new List<string> { string.IsNullOrEmpty(stored) ? null : stored });
#endif
        }

        private void OnLoaded(bool success, List<string> data)
        {
            int trophies = 0;
            string probe = null;
            if (success && data != null)
            {
                if (data.Count > 0 && TryParse(data[0], out int stored))
                    trophies = stored;

                if (data.Count > 1 && !string.IsNullOrEmpty(data[1]))
                    probe = data[1];
            }

            if (!success || data == null)
            {
                if (!success)
                    Debug.LogWarning("Player trophies could not be loaded from storage.", this);

                _guestSession = TracksAuthorization && !IsPlayerAuthorized();
                _deferAccountSave = TracksAuthorization && IsPlayerAuthorized();
                int fallback = 0;
                string fallbackProbe = null;
                if (_guestSession && TryReadGuest(out int guest, out string guestProbe))
                {
                    fallback = guest;
                    fallbackProbe = guestProbe;
                }

                FinishReady(fallback, fallbackProbe, false);
                return;
            }

            if (TracksAuthorization && IsPlayerAuthorized())
            {
                ApplyAuthorization(trophies, probe);
                return;
            }

            _guestSession = TracksAuthorization && !IsPlayerAuthorized();
            if (_guestSession && WasMerged(probe))
            {
                FinishReady(0, null, false);
                return;
            }

            int loadedTrophies = trophies;
            if (_guestSession && TryReadGuest(out int buffered, out string bufferedProbe) && buffered > trophies)
            {
                trophies = buffered;
                if (!string.IsNullOrEmpty(bufferedProbe))
                    probe = bufferedProbe;
            }

            FinishReady(trophies, probe, false);
            if (_guestSession && Trophies > loadedTrophies)
                Save();
        }

        private void BeginAuthorizationMerge()
        {
            if (_saveInFlight)
            {
                _mergeAfterSave = true;
                return;
            }

            _mergeGuest = Mathf.Max(0, Trophies);
            _mergeProbe = _accountProbe;
            _mergeInFlight = true;
            IsReady = false;
#if UNITY_WEBGL
            Bridge.storage.Get(new List<string> { StorageKey, ProbeStorageKey }, OnAuthorizationLoaded);
#else
            OnAuthorizationLoaded(false, null);
#endif
        }

        private void OnAuthorizationLoaded(bool success, List<string> data)
        {
            if (!success || data == null)
            {
                Debug.LogWarning("Account trophies could not be loaded.", this);
                int guest = _mergeGuest >= 0 ? _mergeGuest : Trophies;
                string guestProbe = !string.IsNullOrEmpty(_mergeProbe) ? _mergeProbe : _accountProbe;
                _mergeGuest = -1;
                _mergeProbe = null;
                _mergeInFlight = false;
                _guestSession = true;
                _deferAccountSave = true;
                FinishReady(guest, guestProbe, false);
                return;
            }

            int trophies = 0;
            string probe = null;
            if (data.Count > 0 && TryParse(data[0], out int stored))
                trophies = stored;

            if (data.Count > 1 && !string.IsNullOrEmpty(data[1]))
                probe = data[1];

            _deferAccountSave = false;
            ApplyAuthorization(trophies, probe);
        }

        private void ApplyAuthorization(int accountTrophies, string accountProbe)
        {
            _guestSession = false;

            int guest = 0;
            string guestProbe = null;
            bool hasGuest = TryReadGuest(out guest, out guestProbe);
            if (_mergeGuest > guest)
            {
                guest = _mergeGuest;
                if (!string.IsNullOrEmpty(_mergeProbe))
                    guestProbe = _mergeProbe;

                hasGuest = guest > 0 && !string.IsNullOrEmpty(guestProbe);
            }

            _mergeGuest = -1;
            _mergeProbe = null;

            int queued = _queuedDelta;
            _queuedDelta = 0;
            bool sameProbe = hasGuest
                && !string.IsNullOrEmpty(guestProbe)
                && string.Equals(guestProbe, accountProbe, StringComparison.Ordinal);

            if (!hasGuest || guest <= 0 || sameProbe)
            {
                if (!string.IsNullOrEmpty(accountProbe)
                    && string.Equals(PlayerPrefs.GetString(GuestProbeKey, string.Empty), accountProbe, StringComparison.Ordinal))
                    ClearGuest();

                _mergeInFlight = false;
                FinishReady(ClampAdd(accountTrophies, queued), accountProbe, true);
                return;
            }

            BeginCommit(ClampAdd(ClampAdd(accountTrophies, guest), queued), guestProbe);
        }

        private void BeginCommit(int value, string probe)
        {
            value = Mathf.Max(0, value);
            bool notify = !IsReady || Trophies != value;
            _mergeInFlight = true;
            _clearGuestOnSave = true;
            _submitOnSave = true;
            _guestSession = false;
            _deferAccountSave = false;
            Trophies = value;
            _accountProbe = probe;
            IsReady = true;

            if (notify)
                _changed?.Invoke(Trophies);

            Save();
        }

        private void FinishReady(int trophies, string probe, bool submit)
        {
            trophies = Mathf.Max(0, trophies);
            bool notify = !IsReady || Trophies != trophies;
            Trophies = trophies;
            _accountProbe = probe;
            IsReady = true;

            if (notify)
                _changed?.Invoke(Trophies);

            if (_guestSession && Trophies > 0)
            {
                string storedProbe = probe;
                WriteGuest(Trophies);
                if (!string.Equals(storedProbe, _accountProbe, StringComparison.Ordinal))
                    Save();
            }

            if (submit && !_deferAccountSave && LeaderboardBoard.CanUse)
                LeaderboardBoard.Submit(Trophies, null);

            if (_queuedDelta == 0)
                return;

            int queued = _queuedDelta;
            _queuedDelta = 0;
            SetTrophies(ClampAdd(Trophies, queued));
        }

        private void SetTrophies(int value)
        {
            value = Mathf.Max(0, value);
            if (Trophies == value)
                return;

            Trophies = value;
            _changed?.Invoke(Trophies);
            Save();
            if (!_deferAccountSave && !_guestSession && LeaderboardBoard.CanUse)
                LeaderboardBoard.Submit(Trophies, null);
        }

        private void Save()
        {
            if (_guestSession)
                WriteGuest(Trophies);

            if (_deferAccountSave)
                return;

            if (_saveInFlight)
            {
                _savePending = true;
                return;
            }

            _saveInFlight = true;
#if UNITY_WEBGL
            if ((_guestSession || _clearGuestOnSave) && !string.IsNullOrEmpty(_accountProbe))
            {
                var data = new Dictionary<string, object>
                {
                    { StorageKey, Trophies.ToString(CultureInfo.InvariantCulture) },
                    { ProbeStorageKey, _accountProbe }
                };
                Bridge.storage.Set(data, OnSaved);
                return;
            }

            Bridge.storage.Set(StorageKey, Trophies, OnSaved);
#else
            PlayerPrefs.SetString(StorageKey, Trophies.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            OnSaved(true);
#endif
        }

        private void OnSaved(bool success)
        {
            _saveInFlight = false;

            if (!success)
                Debug.LogWarning("Player trophies could not be saved to storage.", this);
            else if (_clearGuestOnSave)
            {
                RememberMergedProbe(_accountProbe);
                ClearGuest();
                _clearGuestOnSave = false;
            }

            bool submit = success && _submitOnSave;
            _submitOnSave = false;
            bool merging = _mergeInFlight;
            _mergeInFlight = false;

            if (!IsReady)
            {
                IsReady = true;
                _changed?.Invoke(Trophies);
            }

            if (submit && LeaderboardBoard.CanUse)
                LeaderboardBoard.Submit(Trophies, null);

            if (merging)
                LeaderboardBoard.NotifyAvailabilityChanged();

            if (_mergeAfterSave)
            {
                _mergeAfterSave = false;
                _savePending = false;
                BeginAuthorizationMerge();
                return;
            }

            if (_queuedDelta != 0)
            {
                int queued = _queuedDelta;
                _queuedDelta = 0;
                _savePending = false;
                SetTrophies(ClampAdd(Trophies, queued));
                return;
            }

            if (!_savePending)
                return;

            _savePending = false;
            Save();
        }

        private void WriteGuest(int trophies)
        {
            if (trophies <= 0)
                return;

            string probe = PlayerPrefs.GetString(GuestProbeKey, string.Empty);
            if (string.IsNullOrEmpty(probe))
                probe = _accountProbe;

            if (string.IsNullOrEmpty(probe))
                probe = Guid.NewGuid().ToString("N");

            _accountProbe = probe;
            PlayerPrefs.SetString(GuestStorageKey, trophies.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(GuestProbeKey, probe);
            PlayerPrefs.Save();
        }

        private static bool TryReadGuest(out int trophies, out string probe)
        {
            probe = PlayerPrefs.GetString(GuestProbeKey, string.Empty);
            string stored = PlayerPrefs.GetString(GuestStorageKey, null);
            if (string.IsNullOrEmpty(probe) || !TryParse(stored, out trophies) || trophies <= 0)
            {
                trophies = 0;
                probe = null;
                return false;
            }

            return true;
        }

        private static void ClearGuest()
        {
            PlayerPrefs.DeleteKey(GuestStorageKey);
            PlayerPrefs.DeleteKey(GuestProbeKey);
            PlayerPrefs.Save();
        }

        private static void RememberMergedProbe(string probe)
        {
            if (string.IsNullOrEmpty(probe))
                return;

            PlayerPrefs.SetString(MergedProbeKey, probe);
            PlayerPrefs.Save();
        }

        private static bool WasMerged(string probe)
        {
            return !string.IsNullOrEmpty(probe)
                && string.Equals(PlayerPrefs.GetString(MergedProbeKey, string.Empty), probe, StringComparison.Ordinal);
        }

        private static bool TracksAuthorization
        {
            get
            {
#if UNITY_WEBGL
                return !Application.isEditor
                    && Bridge.instance != null
                    && Bridge.player.isAuthorizationSupported;
#else
                return false;
#endif
            }
        }

        private static bool IsPlayerAuthorized()
        {
#if UNITY_WEBGL
            return !Application.isEditor
                && Bridge.instance != null
                && Bridge.player.isAuthorized;
#else
            return false;
#endif
        }

        private static bool TryParse(string data, out int trophies)
        {
            if (string.IsNullOrEmpty(data))
            {
                trophies = 0;
                return false;
            }

            if (!int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out trophies))
                return false;

            trophies = Mathf.Max(0, trophies);
            return true;
        }

        private static int ClampAdd(int total, int amount)
        {
            long sum = (long)total + amount;
            if (sum >= int.MaxValue)
                return int.MaxValue;

            if (sum <= int.MinValue)
                return int.MinValue;

            return (int)sum;
        }
    }
}
