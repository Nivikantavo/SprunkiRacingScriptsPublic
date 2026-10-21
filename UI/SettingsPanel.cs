using System;
using CarControllerwithShooting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
#endif

namespace SprunkiRacing
{
    public class SettingsPanel : MonoBehaviour
    {
        public const string GameplayScene = "GameplayScene";
        public const string MenuScene = "MainMenu";
        const string QualityKey = "QualitySetting";
        const string GamesCatalogPlatformId = "yandex";

        Slider _sfxSlider;
        Slider _musicSlider;
        Toggle _low;
        Toggle _medium;
        Toggle _high;
        [SerializeField] Button languageButton;
        [SerializeField] LanguagePanel languagePanel;
        Button _close;
        Button _quit;
        Button _rate;
        Button _otherGames;
        int _lowLevel = 1;
        int _mediumLevel = 2;
        int _highLevel = 3;
        bool _guard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedQuality()
        {
            int level = ResolveLevel(out bool save);
            QualitySettings.SetQualityLevel(level, true);
            if (!save)
                return;

            PlayerPrefs.SetInt(QualityKey, level);
            PlayerPrefs.Save();
        }

        void Awake()
        {
            _lowLevel = FindLevel("Low", 1);
            _mediumLevel = FindLevel("Medium", 2);
            _highLevel = FindLevel("High", 3);

            _sfxSlider = FindSlider("Sound_Group", "Slider_On");
            _musicSlider = FindSlider("Music_Group", "Slider_Off");
            _low = FindToggle("Toggle_Low");
            _medium = FindToggle("Toggle_Medium");
            _high = FindToggle("Toggle_High");
            _close = FindButton("Button_Close");
            _quit = FindButton("Button_Quit");
            _rate = FindButton("Button_Rate");
            _otherGames = FindButton("Button_OtherGames");

            if (_sfxSlider != null)
                _sfxSlider.onValueChanged.AddListener(GameAudio.SetSfx);
            if (_musicSlider != null)
                _musicSlider.onValueChanged.AddListener(GameAudio.SetMusic);
            if (_low != null)
                _low.onValueChanged.AddListener(on => SelectQuality(_low, _lowLevel, on));
            if (_medium != null)
                _medium.onValueChanged.AddListener(on => SelectQuality(_medium, _mediumLevel, on));
            if (_high != null)
                _high.onValueChanged.AddListener(on => SelectQuality(_high, _highLevel, on));
            if (_close != null)
                _close.onClick.AddListener(Close);
            if (_quit != null)
                _quit.onClick.AddListener(QuitRace);
            if (_rate != null)
                _rate.onClick.AddListener(RateGame);
            if (_otherGames != null)
                _otherGames.onClick.AddListener(OpenOtherGames);
            if (languageButton != null)
                languageButton.onClick.AddListener(OpenLanguage);
        }

        void OnDestroy()
        {
            if (_sfxSlider != null)
                _sfxSlider.onValueChanged.RemoveListener(GameAudio.SetSfx);
            if (_musicSlider != null)
                _musicSlider.onValueChanged.RemoveListener(GameAudio.SetMusic);
            if (_close != null)
                _close.onClick.RemoveListener(Close);
            if (_quit != null)
                _quit.onClick.RemoveListener(QuitRace);
            if (_rate != null)
                _rate.onClick.RemoveListener(RateGame);
            if (_otherGames != null)
                _otherGames.onClick.RemoveListener(OpenOtherGames);
            if (languageButton != null)
                languageButton.onClick.RemoveListener(OpenLanguage);
        }

        void OnEnable()
        {
            RefreshSliders();
            SyncToggles(Snap(QualitySettings.GetQualityLevel()));
            if (_quit != null)
                _quit.gameObject.SetActive(SceneManager.GetActiveScene().name == GameplayScene);
            LeaderboardBoard.AvailabilityChanged += RefreshPlatformButtons;
            RefreshPlatformButtons();
        }

