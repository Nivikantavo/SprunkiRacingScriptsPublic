using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class CharacterSelectPanel : MonoBehaviour
    {
        public enum PreviewVehicle
        {
            Rally,
            MonsterTruck
        }

        [SerializeField] private CharacterCatalog catalog;
        [SerializeField] private CharacterCardView cardPrefab;
        [SerializeField] private RectTransform cardContent;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private CharacterCarPreview carPreview;
        [SerializeField] private PreviewVehicle previewVehicle = PreviewVehicle.Rally;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button buyButton;

        private readonly List<CharacterCardView> _cards = new List<CharacterCardView>();
        private readonly LocalizedTextBinding _nameText = new LocalizedTextBinding();
        private CharacterCardView _selectedCard;
        private bool _appliedSavedSelection;

        private void Awake()
        {
            ResolveButtons();
            if (selectButton != null)
                selectButton.onClick.AddListener(HandleSelect);
            if (buyButton != null)
                buyButton.onClick.AddListener(HandleBuy);

            ClearContent();
            SpawnCards();
        }

        private void OnEnable()
        {
            if (Player.Instance != null)
                Player.Instance.BalanceChanged += HandleBalanceChanged;

            bool progressReady = CharacterProgress.Instance != null && CharacterProgress.Instance.IsReady;
            if (CharacterProgress.Instance != null)
                CharacterProgress.Instance.Changed += HandleProgressChanged;

            if (!progressReady)
                SelectInitial();
        }

        private void OnDisable()
        {
            if (Player.Instance != null)
                Player.Instance.BalanceChanged -= HandleBalanceChanged;

            if (CharacterProgress.Instance != null)
                CharacterProgress.Instance.Changed -= HandleProgressChanged;
        }

        private void OnDestroy()
        {
            _nameText.Clear();
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleSelect);
            if (buyButton != null)
                buyButton.onClick.RemoveListener(HandleBuy);
        }

        private void ResolveButtons()
        {
            if (selectButton == null)
                selectButton = transform.Find("Button_Select")?.GetComponent<Button>();

            if (buyButton == null)
                buyButton = transform.Find("Button_Buy")?.GetComponent<Button>();
        }

        private void ClearContent()
        {
            if (cardContent == null)
                return;

            for (int i = cardContent.childCount - 1; i >= 0; i--)
                DestroyImmediate(cardContent.GetChild(i).gameObject);
        }

        private void SpawnCards()
        {
            _cards.Clear();

            if (catalog == null || cardPrefab == null || cardContent == null)
            {
                Debug.LogError("CharacterSelectPanel is missing catalog, card prefab, or content.", this);
                return;
            }

            for (int i = 0; i < catalog.Characters.Count; i++)
            {
                CharacterConfig config = catalog.Characters[i];
                if (config == null)
                    continue;

                CharacterCardView card = Instantiate(cardPrefab, cardContent);
                card.Bind(config, SelectCard);
                card.SetSelected(false);
                ApplyOffer(card);
                _cards.Add(card);
            }
        }

        private void SelectInitial()
        {
            CharacterCardView card = FindFirstUnlocked() ?? (_cards.Count > 0 ? _cards[0] : null);
            if (card != null)
                SelectCard(card);
            else
                RefreshActions();
        }

        private void HandleProgressChanged()
        {
            RefreshOffers();

            if (!_appliedSavedSelection)
            {
                _appliedSavedSelection = true;
                CharacterCardView saved = FindSavedUnlockedCard();
                if (saved != null)
                {
                    if (saved != _selectedCard)
                        SelectCard(saved);
                    else
                        RefreshActions();
                    return;
                }
            }

            if (_selectedCard == null)
                SelectInitial();
            else
                RefreshActions();
        }

        private void HandleBalanceChanged(int balance)
        {
            RefreshActions();
        }

        private void HandleSelect()
        {
            CharacterConfig config = _selectedCard != null ? _selectedCard.Config : null;
            if (config == null)
                return;

            CharacterProgress progress = CharacterProgress.Instance;
            if (progress == null || !progress.TrySelect(config))
                return;

            PlatformSignals.LoadingStarted();
            SceneManager.LoadScene("GameplayScene");
        }

        private void HandleBuy()
        {
            CharacterConfig config = _selectedCard != null ? _selectedCard.Config : null;
            CharacterProgress progress = CharacterProgress.Instance;
            if (config == null || progress == null || !progress.TryPurchase(config))
                return;

            ApplyOffer(_selectedCard);
            RefreshActions();
        }

        private void SelectCard(CharacterCardView card)
        {
            if (card == null || card == _selectedCard)
                return;

            if (_selectedCard != null)
                _selectedCard.SetSelected(false);

            _selectedCard = card;
            _selectedCard.SetSelected(true);

            CharacterConfig config = card.Config;
            _nameText.Set(config != null ? config.DisplayName : null, ApplyName);

            if (carPreview != null)
                carPreview.Show(GetPreviewPrefab(config));

            RefreshActions();
        }

        private void RefreshOffers()
        {
            for (int i = 0; i < _cards.Count; i++)
                ApplyOffer(_cards[i]);
        }

        private void ApplyOffer(CharacterCardView card)
        {
            if (card == null)
                return;

            CharacterConfig config = card.Config;
            bool unlocked = config != null && IsUnlocked(config);
            card.SetOffer(unlocked, config != null ? config.Price : 0);
        }

        private void RefreshActions()
        {
            CharacterConfig config = _selectedCard != null ? _selectedCard.Config : null;
            bool unlocked = config != null && IsUnlocked(config);
            CharacterProgress progress = CharacterProgress.Instance;

            if (selectButton != null)
                selectButton.interactable = unlocked && progress != null && progress.IsReady;

            if (buyButton != null)
                buyButton.interactable = !unlocked && CanAfford(config);
        }

        private static bool IsUnlocked(CharacterConfig config)
        {
            CharacterProgress progress = CharacterProgress.Instance;
            if (progress == null)
                return config.UnlockedByDefault;

            return progress.IsUnlocked(config);
        }

        private bool CanAfford(CharacterConfig config)
        {
            if (config == null || config.Price <= 0)
                return false;

            CharacterProgress progress = CharacterProgress.Instance;
            if (progress == null || !progress.IsReady || progress.IsUnlocked(config))
                return false;

            Player player = Player.Instance;
            return player != null && player.IsReady && player.Balance >= config.Price;
        }

        private CharacterCardView FindSavedUnlockedCard()
        {
            CharacterProgress progress = CharacterProgress.Instance;
            if (progress == null || string.IsNullOrEmpty(progress.SelectedId))
                return null;

            for (int i = 0; i < _cards.Count; i++)
            {
                CharacterCardView card = _cards[i];
                if (card.Config != null && card.Config.Id == progress.SelectedId && progress.IsUnlocked(card.Config))
                    return card;
            }

            return null;
        }

        private CharacterCardView FindFirstUnlocked()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Config != null && IsUnlocked(_cards[i].Config))
                    return _cards[i];
            }

            return null;
        }

        private GameObject GetPreviewPrefab(CharacterConfig config)
        {
            if (config == null)
                return null;

            return previewVehicle == PreviewVehicle.MonsterTruck ? config.MonsterTruck : config.RallyCar;
        }

        private void ApplyName(string value)
        {
            if (nameLabel != null)
                nameLabel.text = value ?? string.Empty;
        }
    }
}
