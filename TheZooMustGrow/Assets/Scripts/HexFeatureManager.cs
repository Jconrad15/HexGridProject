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

        public void AddFeature(Vector3 position)
        {
            Transform instance = Instantiate(featurePrefab);
            
            // Increase Y coord by half the height of the cube so it sits on the surface
            position.y += instance.localScale.y * 0.5f;

            // Perturb the position so that it matches the hex perturbation
            instance.localPosition = HexMetrics.Perturb(position);

            // Add to container
            instance.SetParent(container, false);
        }


    }
}