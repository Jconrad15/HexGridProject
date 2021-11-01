using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public class HexFeatureManager : MonoBehaviour
    {
        public Transform featurePrefab;
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

            // Don't add features in some cases based on hash A coord and urbanLevel
            if (hash.a >= cell.UrbanLevel * 0.25f) { return; }

            Transform instance = Instantiate(featurePrefab);
            
            // Increase Y coord by half the height of the cube so it sits on the surface
            position.y += instance.localScale.y * 0.5f;

            // Perturb the position so that it matches the hex perturbation
            instance.localPosition = HexMetrics.Perturb(position);
            instance.localRotation = Quaternion.Euler(0f, 360f * hash.b, 0f);

            // Add to container
            instance.SetParent(container, false);
        }


    }
}