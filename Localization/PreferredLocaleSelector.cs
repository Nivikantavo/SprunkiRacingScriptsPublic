using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace SprunkiRacing
{
    [Serializable]
    public class PreferredLocaleSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            return GameLanguage.Resolve(availableLocales, GameLanguage.StartupCode);
        }
    }
}
