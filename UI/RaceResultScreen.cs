using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
#endif

namespace SprunkiRacing
{
    public class RaceResultScreen : MonoBehaviour
    {
        private const string MenuScene = "MainMenu";
        private const string DoubleCoinsPlacement = "race_coins_x2";
        private const string ClaimPlacement = "race_claim";

        [SerializeField] private RectTransform victoryScreen;
        [SerializeField] private RectTransform defeatScreen;
        [SerializeField] private RectTransform clearScreen;
        [SerializeField] private TextMeshProUGUI victoryTrophies;
        [SerializeField] private TextMeshProUGUI defeatTrophies;
        [SerializeField] private Button victoryContinue;
        [SerializeField] private Button defeatContinue;
        [SerializeField] private TextMeshProUGUI placeLabel;
        [SerializeField] private RectTransform itemGroup;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button doubleButton;
        [SerializeField] private PrizeItemView cupPrefab;
        [SerializeField] private PrizeItemView moneyPrefab;
        [SerializeField, Min(0f)] private float prizeRevealDelay = 1f;
        [SerializeField] private AudioClip prizeRevealClip;
        [SerializeField, Min(0.5f)] private float prizePitchStart = 1f;
        [SerializeField, Min(0f)] private float prizePitchStep = 2f;
        [SerializeField, Min(0.5f)] private float prizePitchMax = 2.25f;
        [SerializeField] private ParticleSystem prizeBurst;

        private readonly List<PrizeRequest> _prizeQueue = new List<PrizeRequest>();
        private readonly List<AudioSource> _prizeVoices = new List<AudioSource>();
        private RectTransform _outcome;
        private RaceRewardBreakdown _rewards;
        private Coroutine _prizeReveal;
        private int _prizeCursor;
        private int _prizeSoundIndex;
        private int _place;
        private int _racerCount;
        private bool _shown;
        private bool _clearShown;
        private bool _leaving;
        private bool _doubleInFlight;
        private bool _doubleAttempted;
        private bool _doubleGranted;
        private bool _interstitialInFlight;
        private bool _listeningReward;
        private bool _listeningInterstitial;

        private void Awake()
        {
            Hide(victoryScreen);
            Hide(defeatScreen);
            Hide(clearScreen);
            Listen(victoryContinue, ShowClear);
            Listen(defeatContinue, ShowClear);
            Listen(claimButton, Claim);
            Listen(homeButton, GoToMenu);
            Listen(doubleButton, WatchDouble);
        }

        private void OnDestroy()
        {
            Unlisten(victoryContinue, ShowClear);
            Unlisten(defeatContinue, ShowClear);
            Unlisten(claimButton, Claim);
            Unlisten(homeButton, GoToMenu);
            Unlisten(doubleButton, WatchDouble);
            StopListenReward();
            StopListenInterstitial();
        }

        public bool Show(bool victory, RaceRewardBreakdown rewards, int place, int racerCount)
        {
            if (_shown)
                return true;

            RectTransform screen = victory ? victoryScreen : defeatScreen;
            if (screen == null)
            {
                Debug.LogError(victory
                    ? "Race result screen has no victory panel."
                    : "Race result screen has no defeat panel.", this);
                return false;
            }

            _shown = true;
            _rewards = rewards;
            _place = place;
            _racerCount = racerCount;

            Hide(victoryScreen);
            Hide(defeatScreen);
            Hide(clearScreen);
            _outcome = screen;
            Reveal(screen);

            TextMeshProUGUI trophies = victory ? victoryTrophies : defeatTrophies;
            if (trophies != null)
                trophies.text = PrizeItemView.FormatSigned(rewards.Trophies);
            else
                Debug.LogError("Race result screen has no trophy label.", this);

            return true;
        }

        private void ShowClear()
        {
            if (_clearShown)
                return;

            _clearShown = true;
            Hide(_outcome);

            if (clearScreen == null)
            {
                Debug.LogError("Race result screen has no clear panel.", this);
                return;
            }

            Reveal(clearScreen);
            if (placeLabel != null)
            {
                placeLabel.text = _place.ToString(CultureInfo.InvariantCulture)
                    + " / "
                    + _racerCount.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                Debug.LogError("Race result clear screen has no place label.", this);
            }

            ClearPrizeSlots();
            EnqueuePrize(cupPrefab, _rewards.PlaceTrophies, false, false);
            EnqueuePrize(cupPrefab, _rewards.KillTrophies, false, true);
            EnqueuePrize(moneyPrefab, _rewards.PlaceCoins, false, false);
            EnqueuePrize(moneyPrefab, _rewards.KillCoins, false, true);
            BeginPrizeReveal();
        }

