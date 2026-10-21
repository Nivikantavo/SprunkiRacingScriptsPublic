using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class ShopItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI priceLabel;
        [SerializeField] private Image priceIcon;
        [SerializeField] private Button buyButton;

        private Action _onClick;

        private void OnEnable()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(HandleClick);
        }

        public void SetClick(Action onClick)
        {
            _onClick = onClick;
        }

        public void SetTitle(string value)
        {
            if (title != null)
                title.text = value ?? string.Empty;
        }

        public void SetIcon(Sprite sprite)
        {
            if (icon == null || sprite == null)
                return;

            icon.sprite = sprite;
            icon.preserveAspect = true;
        }

        public void SetPrice(string value)
        {
            if (priceLabel != null)
                priceLabel.text = value ?? string.Empty;
        }

        public void SetPriceIcon(Sprite sprite, bool visible)
        {
            if (priceIcon == null)
                return;

            priceIcon.gameObject.SetActive(visible);
            if (!visible || sprite == null)
                return;

            priceIcon.sprite = sprite;
            priceIcon.preserveAspect = true;
        }

        public void SetInteractable(bool interactable)
        {
            if (buyButton != null)
                buyButton.interactable = interactable;
        }

        private void HandleClick()
        {
            _onClick?.Invoke();
        }
    }
}
