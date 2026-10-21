using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Networking;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
#endif

namespace SprunkiRacing
{
    public class ShopPanel : MonoBehaviour
    {
        [SerializeField] private CarStatsConfig config;
        [SerializeField] private CarUpgradeItemView itemPrefab;
        [SerializeField] private CarSectionView sectionPrefab;
        [SerializeField] private RectTransform content;
        [SerializeField] private TextMeshProUGUI balanceLabel;
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private CurrencyShopConfig currencyConfig;
        [SerializeField] private CurrencyShopItemView currencyItemPrefab;
        [SerializeField] private RectTransform coinContent;

        private struct CatalogOffer
        {
            public string Text;
            public string ImageUrl;
        }

        private readonly List<CarUpgradeItemView> _items = new List<CarUpgradeItemView>();
        private readonly List<CurrencyShopItemView> _currencyItems = new List<CurrencyShopItemView>();
        private readonly Dictionary<string, CatalogOffer> _catalog = new Dictionary<string, CatalogOffer>();
        private readonly Dictionary<string, Sprite> _currencyIcons = new Dictionary<string, Sprite>();
        private readonly HashSet<string> _iconRequests = new HashSet<string>();
        private readonly Queue<string> _consumeQueue = new Queue<string>();
        private bool _built;
        private bool _catalogReady;
        private bool _paymentsBusy;
        private bool _rewardInFlight;
        private bool _rewardGranted;
        private bool _listeningReward;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureBuilt();

            if (Player.Instance != null)
                Player.Instance.BalanceChanged += HandleBalanceChanged;

            if (CarUpgradeProgress.Instance != null)
                CarUpgradeProgress.Instance.Changed += HandleProgressChanged;

            if (CoinRewardProgress.Instance != null)
                CoinRewardProgress.Instance.Changed += HandleRewardChanged;

            Refresh();
            LoadCatalog();
            RecoverPurchases();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (Player.Instance != null)
                Player.Instance.BalanceChanged -= HandleBalanceChanged;

            if (CarUpgradeProgress.Instance != null)
                CarUpgradeProgress.Instance.Changed -= HandleProgressChanged;

            if (CoinRewardProgress.Instance != null)
                CoinRewardProgress.Instance.Changed -= HandleRewardChanged;

            _iconRequests.Clear();
        }

        private void OnDestroy()
        {
            StopListenReward();
            ReleaseCurrencyIcons();
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            SpawnItems();
            SpawnCurrencyItems();
        }

        private void SpawnItems()
        {
            _items.Clear();

            if (config == null || itemPrefab == null || sectionPrefab == null || content == null)
            {
                Debug.LogError("ShopPanel is missing a config, item prefab, section prefab, or Group_Car.", this);
                return;
            }

            CarStatCatalog catalog = config.Catalog;
            IReadOnlyList<CarStatSection> sections = catalog != null
                ? catalog.Sections
                : Array.Empty<CarStatSection>();

            if (sections.Count == 0)
            {
                SpawnSection(null, config.Entries);
                RebuildShopLayout();
                return;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                CarStatSection section = sections[i];
                SpawnSection(section.Title, section.Stats);
            }

            if (_items.Count == 0)
                SpawnSection(null, config.Entries);

            RebuildShopLayout();
        }

        private void SpawnSection(LocalizedString title, IReadOnlyList<CarStatId> stats)
        {
            if (stats == null || stats.Count == 0)
                return;

            int available = 0;
            for (int i = 0; i < stats.Count; i++)
            {
                if (config.TryGetEntry(stats[i], out _))
                    available++;
            }

            if (available == 0)
                return;

            CarSectionView section = Instantiate(sectionPrefab, content);
            if (title != null && !title.IsEmpty)
                section.SetTitle(title);

            RectTransform items = section.Items;
            if (items == null)
            {
                Debug.LogError("Car section is missing an item grid.", section);
                return;
            }

            for (int i = 0; i < stats.Count; i++)
            {
                if (!config.TryGetEntry(stats[i], out CarStatEntry entry))
                    continue;

                SpawnUpgrade(items, entry);
            }
        }

