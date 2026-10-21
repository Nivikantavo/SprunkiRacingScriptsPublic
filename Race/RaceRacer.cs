using CarControllerwithShooting;
using UnityEngine;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    [RequireComponent(typeof(CarController))]
    public class RaceRacer : MonoBehaviour
    {
        private CarController _car;
        private Rigidbody _body;
        private RigidbodyConstraints _driveConstraints;
        private PlayerInput _input;
        private bool _held;
        private bool _sectorPassed;
        private int _finishContacts;

        private static int _nextDeathOrder;

        private readonly LocalizedTextBinding _nameText = new LocalizedTextBinding();
        private float _along;
        private bool _hasProgress;
        private bool _seenAlive;

        public CarController Car => _car;
        public int Laps { get; private set; }
        public bool Finished { get; private set; }
        public Sprite Preview { get; private set; }
        public Sprite LowHealthPreview { get; private set; }
        public Sprite CurrentPreview => SelectPreview(Preview, LowHealthPreview, _car != null ? _car.Health : 0, _car != null ? _car.MaxHealth : 0);
        public string DisplayName { get; private set; }
        public bool IsPlayer { get; private set; }
        public float RaceDistance { get; private set; }
        public bool IsEliminated { get; private set; }
        public int DeathOrder { get; private set; } = -1;
        public int Kills { get; private set; }

        public static void BeginRace()
        {
            _nextDeathOrder = 0;
        }

        private void Awake()
        {
            _car = GetComponent<CarController>();
            _body = GetComponent<Rigidbody>();
            if (_body != null)
                _driveConstraints = _body.constraints;

            _input = GetComponent<PlayerInput>();
            if (GetComponent<CarRecovery>() == null)
                gameObject.AddComponent<CarRecovery>();
        }

        private void OnEnable()
        {
            if (_car != null)
                _car.Died += HandleDied;

            if (RaceDirector.Instance != null)
                RaceDirector.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (_car != null)
                _car.Died -= HandleDied;

            if (RaceDirector.Instance != null)
                RaceDirector.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            _nameText.Clear();
        }

        private void Update()
        {
            if (IsEliminated || _car == null)
                return;

            if (_car.IsAlive)
            {
                _seenAlive = true;
                return;
            }

            if (!_seenAlive)
                return;

            MarkEliminated();
        }

        public void AddKill()
        {
            Kills++;
            if (RaceDirector.Instance != null)
                RaceDirector.Instance.AccountExtraKill();
        }

        private void HandleDied(CarController attacker)
        {
            CreditKill(attacker);
            if (!_seenAlive || IsEliminated)
                return;

            MarkEliminated();
        }

        private void CreditKill(CarController attacker)
        {
            if (attacker == null || attacker == _car || !attacker.IsPlayer)
                return;

            if (RaceDirector.Instance == null || !RaceDirector.Instance.AcceptsKills)
                return;

            RaceRacer killer = attacker.GetComponent<RaceRacer>();
            if (killer != null)
                killer.AddKill();
        }

        private void MarkEliminated()
        {
            if (IsEliminated)
                return;

            IsEliminated = true;
            DeathOrder = _nextDeathOrder++;
            if (RaceDirector.Instance != null)
                RaceDirector.Instance.OnRacerEliminated(this);
        }

        private void FixedUpdate()
        {
            if (!_held || _body == null)
                return;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        public void Hold()
        {
            _held = true;
            if (_input != null)
                _input.ControlsEnabled = false;

            if (_body == null)
                return;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.constraints = RigidbodyConstraints.FreezeAll;
        }

        public static Sprite SelectPreview(Sprite preview, Sprite lowHealthPreview, int health, int maxHealth)
        {
            if (lowHealthPreview != null && maxHealth > 0 && (long)health * 2 < maxHealth)
                return lowHealthPreview;

            return preview;
        }

        public void Bind(Sprite preview, Sprite lowHealthPreview, LocalizedString displayName, bool isPlayer)
        {
            Preview = preview;
            LowHealthPreview = lowHealthPreview;
            IsPlayer = isPlayer;
            _nameText.Set(displayName, ApplyName);
        }

        private void ApplyName(string value)
        {
            DisplayName = string.IsNullOrEmpty(value) ? "Player" : value;
            GameCanvas canvas = GameCanvas.Instance;
            if (canvas == null)
                return;

            if (IsPlayer)
                canvas.SetPlayerIdentity(DisplayName);

            canvas.RefreshTargetName(this);
        }

        public void SeedProgress(float distance, float along)
        {
            RaceDistance = distance;
            _along = along;
            _hasProgress = true;
        }

        public void AdvanceProgress(float along, float length)
        {
            if (!_hasProgress || length < 0.01f)
            {
                SeedProgress(along, along);
                return;
            }

            float delta = Mathf.Repeat(along - _along, length);
            if (delta > length * 0.5f)
                delta -= length;

            RaceDistance += delta;
            _along = along;
        }

        public void Release()
        {
            _held = false;
            if (_body != null)
            {
                _body.constraints = _driveConstraints;
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            if (_input != null && _input.enabled)
                _input.ControlsEnabled = true;
        }

        public void EnterGate(LapGate.GateType type, Vector3 gateForward)
        {
            if (Finished || _body == null || RaceDirector.Instance == null || !RaceDirector.Instance.RaceRunning)
                return;

            if (type == LapGate.GateType.Sector)
            {
                _sectorPassed = true;
                return;
            }

            _finishContacts++;
            if (_finishContacts > 1 || !_sectorPassed)
                return;

            if (Vector3.Dot(_body.linearVelocity, gateForward) <= 0f)
                return;

            _sectorPassed = false;
            Laps++;
            if (_car != null && _car.IsPlayer)
                RaceDirector.Instance.OnPlayerLap(Laps);

            if (Laps >= RaceDirector.LapsRequired)
            {
                Finished = true;
                if (_car != null && _car.IsPlayer)
                    RaceDirector.Instance.FinishRace();
            }
        }

        public void ExitGate(LapGate.GateType type)
        {
            if (type != LapGate.GateType.Finish)
                return;

            _finishContacts = Mathf.Max(0, _finishContacts - 1);
        }
    }
}
