using System.Globalization;
using TMPro;
using UnityEngine;

namespace SprunkiRacing
{
    public class PrizeItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI valueLabel;
        [SerializeField] private GameObject bonusLabel;
        [SerializeField] private GameObject killLabel;

        public void Bind(int amount, bool bonus, bool kill)
        {
            if (valueLabel != null)
                valueLabel.text = FormatSigned(amount);
            else
                Debug.LogError("Prize item has no value label.", this);

            if (bonusLabel != null)
                bonusLabel.SetActive(bonus);

            if (killLabel != null)
                killLabel.SetActive(kill);
        }

        public static string FormatSigned(int value)
        {
            if (value > 0)
                return "+" + value.ToString(CultureInfo.InvariantCulture);

            if (value == 0)
                return "+0";

            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
