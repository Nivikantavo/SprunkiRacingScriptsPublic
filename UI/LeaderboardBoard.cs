using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Leaderboards;
#endif

namespace SprunkiRacing
{
    public readonly struct LeaderboardRow
    {
        public LeaderboardRow(string id, string name, string score, int rank)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Score = score ?? string.Empty;
            Rank = rank;
        }

        public string Id { get; }
        public string Name { get; }
        public string Score { get; }
        public int Rank { get; }
    }

    public static class LeaderboardBoard
    {
        public const string Id = "trophies";
        public const string LocalId = "local";

        private static Action _availabilityChanged;

        public static bool CanUse
        {
            get
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
        }

        public static event Action AvailabilityChanged
        {
            add => _availabilityChanged += value;
            remove => _availabilityChanged -= value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _availabilityChanged = null;
        }

        public static void NotifyAvailabilityChanged()
        {
            _availabilityChanged?.Invoke();
        }

        public static void Load(Action<bool, IReadOnlyList<LeaderboardRow>> onCompleted)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(true, Array.Empty<LeaderboardRow>());
                return;
            }

#if UNITY_WEBGL
            if (Bridge.leaderboards.type == LeaderboardType.Native || Bridge.leaderboards.type == LeaderboardType.NativePopup)
            {
                onCompleted?.Invoke(true, Array.Empty<LeaderboardRow>());
                return;
            }

            if (Bridge.leaderboards.type != LeaderboardType.InGame)
            {
                onCompleted?.Invoke(true, WithLocalPlayer(new List<LeaderboardRow>()));
                return;
            }

            Bridge.leaderboards.GetEntries(Id, (success, entries) =>
            {
                if (!success || entries == null)
                {
                    Debug.LogWarning("Leaderboard entries could not be loaded.");
                    onCompleted?.Invoke(true, WithLocalPlayer(new List<LeaderboardRow>()));
                    return;
                }

                onCompleted?.Invoke(true, WithLocalPlayer(Parse(entries)));
            });
#else
            onCompleted?.Invoke(true, WithLocalPlayer(new List<LeaderboardRow>()));
#endif
        }

        public static void Submit(int score, Action<bool> onCompleted)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(true);
                return;
            }

#if UNITY_WEBGL
            if (Bridge.leaderboards.type == LeaderboardType.NotAvailable)
            {
                onCompleted?.Invoke(true);
                return;
            }

            Bridge.leaderboards.SetScore(Id, score, success =>
            {
                if (!success)
                    Debug.LogWarning("Player trophies could not be submitted to the leaderboard.");

                onCompleted?.Invoke(success);
            });
#else
            onCompleted?.Invoke(true);
#endif
        }

        public static bool BelongsToLocalPlayer(string rowId)
        {
            if (string.IsNullOrEmpty(rowId))
                return false;

            ReadLocalPlayer(out string id, out _, out _);
            if (!string.IsNullOrEmpty(id))
                return rowId == id;

            return rowId == LocalId;
        }

        public static void ReadLocalPlayer(out string id, out string name, out string language)
        {
#if UNITY_WEBGL
            id = Bridge.player.id;
            name = Bridge.player.name;
            language = Bridge.platform.language;
#else
            id = null;
            name = null;
            language = null;
#endif
        }

        private static bool TryLocalScore(out int score)
        {
            TrophyProgress progress = TrophyProgress.Instance;
            if (progress != null && progress.IsReady)
            {
                score = progress.Trophies;
                return true;
            }

#if !UNITY_WEBGL
            string stored = PlayerPrefs.GetString(TrophyProgress.StorageKey, null);
            if (int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out score))
            {
                score = Mathf.Max(0, score);
                return true;
            }
#endif

            score = 0;
            return false;
        }

        private static List<LeaderboardRow> WithLocalPlayer(List<LeaderboardRow> rows)
        {
            if (rows == null)
                rows = new List<LeaderboardRow>();

            if (!TryLocalScore(out int score))
                return rows;

            ReadLocalPlayer(out string id, out string name, out _);
            if (string.IsNullOrEmpty(id))
                id = LocalId;

            string scoreText = score.ToString(CultureInfo.InvariantCulture);
            bool found = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Id != id)
                    continue;

                found = true;
                if (rows[i].Score == scoreText)
                    return rows;

                string shownName = string.IsNullOrEmpty(rows[i].Name) ? name : rows[i].Name;
                if (string.IsNullOrEmpty(shownName))
                    shownName = "Player";

                rows[i] = new LeaderboardRow(rows[i].Id, shownName, scoreText, rows[i].Rank);
                break;
            }

            if (!found)
            {
                if (string.IsNullOrEmpty(name))
                    name = "Player";

                rows.Add(new LeaderboardRow(id, name, scoreText, int.MaxValue));
            }

            rows.Sort((a, b) =>
            {
                int byScore = ParseScore(b.Score).CompareTo(ParseScore(a.Score));
                if (byScore != 0)
                    return byScore;

                return a.Rank.CompareTo(b.Rank);
            });

            for (int i = 0; i < rows.Count; i++)
            {
                LeaderboardRow row = rows[i];
                rows[i] = new LeaderboardRow(row.Id, row.Name, row.Score, i + 1);
            }

            return rows;
        }

        private static int ParseScore(string score)
        {
            if (int.TryParse(score, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value;

            return 0;
        }

        private static List<LeaderboardRow> Parse(List<Dictionary<string, string>> entries)
        {
            var rows = new List<LeaderboardRow>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                Dictionary<string, string> entry = entries[i];
                int rank = 0;
                if (!int.TryParse(Read(entry, "rank"), NumberStyles.Integer, CultureInfo.InvariantCulture, out rank) || rank <= 0)
                    rank = i + 1;

                rows.Add(new LeaderboardRow(Read(entry, "id"), Read(entry, "name"), Read(entry, "score"), rank));
            }

            rows.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            return rows;
        }

        private static string Read(Dictionary<string, string> entry, string key)
        {
            if (entry != null && entry.TryGetValue(key, out string value) && value != null)
                return value;

            return string.Empty;
        }
    }
}
