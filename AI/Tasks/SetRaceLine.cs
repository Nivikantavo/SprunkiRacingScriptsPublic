using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace SprunkiRacing
{
    [TaskCategory("Sprunki/Race")]
    public class SetRaceLine : Action
    {
        private BotBrain _brain;

        public override void OnAwake()
        {
            _brain = gameObject.GetComponent<BotBrain>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_brain == null || _brain.Car == null)
                return TaskStatus.Failure;

            if (_brain.Mode == BotDriveMode.SeekPickup && _brain.Pickup != null)
                return TaskStatus.Failure;

            _brain.SetMode(BotDriveMode.Race);
            Vector3 point = _brain.transform.position + _brain.transform.forward * 12f;
            if (RacePath.Instance != null)
            {
                float progress = _brain.PathProgress;
                float sense = RacePath.SenseDistance(_brain.Profile.Intelligence);
                if (RacePath.Instance.TryProject(_brain.transform.position, ref progress, sense, out RaceGuidance guidance))
                {
                    _brain.SetPathProgress(progress);
                    float lead = RacePath.CornerLookahead(guidance.Curvature, _brain.EaseCorner);
                    float speedLead = Mathf.Clamp(_brain.Car.CurrentSpeed / 3.6f * 0.8f, 12f, 30f);
                    lead = Mathf.Max(lead, speedLead);
                    lead = Mathf.Min(lead, Mathf.Max(_brain.Profile.Lookahead, speedLead));
                    point = RacePath.Instance.AimPoint(progress, lead, _brain.Lane);
                }
            }

            _brain.SetDrivePoint(point);
            return TaskStatus.Success;
        }
    }
}
