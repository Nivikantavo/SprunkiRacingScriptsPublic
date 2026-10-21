using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class CharacterCardView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject focus;
        [SerializeField] private Button button;
        [SerializeField] private GameObject priceRoot;
        [SerializeField] private TextMeshProUGUI priceLabel;

        private Action<CharacterCardView> _onSelected;

        public CharacterConfig Config { get; private set; }

        private void Awake()
        {
            if (portrait == null)
                portrait = transform.Find("Character_(Mask)/Character")?.GetComponent<Image>();

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
            SetSelected(false);
        }

        public void Bind(CharacterConfig config, Action<CharacterCardView> onSelected)
        {
            Config = config;
            _onSelected = onSelected;

            if (portrait != null)
                portrait.sprite = config != null ? config.Preview : null;
        }

        public void SetOffer(bool unlocked, int price)
        {
            if (priceRoot != null)
                priceRoot.SetActive(!unlocked);

            if (priceLabel != null)
                priceLabel.text = price.ToString(CultureInfo.InvariantCulture);
        }

        public void SetSelected(bool selected)
        {
            if (focus != null)
                focus.SetActive(selected);
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
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }
    }
}
