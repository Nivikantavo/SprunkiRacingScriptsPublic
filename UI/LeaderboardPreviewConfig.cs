using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "LeaderboardPreview", menuName = "Sprunki Racing/Leaderboard Preview")]
    public class LeaderboardPreviewConfig : ScriptableObject
    {
        [SerializeField] private bool enabled = true;

        [SerializeField] private Entry[] entries =
        {
            new Entry("p1", "Nova", 2480, "en", "Sprunki_0", false),
            new Entry("p2", "Rift", 2210, "de", "Sprunki_1", false),
            new Entry("p3", "Kite", 1980, "ja", "Sprunki_2", false),
            new Entry("local", "Player", 1760, "ru", "Sprunki_4", true),
            new Entry("p5", "Bolt", 1540, "fr", "Sprunki_3", false),
            new Entry("p6", "Echo", 1320, "es", "Sprunki_6", false),
            new Entry("p7", "Vex", 980, "ko", "Sprunki_7", false),
            new Entry("p8", "Luma", 640, "pt", "Sprunki_8", false)
        };

        public bool Enabled => enabled;
        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();

        [Serializable]
        public struct Entry
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private int score;
            [SerializeField] private string language;
            [SerializeField] private string characterId;
            [SerializeField] private bool localPlayer;

            public Entry(string id, string displayName, int score, string language, string characterId, bool localPlayer)
            {
                this.id = id;
                this.displayName = displayName;
                this.score = score;
                this.language = language;
                this.characterId = characterId;
                this.localPlayer = localPlayer;
            }

            public string Id => id ?? string.Empty;
            public string DisplayName => string.IsNullOrEmpty(displayName) ? "Player" : displayName;
            public int Score => score;
            public string ScoreText => score.ToString(CultureInfo.InvariantCulture);
            public string Language => language ?? string.Empty;
            public string CharacterId => characterId ?? string.Empty;
            public bool IsLocalPlayer => localPlayer;
        }
    }
}
