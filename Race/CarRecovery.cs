using CarControllerwithShooting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SprunkiRacing
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(CarController))]
    public class CarRecovery : MonoBehaviour
    {
        const float FlipUpDot = 0.4f;
        const float FlipSeconds = 1.5f;
        const float StillSeconds = 6f;
        const float StillRadius = 8f;
        const float SamePointSeconds = 12f;
        const float ResetCooldown = 0.75f;
        const float GroundClearance = 0.12f;

        CarController _car;
        Rigidbody _body;
        RaceRacer _racer;
        BotBrain _brain;
        float _alongHint = -1f;
        float _anchorAlong = -1f;
        int _pointIndex;
        bool _hasPoint;
        bool _pointAdvanced;
        float _flipTime;
        float _stillTime;
        float _samePointTime;
        float _cooldown;
        Vector3 _stillAnchor;
        readonly RaycastHit[] _hits = new RaycastHit[24];

        public static CarRecovery PlayerInstance { get; private set; }

        public bool IsFlipped => Vector3.Dot(transform.up, Vector3.up) < FlipUpDot;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _body = GetComponent<Rigidbody>();
            CacheLinks();
        }

        void CacheLinks()
        {
            if (_racer == null)
                _racer = GetComponent<RaceRacer>();

            if (_brain == null)
                _brain = GetComponent<BotBrain>();
        }

        void OnEnable()
        {
            if (_car != null && _car.IsPlayer)
                PlayerInstance = this;
        }

        void OnDisable()
        {
            if (PlayerInstance == this)
                PlayerInstance = null;
        }

        void Update()
        {
            CacheLinks();
            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            TrackPoint();
            if (_car != null && _car.IsPlayer)
                PollPlayer();
            else
                PollBot();
        }

        public bool TryReset()
        {
            CacheLinks();
            if (!CanReset() || RacePath.Instance == null)
                return false;

            if (!RacePath.Instance.TryGetRespawn(_pointIndex, out Vector3 point, out Vector3 forward, out float along))
                return false;

            if (!Place(point, forward))
                return false;

            _alongHint = along;
            _anchorAlong = along;
            _cooldown = ResetCooldown;
            _flipTime = 0f;
            _stillTime = 0f;
            _samePointTime = 0f;
            _stillAnchor = transform.position;
            if (_brain != null)
                _brain.NotifyReset(along);

            return true;
        }

        void PollPlayer()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                TryReset();
        }

        void PollBot()
        {
            if (_car == null || !_car.IsAlive || _racer == null || _racer.IsEliminated)
                return;

            if (RaceDirector.Instance != null && !RaceDirector.Instance.RaceRunning)
            {
                _flipTime = 0f;
                _stillTime = 0f;
                _samePointTime = 0f;
                return;
            }

            if (Vector3.Dot(transform.up, Vector3.up) < FlipUpDot)
                _flipTime += Time.deltaTime;
            else
                _flipTime = 0f;

            if (_pointAdvanced || MadePathProgress())
            {
                _samePointTime = 0f;
                _anchorAlong = _alongHint;
                _stillTime = 0f;
                _stillAnchor = transform.position;
            }
            else
            {
                _samePointTime += Time.deltaTime;
                Vector3 moved = transform.position - _stillAnchor;
                moved.y = 0f;
                if (moved.magnitude > StillRadius)
                {
                    _stillAnchor = transform.position;
                    _stillTime = 0f;
                }
                else
                {
                    _stillTime += Time.deltaTime;
                }
            }

            if (_flipTime >= FlipSeconds || _stillTime >= StillSeconds || _samePointTime >= SamePointSeconds)
                TryReset();
        }

        void TrackPoint()
        {
            _pointAdvanced = false;
            RacePath path = RacePath.Instance;
            if (path == null || _car == null || !_car.IsAlive)
                return;

            if (!_hasPoint)
            {
                if (!path.TryGetNearestPointIndex(transform.position, out _pointIndex))
                    return;

                _hasPoint = true;
                _stillAnchor = transform.position;
                if (path.TryGetRespawn(_pointIndex, out _, out _, out float along))
                {
                    _alongHint = along;
                    _anchorAlong = along;
                }

                return;
            }

            if (_alongHint < 0f)
            {
                if (!path.TryGetProgress(transform.position, out _alongHint))
                    return;
            }
            else if (!path.TryProject(transform.position, ref _alongHint, 48f, out _))
            {
                return;
            }

            if (!path.TryGetPointIndex(_alongHint, out int candidate))
                return;

            int count = path.PointCount;
            if (count < 2)
                return;

            int steps = (candidate - _pointIndex + count) % count;
            if (steps <= 0 || steps > 6)
                return;

            _pointIndex = candidate;
            _pointAdvanced = true;
        }

        bool CanReset()
        {
            if (_cooldown > 0f || !_hasPoint || _car == null || !_car.IsAlive || _body == null)
                return false;

            if (_racer != null && (_racer.IsEliminated || _racer.Finished))
                return false;

            if (RaceDirector.Instance != null && !RaceDirector.Instance.RaceRunning)
                return false;

            if (_car.IsPlayer && GameCanvas.Instance != null && GameCanvas.Instance.isPaused)
                return false;

            return true;
        }

        bool MadePathProgress()
        {
            RacePath path = RacePath.Instance;
            if (path == null || _alongHint < 0f || _anchorAlong < 0f)
                return false;

            float length = path.Length;
            if (length < 1f)
                return false;

            float gained = Mathf.Repeat(_alongHint - _anchorAlong, length);
            return gained > 15f && gained < length * 0.5f;
        }

        bool Place(Vector3 pathPoint, Vector3 heading)
        {
            Vector3 up = Vector3.up;
            Vector3 ground = pathPoint;
            if (TryGround(pathPoint, out RaycastHit groundHit))
            {
                ground = groundHit.point;
                if (groundHit.normal.y > 0.65f)
                    up = groundHit.normal;
            }

            Vector3 forward = Vector3.ProjectOnPlane(heading, up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < 0.0001f)
                return false;

            Quaternion rotation = Quaternion.LookRotation(forward.normalized, up);
            Vector3 position = ground + up * (GroundClearance + WheelLift());

            bool wasKinematic = _body.isKinematic;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
            _body.position = position;
            _body.rotation = rotation;
            _body.isKinematic = wasKinematic;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            ReleaseWheels();
            return true;
        }

        float WheelLift()
        {
            if (_car.wheelColliders == null)
                return 0.6f;

            float lowest = 0f;
            bool any = false;
            for (int i = 0; i < _car.wheelColliders.Length; i++)
            {
                WheelCollider wheel = _car.wheelColliders[i];
                if (wheel == null)
                    continue;

                Vector3 local = transform.InverseTransformPoint(wheel.transform.position);
                float drop = wheel.radius + wheel.suspensionDistance * (1f - Mathf.Clamp01(wheel.suspensionSpring.targetPosition));
                float bottom = local.y - drop;
                if (!any || bottom < lowest)
                    lowest = bottom;

                any = true;
            }

            return any ? -lowest : 0.6f;
        }

        bool TryGround(Vector3 point, out RaycastHit groundHit)
        {
            groundHit = default;
            Vector3 origin = point + Vector3.up * 8f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 24f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || hit.normal.y < 0.45f)
                    continue;

                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                if (hit.collider.GetComponentInParent<CarController>() != null)
                    continue;

                float delta = Mathf.Abs(hit.point.y - point.y);
                if (delta >= best)
                    continue;

                best = delta;
                groundHit = hit;
                found = true;
            }

            return found;
        }

        void ReleaseWheels()
        {
            if (_car.wheelColliders == null)
                return;

            for (int i = 0; i < _car.wheelColliders.Length; i++)
            {
                WheelCollider wheel = _car.wheelColliders[i];
                if (wheel == null)
                    continue;

                wheel.motorTorque = 0f;
                wheel.brakeTorque = 0f;
                wheel.steerAngle = 0f;
            }
        }
    }
}
