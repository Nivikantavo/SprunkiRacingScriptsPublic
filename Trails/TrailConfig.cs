using UnityEngine;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "Trail", menuName = "Sprunki Racing/Trail")]
    public class TrailConfig : ScriptableObject
    {
        [SerializeField] private LocalizedString displayName;
        [SerializeField] private Sprite preview;
        [SerializeField] private GameObject prefab;
        [SerializeField] private bool unlockedByDefault;
        [SerializeField] private int price;

        public string Id => name;
        public LocalizedString LocalizedName => displayName;
        public string DisplayName => LocalizedTextBinding.Read(displayName);
        public Sprite Preview => preview;
        public GameObject Prefab => prefab;
        public bool UnlockedByDefault => unlockedByDefault;
        public int Price => Mathf.Max(0, price);
    }
}
