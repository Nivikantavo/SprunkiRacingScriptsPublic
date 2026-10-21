using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#if UNITY_WEBGL
using Playgama;
#endif

namespace SprunkiRacing
{
    public class GameLanguage : MonoBehaviour
    {
        public const string StorageKey = "game_language";
        public const string FallbackCode = "en";
        private const string ChosenKey = "game_language_chosen";
        private const string ChosenMarker = "1";

        private static bool _chosen;
        private static string _pending;
        private int _applyId;

        public static GameLanguage Instance { get; private set; }

        public static string Code { get; private set; } = FallbackCode;

        public static bool IsReady { get; private set; }

        public static string StartupCode
        {
            get
            {
                if (IsReady && !string.IsNullOrEmpty(Code))
                    return Code;

                if (!string.IsNullOrEmpty(_pending))
                    return _pending;

                if (HasLocalChoice)
                {
                    string cached = PlayerPrefs.GetString(StorageKey, string.Empty);
                    if (!string.IsNullOrEmpty(cached))
                        return cached;
                }

                return PlatformCode();
            }
        }

        private static bool HasLocalChoice =>
            PlayerPrefs.GetString(ChosenKey, string.Empty) == ChosenMarker;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Code = FallbackCode;
            IsReady = false;
            _chosen = false;
            _pending = null;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var languageObject = new GameObject(nameof(GameLanguage));
            languageObject.AddComponent<GameLanguage>();
        }

        public static Locale Resolve(ILocalesProvider locales, string code)
        {
            Locale match = Match(locales, code);
            if (match != null)
                return match;

            if (locales == null)
                return null;

            return locales.GetLocale(new LocaleIdentifier(FallbackCode));
        }

        public static void Select(string code)
        {
            if (Instance == null)
                return;

            _chosen = true;
            Instance.Apply(code, true);
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
            EnsureSelector();
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private static void EnsureSelector()
        {
            List<IStartupLocaleSelector> selectors = LocalizationSettings.StartupLocaleSelectors;
            for (int i = 0; i < selectors.Count; i++)
            {
                if (selectors[i] is PreferredLocaleSelector)
                    return;
            }

            int index = selectors.Count > 0 ? 1 : 0;
            selectors.Insert(index, new PreferredLocaleSelector());
        }

        private void Load()
        {
#if UNITY_WEBGL
            Bridge.storage.Get(new List<string> { ChosenKey, StorageKey }, OnLoaded);
#else
            OnLoaded(true, new List<string>
            {
                HasLocalChoice ? ChosenMarker : null,
                HasLocalChoice ? PlayerPrefs.GetString(StorageKey, null) : null
            });
#endif
        }

        private void OnLoaded(bool success, List<string> data)
        {
            if (_chosen)
                return;

            if (success && data != null && data.Count >= 2 && IsChosen(data[0]) && !string.IsNullOrEmpty(data[1]))
            {
                Apply(data[1], false);
                return;
            }

            if (!success)
            {
                Debug.LogWarning("Game language could not be loaded from storage.", this);
                if (HasLocalChoice)
                {
                    string cached = PlayerPrefs.GetString(StorageKey, string.Empty);
                    if (!string.IsNullOrEmpty(cached))
                    {
                        Apply(cached, false);
                        return;
                    }
                }
            }

            Apply(PlatformCode(), false);
        }

        private void Apply(string preferred, bool persist)
        {
            int id = ++_applyId;
            _pending = preferred;
            EnsureSelector();

            var operation = LocalizationSettings.InitializationOperation;
            if (operation.IsDone)
            {
                if (id == _applyId)
                    FinishApply(preferred, persist);
                return;
            }

            operation.Completed += _ =>
            {
                if (id != _applyId)
                    return;

                FinishApply(preferred, persist);
            };
        }

        private void FinishApply(string preferred, bool persist)
        {
            Locale locale = Resolve(LocalizationSettings.AvailableLocales, preferred);
            if (locale == null)
            {
                Debug.LogError("English locale is missing from Localization Settings.", this);
                return;
            }

            string code = locale.Identifier.Code;
            Code = code;
            IsReady = true;
            _pending = code;

            if (LocalizationSettings.SelectedLocale != locale)
                LocalizationSettings.SelectedLocale = locale;

            if (persist)
                Persist(code);

            Changed?.Invoke();
        }

        private void Persist(string code)
        {
            PlayerPrefs.SetString(StorageKey, code);
            PlayerPrefs.SetString(ChosenKey, ChosenMarker);
            PlayerPrefs.Save();

#if UNITY_WEBGL
            Bridge.storage.Set(StorageKey, code, OnSaved);
            Bridge.storage.Set(ChosenKey, ChosenMarker, OnSaved);
#endif
        }

        private static bool IsChosen(string value)
        {
            return string.Equals(value, ChosenMarker, StringComparison.Ordinal);
        }

        private void OnSaved(bool success)
        {
            if (!success)
                Debug.LogWarning("Game language could not be saved.", this);
        }

        private static Locale Match(ILocalesProvider locales, string code)
        {
            if (locales == null || string.IsNullOrWhiteSpace(code))
                return null;

            string trimmed = code.Trim().Replace('_', '-');
            Locale locale = locales.GetLocale(new LocaleIdentifier(trimmed));
            if (locale != null)
                return locale;

            int dash = trimmed.IndexOf('-');
            if (dash <= 0)
                return null;

            return locales.GetLocale(new LocaleIdentifier(trimmed.Substring(0, dash)));
        }

        private static string PlatformCode()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Bridge.instance != null)
                return Bridge.platform.language;

            return null;
#else
            return SystemCode(Application.systemLanguage);
#endif
        }

        private static string SystemCode(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.English:
                    return "en";
                case SystemLanguage.Russian:
                    return "ru";
                case SystemLanguage.Spanish:
                    return "es";
                case SystemLanguage.Turkish:
                    return "tr";
                case SystemLanguage.Indonesian:
                    return "id";
                case SystemLanguage.French:
                    return "fr";
                case SystemLanguage.German:
                    return "de";
                case SystemLanguage.Italian:
                    return "it";
                case SystemLanguage.Portuguese:
                    return "pt";
                default:
                    return null;
            }
        }
    }
}
