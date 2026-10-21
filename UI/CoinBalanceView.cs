using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class CoinBalanceView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI balanceLabel;
        [SerializeField] private Button addButton;
        [SerializeField] private ShopSectionRequest addRequest;
        [SerializeField] private ShopSection addSection = ShopSection.GoldPack;

        private void Awake()
        {
            if (addButton != null)
                addButton.onClick.AddListener(HandleAdd);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (Player.Instance != null)
                Player.Instance.BalanceChanged += HandleBalanceChanged;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (Player.Instance != null)
                Player.Instance.BalanceChanged -= HandleBalanceChanged;
        }

        private void OnDestroy()
        {
            if (addButton != null)
                addButton.onClick.RemoveListener(HandleAdd);
        }

        private void HandleBalanceChanged(int balance)
        {
            if (balanceLabel == null)
                return;

            balanceLabel.text = balance.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void HandleAdd()
        {
            if (addRequest == null)
            {
                Debug.LogError("CoinBalanceView is missing a shop section request.", this);
                return;
            }

            addRequest.Raise(addSection);
        }
    }
}
