using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    public class LapGate : MonoBehaviour
    {
        public enum GateType
        {
            Finish,
            Sector
        }

        [SerializeField] private GateType type;

        private void OnTriggerEnter(Collider other)
        {
            if (RaceDirector.Instance == null)
                return;

            CarController car = other.GetComponentInParent<CarController>();
            if (car == null)
                return;

            RaceDirector.Instance.OnGateEntered(type, car, transform.forward);
        }

        private void OnTriggerExit(Collider other)
        {
            if (RaceDirector.Instance == null)
                return;

            CarController car = other.GetComponentInParent<CarController>();
            if (car == null)
                return;

            RaceDirector.Instance.OnGateExited(type, car);
        }
    }
}
