using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class ParametrItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI valueLabel;

        private readonly LocalizedTextBinding _titleText = new LocalizedTextBinding();
        private LocalizedString _label;

        public void Bind(Sprite sprite, LocalizedString label, float number)
        {
            Resolve();

            if (icon != null && sprite != null)
                icon.sprite = sprite;

            if (!ReferenceEquals(_label, label))
            {
                _label = label;
                _titleText.Set(label, ApplyTitle);
            }

            if (valueLabel != null)
                valueLabel.text = Mathf.RoundToInt(number).ToString(CultureInfo.InvariantCulture);
        }

        private void OnDestroy()
        {
            _titleText.Clear();
        }

        private void ApplyTitle(string value)
        {
            if (title != null)
                title.text = value ?? string.Empty;
        }

        private void Resolve()
        {
            if (icon == null)
                icon = transform.Find("Skill_Image/Skill")?.GetComponent<Image>();

            if (title == null)
                title = transform.Find("Title/Text (TMP)")?.GetComponent<TextMeshProUGUI>();

            if (valueLabel == null)
                valueLabel = transform.Find("Text Value")?.GetComponent<TextMeshProUGUI>();
        }
    }
}
