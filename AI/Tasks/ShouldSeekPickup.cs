using BehaviorDesigner.Runtime.Tasks;
using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class ShouldSeekPickup : Conditional
    {
        private BotBrain _brain;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || _brain.DriveOnly)
                return TaskStatus.Failure;

            if (!_brain.CanDecide())
                return _brain.Mode == BotDriveMode.SeekPickup && _brain.Pickup != null
                    ? TaskStatus.Success
                    : TaskStatus.Failure;

            BotProfile profile = _brain.Profile;
            bool needGun = _brain.Gun != null && _brain.Gun.Ammo_Machinegun < profile.AmmoThreshold;
            bool needMissile = _brain.Gun != null && _brain.Gun.Ammo_Missile < profile.MissileThreshold;
            bool needFuel = _brain.Fuel != null && _brain.Fuel.CurrentFuel < profile.FuelThreshold;
            bool needHealth = _brain.Car != null && _brain.Car.IsAlive && _brain.Car.Health < profile.HealthThreshold;
            if (!needGun && !needMissile && !needFuel && !needHealth)
                return TaskStatus.Failure;

            CollactableScript best = null;
            float bestDistance = profile.MaxDetour;
            Vector3 position = _brain.transform.position;
            for (int i = 0; i < CollactableScript.Active.Count; i++)
            {
                CollactableScript item = CollactableScript.Active[i];
                if (item == null)
                    continue;

                bool useful = (needFuel && item.collactableType == CollactableType.Nitrous)
                    || (needGun && item.collactableType == CollactableType.AmmoMachinegun)
                    || (needMissile && item.collactableType == CollactableType.AmmoMissile)
                    || (needHealth && item.collactableType == CollactableType.Health);
                if (!useful)
                    continue;

                float distance = Vector3.Distance(position, item.transform.position);
                if (distance >= bestDistance || BlockedByWall(position, item.transform.position))
                    continue;

                bestDistance = distance;
                best = item;
            }

            if (best == null)
                return TaskStatus.Failure;

            _brain.SetPickup(best);
            _brain.SetMode(BotDriveMode.SeekPickup);
            _brain.MarkDecided();
            return TaskStatus.Success;
        }

        private bool BlockedByWall(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 2f)
                return false;

            Vector3 origin = from + Vector3.up * 0.7f;
            if (!Physics.Raycast(origin, delta.normalized, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.normal.y > 0.5f || hit.collider.GetComponentInParent<CarController>() != null)
                return false;

            return hit.distance < distance - 1.5f;
        }
    }
}
