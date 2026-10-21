using System;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class LanguagePanel : MonoBehaviour
    {
        [SerializeField] private LanguageOption[] options = Array.Empty<LanguageOption>();
        [SerializeField] private Button selectButton;
        [SerializeField] private Button closeButton;

        private LanguageOption _pending;
        private bool _picked;

        private void Awake()
        {
            for (int i = 0; i < options.Length; i++)
            {
                LanguageOption option = options[i];
                if (option != null)
                    option.Clicked += HandleOption;
            }

            if (selectButton != null)
                selectButton.onClick.AddListener(ApplySelection);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameLanguage.Changed += HandleLanguageChanged;
            _picked = false;
            Sync();
        }

        private void OnDisable()
        {
            GameLanguage.Changed -= HandleLanguageChanged;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < options.Length; i++)
            {
                LanguageOption option = options[i];
                if (option != null)
                    option.Clicked -= HandleOption;
            }

            if (selectButton != null)
                selectButton.onClick.RemoveListener(ApplySelection);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        private void HandleLanguageChanged()
        {
            if (_picked)
                return;

            Sync();
        }

        private void HandleOption(LanguageOption option)
        {
            if (option == null)
                return;

            _picked = true;
            _pending = option;
            Show(_pending);
        }

        private void ApplySelection()
        {
            if (_pending == null || string.IsNullOrEmpty(_pending.LocaleCode))
                return;

            GameLanguage.Select(_pending.LocaleCode);
        }

        private void Sync()
        {
            _pending = FindOption(GameLanguage.Code);
            Show(_pending);
        }

        private void Show(LanguageOption selected)
        {
            for (int i = 0; i < options.Length; i++)
            {
                LanguageOption option = options[i];
                if (option != null)
                    option.SetFocused(option == selected);
            }
        }

        private LanguageOption FindOption(string code)
        {
            LanguageOption english = null;
            for (int i = 0; i < options.Length; i++)
            {
                LanguageOption option = options[i];
                if (option == null)
                    continue;

                if (english == null && string.Equals(option.LocaleCode, GameLanguage.FallbackCode, StringComparison.OrdinalIgnoreCase))
                    english = option;

                if (string.Equals(option.LocaleCode, code, StringComparison.OrdinalIgnoreCase))
                    return option;
            }

            return english;
        }
    }
}
