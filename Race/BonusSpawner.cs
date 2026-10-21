using System.Collections;
using System.Collections.Generic;
using CarControllerwithShooting;
using UnityEngine;

namespace SprunkiRacing
{
    public class BonusSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject ammoPrefab;
        [SerializeField] private GameObject gasPrefab;
        [SerializeField] private GameObject healthPrefab;
        [SerializeField] private float respawnInterval = 10f;

        private readonly List<Slot> _slots = new List<Slot>();
        private GameObject[] _prefabs;

        private class Slot
        {
            public Transform Point;
            public CollactableScript Occupant;

            public bool IsFree => Occupant == null;
        }

        private void Start()
        {
            _prefabs = new[] { ammoPrefab, gasPrefab, healthPrefab };
            CollectSlots();
            SpawnInitial();
            StartCoroutine(RespawnLoop());
        }

        private void CollectSlots()
        {
            for (int i = 0; i < transform.childCount; i++)
                _slots.Add(new Slot { Point = transform.GetChild(i) });
        }

        private void SpawnInitial()
        {
            for (int i = 0; i < _slots.Count; i += 2)
                SpawnRandom(_slots[i]);
        }

        private IEnumerator RespawnLoop()
        {
            while (RaceDirector.Instance == null || !RaceDirector.Instance.RaceRunning)
                yield return null;

            while (RaceDirector.Instance != null && RaceDirector.Instance.RaceRunning)
            {
                yield return new WaitForSeconds(respawnInterval);
                if (RaceDirector.Instance == null || !RaceDirector.Instance.RaceRunning)
                    yield break;

                SpawnOnFreeSlot();
            }
        }

        private void SpawnOnFreeSlot()
        {
            int freeCount = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsFree)
                    freeCount++;
            }

            if (freeCount == 0)
                return;

            int pick = Random.Range(0, freeCount);
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsFree)
                    continue;

                if (pick == 0)
                {
                    SpawnRandom(_slots[i]);
                    return;
                }

                pick--;
            }
        }

        private void SpawnRandom(Slot slot)
        {
            if (slot.Point == null)
                return;

            GameObject prefab = PickPrefab();
            if (prefab == null)
                return;

            GameObject instance = Instantiate(prefab, slot.Point.position, slot.Point.rotation);
            slot.Occupant = instance.GetComponent<CollactableScript>();
        }

        private GameObject PickPrefab()
        {
            if (_prefabs == null)
                return null;

            int count = 0;
            for (int i = 0; i < _prefabs.Length; i++)
            {
                if (_prefabs[i] != null)
                    count++;
            }

            if (count == 0)
                return null;

            int pick = Random.Range(0, count);
            for (int i = 0; i < _prefabs.Length; i++)
            {
                if (_prefabs[i] == null)
                    continue;

                if (pick == 0)
                    return _prefabs[i];

                pick--;
            }

            return null;
        }
    }
}
