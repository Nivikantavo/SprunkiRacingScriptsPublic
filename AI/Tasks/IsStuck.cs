using BehaviorDesigner.Runtime.Tasks;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class IsStuck : Conditional
    {
        private BotBrain _brain;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || !_brain.IsStuck)
                return TaskStatus.Failure;

            return TaskStatus.Success;
        }
    }
}
