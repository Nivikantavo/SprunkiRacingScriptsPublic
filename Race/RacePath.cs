using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    public struct RaceGuidance
    {
        public Vector3 Closest;
        public Vector3 Tangent;
        public float Curvature;
        public float Lateral;
        public float Progress;
    }

    public readonly struct RacePathPointReport
    {
        public readonly string Name;
        public readonly int Slot;
        public readonly Vector3 Position;
        public readonly float SegmentLength;
        public readonly float TurnDegrees;
        public readonly int SkippedBefore;

        public RacePathPointReport(string name, int slot, Vector3 position, float segmentLength, float turnDegrees, int skippedBefore)
        {
            Name = name;
            Slot = slot;
            Position = position;
            SegmentLength = segmentLength;
            TurnDegrees = turnDegrees;
            SkippedBefore = skippedBefore;
        }
    }

    public class RacePath : MonoBehaviour
    {
        public static RacePath Instance { get; private set; }

        [SerializeField] private Transform[] points;

        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<float> _pointDistances = new List<float>();

        public int PointCount
        {
            get
            {
                if (points == null)
                    return 0;

                int count = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] != null)
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static float SenseDistance(float intelligence)
        {
            return Mathf.Lerp(36f, 56f, Mathf.Clamp01(intelligence));
        }

        public float Length
        {
            get
            {
                if (!CollectPositions())
                    return 0f;

                return LoopLength();
            }
        }

        public float UpcomingTurn(float progress, float distance)
        {
            if (!CollectPositions())
                return 0f;

            return TightestTurn(progress, Mathf.Max(4f, distance));
        }

        public bool TryGetProgress(Vector3 position, out float progress)
        {
            progress = 0f;
            if (!CollectPositions())
                return false;

            progress = ClosestDistance(position);
            return true;
        }

        public static float CornerLookahead(float curvature, bool eased)
        {
            if (eased)
                return 8f;

            return Mathf.Lerp(22f, 12f, Mathf.InverseLerp(8f, 30f, curvature));
        }

        public bool TryProject(Vector3 position, ref float progress, float senseDistance, out RaceGuidance guidance)
        {
            guidance = default;
            if (!CollectPositions())
                return false;

            float along = ClosestDistance(position, progress);
            float length = LoopLength();
            if (length > 0.01f)
                along = Mathf.Repeat(along, length);

            Vector3 tangent = TangentAt(along);
            Vector3 closest = Sample(along);
            Vector3 offset = position - closest;
            offset.y = 0f;

            guidance.Closest = closest;
            guidance.Tangent = tangent;
            guidance.Curvature = TightestTurn(along, Mathf.Max(8f, senseDistance));
            guidance.Lateral = Vector3.Dot(offset, Vector3.Cross(Vector3.up, tangent));
            guidance.Progress = along;
            progress = along;
            return true;
        }

        public Vector3 AimPoint(float progress, float lookahead, float lateral)
        {
            if (!CollectPositions())
                return Vector3.zero;

            float lead = Mathf.Max(4f, lookahead);
            Vector3 point = Sample(progress + lead);
            point += Vector3.Cross(Vector3.up, TangentAt(progress + lead)) * lateral;
            return point;
        }

        public bool TryGetPoint(Vector3 position, float lookahead, float lateral, out Vector3 point, out float upcomingTurn)
        {
            point = position;
            upcomingTurn = 0f;
            if (!CollectPositions())
                return false;

            float along = ClosestDistance(position);
            float lead = Mathf.Max(4f, lookahead);
            point = Sample(along + lead);
            Vector3 ahead = Sample(along + lead + 12f);
            Vector3 further = Sample(along + lead + 24f);
            Vector3 forward = ahead - point;
            Vector3 nextForward = further - ahead;
            forward.y = 0f;
            nextForward.y = 0f;
            if (forward.sqrMagnitude > 0.01f && nextForward.sqrMagnitude > 0.01f)
                upcomingTurn = Vector3.SignedAngle(forward, nextForward, Vector3.up);

            if (forward.sqrMagnitude > 0.01f)
                point += Vector3.Cross(Vector3.up, forward.normalized) * lateral;

            return true;
        }

        public int BuildReport(List<RacePathPointReport> reports)
        {
            reports.Clear();
            if (points == null)
                return 0;

            List<int> slots = new List<int>();
            List<Transform> kept = new List<Transform>();
            List<int> skippedBefore = new List<int>();
            int skipped = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                {
                    skipped++;
                    continue;
                }

                slots.Add(i);
                kept.Add(points[i]);
                skippedBefore.Add(skipped);
                skipped = 0;
            }

            if (kept.Count < 2)
                return 0;

            for (int i = 0; i < kept.Count; i++)
            {
                Transform current = kept[i];
                Transform next = kept[(i + 1) % kept.Count];
                Transform previous = kept[(i + kept.Count - 1) % kept.Count];
                Vector3 incoming = current.position - previous.position;
                Vector3 outgoing = next.position - current.position;
                incoming.y = 0f;
                outgoing.y = 0f;
                float turn = incoming.sqrMagnitude > 0.01f && outgoing.sqrMagnitude > 0.01f
                    ? Vector3.Angle(incoming, outgoing)
                    : 0f;
                reports.Add(new RacePathPointReport(current.name, slots[i], current.position, outgoing.magnitude, turn, skippedBefore[i]));
            }

            return reports.Count;
        }

        public bool TryGetRespawn(int index, out Vector3 position, out Vector3 forward, out float along)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            along = 0f;
            if (!CollectPositions() || index < 0 || index >= _positions.Count)
                return false;

            position = _positions[index];
            along = _pointDistances[index];
            Vector3 heading = _positions[(index + 1) % _positions.Count] - position;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0001f)
                forward = heading.normalized;

            return true;
        }

        public bool TryGetPointIndex(float along, out int index)
        {
            index = 0;
            if (!CollectPositions())
                return false;

            float length = LoopLength();
            if (length > 0.01f)
                along = Mathf.Repeat(along, length);

            for (int i = 0; i < _pointDistances.Count; i++)
            {
                if (_pointDistances[i] <= along + 2f)
                    index = i;
                else
                    break;
            }

            return true;
        }

        public bool TryGetNearestPointIndex(Vector3 position, out int index)
        {
            index = -1;
            if (!CollectPositions())
                return false;

            float best = float.MaxValue;
            for (int i = 0; i < _positions.Count; i++)
            {
                float sqr = (_positions[i] - position).sqrMagnitude;
                if (sqr >= best)
                    continue;

                best = sqr;
                index = i;
            }

            return index >= 0;
        }

        public bool TryNearestPoint(Vector3 position, out int slot, out string pointName, out float distance)
        {
            slot = -1;
            pointName = null;
            distance = 0f;
            if (points == null)
                return false;

            float best = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                    continue;

                float sqr = (points[i].position - position).sqrMagnitude;
                if (sqr >= best)
                    continue;

                best = sqr;
                slot = i;
                pointName = points[i].name;
            }

            if (slot < 0)
                return false;

            distance = Mathf.Sqrt(best);
            return true;
        }

        public bool TryGetAhead(Vector3 from, Vector3 to, out float ahead)
        {
            ahead = 0f;
            if (!CollectPositions())
                return false;

            float length = LoopLength();
            if (length < 0.01f)
                return false;

            ahead = Mathf.Repeat(ClosestDistance(to) - ClosestDistance(from), length);
            return true;
        }

        private bool CollectPositions()
        {
            _positions.Clear();
            _pointDistances.Clear();
            if (points == null)
                return false;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                    _positions.Add(points[i].position);
            }

            if (_positions.Count < 2)
                return false;

            float cursor = 0f;
            for (int i = 0; i < _positions.Count; i++)
            {
                _pointDistances.Add(cursor);
                cursor += Vector3.Distance(_positions[i], _positions[(i + 1) % _positions.Count]);
            }

            return true;
        }

        private float ClosestDistance(Vector3 position)
        {
            return ClosestDistance(position, -1f);
        }

        private float ClosestDistance(Vector3 position, float hint)
        {
            float length = LoopLength();
            if (length < 0.01f)
                return 0f;

            const float back = 12f;
            const float ahead = 30f;
            bool windowed = hint >= 0f && back + ahead < length - 1f;
            if (windowed &&
                TrySearch(position, hint, length, true, out float windowedDistance, out float windowedSqr) &&
                windowedSqr <= 18f * 18f)
                return windowedDistance;

            TrySearch(position, 0f, length, false, out float globalDistance, out _);
            return globalDistance;
        }

        private bool TrySearch(Vector3 position, float hint, float length, bool windowed, out float best, out float bestSqr)
        {
            const float back = 12f;
            const float ahead = 30f;
            float windowStart = windowed ? Mathf.Repeat(hint - back, length) : 0f;
            float windowSpan = back + ahead;
            bestSqr = float.MaxValue;
            best = 0f;
            float cursor = 0f;
            for (int i = 0; i < _positions.Count; i++)
            {
                Vector3 from = _positions[i];
                Vector3 to = _positions[(i + 1) % _positions.Count];
                Vector3 segment = to - from;
                float segmentLength = segment.magnitude;
                float t = segmentLength < 0.001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(position - from, segment) / (segmentLength * segmentLength));
                float along = cursor + segmentLength * t;
                if (!windowed || InWindow(along, windowStart, windowSpan, length))
                {
                    float sqr = (from + segment * t - position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = along;
                    }
                }

                cursor += segmentLength;
            }

            return bestSqr < float.MaxValue;
        }

        private static bool InWindow(float distance, float start, float span, float length)
        {
            return Mathf.Repeat(distance - start, length) <= span;
        }

        private Vector3 TangentAt(float distance)
        {
            Vector3 tangent = Sample(distance + 3f) - Sample(distance);
            tangent.y = 0f;
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
        }

        private float TightestTurn(float distance, float sense)
        {
            float worst = 0f;
            Vector3 previous = TangentAt(distance);
            const float step = 4f;
            for (float traveled = step; traveled <= sense; traveled += step)
            {
                Vector3 next = TangentAt(distance + traveled);
                worst = Mathf.Max(worst, Vector3.Angle(previous, next));
                previous = next;
            }

            return worst;
        }

        private float LoopLength()
        {
            float length = 0f;
            for (int i = 0; i < _positions.Count; i++)
                length += Vector3.Distance(_positions[i], _positions[(i + 1) % _positions.Count]);

            return length;
        }

        private Vector3 Sample(float distance)
        {
            float length = LoopLength();
            if (length < 0.01f)
                return _positions[0];

            distance = Mathf.Repeat(distance, length);
            for (int i = 0; i < _positions.Count; i++)
            {
                Vector3 from = _positions[i];
                Vector3 to = _positions[(i + 1) % _positions.Count];
                float segment = Vector3.Distance(from, to);
                if (distance <= segment || i == _positions.Count - 1)
                    return Vector3.Lerp(from, to, segment < 0.001f ? 0f : distance / segment);

                distance -= segment;
            }

            return _positions[0];
        }

        private void OnDrawGizmos()
        {
            if (!CollectPositions())
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            for (int i = 0; i < _positions.Count; i++)
            {
                Vector3 from = _positions[i];
                Vector3 to = _positions[(i + 1) % _positions.Count];
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(from, 1.1f);
            }
        }
    }
}
