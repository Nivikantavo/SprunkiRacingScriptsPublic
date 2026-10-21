using System;
using UnityEngine;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "ShopSectionRequest", menuName = "Sprunki Racing/Shop Section Request")]
    public class ShopSectionRequest : ScriptableObject
    {
        public event Action<ShopSection> Raised;

        public void Raise(ShopSection section)
        {
            Raised?.Invoke(section);
        }
    }
}
