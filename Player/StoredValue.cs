using System;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
#endif

namespace SprunkiRacing
{
    class StoredValue
    {
        private readonly string _key;
        private readonly string _saveError;
        private string _value;
        private bool _saveInFlight;
        private bool _savePending;

        public StoredValue(string key, string saveError)
        {
            _key = key;
            _saveError = saveError;
        }

        public void Remember(string value)
        {
            _value = value;
        }

        public void Load(Action<bool, string> onLoaded)
        {
#if UNITY_WEBGL
            Bridge.storage.Get(_key, onLoaded);
#else
            string stored = PlayerPrefs.GetString(_key, null);
            onLoaded(true, string.IsNullOrEmpty(stored) ? null : stored);
#endif
        }

        public void Set(string value)
        {
            if (_value == value && !_savePending)
                return;

            _value = value;
            Save();
        }

        private void Save()
        {
            if (_saveInFlight)
            {
                _savePending = true;
                return;
            }

            _saveInFlight = true;
#if UNITY_WEBGL
            Bridge.storage.Set(_key, _value ?? string.Empty, OnSaved);
#else
            PlayerPrefs.SetString(_key, _value ?? string.Empty);
            PlayerPrefs.Save();
            OnSaved(true);
#endif
        }

        private void OnSaved(bool success)
        {
            _saveInFlight = false;

            if (!success)
                Debug.LogWarning(_saveError + " (" + _key + ").");

            if (!_savePending)
                return;

            _savePending = false;
            Save();
        }
    }
}
