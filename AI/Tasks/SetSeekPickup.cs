using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class SetSeekPickup : Action
    {
        private BotBrain _brain;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || _brain.Pickup == null)
                return TaskStatus.Failure;

            _brain.SetMode(BotDriveMode.SeekPickup);
            _brain.SetDrivePoint(_brain.Pickup.transform.position);
            return TaskStatus.Success;
        }
    }
}
