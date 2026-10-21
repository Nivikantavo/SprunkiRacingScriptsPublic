using BehaviorDesigner.Runtime.Tasks;
using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class ApplyDrive : Action
    {
        private BotBrain _brain;
        private float _noise;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || _brain.Car == null || !_brain.Car.IsAlive)
                return TaskStatus.Failure;

            BotProfile profile = _brain.Profile;
            Vector3 point = _brain.DrivePoint;
            if (_brain.Mode == BotDriveMode.SeekPickup && _brain.Pickup != null)
                point = _brain.Pickup.transform.position;

            bool chasing = false;
            if (_brain.Mode == BotDriveMode.Race && _brain.Target != null && _brain.Target.IsAlive)
            {
                if (profile.Goal == BotGoal.Destroy)
                    point = BlendTowardRivalAhead(point, profile.ChaseWeight);
                else if (profile.Goal == BotGoal.Ram)
                {
                    point = Vector3.Lerp(point, _brain.Target.transform.position, Mathf.Max(0.8f, profile.RamBias));
                    chasing = true;
                }
            }

            Vector3 flat = point - _brain.transform.position;
            flat.y = 0f;
            float angle = flat.sqrMagnitude > 0.25f
                ? Vector3.SignedAngle(_brain.transform.forward, flat.normalized, Vector3.up)
                : 0f;

            float steer = Mathf.Clamp(angle / 18f, -1f, 1f);
            if (profile.SteerNoise > 0f)
            {
                float targetNoise = (Mathf.PerlinNoise(Time.time * 0.65f, _brain.GetInstanceID() * 0.01f) * 2f - 1f) * profile.SteerNoise;
                _noise = Mathf.Lerp(_noise, targetNoise, Time.deltaTime * 2f);
                steer = Mathf.Clamp(steer + _noise, -1f, 1f);
            }

            float accel = 1f;
            float brake = 0f;
            bool nitro = false;
            if (_brain.Mode == BotDriveMode.Recover)
            {
                _brain.UpdateRecoverSteer();
                steer = _brain.RecoverSteer;
                accel = 0f;
                brake = 1f;
            }
            else
            {
                bool onPath = false;
                RaceGuidance guidance = default;
                if (RacePath.Instance != null)
                {
                    float progress = _brain.PathProgress;
                    float sense = RacePath.SenseDistance(profile.Intelligence);
                    onPath = RacePath.Instance.TryProject(_brain.transform.position, ref progress, sense, out guidance);
                    if (onPath)
                        _brain.SetPathProgress(progress);
                }

                bool aimBehind = false;
                if (onPath && !chasing && Mathf.Abs(angle) > 95f && guidance.Tangent.sqrMagnitude > 0.01f)
                {
                    Vector3 nose = _brain.transform.forward;
                    nose.y = 0f;
                    float tangentAngle = Vector3.SignedAngle(nose.sqrMagnitude > 0.0001f ? nose.normalized : Vector3.forward, guidance.Tangent, Vector3.up);
                    steer = Mathf.Clamp(tangentAngle / 18f, -1f, 1f);
                    aimBehind = true;
                }

                if (onPath)
                {
                    float curvature = RacePath.Instance.UpcomingTurn(_brain.PathProgress, 32f);
                    float limit = CornerSpeedLimit(curvature, _brain.Car.MaxSpeed, _brain.CornerLateralAccel, _brain.CornerMinSpeed);
                    float lead = PlayerLead();
                    float catchup = Mathf.InverseLerp(12f, 40f, lead);
                    if (catchup > 0f)
                        limit = Mathf.Lerp(limit, _brain.Car.MaxSpeed, catchup);

                    float speed = _brain.Car.CurrentSpeed;
                    float over = speed - limit;
                    float coast = Mathf.Max(1f, _brain.CoastOverSpeed);
                    if (over > coast)
                    {
                        accel = 0f;
                        brake = _brain.CornerBrake * (catchup > 0.5f ? 0.35f : 1f);
                    }
                    else if (over > 0f)
                    {
                        accel = Mathf.Lerp(0.9f, 0.2f, over / coast);
                        brake = 0f;
                    }

                    bool straight = curvature < 12f;
                    bool underLimit = speed <= limit + 8f;
                    bool allowNitro = straight && underLimit && profile.UseNitroOnStraight;
                    if (!allowNitro && lead > 18f && curvature < 18f && profile.UseNitroOnStraight)
                        allowNitro = true;
                    if (straight && underLimit && !allowNitro && profile.NitroChance > 0f)
                        allowNitro = Mathf.PerlinNoise(Time.time * 0.2f, 3.7f) < profile.NitroChance;

                    nitro = allowNitro && _brain.Fuel != null && _brain.Fuel.CurrentFuel > 1f && accel > 0.35f;
                    if (aimBehind)
                    {
                        accel = Mathf.Min(accel, 0.35f);
                        nitro = false;
                    }
                }

                _brain.AvoidWalls(ref steer, ref accel, ref brake);
            }

            _brain.SetDrive(steer, accel, brake, nitro);
            return TaskStatus.Success;
        }

        private Vector3 BlendTowardRivalAhead(Vector3 racePoint, float chaseWeight)
        {
            if (RacePath.Instance == null ||
                !RacePath.Instance.TryGetAhead(_brain.transform.position, _brain.Target.transform.position, out float ahead) ||
                ahead < 4f || ahead > _brain.Profile.FireRange)
                return racePoint;

            float weight = Mathf.Clamp(chaseWeight, 0f, 0.45f);
            Vector3 blended = Vector3.Lerp(racePoint, _brain.Target.transform.position, weight);
            Vector3 toRace = racePoint - _brain.transform.position;
            Vector3 toBlended = blended - _brain.transform.position;
            toRace.y = 0f;
            toBlended.y = 0f;
            if (toRace.sqrMagnitude > 1f && toBlended.sqrMagnitude > 1f && Vector3.Angle(toRace, toBlended) > 40f)
                return racePoint;

            return blended;
        }

        private float PlayerLead()
        {
            CarController player = CarController.Instance;
            if (player == null || !player.IsAlive || player == _brain.Car || RacePath.Instance == null)
                return 0f;

            float length = RacePath.Instance.Length;
            if (length < 0.01f || !RacePath.Instance.TryGetProgress(player.transform.position, out float playerProgress))
                return 0f;

            float botProgress = Mathf.Max(0f, _brain.PathProgress);
            RaceRacer botRace = _brain.GetComponent<RaceRacer>();
            RaceRacer playerRace = player.GetComponent<RaceRacer>();
            int laps = (playerRace != null ? playerRace.Laps : 0) - (botRace != null ? botRace.Laps : 0);
            return laps * length + (playerProgress - botProgress);
        }

        private static float CornerSpeedLimit(float stepDegrees, float topSpeed, float lateralAccel, float minSpeed)
        {
            const float step = 4f;
            float accel = Mathf.Max(0.1f, lateralAccel);
            float theta = stepDegrees * Mathf.Deg2Rad;
            float radius = theta < 0.015f ? 400f : step / theta;
            float kmh = Mathf.Sqrt(accel * radius) * 3.6f;
            return Mathf.Clamp(kmh, minSpeed, topSpeed);
        }
    }
}
