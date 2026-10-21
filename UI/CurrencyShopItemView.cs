using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace SprunkiRacing
{
    public class CurrencyShopItemView : MonoBehaviour
    {
        [SerializeField] private ShopItemView item;
        [SerializeField] private TextMeshProUGUI valueLabel;
        [SerializeField] private GameObject bestLabel;
        [SerializeField] private GameObject popularLabel;

        private readonly LocalizedTextBinding _titleText = new LocalizedTextBinding();

        public string ProductId { get; private set; }

        public bool IsReward { get; private set; }

        public void BindReward(CurrencyRewardSettings reward, int remaining, Action onWatch)
        {
            IsReward = true;
            ProductId = string.Empty;
            _titleText.Set(reward.LocalizedTitle, SetTitle);
            Apply(reward.Coins, reward.Icon, remaining.ToString(CultureInfo.InvariantCulture), reward.AdIcon, true, onWatch);
            SetLabel(bestLabel, false);
            SetLabel(popularLabel, false);
            SetInteractable(remaining > 0);
        }

        public void BindPack(CurrencyPackEntry pack, string price, Action onBuy)
        {
            IsReward = false;
            ProductId = pack.ProductId;
            _titleText.Set(pack.LocalizedTitle, SetTitle);
            Apply(pack.Coins, pack.Icon, price ?? string.Empty, null, false, onBuy);
            SetLabel(bestLabel, pack.IsBest);
            SetLabel(popularLabel, pack.IsPopular);
            SetInteractable(!string.IsNullOrEmpty(price));
        }

        public void SetPriceText(string text)
        {
            if (item != null)
                item.SetPrice(text);
        }

        public void SetCurrencyIcon(Sprite sprite)
        {
            if (IsReward || item == null)
                return;

            item.SetPriceIcon(sprite, sprite != null);
        }

        public void SetInteractable(bool interactable)
        {
            if (item != null)
                item.SetInteractable(interactable);
        }

        private void OnDestroy()
        {
            _titleText.Clear();
        }

        private void SetTitle(string value)
        {
            if (item != null)
                item.SetTitle(value);
        }

        private void Apply(int coins, Sprite icon, string price, Sprite priceIcon, bool showPriceIcon, Action onClick)
        {
            if (item == null)
                return;

            item.SetIcon(icon);
            item.SetPrice(price);
            item.SetPriceIcon(priceIcon, showPriceIcon);
            item.SetClick(onClick);

            if (valueLabel != null)
                valueLabel.text = coins.ToString(CultureInfo.InvariantCulture);
        }

        private static void SetLabel(GameObject label, bool visible)
        {
            if (label != null)
                label.SetActive(visible);
        }
    }
}
