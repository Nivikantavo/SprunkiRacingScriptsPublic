using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SprunkiRacing.Editor
{
    public static class BotRaceProbeMenu
    {
        [MenuItem("Sprunki/Bot Race/Arm Bots Only")]
        public static void ArmBotsOnly()
        {
            Arm(true, true);
        }

        [MenuItem("Sprunki/Bot Race/Arm With Player")]
        public static void ArmWithPlayer()
        {
            Arm(false, false);
        }

        [MenuItem("Sprunki/Bot Race/Disarm")]
        public static void Disarm()
        {
            BotRaceSession session = LoadOrCreate();
            session.Disarm();
            EditorUtility.SetDirty(session);
            AssetDatabase.SaveAssets();
            Debug.Log("Bot race probe disarmed. The next play uses the normal race.");
        }

        [MenuItem("Sprunki/Bot Race/Audit Path")]
        public static void AuditPath()
        {
            RacePath path = Object.FindAnyObjectByType<RacePath>();
            if (path == null)
            {
                Debug.LogError("Bot race path audit found no RacePath. Open GameplayScene and run the audit again.");
                return;
            }

            List<RacePathPointReport> reports = new List<RacePathPointReport>();
            path.BuildReport(reports);
            StringBuilder builder = new StringBuilder();
            builder.Append("Race path points: ").Append(reports.Count).Append('\n');
            for (int i = 0; i < reports.Count; i++)
            {
                RacePathPointReport report = reports[i];
                if (report.SkippedBefore > 0)
                    builder.Append("GAP  ");

                builder.Append(report.Name);
                builder.Append("  slot ").Append(report.Slot);
                builder.Append("  segment ").Append(report.SegmentLength.ToString("0.0", CultureInfo.InvariantCulture));
                builder.Append("m  turn ").Append(report.TurnDegrees.ToString("0", CultureInfo.InvariantCulture));
                builder.Append(" deg  skipped ").Append(report.SkippedBefore);
                builder.Append("  (").Append(report.Position.x.ToString("0.0", CultureInfo.InvariantCulture));
                builder.Append(", ").Append(report.Position.y.ToString("0.0", CultureInfo.InvariantCulture));
                builder.Append(", ").Append(report.Position.z.ToString("0.0", CultureInfo.InvariantCulture)).Append(")\n");
            }

            string text = builder.ToString();
            Debug.Log(text, path);
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "BotRace");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "path-audit.txt"), text);
        }

        private static void Arm(bool botsOnly, bool driveOnly)
        {
            BotRaceSession session = LoadOrCreate();
            session.Arm(botsOnly, driveOnly);
            EditorUtility.SetDirty(session);
            AssetDatabase.SaveAssets();
            string mode = botsOnly ? "bots only, drive only" : "with player, combat on";
            Debug.Log($"Bot race probe armed ({mode}). Play GameplayScene.");
        }

        private static BotRaceSession LoadOrCreate()
        {
            BotRaceSession session = AssetDatabase.LoadAssetAtPath<BotRaceSession>(BotRaceSession.AssetPath);
            if (session != null)
                return session;

            if (!AssetDatabase.IsValidFolder("Assets/Game/AI/Debug"))
                AssetDatabase.CreateFolder("Assets/Game/AI", "Debug");

            session = ScriptableObject.CreateInstance<BotRaceSession>();
            AssetDatabase.CreateAsset(session, BotRaceSession.AssetPath);
            return session;
        }
    }
}
