using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class PrizeBurst : MonoBehaviour
    {
        private const int DotLimit = 48;
        private const int RingCount = 18;
        private const int TextureSize = 32;

        private static Sprite _dot;

        private ParticleSystem _system;
        private ParticleSystem.Particle[] _particles;
        private Image[] _dots;
        private RectTransform _follow;
        private bool _mirror;

        public static void Play(Transform anchor, ParticleSystem prefab)
        {
            if (anchor == null)
                return;

            Transform parent = anchor.parent != null ? anchor.parent : anchor;
            var host = new GameObject("PrizeBurst", typeof(RectTransform));
            var layout = host.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            var hostRect = (RectTransform)host.transform;
            hostRect.SetParent(parent, false);
            hostRect.anchorMin = new Vector2(0.5f, 0.5f);
            hostRect.anchorMax = new Vector2(0.5f, 0.5f);
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.anchoredPosition = Vector2.zero;
            hostRect.sizeDelta = Vector2.zero;
            hostRect.localRotation = Quaternion.identity;
            hostRect.localScale = Vector3.one;

            ParticleSystem system = prefab != null
                ? Instantiate(prefab, hostRect)
                : host.AddComponent<ParticleSystem>();

            if (prefab == null)
                Configure(system);
            else
            {
                system.transform.localPosition = Vector3.zero;
                system.transform.localRotation = Quaternion.identity;
                system.transform.localScale = Vector3.one;
            }

            host.AddComponent<PrizeBurst>().Begin(system, anchor as RectTransform, prefab == null);
        }

        private void Begin(ParticleSystem system, RectTransform follow, bool emitRing)
        {
            _system = system;
            _follow = follow;
            if (_follow != null)
                transform.position = _follow.position;

            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.playOnAwake = false;

            Canvas canvas = GetComponentInParent<Canvas>();
            bool overlay = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (overlay)
            {
                _mirror = true;
                if (renderer != null)
                    renderer.enabled = false;

                CreateDots();
            }
            else if (renderer != null && canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                renderer.sortingLayerID = root.sortingLayerID;
                renderer.sortingOrder = root.sortingOrder + 5;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(true);
            if (emitRing)
                EmitRing(system);

            Destroy(gameObject, main.duration + main.startLifetime.constantMax + 0.15f);
        }

        private void LateUpdate()
        {
            if (_follow != null)
            {
                transform.position = _follow.position;
                transform.SetAsLastSibling();
            }

            if (!_mirror || _system == null)
                return;

            int count = _system.GetParticles(_particles);
            for (int i = 0; i < _dots.Length; i++)
            {
                Image dot = _dots[i];
                if (i >= count)
                {
                    if (dot.enabled)
                        dot.enabled = false;

                    continue;
                }

                ParticleSystem.Particle particle = _particles[i];
                float size = particle.GetCurrentSize(_system);
                RectTransform rect = dot.rectTransform;
                Vector3 position = particle.position;
                rect.localPosition = new Vector3(position.x, position.y, 0f);
                rect.sizeDelta = new Vector2(size, size);
                dot.color = particle.GetCurrentColor(_system);
                if (!dot.enabled)
                    dot.enabled = true;
            }
        }

        private void CreateDots()
        {
            int capacity = Mathf.Clamp(_system.main.maxParticles, 1, DotLimit);
            _particles = new ParticleSystem.Particle[capacity];
            _dots = new Image[capacity];
            Sprite sprite = DotSprite();
            for (int i = 0; i < capacity; i++)
            {
                var dotObject = new GameObject("Dot", typeof(RectTransform));
                dotObject.transform.SetParent(_system.transform, false);
                var image = dotObject.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                image.enabled = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                _dots[i] = image;
            }
        }

        private static void Configure(ParticleSystem system)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.45f, 1f), Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = RingCount;
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            var shape = system.shape;
            shape.enabled = false;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.25f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void EmitRing(ParticleSystem system)
        {
            system.Emit(RingCount);
            var particles = new ParticleSystem.Particle[RingCount];
            int alive = system.GetParticles(particles);
            for (int i = 0; i < alive; i++)
            {
                float angle = (i + Random.value) / RingCount * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                particles[i].position = direction * Random.Range(0f, 12f);
                particles[i].velocity = direction * Random.Range(80f, 150f);
                particles[i].startLifetime = Random.Range(0.45f, 0.7f);
                particles[i].remainingLifetime = particles[i].startLifetime;
                particles[i].startSize = Random.Range(10f, 18f);
                particles[i].startColor = Color.Lerp(new Color(1f, 0.9f, 0.45f, 1f), Color.white, Random.value);
            }

            system.SetParticles(particles, alive);
        }

        private static Sprite DotSprite()
        {
            if (_dot != null)
                return _dot;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[TextureSize * TextureSize];
            float center = (TextureSize - 1) * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * alpha;
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _dot = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
            _dot.hideFlags = HideFlags.HideAndDontSave;
            return _dot;
        }
    }
}
