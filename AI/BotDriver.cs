using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    [RequireComponent(typeof(BotBrain))]
    [RequireComponent(typeof(CarController))]
    public class BotDriver : MonoBehaviour
    {
        private BotBrain _brain;
        private CarController _car;

        private void Awake()
        {
            _brain = GetComponent<BotBrain>();
            _car = GetComponent<CarController>();
        }

        private void FixedUpdate()
        {
            if (_brain == null || _car == null || !_car.IsAlive)
                return;

            if (RaceDirector.Instance != null && !RaceDirector.Instance.RaceRunning)
                return;

            _car.Drive(_brain.Steer, _brain.Accel, _brain.Brake, 0f, _brain.Nitro);
        }
    }
}
