using System;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    public class LocalizedTextBinding
    {
        private LocalizedString _source;
        private Action<string> _apply;
        private LocalizedString.ChangeHandler _handler;

        public static string Read(LocalizedString value)
        {
            if (value == null || value.IsEmpty)
                return string.Empty;

            return value.GetLocalizedString();
        }

        public void Set(LocalizedString source, Action<string> apply)
        {
            Clear();
            _source = source;
            _apply = apply;

            if (_apply == null)
                return;

            if (_source == null || _source.IsEmpty)
            {
                _apply(string.Empty);
                return;
            }

            _handler = OnChanged;
            _source.StringChanged += _handler;
        }

        public void Clear()
        {
            if (_source != null && _handler != null)
                _source.StringChanged -= _handler;

            _source = null;
            _handler = null;
            _apply = null;
        }

        private void OnChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
                value = Read(_source);

            _apply?.Invoke(value ?? string.Empty);
        }
    }
}