        private void WatchDouble()
        {
            if (_doubleInFlight || _doubleGranted)
                return;

            _doubleInFlight = true;
            if (doubleButton != null)
                doubleButton.interactable = false;

#if UNITY_WEBGL
            if (!Bridge.advertisement.isRewardedSupported)
            {
                _doubleInFlight = false;
                if (doubleButton != null)
                    doubleButton.interactable = true;

                Debug.LogWarning("Rewarded ads are not supported on this platform.", this);
                return;
            }

            _doubleAttempted = true;
            ListenReward();
            Bridge.advertisement.ShowRewarded(DoubleCoinsPlacement);
#else
            _doubleAttempted = true;
            CompleteDouble();
            _doubleInFlight = false;
#endif
        }

        private void Claim()
        {
            if (IsBusyLeaving())
                return;

            if (_doubleAttempted)
            {
                GoToMenu();
                return;
            }

            ShowClaimInterstitial();
        }

        private void ShowClaimInterstitial()
        {
#if UNITY_WEBGL
            if (Bridge.instance == null || !Bridge.advertisement.isInterstitialSupported)
            {
                GoToMenu();
                return;
            }

            _interstitialInFlight = true;
            if (claimButton != null)
                claimButton.interactable = false;

            ListenInterstitial();
            Bridge.advertisement.ShowInterstitial(ClaimPlacement);
#else
            GoToMenu();
#endif
        }

        private void CompleteDouble()
        {
            if (_doubleGranted)
                return;

            _doubleGranted = true;
            int bonus = _rewards.Coins;
            if (bonus != 0)
            {
                if (Player.Instance != null)
                    Player.Instance.ApplyBalance(bonus);
                else
                    Debug.LogError("Player is missing.", this);
            }

            EnqueuePrize(moneyPrefab, bonus, true, false);
            BeginPrizeReveal();
            if (doubleButton != null)
                doubleButton.interactable = false;
        }

        private bool IsBusyLeaving()
        {
            return _leaving || _doubleInFlight || _interstitialInFlight;
        }

        private void GoToMenu()
        {
            if (_leaving || _interstitialInFlight)
                return;

            _leaving = true;
            PlatformSignals.LoadingStarted();
            Time.timeScale = PlatformHost.IsHostPaused ? 0f : 1f;
            SceneManager.LoadScene(MenuScene);
        }

        private void EnqueuePrize(PrizeItemView prefab, int amount, bool bonus, bool kill)
        {
            if (kill && amount == 0)
                return;

            _prizeQueue.Add(new PrizeRequest
            {
                Prefab = prefab,
                Amount = amount,
                Bonus = bonus,
                Kill = kill
            });
        }

        private void BeginPrizeReveal()
        {
            if (_prizeReveal == null)
                _prizeReveal = StartCoroutine(RevealPrizes());
        }

        private IEnumerator RevealPrizes()
        {
            while (_prizeCursor < _prizeQueue.Count)
            {
                PrizeRequest request = _prizeQueue[_prizeCursor];
                _prizeCursor++;
                PrizeItemView view = SpawnPrize(request.Prefab, request.Amount, request.Bonus, request.Kill);
                if (view != null)
                {
                    view.transform.localScale = Vector3.zero;
                    RebuildItems();
                    PlayPrizeSound();
                    PrizeBurst.Play(view.transform, prizeBurst);
                    StartCoroutine(Punch(view.transform));
                }

                if (_prizeCursor < _prizeQueue.Count && prizeRevealDelay > 0f)
                    yield return new WaitForSecondsRealtime(prizeRevealDelay);
            }

            _prizeQueue.Clear();
            _prizeCursor = 0;
            _prizeReveal = null;
        }

        private IEnumerator Punch(Transform target)
        {
            const float duration = 0.28f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float scale = EaseOutBack(Mathf.Clamp01(elapsed / duration));
                target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            if (target != null)
                target.localScale = Vector3.one;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float p = t - 1f;
            return 1f + (overshoot + 1f) * p * p * p + overshoot * p * p;
        }

