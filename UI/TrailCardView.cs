using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class TrailCardView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private GameObject focus;
        [SerializeField] private Button button;
        [SerializeField] private GameObject priceRoot;
        [SerializeField] private TextMeshProUGUI priceLabel;

        private readonly LocalizedTextBinding _nameText = new LocalizedTextBinding();
        private Action<TrailCardView> _onSelected;

        public TrailConfig Config { get; private set; }

        private void Awake()
        {
            if (portrait == null)
                portrait = transform.Find("Portrait")?.GetComponent<Image>();

            if (nameLabel == null)
                nameLabel = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();

            if (focus == null)
                focus = transform.Find("Focus")?.gameObject;

            if (priceRoot == null)
                priceRoot = transform.Find("Price")?.gameObject;

            if (priceLabel == null)
                priceLabel = transform.Find("Price/Text_Price")?.GetComponent<TextMeshProUGUI>();

            if (button == null)
                button = GetComponent<Button>();

            if (button == null)
                button = gameObject.AddComponent<Button>();

            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(HandleClick);
            DisableChildRaycasts();
            SetEquipped(false);
        }

        public void Bind(TrailConfig config, Action<TrailCardView> onSelected)
        {
            Config = config;
            _onSelected = onSelected;

            if (portrait != null)
            {
                portrait.sprite = config != null ? config.Preview : null;
                portrait.preserveAspect = true;
            }

            _nameText.Set(config != null ? config.LocalizedName : null, ApplyName);
        }

        private void ApplyName(string value)
        {
            if (nameLabel != null)
                nameLabel.text = value ?? string.Empty;
        }

        public void SetOffer(bool unlocked, int price)
        {
            if (priceRoot != null)
                priceRoot.SetActive(!unlocked);

            if (priceLabel != null)
                priceLabel.text = price.ToString(CultureInfo.InvariantCulture);
        }

        public void SetEquipped(bool equipped)
        {
            if (focus != null)
                focus.SetActive(equipped);
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void HandleClick()
        {
            _onSelected?.Invoke(this);
        }

        private void DisableChildRaycasts()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].gameObject != gameObject)
                    graphics[i].raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            _nameText.Clear();
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }
    }
}
