using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SprunkiRacing
{
    public class BotRaceProbe : MonoBehaviour
    {
        private const int Buckets = 24;
        private const float SampleInterval = 0.25f;
        private const float SlowSpeed = 25f;
        private const float StuckWatchSeconds = 2f;

        private BotRaceSession _session;
        private readonly List<BotBrain> _bots = new List<BotBrain>();
        private readonly Dictionary<BotBrain, int> _seenRecovers = new Dictionary<BotBrain, int>();
        private readonly Dictionary<BotBrain, float> _slowSince = new Dictionary<BotBrain, float>();
        private readonly Dictionary<BotBrain, float> _lapMark = new Dictionary<BotBrain, float>();
        private readonly Dictionary<BotBrain, int> _seenLaps = new Dictionary<BotBrain, int>();
        private readonly Dictionary<BotBrain, List<float>> _lapTimes = new Dictionary<BotBrain, List<float>>();
        private readonly Dictionary<string, HeldBot> _held = new Dictionary<string, HeldBot>();
        private readonly List<Sample> _samples = new List<Sample>();
        private readonly List<RecoverMark> _recovers = new List<RecoverMark>();
        private readonly int[] _slowCounts = new int[Buckets];
        private readonly int[] _recoverCounts = new int[Buckets];
        private readonly float[] _speedSums = new float[Buckets];
        private readonly int[] _speedCounts = new int[Buckets];
        private Camera _camera;
        private RaceRacer _player;
        private float _playerLapMark;
        private int _playerSeenLaps = -1;
        private int _playerLaps;
        private float _playerDistance;
        private float _playerSpeed;
        private float _playerSpeedSum;
        private int _playerSpeedCount;
        private float _playerMaxSpeed;
        private readonly List<float> _playerLapTimes = new List<float>();
        private float _nextSample;
        private float _nextLive;
        private float _startedAt;
        private float _clockStartedAt;
        private bool _clockStarted;
        private bool _running;
        private bool _done;
        private bool _written;
        private string _reason = "running";
        private string _watchName = "-";
        private float _watchSpeed;

        public static string ReportDirectory
        {
            get
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(root, "Logs", "BotRace");
            }
        }

        public static string ReportPath => Path.Combine(ReportDirectory, "latest.json");

        public void Begin(BotRaceSession session)
        {
            _session = session;
            _running = session != null;
            _startedAt = Time.unscaledTime;
            _camera = Camera.main;
            if (_running)
                Time.timeScale = session.TimeScale;
        }

        private void OnDestroy()
        {
            if (_running && !_written)
                WriteReport(_done ? _reason : "stopped");
        }

        private void Update()
        {
            if (!_running || _done || _session == null)
                return;

            if (RaceDirector.Instance == null || !RaceDirector.Instance.RaceRunning)
                return;

            if (!_clockStarted)
            {
                _clockStarted = true;
                _clockStartedAt = Time.unscaledTime;
                _nextSample = Time.unscaledTime;
            }

            RefreshBots();
            WatchPlayer();
            WatchLaps();
            WatchRecovers();
            FollowCamera();
            WriteLive();

            if (Time.unscaledTime >= _nextSample)
            {
                _nextSample = Time.unscaledTime + SampleInterval;
                TakeSample();
            }

            if (!_session.BotsOnly)
            {
                if (_playerLaps >= _session.Laps)
                    Finish("player");
                else if (Time.unscaledTime - _clockStartedAt >= _session.MaxRealSeconds)
                    Finish("timeout");
            }
            else
            {
                int majority = Mathf.Max(1, (_bots.Count + 1) / 2);
                if (FinishedCount() >= majority)
                    Finish("laps");
                else if (Time.unscaledTime - _clockStartedAt >= _session.MaxRealSeconds)
                    Finish("timeout");
            }
        }

        private void OnGUI()
        {
            if (!_running || _session == null)
                return;

            int recovers = 0;
            for (int i = 0; i < _bots.Count; i++)
                recovers += _bots[i] != null ? _bots[i].RecoverCount : 0;

            GUI.Box(new Rect(12f, 12f, 460f, 68f), GUIContent.none);
            if (_session.BotsOnly)
            {
                GUI.Label(new Rect(20f, 16f, 440f, 24f),
                    $"Lap {LeaderLaps()}/{_session.Laps}   recovers {recovers}   {_reason}");
                GUI.Label(new Rect(20f, 40f, 440f, 24f),
                    $"Watch {_watchName}   {_watchSpeed:0} km/h");
            }
            else
            {
                GUI.Label(new Rect(20f, 16f, 440f, 24f),
                    $"You {_playerLaps}/{_session.Laps}   {_playerSpeed:0} km/h   {_reason}");
                GUI.Label(new Rect(20f, 40f, 440f, 24f),
                    $"Best bot lap {LeaderLaps()}/{_session.Laps}   recovers {recovers}");
            }
        }

        private void RefreshBots()
        {
            _bots.Clear();
            BotBrain[] found = FindObjectsByType<BotBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                BotBrain bot = found[i];
                if (bot == null || bot.Car == null)
                    continue;

                if (_session.DriveOnly)
                    bot.SetDriveOnly(true);

                _bots.Add(bot);
                if (!_lapMark.ContainsKey(bot))
                {
                    _lapMark[bot] = Time.time;
                    _seenLaps[bot] = bot.GetComponent<RaceRacer>() != null ? bot.GetComponent<RaceRacer>().Laps : 0;
                    _lapTimes[bot] = new List<float>();
                }
            }
        }

        private void WatchPlayer()
        {
            if (_player == null)
            {
                RaceRacer[] racers = FindObjectsByType<RaceRacer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < racers.Length; i++)
                {
                    if (racers[i] == null || !racers[i].IsPlayer)
                        continue;

                    _player = racers[i];
                    _playerLapMark = Time.time;
                    _playerSeenLaps = _player.Laps;
                    break;
                }
            }

            if (_player == null)
                return;

            _playerLaps = _player.Laps;
            _playerDistance = _player.RaceDistance;
            if (_player.Car != null)
                _playerSpeed = _player.Car.CurrentSpeed;

            if (_playerLaps <= _playerSeenLaps)
                return;

            _playerLapTimes.Add(Time.time - _playerLapMark);
            _playerLapMark = Time.time;
            _playerSeenLaps = _playerLaps;
        }

        private void WriteLive()
        {
            if (Time.unscaledTime < _nextLive)
                return;

            _nextLive = Time.unscaledTime + 2f;
            Directory.CreateDirectory(ReportDirectory);
            float average = _playerSpeedCount > 0 ? _playerSpeedSum / _playerSpeedCount : 0f;
            string text = string.Format(
                CultureInfo.InvariantCulture,
                "playerLaps={0}\nplayerDistance={1:0.0}\nplayerSpeed={2:0.0}\nplayerAverage={3:0.0}\nplayerMax={4:0.0}\nbotBestLaps={5}\n",
                _playerLaps,
                _playerDistance,
                _playerSpeed,
                average,
                _playerMaxSpeed,
                LeaderLaps());
            File.WriteAllText(Path.Combine(ReportDirectory, "live.txt"), text);
        }

        private void WatchLaps()
        {
            for (int i = 0; i < _bots.Count; i++)
            {
                BotBrain bot = _bots[i];
                RaceRacer racer = bot.GetComponent<RaceRacer>();
                int laps = racer != null ? racer.Laps : 0;
                _seenLaps.TryGetValue(bot, out int seen);
                if (laps <= seen)
                    continue;

                float marked = _lapMark.TryGetValue(bot, out float start) ? start : Time.time;
                _lapTimes[bot].Add(Time.time - marked);
                _lapMark[bot] = Time.time;
                _seenLaps[bot] = laps;
                RememberBot(bot);
            }
        }

        private void WatchRecovers()
        {
            for (int i = 0; i < _bots.Count; i++)
            {
                BotBrain bot = _bots[i];
                _seenRecovers.TryGetValue(bot, out int seen);
                if (bot.RecoverCount <= seen)
                    continue;

                _seenRecovers[bot] = bot.RecoverCount;
                Vector3 position = bot.LastRecoverPosition;
                int bucket = Bucket(bot);
                if (bucket >= 0)
                    _recoverCounts[bucket]++;

                string point = null;
                if (RacePath.Instance != null &&
                    RacePath.Instance.TryNearestPoint(position, out _, out string name, out _))
                    point = name;

                _recovers.Add(new RecoverMark(bot.name, position, point, bucket));
            }
        }

        private void TakeSample()
        {
            if (_player != null)
            {
                _playerSpeedSum += _playerSpeed;
                _playerSpeedCount++;
                if (_playerSpeed > _playerMaxSpeed)
                    _playerMaxSpeed = _playerSpeed;
            }

            for (int i = 0; i < _bots.Count; i++)
            {
                BotBrain bot = _bots[i];
                RaceRacer racer = bot.GetComponent<RaceRacer>();
                float speed = bot.Car != null ? bot.Car.CurrentSpeed : 0f;
                float progress = bot.PathProgress;
                float lateral = 0f;
                if (RacePath.Instance != null)
                {
                    float hint = progress;
                    if (RacePath.Instance.TryProject(bot.transform.position, ref hint, 56f, out RaceGuidance guidance))
                    {
                        progress = guidance.Progress;
                        lateral = guidance.Lateral;
                    }
                }

                int laps = racer != null ? racer.Laps : 0;
                _samples.Add(new Sample(bot.name, laps, racer != null ? racer.RaceDistance : 0f, speed, bot.Mode, bot.IsStuck, lateral, progress, bot.Steer, bot.Accel, bot.Brake, bot.transform.position));

                int bucket = Bucket(progress);
                if (bucket >= 0)
                {
                    _speedSums[bucket] += speed;
                    _speedCounts[bucket]++;
                    if (speed < SlowSpeed)
                        _slowCounts[bucket]++;
                }

                if (speed < SlowSpeed)
                {
                    if (!_slowSince.ContainsKey(bot))
                        _slowSince[bot] = Time.unscaledTime;
                }
                else
                {
                    _slowSince.Remove(bot);
                }

                RememberBot(bot);
            }
        }

        private void RememberBot(BotBrain bot)
        {
            if (bot == null)
                return;

            RaceRacer racer = bot.GetComponent<RaceRacer>();
            _lapTimes.TryGetValue(bot, out List<float> times);
            float[] copy = times == null || times.Count == 0 ? System.Array.Empty<float>() : times.ToArray();
            _held[bot.name] = new HeldBot(
                bot.name,
                racer != null ? racer.Laps : 0,
                racer != null ? racer.RaceDistance : 0f,
                bot.Car != null ? bot.Car.CurrentSpeed : 0f,
                bot.RecoverCount,
                copy);
        }

        private void FollowCamera()
        {
            BotBrain watch = StuckWatch();
            if (watch == null)
                watch = Leader();

            if (watch == null)
                return;

            _watchName = watch.name;
            _watchSpeed = watch.Car != null ? watch.Car.CurrentSpeed : 0f;
            if (!_session.BotsOnly || _camera == null)
                return;
            Vector3 forward = watch.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 desired = watch.transform.position - forward * 16f + Vector3.up * 7f;
            float follow = 1f - Mathf.Exp(-4f * Time.deltaTime);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, follow);
            Vector3 look = watch.transform.position + Vector3.up * 1.2f - _camera.transform.position;
            if (look.sqrMagnitude > 0.01f)
                _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, Quaternion.LookRotation(look), follow);
        }

        private BotBrain StuckWatch()
        {
            BotBrain worst = null;
            float worstTime = StuckWatchSeconds;
            for (int i = 0; i < _bots.Count; i++)
            {
                BotBrain bot = _bots[i];
                if (!_slowSince.TryGetValue(bot, out float since))
                    continue;

                float held = Time.unscaledTime - since;
                if (held < worstTime)
                    continue;

                worstTime = held;
                worst = bot;
            }

            return worst;
        }

        private BotBrain Leader()
        {
            BotBrain best = null;
            int bestLaps = -1;
            float bestDistance = float.MinValue;
            for (int i = 0; i < _bots.Count; i++)
            {
                BotBrain bot = _bots[i];
                RaceRacer racer = bot.GetComponent<RaceRacer>();
                int laps = racer != null ? racer.Laps : 0;
                float distance = racer != null ? racer.RaceDistance : 0f;
                if (laps < bestLaps)
                    continue;

                if (laps == bestLaps && distance <= bestDistance)
                    continue;

                best = bot;
                bestLaps = laps;
                bestDistance = distance;
            }

            return best;
        }

        private int LeaderLaps()
        {
            int best = 0;
            for (int i = 0; i < _bots.Count; i++)
            {
                RaceRacer racer = _bots[i].GetComponent<RaceRacer>();
                if (racer != null && racer.Laps > best)
                    best = racer.Laps;
            }

            return best;
        }

        private int Bucket(BotBrain bot)
        {
            return Bucket(bot.PathProgress);
        }

        private static int Bucket(float progress)
        {
            if (RacePath.Instance == null)
                return -1;

            float length = RacePath.Instance.Length;
            if (length < 0.01f || progress < 0f)
                return -1;

            int index = Mathf.FloorToInt(Mathf.Repeat(progress, length) / length * Buckets);
            return Mathf.Clamp(index, 0, Buckets - 1);
        }

        private void Finish(string reason)
        {
            if (_done)
                return;

            _done = true;
            _reason = reason;
            Time.timeScale = 1f;
            WriteReport(reason);
            Debug.Log($"Bot race probe {reason}. Report: {ReportPath}", this);
            if (_session.ExitPlayModeWhenDone)
                ExitPlay();
        }

        private static void ExitPlay()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void WriteReport(string reason)
        {
            if (_written)
                return;

            Directory.CreateDirectory(ReportDirectory);
            float realSeconds = _clockStarted ? Time.unscaledTime - _clockStartedAt : Time.unscaledTime - _startedAt;
            File.WriteAllText(ReportPath, BuildJson(reason, realSeconds));
            _written = true;
        }

        private string BuildJson(string reason, float realSeconds)
        {
            StringBuilder builder = new StringBuilder(1 << 16);
            builder.Append("{\n");
            Append(builder, "reason", reason, 1, true);
            Append(builder, "realSeconds", realSeconds, 1, true);
            Append(builder, "timeScale", _session != null ? _session.TimeScale : 1f, 1, true);
            Append(builder, "botsOnly", _session != null && _session.BotsOnly, 1, true);
            Append(builder, "driveOnly", _session != null && _session.DriveOnly, 1, true);
            Append(builder, "botCount", _bots.Count, 1, true);
            Append(builder, "finishedCount", FinishedCount(), 1, true);
            Append(builder, "recoverCount", _recovers.Count, 1, true);
            AppendPlayer(builder);
            List<HeldBot> held = new List<HeldBot>(_held.Values);
            builder.Append("  \"bots\": [\n");
            for (int i = 0; i < held.Count; i++)
                AppendBot(builder, held[i], i == held.Count - 1);

            builder.Append("  ],\n");
            builder.Append("  \"stalls\": [\n");
            AppendStalls(builder);
            builder.Append("  ],\n");
            builder.Append("  \"recovers\": [\n");
            for (int i = 0; i < _recovers.Count; i++)
                AppendRecover(builder, _recovers[i], i == _recovers.Count - 1);

            builder.Append("  ],\n");
            builder.Append("  \"samples\": [\n");
            for (int i = 0; i < _samples.Count; i++)
                AppendSample(builder, _samples[i], i == _samples.Count - 1);

            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        private void AppendPlayer(StringBuilder builder)
        {
            builder.Append("  \"player\": {");
            AppendInline(builder, "laps", _playerLaps);
            builder.Append(", ");
            AppendInline(builder, "finished", _session != null && _playerLaps >= _session.Laps);
            builder.Append(", ");
            AppendInline(builder, "distance", _playerDistance);
            builder.Append(", ");
            AppendInline(builder, "speed", _playerSpeed);
            builder.Append(", ");
            AppendInline(builder, "averageSpeed", _playerSpeedCount > 0 ? _playerSpeedSum / _playerSpeedCount : 0f);
            builder.Append(", ");
            AppendInline(builder, "maxSpeed", _playerMaxSpeed);
            builder.Append(", \"lapTimes\": [");
            for (int i = 0; i < _playerLapTimes.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(_playerLapTimes[i].ToString("0.00", CultureInfo.InvariantCulture));
            }

            builder.Append("]},\n");
        }

        private int FinishedCount()
        {
            int lapsNeeded = _session != null ? _session.Laps : RaceDirector.LapsRequired;
            int count = 0;
            foreach (HeldBot bot in _held.Values)
            {
                if (bot.Laps >= lapsNeeded)
                    count++;
            }

            return count;
        }

        private void AppendBot(StringBuilder builder, HeldBot bot, bool last)
        {
            builder.Append("    {");
            AppendInline(builder, "name", bot.Name);
            builder.Append(", ");
            AppendInline(builder, "laps", bot.Laps);
            builder.Append(", ");
            AppendInline(builder, "finished", _session != null && bot.Laps >= _session.Laps);
            builder.Append(", ");
            AppendInline(builder, "distance", bot.Distance);
            builder.Append(", ");
            AppendInline(builder, "speed", bot.Speed);
            builder.Append(", ");
            AppendInline(builder, "recovers", bot.Recovers);
            builder.Append(", \"lapTimes\": [");
            for (int i = 0; i < bot.LapTimes.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(bot.LapTimes[i].ToString("0.00", CultureInfo.InvariantCulture));
            }

            builder.Append("]}");
            builder.Append(last ? "\n" : ",\n");
        }

        private void AppendStalls(StringBuilder builder)
        {
            float length = RacePath.Instance != null ? RacePath.Instance.Length : 0f;
            bool any = false;
            for (int i = 0; i < Buckets; i++)
            {
                if (_slowCounts[i] == 0 && _recoverCounts[i] == 0)
                    continue;

                if (any)
                    builder.Append(",\n");
                any = true;
                float average = _speedCounts[i] > 0 ? _speedSums[i] / _speedCounts[i] : 0f;
                builder.Append("    {");
                AppendInline(builder, "bucket", i);
                builder.Append(", ");
                AppendInline(builder, "along", length * (i + 0.5f) / Buckets);
                builder.Append(", ");
                AppendInline(builder, "averageSpeed", average);
                builder.Append(", ");
                AppendInline(builder, "slowSamples", _slowCounts[i]);
                builder.Append(", ");
                AppendInline(builder, "recovers", _recoverCounts[i]);
                builder.Append('}');
            }

            builder.Append(any ? "\n" : "");
        }

        private static void AppendRecover(StringBuilder builder, RecoverMark mark, bool last)
        {
            builder.Append("    {");
            AppendInline(builder, "name", mark.Name);
            builder.Append(", ");
            AppendInline(builder, "point", mark.Point);
            builder.Append(", ");
            AppendInline(builder, "bucket", mark.Bucket);
            builder.Append(", \"position\": [");
            AppendVector(builder, mark.Position);
            builder.Append("]}");
            builder.Append(last ? "\n" : ",\n");
        }

        private static void AppendSample(StringBuilder builder, Sample sample, bool last)
        {
            builder.Append("    {");
            AppendInline(builder, "name", sample.Name);
            builder.Append(", ");
            AppendInline(builder, "laps", sample.Laps);
            builder.Append(", ");
            AppendInline(builder, "distance", sample.Distance);
            builder.Append(", ");
            AppendInline(builder, "speed", sample.Speed);
            builder.Append(", ");
            AppendInline(builder, "mode", sample.Mode.ToString());
            builder.Append(", ");
            AppendInline(builder, "stuck", sample.Stuck);
            builder.Append(", ");
            AppendInline(builder, "lateral", sample.Lateral);
            builder.Append(", ");
            AppendInline(builder, "progress", sample.Progress);
            builder.Append(", ");
            AppendInline(builder, "steer", sample.Steer);
            builder.Append(", ");
            AppendInline(builder, "accel", sample.Accel);
            builder.Append(", ");
            AppendInline(builder, "brake", sample.Brake);
            builder.Append(", \"position\": [");
            AppendVector(builder, sample.Position);
            builder.Append("]}");
            builder.Append(last ? "\n" : ",\n");
        }

        private static void Append(StringBuilder builder, string name, string value, int indent, bool comma)
        {
            Pad(builder, indent);
            builder.Append('"').Append(name).Append("\": ");
            AppendString(builder, value);
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void Append(StringBuilder builder, string name, float value, int indent, bool comma)
        {
            Pad(builder, indent);
            builder.Append('"').Append(name).Append("\": ");
            builder.Append(value.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void Append(StringBuilder builder, string name, int value, int indent, bool comma)
        {
            Pad(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(value);
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void Append(StringBuilder builder, string name, bool value, int indent, bool comma)
        {
            Pad(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(value ? "true" : "false");
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendInline(StringBuilder builder, string name, string value)
        {
            builder.Append('"').Append(name).Append("\": ");
            AppendString(builder, value);
        }

        private static void AppendInline(StringBuilder builder, string name, float value)
        {
            builder.Append('"').Append(name).Append("\": ");
            builder.Append(value.ToString("0.00", CultureInfo.InvariantCulture));
        }

        private static void AppendInline(StringBuilder builder, string name, int value)
        {
            builder.Append('"').Append(name).Append("\": ").Append(value);
        }

        private static void AppendInline(StringBuilder builder, string name, bool value)
        {
            builder.Append('"').Append(name).Append("\": ").Append(value ? "true" : "false");
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    if (character == '"' || character == '\\')
                        builder.Append('\\');
                    builder.Append(character);
                }
            }

            builder.Append('"');
        }

        private static void AppendVector(StringBuilder builder, Vector3 value)
        {
            builder.Append(value.x.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(value.y.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(value.z.ToString("0.00", CultureInfo.InvariantCulture));
        }

        private static void Pad(StringBuilder builder, int indent)
        {
            for (int i = 0; i < indent; i++)
                builder.Append("  ");
        }

        private readonly struct HeldBot
        {
            public readonly string Name;
            public readonly int Laps;
            public readonly float Distance;
            public readonly float Speed;
            public readonly int Recovers;
            public readonly float[] LapTimes;

            public HeldBot(string name, int laps, float distance, float speed, int recovers, float[] lapTimes)
            {
                Name = name;
                Laps = laps;
                Distance = distance;
                Speed = speed;
                Recovers = recovers;
                LapTimes = lapTimes ?? System.Array.Empty<float>();
            }
        }

        private readonly struct Sample
        {
            public readonly string Name;
            public readonly int Laps;
            public readonly float Distance;
            public readonly float Speed;
            public readonly BotDriveMode Mode;
            public readonly bool Stuck;
            public readonly float Lateral;
            public readonly float Progress;
            public readonly float Steer;
            public readonly float Accel;
            public readonly float Brake;
            public readonly Vector3 Position;

            public Sample(string name, int laps, float distance, float speed, BotDriveMode mode, bool stuck, float lateral, float progress, float steer, float accel, float brake, Vector3 position)
            {
                Name = name;
                Laps = laps;
                Distance = distance;
                Speed = speed;
                Mode = mode;
                Stuck = stuck;
                Lateral = lateral;
                Progress = progress;
                Steer = steer;
                Accel = accel;
                Brake = brake;
                Position = position;
            }
        }

        private readonly struct RecoverMark
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly string Point;
            public readonly int Bucket;

            public RecoverMark(string name, Vector3 position, string point, int bucket)
            {
                Name = name;
                Position = position;
                Point = point;
                Bucket = bucket;
            }
        }
    }
}
