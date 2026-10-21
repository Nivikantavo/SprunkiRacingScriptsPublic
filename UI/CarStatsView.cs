using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public class CarStatsView : MonoBehaviour
    {
        [SerializeField] private CarStatsConfig config;
        [SerializeField] private ParametrItemView itemPrefab;

        private readonly List<ParametrItemView> _items = new List<ParametrItemView>();

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (CarUpgradeProgress.Instance != null)
                CarUpgradeProgress.Instance.Changed += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (CarUpgradeProgress.Instance != null)
                CarUpgradeProgress.Instance.Changed -= Refresh;
        }

        public void Refresh()
        {
            if (config == null || itemPrefab == null)
            {
                Debug.LogError("CarStatsView is missing a config or item prefab.", this);
                return;
            }

            EnsureItems();

            IReadOnlyList<CarStatEntry> entries = config.Entries;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null)
                    continue;

                if (i >= entries.Count)
                {
                    _items[i].gameObject.SetActive(false);
                    continue;
                }

                CarStatEntry entry = entries[i];
                _items[i].gameObject.SetActive(true);
                _items[i].Bind(entry.Icon, entry.Title, config.GetDisplayValue(entry.Id));
            }
        }

        private void EnsureItems()
        {
            if (_items.Count > 0)
                return;

            ClearPlaceholders();

            IReadOnlyList<CarStatEntry> entries = config.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                ParametrItemView item = Instantiate(itemPrefab, transform);
                _items.Add(item);
            }
        }

        private void ClearPlaceholders()
        {
            int existing = transform.childCount;
            if (existing == 0)
                return;

            var stale = new GameObject[existing];
            for (int i = 0; i < existing; i++)
                stale[i] = transform.GetChild(i).gameObject;

            for (int i = 0; i < stale.Length; i++)
                Destroy(stale[i]);
        }

    }
}
