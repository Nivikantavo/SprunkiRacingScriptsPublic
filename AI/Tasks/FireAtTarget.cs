using BehaviorDesigner.Runtime.Tasks;
using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class FireAtTarget : Action
    {
        private BotBrain _brain;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || _brain.Gun == null || _brain.Target == null || !_brain.Target.IsAlive)
            {
                if (_brain != null)
                    _brain.ClearBurst();

                return TaskStatus.Failure;
            }

            GunController gun = _brain.Gun;
            Vector3 straight = _brain.transform.forward;
            if (_brain.BurstLeft > 0)
                return ContinueBurst(gun, straight);

            if (!_brain.CanFire())
                return TaskStatus.Failure;

            if (!InCone(_brain.Profile.FireCone))
                return TaskStatus.Failure;

            if (_brain.NextShotIsMissile && gun.Ammo_Missile > 0 && gun.TryFireMissile(straight))
            {
                _brain.MarkFired();
                return TaskStatus.Success;
            }

            if (gun.Ammo_Machinegun > 0 && gun.TryFireMachineGun(straight))
            {
                _brain.StartBurst();
                return TaskStatus.Success;
            }

            if (gun.Ammo_Missile > 0 && gun.TryFireMissile(straight))
            {
                _brain.MarkFired();
                return TaskStatus.Success;
            }

            return TaskStatus.Failure;
        }

        private TaskStatus ContinueBurst(GunController gun, Vector3 straight)
        {
            float limit = Mathf.Max(28f, _brain.Profile.FireCone * 2f);
            if (!InCone(limit) || gun.Ammo_Machinegun <= 0)
            {
                _brain.ClearBurst();
                _brain.MarkFired();
                return TaskStatus.Failure;
            }

            if (gun.TryFireMachineGun(straight))
                _brain.NoteBurstRound();

            return TaskStatus.Success;
        }

        private bool InCone(float degrees)
        {
            Vector3 to = _brain.Target.transform.position - _brain.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude <= 0.01f)
                return true;

            return Vector3.Angle(_brain.transform.forward, to) <= degrees;
        }
    }
}
