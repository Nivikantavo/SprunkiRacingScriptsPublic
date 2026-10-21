using System;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class LanguageOption : MonoBehaviour
    {
        [SerializeField] private string localeCode;
        [SerializeField] private GameObject focus;
        [SerializeField] private Button button;

        public string LocaleCode => localeCode;

        public event Action<LanguageOption> Clicked;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (focus != null)
            {
                Graphic plate = focus.GetComponent<Graphic>();
                if (plate != null)
                    plate.raycastTarget = false;
            }

            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void SetFocused(bool focused)
        {
            if (focus != null)
                focus.SetActive(focused);
        }

        private void HandleClick()
        {
            Clicked?.Invoke(this);
        }
    }
}
