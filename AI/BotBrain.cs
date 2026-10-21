using BehaviorDesigner.Runtime;
using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    public enum BotDriveMode
    {
        Race,
        SeekPickup,
        Recover
    }

    [DefaultExecutionOrder(-100)]
    public class BotBrain : MonoBehaviour
    {
        [SerializeField] private BotProfile profile;
        [SerializeField] private float cornerLateralAccel = 32f;
        [SerializeField] private float cornerMinSpeed = 48f;
        [SerializeField] private float coastOverSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float cornerBrake = 0.45f;
        [SerializeField] private float laneHalfWidth = 5f;
        [SerializeField] private float laneDriftSpeed = 1.5f;

        private CarController _car;
        private GunController _gun;
        private Gasoline _fuel;
        private float _stuckTime;
        private float _stuckWindow;
        private Vector3 _stuckAnchor;
        private float _nextDecisionTime;
        private float _nextFireTime;
        private bool _nextShotIsMissile;
        private int _burstLeft;
        private float _pathProgress = -1f;
        private float _easeUntil;
        private Vector3 _recoverOrigin;
        private Rigidbody _body;
        private float _laneTarget;
        private float _laneVelocity;

        public BotProfile Profile => profile != null ? profile : BotProfile.Fallback;
        public CarController Car => _car;
        public GunController Gun => _gun;
        public Gasoline Fuel => _fuel;
        public BotDriveMode Mode { get; private set; }
        public Vector3 DrivePoint { get; private set; }
        public float Steer { get; private set; }
        public float Accel { get; private set; }
        public float Brake { get; private set; }
        public bool Nitro { get; private set; }
        public CarController Target { get; private set; }
        public CollactableScript Pickup { get; private set; }
        public float Lane { get; set; }
        public float CornerLateralAccel => cornerLateralAccel;
        public float CornerMinSpeed => cornerMinSpeed;
        public float CoastOverSpeed => coastOverSpeed;
        public float CornerBrake => cornerBrake;
        public float LaneHalfWidth => laneHalfWidth;
        public float RecoverSteer { get; private set; }
        public float PathProgress => _pathProgress;
        public bool DriveOnly { get; private set; }
        public int RecoverCount { get; private set; }
        public Vector3 LastRecoverPosition { get; private set; }
        public float ResetTime { get; private set; }
        public bool EaseCorner => Time.time < _easeUntil;
        public bool IsStuck => _stuckTime >= 1f;

        private void Awake()
        {
            _car = GetComponent<CarController>();
            _gun = GetComponent<GunController>();
            _fuel = GetComponent<Gasoline>();
            _body = GetComponent<Rigidbody>();
            if (GetComponentInParent<CarSystemManager>() != null)
                return;

            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null)
                input.enabled = false;

            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
                cameras[i].gameObject.SetActive(false);

            AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].enabled = false;

            BehaviorTree tree = GetComponent<BehaviorTree>();
            if (tree != null)
                RaceBotTree.Ensure(tree);

            float half = Mathf.Max(0f, laneHalfWidth);
            Lane = Random.Range(-half, half);
            PickLaneTarget();
        }

        public void SetProfile(BotProfile value)
        {
            profile = value;
        }

        public void SetDriveOnly(bool value)
        {
            DriveOnly = value;
        }

        public void SetMode(BotDriveMode mode)
        {
            Mode = mode;
        }

        public void BeginRecover()
        {
            RecoverCount++;
            LastRecoverPosition = transform.position;
            SetMode(BotDriveMode.Recover);
            _stuckTime = 0f;
            _stuckWindow = 0f;
            _stuckAnchor = transform.position;
            Lane = Mathf.MoveTowards(Lane, 0f, 1.5f);
            _laneVelocity = 0f;
            SetPickup(null);
            _recoverOrigin = transform.position;
            UpdateRecoverSteer();
        }

        public void EndRecover()
        {
            _stuckTime = 0f;
            _stuckWindow = 0f;
            _stuckAnchor = transform.position;
            _easeUntil = Time.time + 1.4f;
            if (Mode == BotDriveMode.Recover)
                SetMode(BotDriveMode.Race);
        }

        public void NotifyReset(float pathProgress)
        {
            _stuckTime = 0f;
            _stuckWindow = 0f;
            _stuckAnchor = transform.position;
            _pathProgress = pathProgress;
            _easeUntil = Time.time + 1.4f;
            _laneVelocity = 0f;
            Lane = Mathf.MoveTowards(Lane, 0f, 2f);
            ResetTime = Time.time;
            SetPickup(null);
            if (Mode != BotDriveMode.Race)
                SetMode(BotDriveMode.Race);

            Vector3 ahead = transform.position + transform.forward * 16f;
            if (RacePath.Instance != null)
                ahead = RacePath.Instance.AimPoint(pathProgress, 16f, Lane);

            SetDrivePoint(ahead);
        }

        public void SetPathProgress(float value)
        {
            _pathProgress = value;
        }

        public void UpdateRecoverSteer()
        {
            if (!TryPathTangent(out Vector3 tangent))
            {
                RecoverSteer = ChooseOpenSteer();
                return;
            }

            float angle = Vector3.SignedAngle(FlatForward(), tangent, Vector3.up);
            float steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            if (MovingBackward())
                steer = -steer;

            RecoverSteer = steer;
        }

        public bool IsRecoverAligned()
        {
            if (!TryPathTangent(out Vector3 tangent))
                return false;

            Vector3 backed = transform.position - _recoverOrigin;
            backed.y = 0f;
            if (backed.magnitude < 1.5f)
                return false;

            if (Vector3.Angle(FlatForward(), tangent) > 60f)
                return false;

            Vector3 origin = transform.position + Vector3.up * 0.7f;
            return Clearance(origin, FlatForward()) > 3.5f;
        }

        public void AvoidWalls(ref float steer, ref float accel, ref float brake)
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f;
            Vector3 forward = FlatForward();
            float left = Clearance(origin, Quaternion.AngleAxis(-32f, Vector3.up) * forward);
            float right = Clearance(origin, Quaternion.AngleAxis(32f, Vector3.up) * forward);
            const float danger = 3.5f;
            if (left < danger)
                steer += 1f - left / danger;

            if (right < danger)
                steer -= 1f - right / danger;

            float nearest = Mathf.Min(left, right);
            if (nearest < danger)
            {
                float closeness = 1f - nearest / danger;
                brake = Mathf.Max(brake, closeness * 0.35f);
                accel = Mathf.Min(accel, 1f - closeness * 0.2f);
            }

            steer = Mathf.Clamp(steer, -1f, 1f);
        }

        public void SetDrivePoint(Vector3 point)
        {
            DrivePoint = point;
        }

        public void SetTarget(CarController target)
        {
            Target = target;
        }

        public void SetPickup(CollactableScript pickup)
        {
            Pickup = pickup;
        }

        public void SetDrive(float steer, float accel, float brake, bool nitro)
        {
            Steer = steer;
            Accel = accel;
            Brake = brake;
            Nitro = nitro;
        }

        public bool CanDecide()
        {
            return Time.time >= _nextDecisionTime;
        }

        public void MarkDecided()
        {
            _nextDecisionTime = Time.time + Profile.ReactionDelay;
        }

        public bool CanFire()
        {
            return Time.time >= _nextFireTime;
        }

        public bool NextShotIsMissile => _nextShotIsMissile;
        public int BurstLeft => _burstLeft;

        public void MarkFired()
        {
            _nextShotIsMissile = !_nextShotIsMissile;
            _nextFireTime = Time.time + Mathf.Max(0.35f, Profile.ShotDelay);
        }

        public void StartBurst()
        {
            int left = Profile.MachineGunBurst - 1;
            if (left <= 0)
            {
                _burstLeft = 0;
                MarkFired();
                return;
            }

            _burstLeft = left;
        }

        public void NoteBurstRound()
        {
            _burstLeft--;
            if (_burstLeft > 0)
                return;

            _burstLeft = 0;
            MarkFired();
        }

        public void ClearBurst()
        {
            _burstLeft = 0;
        }

        private void Update()
        {
            if (_car == null || !_car.IsAlive || Mode == BotDriveMode.Recover)
                return;

            if (RaceDirector.Instance != null && !RaceDirector.Instance.RaceRunning)
            {
                _stuckTime = 0f;
                _stuckWindow = 0f;
                _stuckAnchor = transform.position;
                return;
            }

            DriftLane();

            if (_stuckWindow <= 0f)
                _stuckAnchor = transform.position;

            _stuckWindow += Time.deltaTime;
            if (_stuckWindow < 0.45f)
                return;

            Vector3 moved = transform.position - _stuckAnchor;
            moved.y = 0f;
            bool againstWall = NoseAgainstWall();
            if (againstWall || moved.magnitude < 1.1f)
                _stuckTime += againstWall ? _stuckWindow * 1.5f : _stuckWindow;
            else
                _stuckTime = 0f;

            _stuckAnchor = transform.position;
            _stuckWindow = 0f;
        }

        private void DriftLane()
        {
            float half = Mathf.Max(0f, laneHalfWidth);
            if (half <= 0.01f)
            {
                Lane = 0f;
                _laneTarget = 0f;
                _laneVelocity = 0f;
                return;
            }

            Lane = Mathf.Clamp(Lane, -half, half);
            _laneTarget = Mathf.Clamp(_laneTarget, -half, half);
            if (laneDriftSpeed <= 0f)
                return;

            Lane = Mathf.SmoothDamp(Lane, _laneTarget, ref _laneVelocity, 0.8f, laneDriftSpeed);
            if (Mathf.Abs(Lane - _laneTarget) <= half * 0.08f)
                PickLaneTarget();
        }

        private void PickLaneTarget()
        {
            float half = Mathf.Max(0f, laneHalfWidth);
            if (half <= 0.01f)
            {
                _laneTarget = 0f;
                return;
            }

            float direction = Lane >= 0f ? -1f : 1f;
            _laneTarget = direction * Random.Range(half * 0.5f, half);
        }

        private bool TryPathTangent(out Vector3 tangent)
        {
            tangent = FlatForward();
            if (RacePath.Instance == null)
                return false;

            float progress = _pathProgress;
            if (!RacePath.Instance.TryProject(transform.position, ref progress, RacePath.SenseDistance(Profile.Intelligence), out RaceGuidance guidance) ||
                guidance.Tangent.sqrMagnitude < 0.01f)
                return false;

            _pathProgress = progress;
            tangent = guidance.Tangent;
            return true;
        }

        private bool MovingBackward()
        {
            if (_car != null && _car.CurrentSpeed <= 5f)
                return true;

            if (_body == null)
                return false;

            return Vector3.Angle(transform.forward, _body.linearVelocity) > 50f;
        }

        private bool NoseAgainstWall()
        {
            if (_body == null)
                return false;

            if (Vector3.Dot(_body.linearVelocity, FlatForward()) > 3f)
                return false;

            Vector3 origin = transform.position + Vector3.up * 0.7f;
            return Clearance(origin, FlatForward()) < 2.4f;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private float ChooseOpenSteer()
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f;
            float left = Clearance(origin, -transform.right);
            float right = Clearance(origin, transform.right);
            return right >= left ? 1f : -1f;
        }

        private float Clearance(Vector3 origin, Vector3 direction)
        {
            const float maxDistance = 12f;
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = maxDistance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    continue;

                if (hit.normal.y > 0.5f)
                    continue;

                if (hit.collider.GetComponentInParent<CarController>() != null)
                    continue;

                if (hit.distance < best)
                    best = hit.distance;
            }

            return best;
        }

        private readonly RaycastHit[] _hits = new RaycastHit[32];
    }
}
