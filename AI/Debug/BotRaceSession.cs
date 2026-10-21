using UnityEngine;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "BotRaceSession", menuName = "Sprunki Racing/Bot Race Session")]
    public class BotRaceSession : ScriptableObject
    {
        public const string AssetPath = "Assets/Game/AI/Debug/BotRaceSession.asset";

        [SerializeField] private bool armed;
        [SerializeField] private bool botsOnly = true;
        [SerializeField] private bool driveOnly = true;
        [SerializeField] private float timeScale = 3f;
        [SerializeField] private int laps = 2;
        [SerializeField] private float maxRealSeconds = 120f;
        [SerializeField] private bool exitPlayModeWhenDone = true;

        public bool Armed => armed;
        public bool BotsOnly => botsOnly;
        public bool DriveOnly => driveOnly;
        public float TimeScale => Mathf.Clamp(timeScale, 0.1f, 8f);
        public int Laps => Mathf.Max(1, laps);
        public float MaxRealSeconds => Mathf.Max(5f, maxRealSeconds);
        public bool ExitPlayModeWhenDone => exitPlayModeWhenDone;

        public static BotRaceSession Active
        {
            get
            {
                BotRaceSession session = Load();
                return session != null && session.armed ? session : null;
            }
        }

        public static BotRaceSession Load()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<BotRaceSession>(AssetPath);
#else
            return null;
#endif
        }

        public void Arm(bool onlyBots, bool onlyDrive)
        {
            armed = true;
            botsOnly = onlyBots;
            driveOnly = onlyDrive;
        }

        public void Disarm()
        {
            armed = false;
        }
    }
}