        void OnDisable()
        {
            LeaderboardBoard.AvailabilityChanged -= RefreshPlatformButtons;
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        void OpenLanguage()
        {
            if (languagePanel != null)
                languagePanel.Open();
        }

        void RefreshSliders()
        {
            if (_sfxSlider != null)
                _sfxSlider.SetValueWithoutNotify(GameAudio.Sfx);
            if (_musicSlider != null)
                _musicSlider.SetValueWithoutNotify(GameAudio.Music);
        }

        void SelectQuality(Toggle toggle, int level, bool isOn)
        {
            if (_guard)
                return;

            if (!isOn)
            {
                if (QualitySettings.GetQualityLevel() == level && toggle != null)
                    toggle.SetIsOnWithoutNotify(true);
                return;
            }

            QualitySettings.SetQualityLevel(level, true);
            PlayerPrefs.SetInt(QualityKey, level);
            PlayerPrefs.Save();
            SyncToggles(level);
        }

        void SyncToggles(int level)
        {
            _guard = true;
            if (_low != null)
                _low.SetIsOnWithoutNotify(level == _lowLevel);
            if (_medium != null)
                _medium.SetIsOnWithoutNotify(level == _mediumLevel);
            if (_high != null)
                _high.SetIsOnWithoutNotify(level == _highLevel);
            _guard = false;
        }

        void Close()
        {
            GameCanvas canvas = GameCanvas.Instance;
            if (canvas != null && canvas.Panel_Settings == gameObject)
            {
                canvas.Click_Close_Settings();
                return;
            }

            gameObject.SetActive(false);
        }

        void QuitRace()
        {
            if (RaceDirector.Instance != null && !RaceDirector.Instance.Settled)
                PlatformSignals.GameplayEnded(true);

            PlatformSignals.LoadingStarted();
            Time.timeScale = PlatformHost.IsHostPaused ? 0f : 1f;
            SceneManager.LoadScene(MenuScene);
        }

        void RefreshPlatformButtons()
        {
            if (_rate != null)
            {
                bool rateSupported = RateSupported();
                _rate.gameObject.SetActive(rateSupported);
                _rate.interactable = rateSupported && PlayerCanRate();
            }

            if (_otherGames != null)
                _otherGames.gameObject.SetActive(GamesCatalogSupported());
        }

        static bool RateSupported()
        {
#if UNITY_WEBGL
            return Bridge.instance != null && Bridge.social.isRateSupported;
#else
            return false;
#endif
        }

        static bool PlayerCanRate()
        {
#if UNITY_WEBGL
            if (Application.isEditor)
                return true;

            if (Bridge.instance == null)
                return false;

            if (!Bridge.player.isAuthorizationSupported)
                return true;

            return Bridge.player.isAuthorized;
#else
            return true;
#endif
        }

        static bool GamesCatalogSupported()
        {
#if UNITY_WEBGL
            return Bridge.instance != null
                && string.Equals(Bridge.platform.id, GamesCatalogPlatformId, StringComparison.Ordinal);
#else
            return false;
#endif
        }

        void RateGame()
        {
#if UNITY_WEBGL
            if (!RateSupported() || !PlayerCanRate())
                return;

            Bridge.social.Rate(null);
#endif
        }

        void OpenOtherGames()
        {
#if UNITY_WEBGL
            if (!GamesCatalogSupported())
                return;

            Bridge.crossPromo.Show();
#endif
        }

        static int ResolveLevel(out bool save)
        {
            int low = FindLevel("Low", 1);
            int medium = FindLevel("Medium", 2);
            int high = FindLevel("High", 3);
            int saved = PlayerPrefs.GetInt(QualityKey, -1);
            int source = saved >= 0 ? saved : QualitySettings.GetQualityLevel();
            int snapped = Snap(source, low, medium, high);
            save = snapped != saved;
            return snapped;
        }

        static int Snap(int level)
        {
            return Snap(level, FindLevel("Low", 1), FindLevel("Medium", 2), FindLevel("High", 3));
        }

        static int Snap(int level, int low, int medium, int high)
        {
            if (level <= low)
                return low;
            if (level <= medium)
                return medium;
            return high;
        }

        static int FindLevel(string name, int fallback)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0)
                return 0;

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return Mathf.Clamp(fallback, 0, names.Length - 1);
        }

        Slider FindSlider(string groupName, string sliderName)
        {
            Transform group = FindChild(transform, groupName);
            if (group == null)
                return null;

            Transform slider = group.Find(sliderName);
            return slider != null ? slider.GetComponent<Slider>() : null;
        }

        Toggle FindToggle(string childName)
        {
            Transform child = FindChild(transform, childName);
            return child != null ? child.GetComponent<Toggle>() : null;
        }

        Button FindButton(string childName)
        {
            Transform child = FindChild(transform, childName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
