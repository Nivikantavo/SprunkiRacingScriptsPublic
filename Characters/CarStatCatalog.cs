using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    [Serializable]
    public struct CarStatSection
    {
        [SerializeField] private LocalizedString title;
        [SerializeField] private CarStatId[] stats;

        public LocalizedString Title => title;
        public IReadOnlyList<CarStatId> Stats => stats ?? Array.Empty<CarStatId>();
    }

    [CreateAssetMenu(fileName = "CarStatCatalog", menuName = "Sprunki Racing/Car Stat Catalog")]
    public class CarStatCatalog : ScriptableObject
    {
        [SerializeField] private CarStatEntry[] entries = Array.Empty<CarStatEntry>();
        [SerializeField] private CarStatSection[] sections = Array.Empty<CarStatSection>();

        public IReadOnlyList<CarStatEntry> Entries => entries ?? Array.Empty<CarStatEntry>();
        public IReadOnlyList<CarStatSection> Sections => sections ?? Array.Empty<CarStatSection>();

        public bool TryGetEntry(CarStatId id, out CarStatEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Id != id)
                        continue;

                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
