using BehaviorDesigner.Runtime.Tasks;
using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class HasCombatTarget : Conditional
    {
        private BotBrain _brain;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || _brain.Car == null || _brain.Gun == null)
                return TaskStatus.Failure;

            if (_brain.DriveOnly)
            {
                _brain.SetTarget(null);
                return TaskStatus.Failure;
            }

            BotProfile profile = _brain.Profile;
            bool hasAmmo = _brain.Gun.Ammo_Machinegun > 0 || _brain.Gun.Ammo_Missile > 0;
            if (!hasAmmo && profile.Goal == BotGoal.WinRace)
            {
                _brain.SetTarget(null);
                return TaskStatus.Failure;
            }

            if (!_brain.CanDecide() && _brain.Target != null && _brain.Target.IsAlive && StillWorthChasing(profile))
            {
                if (profile.Goal == BotGoal.Ram || IsInFront(_brain.Target, profile.FireCone))
                    return TaskStatus.Success;

                return TaskStatus.Failure;
            }
            CarController best = FindRival(profile);
            CarController previous = _brain.Target;
            _brain.SetTarget(best);
            if (best != previous)
                _brain.MarkDecided();

            if (best == null)
                return TaskStatus.Failure;

            if (profile.Goal == BotGoal.Ram)
                return TaskStatus.Success;

            return IsInFront(best, profile.FireCone) ? TaskStatus.Success : TaskStatus.Failure;
        }

        private CarController FindRival(BotProfile profile)
        {
            if (RaceDirector.Instance == null)
                return null;

            float range = profile.Goal == BotGoal.Ram ? 160f : profile.FireRange;
            float cone = profile.Goal == BotGoal.WinRace ? profile.FireCone : profile.Goal == BotGoal.Ram ? 110f : 180f;
            CarController best = null;
            float bestDistance = range;
            var racers = RaceDirector.Instance.Racers;
            for (int i = 0; i < racers.Count; i++)
            {
                RaceRacer racer = racers[i];
                if (racer == null || racer.Car == null || racer.Car == _brain.Car || !racer.Car.IsAlive)
                    continue;

                if (profile.Goal == BotGoal.Destroy)
                {
                    if (!IsAheadOnTrack(racer.Car, profile.FireRange, out float ahead))
                        continue;

                    if (ahead < bestDistance)
                    {
                        bestDistance = ahead;
                        best = racer.Car;
                    }

                    continue;
                }

                if (!IsInFront(racer.Car, cone))
                    continue;

                float distance = FlatDistance(racer.Car);
                if (distance > bestDistance)
                    continue;

                if (profile.Goal == BotGoal.WinRace && distance < 6f)
                    continue;

                bestDistance = distance;
                best = racer.Car;
            }

            return best;
        }

        private bool StillWorthChasing(BotProfile profile)
        {
            if (profile.Goal != BotGoal.Destroy)
                return true;

            return IsAheadOnTrack(_brain.Target, profile.FireRange, out _);
        }

        private bool IsAheadOnTrack(CarController rival, float range, out float ahead)
        {
            ahead = 0f;
            return RacePath.Instance != null &&
                   RacePath.Instance.TryGetAhead(_brain.transform.position, rival.transform.position, out ahead) &&
                   ahead >= 4f && ahead <= range;
        }

        private bool IsInFront(CarController rival, float cone)
        {
            Vector3 to = rival.transform.position - _brain.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
                return true;

            return Vector3.Angle(_brain.transform.forward, to) <= cone;
        }

        private float FlatDistance(CarController rival)
        {
            Vector3 to = rival.transform.position - _brain.transform.position;
            to.y = 0f;
            return to.magnitude;
        }
    }
}
