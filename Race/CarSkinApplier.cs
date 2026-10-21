using UnityEngine;

namespace SprunkiRacing
{
    public static class CarSkinApplier
    {
        private const string DesignName = "CarDesign";

        public static void Apply(GameObject vehicleRoot, GameObject skinPrefab)
        {
            if (vehicleRoot == null || skinPrefab == null)
                return;

            Transform design = FindDesign(vehicleRoot.transform);
            if (design == null)
            {
                Debug.LogError("Car skin target is missing CarDesign.", vehicleRoot);
                return;
            }

            ClearVisual(design);
            GameObject skin = Object.Instantiate(skinPrefab, design);
            Transform skinTransform = skin.transform;
            skinTransform.localPosition = Vector3.zero;
            skinTransform.localRotation = Quaternion.identity;
            skinTransform.localScale = Vector3.one;
            DisableColliders(skin);
        }

        private static Transform FindDesign(Transform root)
        {
            if (root.name == DesignName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDesign(root.GetChild(i));
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void ClearVisual(Transform design)
        {
            for (int i = design.childCount - 1; i >= 0; i--)
            {
                Transform child = design.GetChild(i);
                if (IsKept(child.name))
                    continue;

                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool IsKept(string objectName)
        {
            return objectName == "Body" || objectName == "WheelMeshes" || objectName == "WheelColliders";
        }

        private static void DisableColliders(GameObject skin)
        {
            Collider[] colliders = skin.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }
    }
}
