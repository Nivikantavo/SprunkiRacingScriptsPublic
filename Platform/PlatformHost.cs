using CarControllerwithShooting;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_WEBGL && !UNITY_EDITOR
using Playgama;
#endif

namespace SprunkiRacing
{
    public static class PlatformHost
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        static bool _listening;
        static bool _hostOwnsGameplayPause;
        static float _savedTimeScale = 1f;
#endif

        public static bool IsHostPaused { get; private set; }

        public static void HoldGameplayPause()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PlatformSignals.GameplayActive)
                _hostOwnsGameplayPause = true;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_WEBGL && !UNITY_EDITOR
            Subscribe();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static void Subscribe()
        {
            if (_listening || Bridge.instance == null)
                return;

            _listening = true;
            Bridge.platform.audioStateChanged += OnAudio;
            Bridge.platform.pauseStateChanged += OnPause;
            GameAudio.SetPlatformMuted(!Bridge.platform.isAudioEnabled);
        }

        static void OnAudio(bool enabled)
        {
            GameAudio.SetPlatformMuted(!enabled);
        }

        static void OnPause(bool paused)
        {
            if (paused)
            {
                if (IsHostPaused)
                    return;

                IsHostPaused = true;
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                GameAudio.SetHostSilenced(true);
                if (CarSystemManager.Instance != null)
                    CarSystemManager.Instance.ReleasePointer();

                bool playerPaused = GameCanvas.Instance != null && GameCanvas.Instance.isPaused;
                if (!playerPaused && PlatformSignals.GameplayActive)
                {
                    PlatformSignals.GameplayPaused();
                    _hostOwnsGameplayPause = true;
                }

                return;
            }

            if (!IsHostPaused)
                return;

            IsHostPaused = false;
            GameAudio.SetHostSilenced(false);

            bool menuOpen = GameCanvas.Instance != null && GameCanvas.Instance.isPaused;
            bool settled = RaceDirector.Instance != null && RaceDirector.Instance.Settled;
            if (_hostOwnsGameplayPause)
            {
                _hostOwnsGameplayPause = false;
                if (!menuOpen && !settled)
                    PlatformSignals.GameplayResumed();
            }

            if (menuOpen)
                return;

            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            if (!settled && CarSystemManager.Instance != null)
                CarSystemManager.Instance.RestorePointer();
        }
#endif

        static void OnSceneLoaded(Scene _, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || !IsHostPaused)
                return;

            Time.timeScale = 0f;
            GameAudio.SetHostSilenced(true);
        }
    }
}
