using UnityEngine;

namespace SprunkiRacing
{
    [RequireComponent(typeof(AudioSource))]
    public class SfxSource : MonoBehaviour
    {
        AudioSource _source;
        float _baseVolume = 1f;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null)
                return;

            _baseVolume = _source.volume;
            _source.playOnAwake = false;
            Apply();
            if (_source.clip != null)
                _source.Play();
        }

        void OnEnable()
        {
            GameAudio.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            GameAudio.Changed -= Apply;
        }

        void Apply()
        {
            if (_source != null)
                _source.volume = _baseVolume * GameAudio.Sfx;
        }
    }
}