        private PrizeItemView SpawnPrize(PrizeItemView prefab, int amount, bool bonus, bool kill)
        {
            if (itemGroup == null || prefab == null)
            {
                Debug.LogError("Race result screen is missing a prize slot or prefab.", this);
                return null;
            }

            PrizeItemView view = Instantiate(prefab, itemGroup);
            view.Bind(amount, bonus, kill);
            return view;
        }

        private void PlayPrizeSound()
        {
            if (prizeRevealClip == null)
                return;

            AudioSource voice = RentPrizeVoice();
            voice.pitch = NextPrizePitch();
            voice.PlayOneShot(prizeRevealClip, GameAudio.Sfx);
        }

        private float NextPrizePitch()
        {
            int card = _prizeSoundIndex;
            _prizeSoundIndex++;
            float semitones = prizePitchStep * card * (card + 1f) * 0.5f;
            float pitch = prizePitchStart * Mathf.Pow(2f, semitones / 12f);
            return Mathf.Clamp(pitch, 0.5f, prizePitchMax);
        }

        private AudioSource RentPrizeVoice()
        {
            for (int i = 0; i < _prizeVoices.Count; i++)
            {
                AudioSource voice = _prizeVoices[i];
                if (voice != null && !voice.isPlaying)
                    return voice;
            }

            AudioSource created = gameObject.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.spatialBlend = 0f;
            created.ignoreListenerPause = true;
            _prizeVoices.Add(created);
            return created;
        }

        private void ClearPrizeSlots()
        {
            _prizeQueue.Clear();
            _prizeCursor = 0;
            _prizeSoundIndex = 0;
            if (_prizeReveal != null)
            {
                StopCoroutine(_prizeReveal);
                _prizeReveal = null;
            }

            if (itemGroup == null)
                return;

            for (int i = itemGroup.childCount - 1; i >= 0; i--)
            {
                GameObject child = itemGroup.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private struct PrizeRequest
        {
            public PrizeItemView Prefab;
            public int Amount;
            public bool Bonus;
            public bool Kill;
        }

        private void RebuildItems()
        {
            if (itemGroup == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(itemGroup);
        }

        private void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError("Race result screen is missing a button.", this);
                return;
            }

            button.onClick.AddListener(action);
        }

        private static void Unlisten(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }

        private static void Hide(RectTransform screen)
        {
            if (screen != null)
                screen.gameObject.SetActive(false);
        }

        private static void Reveal(RectTransform screen)
        {
            screen.gameObject.SetActive(true);
            screen.SetAsLastSibling();
        }

#if UNITY_WEBGL
        private void ListenReward()
        {
            if (_listeningReward)
                return;

            Bridge.advertisement.rewardedStateChanged += HandleRewardedState;
            _listeningReward = true;
        }

        private void HandleRewardedState(RewardedState state)
        {
            if (state == RewardedState.Rewarded)
                CompleteDouble();

            if (state != RewardedState.Closed && state != RewardedState.Failed)
                return;

            _doubleInFlight = false;
            StopListenReward();
            if (doubleButton != null && !_doubleGranted)
                doubleButton.interactable = true;
        }
#endif

        private void StopListenReward()
        {
#if UNITY_WEBGL
            if (!_listeningReward)
                return;

            if (Bridge.instance != null)
                Bridge.advertisement.rewardedStateChanged -= HandleRewardedState;

            _listeningReward = false;
#endif
        }

#if UNITY_WEBGL
        private void ListenInterstitial()
        {
            if (_listeningInterstitial)
                return;

            Bridge.advertisement.interstitialStateChanged += HandleInterstitialState;
            _listeningInterstitial = true;
        }

        private void HandleInterstitialState(InterstitialState state)
        {
            if (state != InterstitialState.Closed && state != InterstitialState.Failed)
                return;

            _interstitialInFlight = false;
            StopListenInterstitial();
            GoToMenu();
        }
#endif

        private void StopListenInterstitial()
        {
#if UNITY_WEBGL
            if (!_listeningInterstitial)
                return;

            if (Bridge.instance != null)
                Bridge.advertisement.interstitialStateChanged -= HandleInterstitialState;

            _listeningInterstitial = false;
#endif
        }
    }
}
