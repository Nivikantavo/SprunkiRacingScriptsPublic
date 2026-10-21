using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SprunkiRacing
{
    public static class GameAudio
    {
        public const string SfxKey = "SfxVolume";
        public const string MusicKey = "MusicVolume";
        const string LegacyMuteKey = "Music";

        public static event Action Changed;

        public static float Sfx { get; private set; } = 1f;
        public static float Music { get; private set; } = 1f;

        static bool _platformMuted;
        static bool _hostSilenced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetWorldPaused(false);

            if (!PlayerPrefs.HasKey(SfxKey) && !PlayerPrefs.HasKey(MusicKey) && PlayerPrefs.GetInt(LegacyMuteKey, 1) == 0)
            {
                Sfx = 0f;
                Music = 0f;
                PlayerPrefs.SetFloat(SfxKey, 0f);
                PlayerPrefs.SetFloat(MusicKey, 0f);
                PlayerPrefs.Save();
                return;
            }

            Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
            Music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
        }

        public static void SetSfx(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(Sfx, value))
                return;

            Sfx = value;
            PlayerPrefs.SetFloat(SfxKey, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetMusic(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(Music, value))
                return;

            Music = value;
            PlayerPrefs.SetFloat(MusicKey, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetWorldPaused(bool paused)
        {
            AudioListener.pause = paused;
        }

        public static void SetPlatformMuted(bool muted)
        {
            _platformMuted = muted;
            ApplyOutput();
        }

        public static void SetHostSilenced(bool silenced)
        {
            _hostSilenced = silenced;
            ApplyOutput();
        }

        static void OnSceneLoaded(Scene _, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            SetWorldPaused(false);
            ApplyOutput();
        }

        static void ApplyOutput()
        {
            AudioListener.volume = _platformMuted || _hostSilenced ? 0f : 1f;
        }
    }
}
