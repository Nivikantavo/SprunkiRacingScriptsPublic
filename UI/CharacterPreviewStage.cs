using UnityEngine;

namespace SprunkiRacing
{
    public class CharacterPreviewStage : MonoBehaviour
    {
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform cylinder;
        [SerializeField] private Transform carAnchor;
        [SerializeField] private float rotationSpeed = 25f;

        public Camera PreviewCamera => previewCamera;
        public Transform CarAnchor => carAnchor != null ? carAnchor : transform;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (cylinder == null)
                return;

            if (previewCamera != null && !previewCamera.enabled)
                return;

            cylinder.Rotate(0f, rotationSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (previewCamera == null)
                previewCamera = GetComponentInChildren<Camera>(true);

            if (cylinder == null)
            {
                Transform foundCylinder = transform.Find("Cylinder");
                if (foundCylinder != null)
                    cylinder = foundCylinder;
            }

            if (carAnchor == null && cylinder != null)
            {
                Transform anchor = cylinder.Find("CarAnchor");
                if (anchor != null)
                    carAnchor = anchor;
            }
        }
    }
}
