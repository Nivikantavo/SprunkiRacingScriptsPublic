using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class LeaderboardPanel : MonoBehaviour
    {
        private const string MissingPlace = "—";
        private const string FallbackName = "Player";
        private const string EmptyScore = "0";

        private static readonly Color FirstPlaceColor = Hex("#EA764E");
        private static readonly Color SecondPlaceColor = Hex("#4C549F");
        private static readonly Color ThirdPlaceColor = Hex("#A63724");
        private static readonly Color OtherPlaceColor = Hex("#1CB0F6");

        [SerializeField] private LeaderboardItemView itemPrefab;
        [SerializeField] private CharacterCatalog catalog;
        [SerializeField] private Sprite firstFrame;
        [SerializeField] private Sprite secondFrame;
        [SerializeField] private Sprite thirdFrame;
        [SerializeField] private Sprite otherFrame;
        [SerializeField] private Sprite goldMedal;
        [SerializeField] private Sprite silverMedal;
        [SerializeField] private Sprite bronzeMedal;
        [SerializeField] private FlagBinding[] flags = Array.Empty<FlagBinding>();
        [SerializeField] private LeaderboardPreviewConfig editorPreview;

        private readonly List<LeaderboardItemView> _rows = new List<LeaderboardItemView>(16);
        private RectTransform _content;
        private TextMeshProUGUI _myPlace;
        private TextMeshProUGUI _myName;
        private TextMeshProUGUI _myScore;
        private Image _myCharacter;
        private Image _myCountry;
        private int _loadVersion;
        private int _playerRank;
        private string _playerScore = EmptyScore;
        private bool _editorPreviewActive;
        private string _previewName;
        private string _previewLanguage;
        private Sprite _previewPortrait;

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            if (CharacterProgress.Instance != null)
                CharacterProgress.Instance.Changed += HandleCharacterChanged;

            if (TrophyProgress.Instance != null)
                TrophyProgress.Instance.Changed += HandleTrophiesChanged;

            LeaderboardBoard.AvailabilityChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            _loadVersion++;
            if (CharacterProgress.Instance != null)
                CharacterProgress.Instance.Changed -= HandleCharacterChanged;

            if (TrophyProgress.Instance != null)
                TrophyProgress.Instance.Changed -= HandleTrophiesChanged;

            LeaderboardBoard.AvailabilityChanged -= Refresh;
        }

        private void Refresh()
        {
            Resolve();
            if (itemPrefab == null || _content == null)
                Debug.LogError("Leaderboard panel is missing the row prefab or list content.", this);

            _playerRank = 0;
            _playerScore = EmptyScore;
            _editorPreviewActive = false;
            _previewName = null;
            _previewLanguage = null;
            _previewPortrait = null;
            BindLocal();
            ClearRows();

            if (Application.isEditor && Application.isPlaying && editorPreview != null && editorPreview.Enabled)
            {
                ApplyEditorPreview();
                return;
            }

            int version = ++_loadVersion;
            LeaderboardBoard.Load((success, rows) =>
            {
                if (version != _loadVersion || !isActiveAndEnabled)
                    return;

                if (success)
                    Apply(rows);
            });
        }

        private void HandleCharacterChanged()
        {
            BindLocal();
        }

        private void HandleTrophiesChanged(int trophies)
        {
            Refresh();
        }

        private void ApplyEditorPreview()
        {
            _editorPreviewActive = true;
            IReadOnlyList<LeaderboardPreviewConfig.Entry> entries = editorPreview.Entries;
            var rows = new List<PreviewRow>(entries.Count);
            bool hasLocal = false;
            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardPreviewConfig.Entry entry = entries[i];
                bool local = entry.IsLocalPlayer;
                if (local)
                    hasLocal = true;

                rows.Add(new PreviewRow(
                    i,
                    local ? LiveScore(entry.Score) : entry.Score,
                    entry.DisplayName,
                    entry.Language,
                    entry.CharacterId,
                    local));
            }

            if (!hasLocal)
            {
                rows.Add(new PreviewRow(
                    entries.Count,
                    LiveScore(0),
                    FallbackName,
                    string.Empty,
                    string.Empty,
                    true));
            }

            rows.Sort((a, b) =>
            {
                int byScore = b.Score.CompareTo(a.Score);
                if (byScore != 0)
                    return byScore;

                return a.Order.CompareTo(b.Order);
            });

            for (int i = 0; i < rows.Count; i++)
            {
                PreviewRow row = rows[i];
                int rank = i + 1;
                if (row.Local && _playerRank <= 0)
                {
                    _playerRank = rank;
                    _playerScore = row.Score.ToString(CultureInfo.InvariantCulture);
                    _previewName = row.Name;
                    _previewLanguage = row.Language;
                    _previewPortrait = PortraitFor(row.CharacterId);
                }

                if (itemPrefab == null || _content == null)
                    continue;

                LeaderboardItemView view = Instantiate(itemPrefab, _content);
                StyleFor(rank, out Sprite frame, out Sprite medal, out Color color);
                view.Bind(
                    rank,
                    row.Name,
                    row.Score.ToString(CultureInfo.InvariantCulture),
                    frame,
                    medal,
                    color,
                    PortraitFor(row.CharacterId),
                    FlagFor(row.Language));
                _rows.Add(view);
            }

            BindLocal();
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private static int LiveScore(int fallback)
        {
            TrophyProgress progress = TrophyProgress.Instance;
            if (progress != null && progress.IsReady)
                return progress.Trophies;

            return fallback;
        }

        private void Apply(IReadOnlyList<LeaderboardRow> rows)
        {
            ClearRows();
            _playerRank = 0;
            _playerScore = EmptyScore;

            if (rows != null && itemPrefab != null && _content != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    LeaderboardRow row = rows[i];
                    if (_playerRank <= 0 && LeaderboardBoard.BelongsToLocalPlayer(row.Id))
                    {
                        _playerRank = row.Rank;
                        _playerScore = string.IsNullOrEmpty(row.Score) ? EmptyScore : row.Score;
                    }

                    LeaderboardItemView view = Instantiate(itemPrefab, _content);
                    StyleFor(row.Rank, out Sprite frame, out Sprite medal, out Color color);
                    view.Bind(row.Rank, row.Name, row.Score, frame, medal, color);
                    _rows.Add(view);
                }
            }

            BindLocal();
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void BindLocal()
        {
            string playerName;
            string language;
            if (_editorPreviewActive)
            {
                playerName = _previewName;
                language = _previewLanguage;
            }
            else
            {
                LeaderboardBoard.ReadLocalPlayer(out _, out playerName, out language);
            }

            if (_myPlace != null)
            {
                _myPlace.text = _playerRank > 0 ? _playerRank.ToString() : MissingPlace;
                _myPlace.color = PlaceColor(_playerRank);
            }

            if (_myName != null)
                _myName.text = string.IsNullOrEmpty(playerName) ? FallbackName : playerName;

            if (_myScore != null)
                _myScore.text = string.IsNullOrEmpty(_playerScore) ? EmptyScore : _playerScore;

            Sprite portrait = _editorPreviewActive && _previewPortrait != null ? _previewPortrait : SelectedPortrait();
            if (_myCharacter != null)
            {
                _myCharacter.enabled = portrait != null;
                if (portrait != null)
                {
                    _myCharacter.sprite = portrait;
                    _myCharacter.preserveAspect = true;
                }
            }

            Sprite flag = FlagFor(language);
            if (_myCountry != null)
            {
                _myCountry.enabled = flag != null;
                if (flag != null)
                {
                    _myCountry.sprite = flag;
                    _myCountry.preserveAspect = true;
                }
            }
        }

        private Sprite PortraitFor(string characterId)
        {
            if (catalog == null || string.IsNullOrEmpty(characterId))
                return null;

            CharacterConfig config = catalog.Find(characterId);
            return config != null ? config.Preview : null;
        }

        private Sprite SelectedPortrait()
        {
            CharacterProgress progress = CharacterProgress.Instance;
            if (catalog == null || progress == null || string.IsNullOrEmpty(progress.SelectedId))
                return null;

            CharacterConfig config = catalog.Find(progress.SelectedId);
            return config != null ? config.Preview : null;
        }

        private Sprite FlagFor(string language)
        {
            if (string.IsNullOrEmpty(language) || flags == null)
                return null;

            string code = language.Trim().ToLowerInvariant().Replace('_', '-');
            Sprite exact = FindFlag(code);
            if (exact != null)
                return exact;

            int dash = code.IndexOf('-');
            if (dash <= 0)
                return null;

            return FindFlag(code.Substring(0, dash));
        }

        private Sprite FindFlag(string language)
        {
            for (int i = 0; i < flags.Length; i++)
            {
                FlagBinding binding = flags[i];
                if (binding.sprite != null && string.Equals(binding.language, language, StringComparison.OrdinalIgnoreCase))
                    return binding.sprite;
            }

            return null;
        }

        private void StyleFor(int rank, out Sprite frame, out Sprite medal, out Color color)
        {
            switch (rank)
            {
                case 1:
                    frame = firstFrame;
                    medal = goldMedal;
                    color = FirstPlaceColor;
                    break;
                case 2:
                    frame = secondFrame;
                    medal = silverMedal;
                    color = SecondPlaceColor;
                    break;
                case 3:
                    frame = thirdFrame;
                    medal = bronzeMedal;
                    color = ThirdPlaceColor;
                    break;
                default:
                    frame = otherFrame;
                    medal = null;
                    color = OtherPlaceColor;
                    break;
            }
        }

        private static Color PlaceColor(int rank)
        {
            switch (rank)
            {
                case 1:
                    return FirstPlaceColor;
                case 2:
                    return SecondPlaceColor;
                case 3:
                    return ThirdPlaceColor;
                default:
                    return OtherPlaceColor;
            }
        }

        private void ClearRows()
        {
            _rows.Clear();
            if (_content == null)
                return;

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Transform child = _content.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void Resolve()
        {
            if (_content == null)
            {
                ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
                if (scroll != null && scroll.content != null)
                    _content = scroll.content;
            }

            Transform mine = FindChild(transform, "List_Me");
            if (mine == null)
                return;

            if (_myPlace == null)
                _myPlace = FindText(mine, "Text_Ranking_Num");

            if (_myName == null)
                _myName = FindText(mine, "Text_UserName");

            if (_myScore == null)
                _myScore = FindText(mine, "Text_Score_Value");

            if (_myCharacter == null)
                _myCharacter = FindImage(mine, "Character");

            if (_myCountry == null)
                _myCountry = FindImage(mine, "Icon_Country");
        }

        private static Image FindImage(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static TextMeshProUGUI FindText(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Transform FindChild(Transform root, string childName)
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

        private static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color color);
            return color;
        }

        private readonly struct PreviewRow
        {
            public PreviewRow(int order, int score, string name, string language, string characterId, bool local)
            {
                Order = order;
                Score = score;
                Name = name;
                Language = language;
                CharacterId = characterId;
                Local = local;
            }

            public int Order { get; }
            public int Score { get; }
            public string Name { get; }
            public string Language { get; }
            public string CharacterId { get; }
            public bool Local { get; }
        }

        [Serializable]
        private struct FlagBinding
        {
            public string language;
            public Sprite sprite;
        }
    }
}
