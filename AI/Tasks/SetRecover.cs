using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class SetRecover : Action
    {
        private BotBrain _brain;
        private float _endTime;
        private float _minTime;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override void OnStart()
        {
            if (_brain == null)
                return;

            _brain.BeginRecover();
            _brain.MarkDecided();
            _minTime = Time.time + 0.45f;
            _endTime = Time.time + 3f;
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null)
                return TaskStatus.Failure;

            if (Time.time - _brain.ResetTime <= 0.25f)
                return TaskStatus.Success;

            _brain.UpdateRecoverSteer();
            if (Time.time >= _endTime)
                return TaskStatus.Success;

            if (Time.time >= _minTime && _brain.IsRecoverAligned())
                return TaskStatus.Success;

            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (_brain != null)
                _brain.EndRecover();
        }
    }
}
