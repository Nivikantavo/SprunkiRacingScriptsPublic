#if UNITY_WEBGL && !UNITY_EDITOR
using Playgama;
using Playgama.Modules.Platform;
#endif

namespace SprunkiRacing
{
    public static class PlatformSignals
    {
        static bool _ready;
        static bool _loading;
        static bool _gameplay;
        static bool _paused;

        public static bool GameplayActive => _gameplay;

        public static void GameReady()
        {
            if (_ready)
                return;

            _ready = true;
            Send(Message.GameReady);
        }

        public static void LoadingStarted()
        {
            if (_loading)
                return;

            _loading = true;
            Send(Message.LoadingStarted);
        }

        public static void LoadingStopped()
        {
            if (!_loading)
                return;

            _loading = false;
            Send(Message.LoadingStopped);
        }

        public static void GameplayStarted()
        {
            if (_gameplay)
                return;

            _gameplay = true;
            _paused = false;
            Send(Message.LevelStarted);
        }

        public static void GameplayPaused()
        {
            if (!_gameplay || _paused)
                return;

            _paused = true;
            Send(Message.LevelPaused);
        }

        public static void GameplayResumed()
        {
            if (!_gameplay || !_paused)
                return;

            _paused = false;
            Send(Message.LevelResumed);
        }

        public static void GameplayEnded(bool failed)
        {
            if (!_gameplay)
                return;

            _gameplay = false;
            _paused = false;
            Send(failed ? Message.LevelFailed : Message.LevelCompleted);
        }

        enum Message
        {
            GameReady,
            LoadingStarted,
            LoadingStopped,
            LevelStarted,
            LevelPaused,
            LevelResumed,
            LevelCompleted,
            LevelFailed
        }

        static void Send(Message message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Bridge.instance == null)
                return;

            switch (message)
            {
                case Message.GameReady:
                    Bridge.platform.SendMessage(PlatformMessage.GameReady);
                    break;
                case Message.LoadingStarted:
                    Bridge.platform.SendMessage(PlatformMessage.InGameLoadingStarted);
                    break;
                case Message.LoadingStopped:
                    Bridge.platform.SendMessage(PlatformMessage.InGameLoadingStopped);
                    break;
                case Message.LevelStarted:
                    Bridge.platform.SendMessage(PlatformMessage.LevelStarted);
                    break;
                case Message.LevelPaused:
                    Bridge.platform.SendMessage(PlatformMessage.LevelPaused);
                    break;
                case Message.LevelResumed:
                    Bridge.platform.SendMessage(PlatformMessage.LevelResumed);
                    break;
                case Message.LevelCompleted:
                    Bridge.platform.SendMessage(PlatformMessage.LevelCompleted);
                    break;
                case Message.LevelFailed:
                    Bridge.platform.SendMessage(PlatformMessage.LevelFailed);
                    break;
            }
#endif
        }
    }
}
