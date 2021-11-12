using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public class CloudManager : MonoBehaviour
    {
        [SerializeField]
        private HexGrid hexGrid;

        [SerializeField]
        private Material cloudMaterial;

        private int cloudCountMax;

        private float minCloudOffGroundHeight = 10f;
        private float maxCloudOffGroundHeight = 15f;

        private int minCloudSections = 6;
        private int maxCloudSections = 12;

        private Transform[] clouds;

        public void GenerateNewClouds(int seed)
        {
            Random.State originalRandomState = Random.state;
            Random.InitState(seed);

            // One cloud per chunk
            cloudCountMax = hexGrid.GetChunkCount();
            clouds = new Transform[cloudCountMax];

            // Determine cloud positions
            HexCell[] cloudCells = new HexCell[cloudCountMax];
            List<int> possibleIndices = CreateIndexList();
            for (int i = 0; i < cloudCountMax; i++)
            {
                // Select index
                int selectedIndex = possibleIndices[Random.Range(0, possibleIndices.Count)];

                // Remove index from possible indices
                int lastIndex = possibleIndices.Count - 1;
                possibleIndices[selectedIndex] = possibleIndices[lastIndex];
                possibleIndices.RemoveAt(lastIndex);

                cloudCells[i] = hexGrid.GetCell(selectedIndex);
            }
            
            // Create the clouds
            for (int i = 0; i < cloudCountMax; i++)
            {
                float height = Random.Range(minCloudOffGroundHeight, maxCloudOffGroundHeight);

                clouds[i] = PlaceCloud(cloudCells[i], height);
            }

            Random.state = originalRandomState;
        }

        private Transform PlaceCloud(HexCell targetCell, float height)
        {
            GameObject sphere_go = CreateCloudShape();

            Vector3 targetPosition = targetCell.transform.position;
            targetPosition.y += height;

            // If targetcell is underwater, add extra height
            if (targetCell.IsUnderwater)
            {
                targetPosition.y += targetCell.WaterSurfaceY;
            }

            sphere_go.transform.position = targetPosition;
            hexGrid.MakeChildOfColumn(sphere_go.transform, targetCell.ColumnIndex);

            return null;
        }

        private GameObject CreateCloudShape()
        {
            GameObject cloud = new GameObject("Cloud");

            int cloudSections = Random.Range(minCloudSections, maxCloudSections + 1);

            // Determine cloud sizes and positions
            float[] cloudRadii = new float[cloudSections];
            float maxRadius = 2f;

            Vector3[] cloudPositions = new Vector3[cloudSections];
            Vector3 startingPos = new Vector3(2, 0, 0);

            for (int i = 0; i < cloudSections; i++)
            {
                cloudRadii[i] = maxRadius - (i / (float)cloudSections);

                Vector3 pos = startingPos;
                pos.x -= i > 0 ? cloudRadii[i - 1] + cloudRadii[i] : i;
                cloudPositions[i] = pos;

                startingPos = pos;
            }

            // Create cloud GameObjects
            for (int i = 0; i < cloudSections; i++)
            {
                // Create Cloud Section
                GameObject newCloudSection = IcoSphere.Create(cloudMaterial, cloudRadii[i]);
                newCloudSection.name = "CloudSection" + i.ToString();
                newCloudSection.transform.SetParent(cloud.transform);

                // Set position
                newCloudSection.transform.localPosition = cloudPositions[i];
            }

            return cloud;
        }

        private List<int> CreateIndexList()
        {
            List<int> indices = new List<int>();

            for (int i = 0; i < hexGrid.GetCellCount(); i++)
            {
                indices.Add(i);
            }
            return indices;
        }


    }
}