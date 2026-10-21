using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class CarUpgradeItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI currentValue;
        [SerializeField] private TextMeshProUGUI nextValue;
        [SerializeField] private TextMeshProUGUI priceLabel;
        [SerializeField] private Image priceIcon;
        [SerializeField] private Button buyButton;

        private readonly LocalizedTextBinding _titleText = new LocalizedTextBinding();
        private CarStatsConfig _config;
        private CarStatId _statId;

        private void OnEnable()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(HandleBuy);
        }

        private void OnDisable()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(HandleBuy);
        }

        private void OnDestroy()
        {
            _titleText.Clear();
        }

        public void Bind(CarStatsConfig config, CarStatEntry entry, Sprite coinIcon)
        {
            _config = config;
            _statId = entry.Id;

            if (icon != null && entry.Icon != null)
                icon.sprite = entry.Icon;

            _titleText.Set(entry.Title, ApplyTitle);

            if (priceIcon != null && coinIcon != null)
            {
                priceIcon.sprite = coinIcon;
                priceIcon.preserveAspect = true;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_config == null)
                return;

            bool maxed = _config.IsMaxLevel(_statId);
            if (currentValue != null)
                currentValue.text = Format(_config.GetDisplayValue(_statId));

            if (nextValue != null)
                nextValue.text = maxed ? "MAX" : Format(_config.GetNextDisplayValue(_statId));

            if (priceLabel != null)
                priceLabel.text = maxed
                    ? "MAX"
                    : _config.GetUpgradePrice(_statId).ToString(CultureInfo.InvariantCulture);

            if (buyButton != null)
                buyButton.interactable = !maxed && CanAfford();
        }

        private void HandleBuy()
        {
            if (_config == null || CarUpgradeProgress.Instance == null)
                return;

            if (CarUpgradeProgress.Instance.TryUpgrade(_config, _statId))
                Refresh();
        }

        private bool CanAfford()
        {
            Player player = Player.Instance;
            if (player == null || !player.IsReady || _config == null)
                return false;

            return player.Balance >= _config.GetUpgradePrice(_statId);
        }

        private void ApplyTitle(string value)
        {
            if (title != null)
                title.text = value ?? string.Empty;
        }

        private static string Format(float number)
        {
            return Mathf.RoundToInt(number).ToString(CultureInfo.InvariantCulture);
        }
    }
}
