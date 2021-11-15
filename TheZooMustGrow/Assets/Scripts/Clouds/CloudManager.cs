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

        private float minCloudOffGroundHeight = 15f;
        private float maxCloudOffGroundHeight = 20f;

        private int minCloudSections = 6;
        private int maxCloudSections = 12;

        private float maxCloudRadius = 2f;
        private float minCloudRadius = 0.5f;

        private Transform[] clouds;

        public void GenerateNewClouds(int seed)
        {
            Random.State originalRandomState = Random.state;
            Random.InitState(seed);

            // Two clouds per chunk on average
            cloudCountMax = hexGrid.GetChunkCount() * 2;
            clouds = new Transform[cloudCountMax];

            // Determine cloud positions
            HexCell[] cloudCells = new HexCell[cloudCountMax];
            List<int> possibleIndices = CreateIndexList();
            for (int i = 0; i < cloudCountMax; i++)
            {
                // Select cell index
                int cellIndex = possibleIndices[Random.Range(0, possibleIndices.Count-1)];

                // Remove index from possible indices
                int lastIndex = possibleIndices.Count - 1;

                possibleIndices[possibleIndices.IndexOf(cellIndex)] = possibleIndices[lastIndex];
                possibleIndices.RemoveAt(lastIndex);

                cloudCells[i] = hexGrid.GetCell(cellIndex);
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

            return sphere_go.transform;
        }

        private GameObject CreateCloudShape()
        {
            GameObject cloud = new GameObject("Cloud");

            int cloudSections = Random.Range(minCloudSections, maxCloudSections + 1);

            // Determine cloud sizes and positions
            float[] cloudRadii = new float[cloudSections];

            Vector3[] cloudPositions = new Vector3[cloudSections];
            Vector3 previousPos = new Vector3(0, 0, 0);

            for (int i = 0; i < cloudSections; i++)
            {
                cloudRadii[i] = Mathf.Lerp(maxCloudRadius, minCloudRadius, (i/(float)cloudSections));

                Vector3 pos = previousPos;
                if (i > 0)
                {
                    // Get random direction
                    Vector3 direction = Vector3.zero;

                    int abortCounter = 2000;
                    int counter = 0;
                    // Check if the direction brings pos too close to the previous pos
                    while (Mathf.Abs(Vector3.Distance(pos + direction, previousPos))
                        <= 0.8f * cloudRadii[i - 1])
                    {
                        if (counter >= abortCounter) 
                        {
                            Debug.LogWarning("Cloud direction abort counter reached");
                            break; 
                        }

                        direction = new Vector3(
                            Random.Range(-1f, 0.5f),
                            Random.Range(-0.1f, 0.1f),
                            Random.Range(-1f, 1f));

                        direction = Vector3.Normalize(direction);
                        direction *= cloudRadii[i - 1];

                        counter += 1;
                    }

                    pos += direction;
                }

                cloudPositions[i] = pos;
                previousPos = pos;
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