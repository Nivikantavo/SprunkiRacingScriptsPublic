using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public class RaceOrder
    {
        private const float TieMeters = 2f;

        private readonly List<RaceStanding> _ordered = new List<RaceStanding>(8);
        private readonly List<Sample> _samples = new List<Sample>(8);
        private readonly List<int> _byAlong = new List<int>(8);
        private int _fallbackDeathOrder;

        public bool IsSeeded { get; private set; }
        public IReadOnlyList<RaceStanding> Ordered => _ordered;

        public void Prepare(IReadOnlyList<RaceRacer> racers)
        {
            RaceRacer.BeginRace();
            _fallbackDeathOrder = 1000;
            _ordered.Clear();
            IsSeeded = false;

            if (racers == null)
                return;

            for (int i = 0; i < racers.Count; i++)
            {
                RaceRacer racer = racers[i];
                if (racer == null)
                    continue;

                _ordered.Add(new RaceStanding(racer));
            }
        }

        public bool Tick(RacePath path, bool sampleProgress)
        {
            bool changed = CaptureDeaths();
            if (path == null)
                return changed;

            if (!IsSeeded)
                return Seed(path) || changed;

            if (!sampleProgress)
                return changed;

            float length = path.Length;
            if (length < 0.01f)
                return changed;

            for (int i = 0; i < _ordered.Count; i++)
            {
                RaceStanding standing = _ordered[i];
                if (standing.IsEliminated)
                    continue;

                RaceRacer racer = standing.Racer;
                if (racer == null)
                    continue;

                if (!path.TryGetProgress(racer.transform.position, out float along))
                    continue;

                racer.AdvanceProgress(along, length);
            }

            return Sort(TieMeters) || changed;
        }

        private bool Seed(RacePath path)
        {
            float length = path.Length;
            if (length < 0.01f || _ordered.Count == 0)
                return false;

            _samples.Clear();
            for (int i = 0; i < _ordered.Count; i++)
            {
                RaceStanding standing = _ordered[i];
                RaceRacer racer = standing.Racer;
                if (standing.IsEliminated || racer == null)
                    continue;

                if (!path.TryGetProgress(racer.transform.position, out float along))
                    continue;

                _samples.Add(new Sample(racer, along));
            }

            if (_samples.Count == 0)
            {
                IsSeeded = true;
                return CaptureDeaths();
            }

            float origin = PackOrigin(length);
            for (int i = 0; i < _samples.Count; i++)
            {
                Sample sample = _samples[i];
                float distance = Mathf.Repeat(sample.Along - origin, length);
                sample.Racer.SeedProgress(distance, sample.Along);
            }

            Sort(0f);
            IsSeeded = true;
            return true;
        }

        private bool CaptureDeaths()
        {
            bool changed = false;
            for (int i = 0; i < _ordered.Count; i++)
            {
                RaceStanding standing = _ordered[i];
                if (standing.IsEliminated)
                    continue;

                RaceRacer racer = standing.Racer;
                if (racer != null && !racer.IsEliminated)
                    continue;

                int order = racer != null && racer.DeathOrder >= 0
                    ? racer.DeathOrder
                    : _fallbackDeathOrder++;
                standing.Eliminate(order);
                changed = true;
            }

            if (changed)
                Sort(TieMeters);

            return changed;
        }

        private float PackOrigin(float length)
        {
            int count = _samples.Count;
            if (count == 1)
                return _samples[0].Along;

            _byAlong.Clear();
            for (int i = 0; i < count; i++)
                _byAlong.Add(i);

            for (int i = 1; i < count; i++)
            {
                int current = _byAlong[i];
                float along = _samples[current].Along;
                int slot = i;
                while (slot > 0 && _samples[_byAlong[slot - 1]].Along > along)
                {
                    _byAlong[slot] = _byAlong[slot - 1];
                    slot--;
                }

                _byAlong[slot] = current;
            }

            float largestGap = _samples[_byAlong[0]].Along + length - _samples[_byAlong[count - 1]].Along;
            int afterGap = 0;
            for (int i = 1; i < count; i++)
            {
                float gap = _samples[_byAlong[i]].Along - _samples[_byAlong[i - 1]].Along;
                if (gap > largestGap)
                {
                    largestGap = gap;
                    afterGap = i;
                }
            }

            return _samples[_byAlong[afterGap]].Along;
        }

        private bool Sort(float tieMeters)
        {
            bool changed = false;
            for (int i = 1; i < _ordered.Count; i++)
            {
                RaceStanding current = _ordered[i];
                int insertAt = i;
                while (insertAt > 0 && IsAhead(current, _ordered[insertAt - 1], tieMeters))
                {
                    _ordered[insertAt] = _ordered[insertAt - 1];
                    insertAt--;
                    changed = true;
                }

                _ordered[insertAt] = current;
            }

            return changed;
        }

        private static bool IsAhead(RaceStanding current, RaceStanding previous, float tieMeters)
        {
            if (previous == null)
                return true;

            if (current.IsEliminated != previous.IsEliminated)
                return !current.IsEliminated;

            if (current.IsEliminated)
                return current.DeathOrder > previous.DeathOrder;

            RaceRacer currentRacer = current.Racer;
            RaceRacer previousRacer = previous.Racer;
            float distance = currentRacer != null ? currentRacer.RaceDistance : 0f;
            float previousDistance = previousRacer != null ? previousRacer.RaceDistance : 0f;
            return distance > previousDistance + tieMeters;
        }

        private readonly struct Sample
        {
            public Sample(RaceRacer racer, float along)
            {
                Racer = racer;
                Along = along;
            }

            public RaceRacer Racer { get; }
            public float Along { get; }
        }
    }
}
