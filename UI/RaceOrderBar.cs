using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public class RaceOrderBar : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private RaceOrderSlotView slotPrefab;
        [SerializeField] private float refreshInterval = 0.1f;

        private readonly List<RaceOrderSlotView> _slots = new List<RaceOrderSlotView>(8);
        private readonly RaceOrder _order = new RaceOrder();
        private RacePath _path;
        private float _timer;

        public static RaceOrderBar Instance { get; private set; }

        public int FieldCount => _order.Ordered.Count;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryGetPlayerPlace(out int place)
        {
            place = 0;
            if (_path == null)
                _path = RacePath.Instance;

            if (_path != null)
                _order.Tick(_path, true);

            IReadOnlyList<RaceStanding> ordered = _order.Ordered;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (!ordered[i].IsPlayer)
                    continue;

                place = i + 1;
                return true;
            }

            return false;
        }

        private void Start()
        {
            if (content == null)
                content = transform as RectTransform;

            _path = RacePath.Instance;
            BuildSlots();
        }

        private void LateUpdate()
        {
            if (_slots.Count == 0 || _path == null)
                return;

            RaceDirector director = RaceDirector.Instance;
            bool sample = director != null && director.RaceRunning;
            if (sample)
            {
                _timer += Time.deltaTime;
                if (_order.IsSeeded && _timer < refreshInterval)
                    sample = false;
                else
                    _timer = 0f;
            }

            if (_order.Tick(_path, sample))
                ApplyOrder();

            RefreshPortraits();
        }

        private void BuildSlots()
        {
            RaceDirector director = RaceDirector.Instance;
            if (director == null || slotPrefab == null || content == null)
            {
                Debug.LogError("Race order bar is missing a director, slot prefab, or content.", this);
                return;
            }

            _order.Prepare(director.Racers);
            IReadOnlyList<RaceStanding> standings = _order.Ordered;
            for (int i = 0; i < standings.Count; i++)
            {
                RaceOrderSlotView slot = Instantiate(slotPrefab, content);
                slot.Bind(standings[i]);
                _slots.Add(slot);
            }
        }

        private void ApplyOrder()
        {
            IReadOnlyList<RaceStanding> ordered = _order.Ordered;
            int count = ordered.Count;
            for (int visual = 0; visual < count; visual++)
            {
                RaceOrderSlotView slot = SlotFor(ordered[count - 1 - visual]);
                if (slot == null)
                    continue;

                slot.Refresh();
                if (slot.transform.GetSiblingIndex() != visual)
                    slot.transform.SetSiblingIndex(visual);
            }
        }

        private void RefreshPortraits()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].RefreshPortrait();
        }

        private RaceOrderSlotView SlotFor(RaceStanding standing)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Standing == standing)
                    return _slots[i];
            }

            return null;
        }
    }
}
