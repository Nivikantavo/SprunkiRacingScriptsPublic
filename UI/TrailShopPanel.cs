using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public class TrailShopPanel : MonoBehaviour
    {
        [SerializeField] private TrailCatalog catalog;
        [SerializeField] private TrailCardView cardPrefab;
        [SerializeField] private RectTransform cardContent;

        private readonly List<TrailCardView> _cards = new List<TrailCardView>();
        private bool _built;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureBuilt();

            if (Player.Instance != null)
                Player.Instance.BalanceChanged += HandleBalanceChanged;

            if (TrailProgress.Instance != null)
                TrailProgress.Instance.Changed += HandleProgressChanged;

            Refresh();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (Player.Instance != null)
                Player.Instance.BalanceChanged -= HandleBalanceChanged;

            if (TrailProgress.Instance != null)
                TrailProgress.Instance.Changed -= HandleProgressChanged;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            SpawnCards();
        }

        private void SpawnCards()
        {
            _cards.Clear();
            if (catalog == null || cardPrefab == null || cardContent == null)
            {
                Debug.LogError("TrailShopPanel is missing catalog, card prefab, or content.", this);
                return;
            }

            for (int i = 0; i < catalog.Trails.Count; i++)
            {
                TrailConfig config = catalog.Trails[i];
                if (config == null)
                    continue;

                TrailCardView card = Instantiate(cardPrefab, cardContent);
                card.Bind(config, HandleCard);
                _cards.Add(card);
            }
        }

        private void HandleBalanceChanged(int balance)
        {
            Refresh();
        }

        private void HandleProgressChanged()
        {
            Refresh();
        }

        private void HandleCard(TrailCardView card)
        {
            TrailConfig config = card != null ? card.Config : null;
            TrailProgress progress = TrailProgress.Instance;
            if (config == null || progress == null || !progress.IsReady)
                return;

            if (!progress.IsUnlocked(config))
            {
                progress.TryPurchase(config);
                return;
            }

            progress.TrySelect(config);
        }

        private void Refresh()
        {
            TrailProgress progress = TrailProgress.Instance;
            TrailConfig equipped = catalog != null ? catalog.Equipped(progress) : null;
            string equippedId = equipped != null ? equipped.Id : null;

            for (int i = 0; i < _cards.Count; i++)
            {
                TrailCardView card = _cards[i];
                TrailConfig config = card.Config;
                if (config == null)
                    continue;

                bool unlocked = progress != null ? progress.IsUnlocked(config) : config.UnlockedByDefault;
                bool ready = progress != null && progress.IsReady;
                card.SetOffer(unlocked, config.Price);
                card.SetEquipped(unlocked && config.Id == equippedId);
                card.SetInteractable(ready && (unlocked || CanAfford(config)));
            }
        }

        private static bool CanAfford(TrailConfig config)
        {
            if (config == null || config.Price <= 0)
                return false;

            Player player = Player.Instance;
            return player != null && player.IsReady && player.Balance >= config.Price;
        }
    }
}
