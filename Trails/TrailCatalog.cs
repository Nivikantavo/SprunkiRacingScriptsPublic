using System.Collections.Generic;
using UnityEngine;

namespace SprunkiRacing
{
    [CreateAssetMenu(fileName = "TrailCatalog", menuName = "Sprunki Racing/Trail Catalog")]
    public class TrailCatalog : ScriptableObject
    {
        [SerializeField] private TrailConfig[] trails = System.Array.Empty<TrailConfig>();

        public IReadOnlyList<TrailConfig> Trails => trails;

        public TrailConfig DefaultTrail
        {
            get
            {
                for (int i = 0; i < trails.Length; i++)
                {
                    TrailConfig trail = trails[i];
                    if (trail != null && trail.UnlockedByDefault)
                        return trail;
                }

                return trails.Length > 0 ? trails[0] : null;
            }
        }

        public TrailConfig Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < trails.Length; i++)
            {
                TrailConfig trail = trails[i];
                if (trail != null && trail.Id == id)
                    return trail;
            }

            return null;
        }

        public TrailConfig Equipped(TrailProgress progress)
        {
            if (progress != null)
            {
                TrailConfig selected = Find(progress.SelectedId);
                if (selected != null && progress.IsUnlocked(selected))
                    return selected;
            }

            return DefaultTrail;
        }

        public TrailConfig RandomTrail()
        {
            if (trails.Length == 0)
                return null;

            int start = Random.Range(0, trails.Length);
            for (int i = 0; i < trails.Length; i++)
            {
                TrailConfig trail = trails[(start + i) % trails.Length];
                if (trail != null)
                    return trail;
            }

            return null;
        }
    }
}
