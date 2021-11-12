using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public class CloudManager : MonoBehaviour
    {
        [SerializeField]
        private HexGrid hexGrid;

        private int cloudCountMax;

        private float minCloudOffGroundHeight = 10f;
        private float maxCloudOffGroundHeight = 15f;

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
            Debug.Log("CreateCloud");
            GameObject sphere_go = CreateCloudShape();

            Vector3 targetPosition = targetCell.transform.position;
            targetPosition.y += height;

            // If targetcell is underwater, add extra height
            if (targetCell.IsUnderwater)
            {
                targetPosition.y += targetCell.WaterSurfaceY;
            }

            sphere_go.transform.localScale = new Vector3(4, 4, 4);
            sphere_go.transform.position = targetPosition;
            hexGrid.MakeChildOfColumn(sphere_go.transform, targetCell.ColumnIndex);

            return null;
        }

        private GameObject CreateCloudShape()
        {
            GameObject cloud = new GameObject("Cloud");



            return GameObject.CreatePrimitive(PrimitiveType.Sphere);
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