using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace SprunkiRacing
{
    public class CarSectionView : MonoBehaviour
    {
        [SerializeField] private LocalizeStringEvent titleEvent;
        [SerializeField] private RectTransform items;

        private readonly LocalizedTextBinding _title = new LocalizedTextBinding();

        public RectTransform Items => items;

        private void Awake()
        {
            if (titleEvent == null)
                titleEvent = GetComponentInChildren<LocalizeStringEvent>(true);

            if (items == null)
            {
                Transform group = transform.Find("Group_Car");
                if (group is RectTransform rect)
                    items = rect;
            }
        }

        private void OnDestroy()
        {
            _title.Clear();
        }

        public void SetTitle(LocalizedString title)
        {
            if (titleEvent != null)
                titleEvent.enabled = false;

            TextMeshProUGUI label = titleEvent != null ? titleEvent.GetComponent<TextMeshProUGUI>() : null;
            _title.Set(title, value => ApplyTitle(label, value));
        }

        private static void ApplyTitle(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