        private void SpawnSection(LocalizedString title, IReadOnlyList<CarStatEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            CarSectionView section = Instantiate(sectionPrefab, content);
            if (title != null && !title.IsEmpty)
                section.SetTitle(title);

            RectTransform items = section.Items;
            if (items == null)
            {
                Debug.LogError("Car section is missing an item grid.", section);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
                SpawnUpgrade(items, entries[i]);
        }

        private void SpawnUpgrade(RectTransform parent, CarStatEntry entry)
        {
            CarUpgradeItemView item = Instantiate(itemPrefab, parent);
            item.Bind(config, entry, coinIcon);
            _items.Add(item);
        }

        private void RebuildShopLayout()
        {
            if (content == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (content.parent is RectTransform parent)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }

        private void SpawnCurrencyItems()
        {
            _currencyItems.Clear();

            if (currencyConfig == null || currencyItemPrefab == null || coinContent == null)
            {
                Debug.LogError("ShopPanel is missing a currency config, item prefab, or Group_Coin.", this);
                return;
            }

            CurrencyShopItemView rewardItem = CreateCurrencyItem();
            rewardItem.BindReward(
                currencyConfig.Reward,
                RemainingRewardViews(),
                WatchReward);
            _currencyItems.Add(rewardItem);

            if (!ShowCurrencyPacks())
                return;

            bool platformPrices = UsePlatformPrices();
            IReadOnlyList<CurrencyPackEntry> packs = currencyConfig.Packs;
            for (int i = 0; i < packs.Count; i++)
            {
                CurrencyPackEntry pack = packs[i];
                CurrencyShopItemView item = CreateCurrencyItem();
                string productId = pack.ProductId;
                string price = platformPrices ? string.Empty : pack.FallbackPrice;
                item.BindPack(pack, price, () => BuyPack(productId));
                _currencyItems.Add(item);
            }
        }

        private CurrencyShopItemView CreateCurrencyItem()
        {
            return Instantiate(currencyItemPrefab, coinContent);
        }

        private void HandleBalanceChanged(int balance)
        {
            Refresh();
        }

        private void HandleProgressChanged()
        {
            Refresh();
        }

        private void HandleRewardChanged()
        {
            RefreshRewardItem();
        }

        private void Refresh()
        {
            if (balanceLabel != null)
            {
                int balance = Player.Instance != null ? Player.Instance.Balance : 0;
                balanceLabel.text = balance.ToString("N0", CultureInfo.InvariantCulture);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                    _items[i].Refresh();
            }

            RefreshCurrencyPrices();
            RefreshCurrencyInteractable();
        }

        private void RefreshCurrencyPrices()
        {
            if (currencyConfig == null)
                return;

            bool platformPrices = UsePlatformPrices();
            for (int i = 0; i < _currencyItems.Count; i++)
            {
                CurrencyShopItemView item = _currencyItems[i];
                if (item == null || item.IsReward)
                    continue;

                if (!currencyConfig.TryGetPack(item.ProductId, out CurrencyPackEntry pack))
                    continue;

                ApplyPackPrice(item, pack, platformPrices);
            }
        }

        private void ApplyPackPrice(CurrencyShopItemView item, CurrencyPackEntry pack, bool platformPrices)
        {
            if (!platformPrices)
            {
                item.gameObject.SetActive(true);
                item.SetPriceText(pack.FallbackPrice);
                item.SetCurrencyIcon(null);
                return;
            }

            if (!_catalogReady)
            {
                item.gameObject.SetActive(true);
                item.SetPriceText(string.Empty);
                item.SetCurrencyIcon(null);
                return;
            }

            if (!_catalog.TryGetValue(item.ProductId, out CatalogOffer offer) || string.IsNullOrEmpty(offer.Text))
            {
                item.gameObject.SetActive(false);
                return;
            }

            item.gameObject.SetActive(true);
            item.SetPriceText(offer.Text);
            Sprite icon = null;
            if (!string.IsNullOrEmpty(offer.ImageUrl) && !_currencyIcons.TryGetValue(offer.ImageUrl, out icon))
                RequestCurrencyIcon(offer.ImageUrl);

            item.SetCurrencyIcon(icon);
        }

        private void RefreshRewardItem()
        {
            int remaining = RemainingRewardViews();
            for (int i = 0; i < _currencyItems.Count; i++)
            {
                CurrencyShopItemView item = _currencyItems[i];
                if (item == null || !item.IsReward)
                    continue;

                item.SetPriceText(remaining.ToString(CultureInfo.InvariantCulture));
                item.SetInteractable(remaining > 0 && !_rewardInFlight);
            }
        }

        private void RefreshCurrencyInteractable()
        {
            RefreshRewardItem();

            for (int i = 0; i < _currencyItems.Count; i++)
            {
                CurrencyShopItemView item = _currencyItems[i];
                if (item == null || item.IsReward)
                    continue;

                bool listed = !UsePlatformPrices() || (_catalogReady && _catalog.ContainsKey(item.ProductId));
                item.SetInteractable(listed && !_paymentsBusy);
            }
        }

        private int RemainingRewardViews()
        {
            if (currencyConfig == null || CoinRewardProgress.Instance == null)
                return 0;

            return CoinRewardProgress.Instance.GetRemaining(currencyConfig.Reward.DailyViewLimit);
        }

        private static bool ShowCurrencyPacks()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Bridge.payments.isSupported;
#else
            return true;
#endif
        }

        private static bool UsePlatformPrices()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private void WatchReward()
        {
            if (_rewardInFlight || currencyConfig == null)
                return;

            CoinRewardProgress progress = CoinRewardProgress.Instance;
            if (progress == null || !progress.IsReady || progress.GetRemaining(currencyConfig.Reward.DailyViewLimit) <= 0)
                return;

            _rewardInFlight = true;
            _rewardGranted = false;
            RefreshRewardItem();

#if UNITY_WEBGL
            if (!Bridge.advertisement.isRewardedSupported)
            {
                _rewardInFlight = false;
                RefreshRewardItem();
                Debug.LogWarning("Rewarded ads are not supported on this platform.", this);
                return;
            }

            ListenReward();
            Bridge.advertisement.ShowRewarded(currencyConfig.Reward.Placement);
#else
            CompleteReward();
            _rewardInFlight = false;
            RefreshRewardItem();
#endif
        }

        private void CompleteReward()
        {
            if (_rewardGranted || currencyConfig == null)
                return;

            CoinRewardProgress progress = CoinRewardProgress.Instance;
            if (progress == null || !progress.TryConsumeView(currencyConfig.Reward.DailyViewLimit))
                return;

            _rewardGranted = true;
            if (Player.Instance != null)
                Player.Instance.AddBalance(currencyConfig.Reward.Coins);
        }

#if UNITY_WEBGL
        private void ListenReward()
        {
            if (_listeningReward)
                return;

            Bridge.advertisement.rewardedStateChanged += HandleRewardedState;
            _listeningReward = true;
        }

        private void HandleRewardedState(RewardedState state)
        {
            if (state == RewardedState.Rewarded)
                CompleteReward();

            if (state != RewardedState.Closed && state != RewardedState.Failed)
                return;

            _rewardInFlight = false;
            StopListenReward();
            RefreshRewardItem();
        }
#endif

        private void StopListenReward()
        {
#if UNITY_WEBGL
            if (!_listeningReward)
                return;

            if (Bridge.instance != null)
                Bridge.advertisement.rewardedStateChanged -= HandleRewardedState;

            _listeningReward = false;
#endif
        }

        private void BuyPack(string productId)
        {
            if (_paymentsBusy || currencyConfig == null || !currencyConfig.TryGetPack(productId, out _))
                return;

#if UNITY_WEBGL
            if (!Bridge.payments.isSupported)
            {
                Debug.LogWarning("Payments are not supported on this platform.", this);
                return;
            }

            _paymentsBusy = true;
            RefreshCurrencyInteractable();
            Bridge.payments.Purchase(productId, (success, purchase) =>
            {
                if (this == null)
                    return;

                if (!success)
                {
                    _paymentsBusy = false;
                    RefreshCurrencyInteractable();
                    return;
                }

                string id = productId;
                if (purchase != null && purchase.TryGetValue("id", out string purchasedId) && !string.IsNullOrEmpty(purchasedId))
                    id = purchasedId;

                _consumeQueue.Enqueue(id);
                ConsumeNext();
            });
#else
            GrantPack(productId);
#endif
        }

        private void LoadCatalog()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (currencyConfig == null || !Bridge.payments.isSupported)
                return;

            Bridge.payments.GetCatalog((success, items) =>
            {
                if (this == null)
                    return;

                if (!success || items == null)
                {
                    Debug.LogWarning("Currency catalog could not be loaded.", this);
                    return;
                }

                _catalog.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    Dictionary<string, string> item = items[i];
                    if (!TryReadField(item, "id", out string id))
                        continue;

                    string text = FormatCatalogPrice(item, out string imageUrl);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    _catalog[id] = new CatalogOffer
                    {
                        Text = text,
                        ImageUrl = imageUrl
                    };
                    RequestCurrencyIcon(imageUrl);
                }

                _catalogReady = true;
                RefreshCurrencyPrices();
                RefreshCurrencyInteractable();
            });
#endif
        }

        private static string FormatCatalogPrice(Dictionary<string, string> item, out string imageUrl)
        {
            TryReadField(item, "price", out string price);
            TryReadField(item, "priceValue", out string value);
            TryReadField(item, "priceCurrencyCode", out string code);
            TryReadField(item, "priceCurrencyImage", out string image);
            imageUrl = IsHttpUrl(image) ? image : string.Empty;

            if (!string.IsNullOrEmpty(price) && !IsBareNumber(price))
                return price;

            string number = !string.IsNullOrEmpty(price) ? price : value;
            if (string.IsNullOrEmpty(number))
                return string.Empty;

            if (string.IsNullOrEmpty(imageUrl) && !string.IsNullOrEmpty(code))
                return number + " " + code;

            return number;
        }

        private static bool TryReadField(Dictionary<string, string> item, string key, out string value)
        {
            value = string.Empty;
            if (item == null || !item.TryGetValue(key, out string raw) || string.IsNullOrEmpty(raw))
                return false;

            if (raw == "null" || raw == "undefined")
                return false;

            value = raw;
            return true;
        }

        private static bool IsHttpUrl(string value)
        {
            return value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBareNumber(string value)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
        }

        private void RequestCurrencyIcon(string url)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(url) || _currencyIcons.ContainsKey(url) || !_iconRequests.Add(url))
                return;

            StartCoroutine(LoadCurrencyIcon(url));
        }

        private IEnumerator LoadCurrencyIcon(string url)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (this == null)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Currency icon could not be loaded from '{url}'.", this);
                    yield break;
                }

                _iconRequests.Remove(url);
                Texture2D downloaded = DownloadHandlerTexture.GetContent(request);
                if (downloaded == null)
                    yield break;

                Texture2D texture = new Texture2D(downloaded.width, downloaded.height, TextureFormat.RGBA32, false);
                texture.SetPixels32(downloaded.GetPixels32());
                texture.Apply();
                _currencyIcons[url] = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                RefreshCurrencyPrices();
            }
        }

        private void ReleaseCurrencyIcons()
        {
            foreach (KeyValuePair<string, Sprite> pair in _currencyIcons)
            {
                Sprite sprite = pair.Value;
                if (sprite == null)
                    continue;

                if (sprite.texture != null)
                    Destroy(sprite.texture);

                Destroy(sprite);
            }

            _currencyIcons.Clear();
        }

        private void RecoverPurchases()
        {
#if UNITY_WEBGL
            if (_paymentsBusy || currencyConfig == null || !Bridge.payments.isSupported)
                return;

            _paymentsBusy = true;
            RefreshCurrencyInteractable();
            Bridge.payments.GetPurchases((success, purchases) =>
            {
                if (this == null)
                    return;

                if (success && purchases != null)
                {
                    for (int i = 0; i < purchases.Count; i++)
                    {
                        Dictionary<string, string> purchase = purchases[i];
                        if (purchase == null || !purchase.TryGetValue("id", out string id))
                            continue;

                        if (currencyConfig.TryGetPack(id, out _))
                            _consumeQueue.Enqueue(id);
                    }
                }

                ConsumeNext();
            });
#endif
        }

        private void ConsumeNext()
        {
            if (_consumeQueue.Count == 0)
            {
                _paymentsBusy = false;
                RefreshCurrencyInteractable();
                return;
            }

            string id = _consumeQueue.Dequeue();
#if UNITY_WEBGL
            Bridge.payments.ConsumePurchase(id, (success, _) =>
            {
                if (this == null)
                    return;

                if (success)
                    GrantPack(id);

                ConsumeNext();
            });
#else
            GrantPack(id);
            ConsumeNext();
#endif
        }

        private void GrantPack(string productId)
        {
            if (currencyConfig == null || !currencyConfig.TryGetPack(productId, out CurrencyPackEntry pack))
            {
                Debug.LogWarning($"Currency pack '{productId}' is not in the shop config.", this);
                return;
            }

            if (Player.Instance != null)
                Player.Instance.AddBalance(pack.Coins);
        }
    }
}
