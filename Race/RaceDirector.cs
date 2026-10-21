using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using CarControllerwithShooting;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    public class RaceDirector : MonoBehaviour
    {
        public const int LapsRequired = 2;

        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject botPrefab;
        [SerializeField] private CharacterCatalog catalog;
        [SerializeField] private TrailCatalog trailCatalog;
        [FormerlySerializedAs("statsConfig")]
        [SerializeField] private CarStatsConfig playerStats;
        [SerializeField] private CarStatsConfig botStats;
        [SerializeField] private RaceRewardConfig rewardConfig;
        [SerializeField] private BotProfile[] botProfiles;
        [SerializeField] private int botCount = -1;
        [SerializeField] private int botUpgradeSpread = 2;
        [SerializeField] private Transform startPlaces;
        [SerializeField] private TextMeshProUGUI centerLabel;
        [SerializeField] private LocalizedString centerLabelText;
        [SerializeField] private TextMeshProUGUI lapLabel;
        [SerializeField] private LocalizedString lapLabelText;
        [SerializeField] private Button restartButton;
        [SerializeField] private RaceResultScreen raceResultScreen;

        private readonly List<RaceRacer> _racers = new List<RaceRacer>();
        private GameObject _playerCar;
        private bool _waitingForTrail;
        private bool _raceRunning;
        private bool _settled;
        private bool _acceptsKills;
        private int _laps;
        private int _fieldCount;
        private int _resultPlace;
        private BotRaceSession _probe;

        public static RaceDirector Instance { get; private set; }

        public bool RaceRunning => _raceRunning;
        public bool Settled => _settled;
        public bool AcceptsKills => _acceptsKills;
        public IReadOnlyList<RaceRacer> Racers => _racers;

        private void Awake()
        {
            Instance = this;
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
                restartButton.gameObject.SetActive(false);
            }

            _probe = BotRaceSession.Active;
            List<Transform> slots = CollectSlots();
            bool botsOnly = _probe != null && _probe.BotsOnly;
            int playerSlot = botsOnly || slots.Count == 0 ? -1 : Random.Range(0, slots.Count);
            CharacterConfig playerCharacter = ResolvePlayerCharacter();
            if (!botsOnly)
                SpawnPlayer(slots, playerSlot, playerCharacter);

            GameObject reservedSkin = playerCharacter != null ? playerCharacter.RallyCar : FindPlaceholderSkin();
            SpawnBots(slots, playerSlot, reservedSkin);
            _fieldCount = _racers.Count;
            HoldAll();
            SetLapLabel();
            if (_probe != null)
            {
                BotRaceProbe probe = gameObject.GetComponent<BotRaceProbe>();
                if (probe == null)
                    probe = gameObject.AddComponent<BotRaceProbe>();

                probe.Begin(_probe);
            }
        }

        private void OnDestroy()
        {
            if (_waitingForTrail && TrailProgress.Instance != null)
                TrailProgress.Instance.Changed -= HandleTrailProgressChanged;

            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            PlatformSignals.LoadingStopped();
            StartCoroutine(Countdown());
        }

        public void Register(RaceRacer racer)
        {
            if (racer != null && !_racers.Contains(racer))
                _racers.Add(racer);
        }

        public void Unregister(RaceRacer racer)
        {
            _racers.Remove(racer);
        }

        public void OnGateEntered(LapGate.GateType type, CarController car, Vector3 gateForward)
        {
            if (car == null)
                return;

            RaceRacer racer = car.GetComponent<RaceRacer>();
            if (racer != null)
                racer.EnterGate(type, gateForward);
        }

        public void OnGateExited(LapGate.GateType type, CarController car)
        {
            if (car == null)
                return;

            RaceRacer racer = car.GetComponent<RaceRacer>();
            if (racer != null)
                racer.ExitGate(type);
        }

        public void OnPlayerLap(int completedLaps)
        {
            _laps = completedLaps;
            SetLapLabel();
        }

        public void FinishRace()
        {
            SettlePlayer();
        }

        public void OnRacerEliminated(RaceRacer racer)
        {
            if (!_raceRunning || _settled)
                return;

            if (racer != null && racer.IsPlayer)
            {
                SettlePlayer();
                return;
            }

            if (CompetingCount() <= 1)
                SettlePlayer();
        }

        private List<Transform> CollectSlots()
        {
            List<Transform> slots = new List<Transform>();
            if (startPlaces == null)
                return slots;

            for (int i = 0; i < startPlaces.childCount; i++)
            {
                Transform child = startPlaces.GetChild(i);
                if (child.GetComponent<LapGate>() != null)
                    continue;

                slots.Add(child);
            }

            return slots;
        }

        private void SpawnPlayer(List<Transform> slots, int playerSlot, CharacterConfig playerCharacter)
        {
            if (playerPrefab == null || playerSlot < 0)
            {
                Debug.LogError("RaceDirector is missing the player prefab or start places.", this);
                return;
            }

            Transform place = slots[playerSlot];
            GameObject player = Instantiate(playerPrefab, place.position, place.rotation);
            if (playerStats != null)
                playerStats.ApplyTo(player);
            else
                Debug.LogWarning("No player car stats config.", this);

            GameObject playerSkin = playerCharacter != null ? playerCharacter.RallyCar : null;
            CarSkinApplier.Apply(player, playerSkin);
            ApplyPlayerTrail(player);
            CarController car = player.GetComponentInChildren<CarController>(true);
            BindRacer(car, playerCharacter, true);

            Camera sceneCamera = Camera.main;
            if (sceneCamera != null)
                sceneCamera.gameObject.SetActive(false);
        }

        private void SpawnBots(List<Transform> slots, int playerSlot, GameObject reservedSkin)
        {
            if (botPrefab == null || slots.Count == 0)
                return;

            if (botStats == null)
                Debug.LogWarning("No bot car stats config.", this);

            int wanted = botCount < 0 ? Mathf.Max(0, slots.Count - 1) : botCount;
            BotCharacterPicker characters = new BotCharacterPicker(catalog, reservedSkin);
            int spawned = 0;
            for (int i = 0; i < slots.Count && spawned < wanted; i++)
            {
                if (i == playerSlot)
                    continue;

                Transform place = slots[i];
                GameObject bot = Instantiate(botPrefab, place.position, place.rotation);
                if (botStats != null)
                    botStats.ApplyBotLoadout(bot, botUpgradeSpread);

                CharacterConfig botCharacter = characters.Next();
                CarSkinApplier.Apply(bot, botCharacter != null ? botCharacter.RallyCar : null);
                ApplyBotTrail(bot);
                spawned++;
                bot.name = "Bot " + spawned;
                CarController car = PrepareBot(bot);
                BindRacer(car, botCharacter, false);
            }
        }

        private static void BindRacer(CarController car, CharacterConfig character, bool isPlayer)
        {
            if (car == null)
                return;

            RaceRacer racer = car.GetComponent<RaceRacer>();
            if (racer == null)
                racer = car.gameObject.AddComponent<RaceRacer>();

            racer.Bind(
                character != null ? character.Preview : null,
                character != null ? character.LowHealthPreview : null,
                character != null ? character.DisplayName : null,
                isPlayer);
        }

        private void ApplyPlayerTrail(GameObject player)
        {
            _playerCar = player;
            TrailProgress progress = TrailProgress.Instance;
            if (progress != null && !progress.IsReady)
            {
                if (!_waitingForTrail)
                {
                    _waitingForTrail = true;
                    progress.Changed += HandleTrailProgressChanged;
                }

                return;
            }

            ApplyEquippedTrail();
        }

        private void HandleTrailProgressChanged()
        {
            if (TrailProgress.Instance == null || !TrailProgress.Instance.IsReady)
                return;

            TrailProgress.Instance.Changed -= HandleTrailProgressChanged;
            _waitingForTrail = false;
            ApplyEquippedTrail();
        }

        private void ApplyEquippedTrail()
        {
            if (_playerCar == null)
                return;

            TrailConfig equipped = trailCatalog != null ? trailCatalog.Equipped(TrailProgress.Instance) : null;
            TrailApplier.Apply(_playerCar, equipped != null ? equipped.Prefab : null);
        }

        private void ApplyBotTrail(GameObject bot)
        {
            TrailConfig trail = trailCatalog != null ? trailCatalog.RandomTrail() : null;
            TrailApplier.Apply(bot, trail != null ? trail.Prefab : null);
        }

        private CharacterConfig ResolvePlayerCharacter()
        {
            if (catalog == null)
            {
                Debug.LogWarning("No character catalog.", this);
                return null;
            }

            string selectedId = CharacterProgress.Instance != null ? CharacterProgress.Instance.SelectedId : null;
            return catalog.Find(selectedId);
        }

        private GameObject FindPlaceholderSkin()
        {
            if (catalog == null)
                return null;

            IReadOnlyList<CharacterConfig> characters = catalog.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                GameObject skin = characters[i] != null ? characters[i].RallyCar : null;
                if (skin != null && skin.name == "Rally_Car_1")
                    return skin;
            }

            return null;
        }

        private CarController PrepareBot(GameObject bot)
        {
            CarController car = bot.GetComponentInChildren<CarController>(true);
            if (car == null)
                return null;

            GameObject carObject = car.gameObject;
            if (carObject.GetComponent<RaceRacer>() == null)
                carObject.AddComponent<RaceRacer>();

            BotBrain brain = carObject.GetComponent<BotBrain>();
            if (brain == null)
                brain = carObject.AddComponent<BotBrain>();

            if (carObject.GetComponent<BotDriver>() == null)
                carObject.AddComponent<BotDriver>();

            BehaviorTree tree = carObject.GetComponent<BehaviorTree>();
            if (tree == null)
                tree = carObject.AddComponent<BehaviorTree>();

            if (botProfiles != null && botProfiles.Length > 0)
                brain.SetProfile(botProfiles[Random.Range(0, botProfiles.Length)]);

            if (_probe != null)
                brain.SetDriveOnly(_probe.DriveOnly);

            RaceBotTree.Ensure(tree);
            return car;
        }

        private IEnumerator Countdown()
        {
            if (_probe != null)
            {
                Time.timeScale = _probe.TimeScale;
                ShowCenter(centerLabelText.GetLocalizedString());
                ReleaseAll();
                yield return null;
                if (_raceRunning)
                    HideCenter();

                yield break;
            }

            string[] steps = { "3", "2", "1", centerLabelText.GetLocalizedString() };
            for (int i = 0; i < steps.Length; i++)
            {
                ShowCenter(steps[i]);
                if (steps[i] == steps[steps.Length - 1])
                    ReleaseAll();

                yield return new WaitForSeconds(1f);
            }

            if (_raceRunning)
                HideCenter();
        }

        public void SettlePlayer()
        {
            if (_probe != null && _probe.BotsOnly)
                return;

            if (_settled)
                return;

            _settled = true;
            _raceRunning = false;
            HoldAll();
            GameAudio.SetWorldPaused(true);

            _resultPlace = 0;
            if (RaceOrderBar.Instance == null || !RaceOrderBar.Instance.TryGetPlayerPlace(out _resultPlace))
                Debug.LogError("Race reward could not resolve the player place.", this);

            int kills = 0;
            RaceRacer player = FindPlayer();
            if (player != null)
                kills = player.Kills;

            RaceRewardBreakdown rewards = default;
            if (rewardConfig == null)
            {
                Debug.LogError("RaceDirector has no race reward config.", this);
            }
            else
            {
                if (!rewardConfig.ContainsPlace(_resultPlace))
                    Debug.LogError($"Race reward has no payout for place {_resultPlace}.", this);

                rewards = rewardConfig.EvaluateBreakdown(_resultPlace, kills);
                Debug.Log($"Race reward: place {_resultPlace}, kills {kills}, trophies {rewards.Trophies}, coins {rewards.Coins}.", this);
                Grant(rewards.Trophies, rewards.Coins);
            }

            bool eliminated = player != null && player.IsEliminated;
            PlatformSignals.GameplayEnded(eliminated);
            PresentResult(rewards, eliminated);
        }

        public void AccountExtraKill()
        {
            if (!_settled || rewardConfig == null)
                return;

            int trophies = rewardConfig.TrophiesPerKill;
            int coins = rewardConfig.CoinsPerKill;
            if (trophies == 0 && coins == 0)
                return;

            Grant(trophies, coins);
        }

        private void HoldAll()
        {
            for (int i = 0; i < _racers.Count; i++)
            {
                if (_racers[i] != null)
                    _racers[i].Hold();
            }
        }

        private void ReleaseAll()
        {
            _raceRunning = true;
            _acceptsKills = true;
            PlatformSignals.GameplayStarted();
            for (int i = 0; i < _racers.Count; i++)
            {
                if (_racers[i] != null)
                    _racers[i].Release();
            }
        }

        private void ShowCenter(string text)
        {
            if (centerLabel == null)
                return;

            centerLabel.gameObject.SetActive(true);
            centerLabel.text = text;
        }

        private void HideCenter()
        {
            if (centerLabel != null)
                centerLabel.gameObject.SetActive(false);
        }

        private int CompetingCount()
        {
            int count = 0;
            for (int i = 0; i < _racers.Count; i++)
            {
                RaceRacer racer = _racers[i];
                if (racer != null && !racer.IsEliminated)
                    count++;
            }

            return count;
        }

        private int FieldCount()
        {
            int count = _fieldCount;
            if (RaceOrderBar.Instance != null && RaceOrderBar.Instance.FieldCount > count)
                count = RaceOrderBar.Instance.FieldCount;

            return count > 0 ? count : _racers.Count;
        }

        private void PresentResult(RaceRewardBreakdown rewards, bool eliminated)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            HideCenter();

            bool victory = !eliminated && _resultPlace >= 1 && _resultPlace <= 4;
            
            if (raceResultScreen != null && raceResultScreen.Show(victory, rewards, _resultPlace, FieldCount()))
                return;

            Debug.LogError("Race result screen could not be shown.", this);
            if (restartButton != null)
                restartButton.gameObject.SetActive(true);
        }

        private void Grant(int trophies, int coins)
        {
            if (trophies != 0)
            {
                if (TrophyProgress.Instance != null)
                    TrophyProgress.Instance.Apply(trophies);
                else
                    Debug.LogError("Trophy progress is missing.", this);
            }

            if (coins != 0)
            {
                if (Player.Instance != null)
                    Player.Instance.ApplyBalance(coins);
                else
                    Debug.LogError("Player is missing.", this);
            }
        }

        private RaceRacer FindPlayer()
        {
            for (int i = 0; i < _racers.Count; i++)
            {
                RaceRacer racer = _racers[i];
                if (racer != null && racer.IsPlayer)
                    return racer;
            }

            return null;
        }

        private void SetLapLabel()
        {
            if (lapLabel == null)
                return;

            int shown = Mathf.Min(_laps + 1, LapsRequired);
            lapLabel.text = lapLabelText.GetLocalizedString(shown, LapsRequired);
        }

        public void Restart()
        {
            if (!_settled)
                PlatformSignals.GameplayEnded(true);

            PlatformSignals.LoadingStarted();
            Time.timeScale = PlatformHost.IsHostPaused ? 0f : 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private class BotCharacterPicker
        {
            private readonly List<CharacterConfig> _unique = new List<CharacterConfig>();
            private int _index;
            private bool _warnedRepeat;

            public BotCharacterPicker(CharacterCatalog source, GameObject reservedSkin)
            {
                if (source == null)
                    return;

                HashSet<GameObject> seen = new HashSet<GameObject>();
                if (reservedSkin != null)
                    seen.Add(reservedSkin);

                IReadOnlyList<CharacterConfig> characters = source.Characters;
                for (int i = 0; i < characters.Count; i++)
                {
                    CharacterConfig config = characters[i];
                    GameObject skin = config != null ? config.RallyCar : null;
                    if (skin == null || !seen.Add(skin))
                        continue;

                    _unique.Add(config);
                }

                for (int i = _unique.Count - 1; i > 0; i--)
                {
                    int swap = Random.Range(0, i + 1);
                    CharacterConfig current = _unique[i];
                    _unique[i] = _unique[swap];
                    _unique[swap] = current;
                }
            }

            public CharacterConfig Next()
            {
                if (_unique.Count == 0)
                    return null;

                if (_index < _unique.Count)
                    return _unique[_index++];

                if (!_warnedRepeat)
                {
                    _warnedRepeat = true;
                    Debug.LogWarning("Not enough unique car skins.");
                }

                CharacterConfig config = _unique[_index % _unique.Count];
                _index++;
                return config;
            }
        }
    }
}
