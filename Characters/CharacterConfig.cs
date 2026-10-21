using UnityEngine;
using UnityEngine.Localization;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "Character", menuName = "Sprunki Racing/Character")]
    public class CharacterConfig : ScriptableObject
    {
        [SerializeField] private LocalizedString displayName;
        [SerializeField] private Sprite preview;
        [SerializeField] private Sprite lowHealthPreview;
        [SerializeField] private GameObject rallyCar;
        [SerializeField] private GameObject monsterTruck;
        [SerializeField] private bool unlockedByDefault;
        [SerializeField] private int price;

        public string Id => name;
        public LocalizedString DisplayName => displayName;
        public Sprite Preview => preview;
        public Sprite LowHealthPreview => lowHealthPreview;
        public GameObject RallyCar => rallyCar;
        public GameObject MonsterTruck => monsterTruck;
        public bool UnlockedByDefault => unlockedByDefault;
        public int Price => price;
    }
}
