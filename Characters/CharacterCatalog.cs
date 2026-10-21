using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Sprunki Racing/Character Catalog")]
    public class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private CharacterConfig[] characters = System.Array.Empty<CharacterConfig>();

        public IReadOnlyList<CharacterConfig> Characters => characters;

        public CharacterConfig Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < characters.Length; i++)
            {
                CharacterConfig config = characters[i];
                if (config != null && config.Id == id)
                    return config;
            }

            return null;
        }
    }
}
