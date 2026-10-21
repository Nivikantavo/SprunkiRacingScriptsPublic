using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class CharacterCarPreview : MonoBehaviour
    {
        private const int PreviewLayer = 8;

        [SerializeField] private RawImage previewImage;
        [SerializeField] private CharacterPreviewStage stagePrefab;

        private CharacterPreviewStage _stage;
        private Camera _camera;
        private GameObject _carInstance;
        private int _excludedMask;

        private void Awake()
        {
            EnsurePreviewImage();
            SpawnStage();
            ExcludePreviewLayerFromMainCamera();
        }

        public void Show(GameObject carPrefab)
        {
            if (_stage == null)
                SpawnStage();

            if (_carInstance != null)
                Destroy(_carInstance);

            if (carPrefab == null || _stage == null)
                return;

            _carInstance = Instantiate(carPrefab, _stage.CarAnchor);
            _carInstance.transform.localPosition = Vector3.zero;
            _carInstance.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(_carInstance, PreviewLayer);
        }

        private void EnsurePreviewImage()
        {
            if (previewImage != null)
                return;

            Transform character = transform.Find("Character");
            if (character == null)
                return;

            Image image = character.GetComponent<Image>();
            if (image != null)
            {
                Color color = image.color;
                Destroy(image);
                previewImage = character.gameObject.AddComponent<RawImage>();
                previewImage.color = color;
                previewImage.raycastTarget = false;
            }
            else
            {
                previewImage = character.GetComponent<RawImage>();
            }
        }

        private void SpawnStage()
        {
            if (_stage != null)
                return;

            if (stagePrefab == null)
            {
                Debug.LogError("Character preview stage prefab is not assigned.", this);
                return;
            }

            _stage = Instantiate(stagePrefab);
            _camera = _stage.PreviewCamera;
            if (_camera == null)
            {
                Debug.LogError("Character preview stage has no camera.", _stage);
                return;
            }

            RenderTexture previewTexture = _camera.targetTexture;
            if (previewTexture == null)
            {
                Debug.LogError("Assign a Render Texture on PreviewCamera.", _camera);
                return;
            }

            EnsurePreviewImage();
            if (previewImage != null)
                previewImage.texture = previewTexture;
        }

        private void ExcludePreviewLayerFromMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            _excludedMask = 1 << PreviewLayer;
            mainCamera.cullingMask &= ~_excludedMask;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            Transform root = target.transform;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i).gameObject, layer);
        }

        private void OnDisable()
        {
            if (_camera != null)
                _camera.enabled = false;

            Camera mainCamera = Camera.main;
            if (mainCamera != null && _excludedMask != 0)
                mainCamera.cullingMask |= _excludedMask;
        }

        private void OnEnable()
        {
            if (_camera != null)
                _camera.enabled = true;

            ExcludePreviewLayerFromMainCamera();
        }

        private void OnDestroy()
        {
            if (_stage != null)
                Destroy(_stage.gameObject);
        }
    }
}
