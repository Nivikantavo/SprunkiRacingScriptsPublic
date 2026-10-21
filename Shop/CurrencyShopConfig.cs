using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    [Serializable]
    public struct CurrencyRewardSettings
    {
        [SerializeField] private LocalizedString title;
        [SerializeField] private int coins;
        [SerializeField] private Sprite icon;
        [SerializeField] private int dailyViewLimit;
        [SerializeField] private string placement;
        [SerializeField] private Sprite adIcon;

        public LocalizedString LocalizedTitle => title;
        public string Title => LocalizedTextBinding.Read(title);
        public int Coins => Mathf.Max(0, coins);
        public Sprite Icon => icon;
        public int DailyViewLimit => Mathf.Max(0, dailyViewLimit);
        public string Placement => string.IsNullOrEmpty(placement) ? "shop_coins" : placement;
        public Sprite AdIcon => adIcon;
    }

    [Serializable]
    public struct CurrencyPackEntry
    {
        [SerializeField] private string productId;
        [SerializeField] private LocalizedString title;
        [SerializeField] private int coins;
        [SerializeField] private Sprite icon;
        [SerializeField] private string fallbackPrice;
        [SerializeField] private bool isBest;
        [SerializeField] private bool isPopular;

        public string ProductId => productId ?? string.Empty;
        public LocalizedString LocalizedTitle => title;
        public string Title => LocalizedTextBinding.Read(title);
        public int Coins => Mathf.Max(0, coins);
        public Sprite Icon => icon;
        public string FallbackPrice => fallbackPrice ?? string.Empty;
        public bool IsBest => isBest;
        public bool IsPopular => isPopular;
    }

    [CreateAssetMenu(fileName = "CurrencyShopConfig", menuName = "Sprunki Racing/Currency Shop")]
    public class CurrencyShopConfig : ScriptableObject
    {
        [SerializeField] private CurrencyRewardSettings reward;
        [SerializeField] private CurrencyPackEntry[] packs = Array.Empty<CurrencyPackEntry>();

        public CurrencyRewardSettings Reward => reward;
        public IReadOnlyList<CurrencyPackEntry> Packs => packs;

        public bool TryGetPack(string productId, out CurrencyPackEntry pack)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                for (int i = 0; i < packs.Length; i++)
                {
                    if (packs[i].ProductId != productId)
                        continue;

                    pack = packs[i];
                    return true;
                }
            }

            pack = default;
            return false;
        }
    }
}
