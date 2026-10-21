using UnityEngine;

namespace SprunkiRacing
{
    public static class TrailApplier
    {
        private const string CarName = "Car";
        private const string TrailPrefix = "VFX_Trail";
        private static readonly Vector3 LocalPosition = new Vector3(0f, 0.41f, -2.34f);

        public static void Apply(GameObject vehicleRoot, GameObject trailPrefab)
        {
            if (vehicleRoot == null)
                return;

            Transform car = FindNamed(vehicleRoot.transform, CarName) ?? vehicleRoot.transform;
            ClearTrails(car);
            if (trailPrefab == null)
                return;

            GameObject trail = Object.Instantiate(trailPrefab, car);
            Transform trailTransform = trail.transform;
            trailTransform.localPosition = LocalPosition;
            trailTransform.localRotation = Quaternion.identity;
            trailTransform.localScale = Vector3.one;
        }

        private static void ClearTrails(Transform car)
        {
            for (int i = car.childCount - 1; i >= 0; i--)
            {
                Transform child = car.GetChild(i);
                if (!child.name.StartsWith(TrailPrefix))
                    continue;

                Object.Destroy(child.gameObject);
            }
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
