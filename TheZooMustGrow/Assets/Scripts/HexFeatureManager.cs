using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public class HexFeatureManager : MonoBehaviour
    {
        public HexFeatureCollection[] urbanCollections;
        public HexFeatureCollection[] farmCollections;
        public HexFeatureCollection[] plantCollections;

        private Transform container;

        public void Clear()
        {
            if (container)
            {
                Destroy(container.gameObject);
            }
            container = new GameObject("Features Container").transform;
            container.SetParent(transform, false);
        }

        public void Apply()
        {

        }

        public void AddFeature(HexCell cell, Vector3 position)
        {
            HexHash hash = HexMetrics.SampleHashGrid(position);

            // Choose a prefab
            Transform prefab = PickPrefab(urbanCollections, cell.UrbanLevel, hash.a, hash.d);
            Transform otherPrefab = PickPrefab(farmCollections, cell.FarmLevel, hash.b, hash.d);

            // Determine which prefabcollection to use (e.g., plant vs farm vs urban)
            float usedHash = hash.a;
            if (prefab) 
            {
                // If the Prefab exists
                if (otherPrefab && hash.b < hash.a)
                {
                    prefab = otherPrefab;
                    usedHash = hash.b;
                }
            }
            else if (otherPrefab)
            {
                // OtherPrefab choosen, prefab does not exist
                prefab = otherPrefab;
                usedHash = hash.b;
            }
            // Now also choose plant prefab
            otherPrefab = PickPrefab(
                plantCollections, cell.PlantLevel, hash.c, hash.d);
            if (prefab)
            {
                if (otherPrefab && hash.c < usedHash)
                {
                    prefab = otherPrefab;
                }
            }
            else if (otherPrefab)
            {
                prefab = otherPrefab;
            }
            else
            {
                // No prefabs choosen
                return;
            }

            Transform instance = Instantiate(prefab);

            // Increase Y coord by half the height of the cube so it sits on the surface
            position.y += instance.localScale.y * 0.5f;

            // Perturb the position so that it matches the hex perturbation
            instance.localPosition = HexMetrics.Perturb(position);
            instance.localRotation = Quaternion.Euler(0f, 360f * hash.e, 0f);

            // Add to container
            instance.SetParent(container, false);
        }

        Transform PickPrefab(HexFeatureCollection[] collection,
            int level, float hash, float choice)
        {
            if (level > 0)
            {
                float[] thresholds = HexMetrics.GetFeatureThresholds(level - 1);
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (hash < thresholds[i])
                    {
                        return collection[i].Pick(choice);
                    }
                }
            }
            return null;
        }

    }
}