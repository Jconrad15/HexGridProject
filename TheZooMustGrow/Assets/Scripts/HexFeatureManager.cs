using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public class HexFeatureManager : MonoBehaviour
    {
        public HexFeatureCollection[] urbanCollections;
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
            Transform prefab = PickPrefab(cell.UrbanLevel, hash.a, hash.b);
            // Return if none choosen
            if (!prefab) { return; }

            Transform instance = Instantiate(prefab);

            // Increase Y coord by half the height of the cube so it sits on the surface
            position.y += instance.localScale.y * 0.5f;

            // Perturb the position so that it matches the hex perturbation
            instance.localPosition = HexMetrics.Perturb(position);
            instance.localRotation = Quaternion.Euler(0f, 360f * hash.c, 0f);

            // Add to container
            instance.SetParent(container, false);
        }

        Transform PickPrefab(int level, float hash, float choice)
        {
            if (level > 0)
            {
                float[] thresholds = HexMetrics.GetFeatureThresholds(level - 1);
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (hash < thresholds[i])
                    {
                        return urbanCollections[i].Pick(choice);
                    }
                }
            }
            return null;
        }

    }
}