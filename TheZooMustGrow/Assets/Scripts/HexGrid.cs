using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace TheZooMustGrow
{
    public class HexGrid : MonoBehaviour
    {
        public int cellCountX = 20, cellCountZ = 15;

        private int chunkCountX, chunkCountZ;

        public HexCell cellPrefab;
        public TextMeshProUGUI cellLabelPrefab;

        HexCell[] cells;

        public Texture2D noiseSource;

        public HexGridChunk chunkPrefab;
        HexGridChunk[] chunks;

        public int seed;

        //public Color[] colors;

        HexCellPriorityQueue searchFrontier;

        private void Awake()
        {
            HexMetrics.noiseSource = noiseSource;
            HexMetrics.InitializeHashGrid(seed);
            //HexMetrics.colors = colors;

            CreateMap(cellCountX, cellCountZ);
        }

        public bool CreateMap(int x, int z)
        {
            // Verify sizes
            if (x <= 0 || x % HexMetrics.chunkSizeX != 0 ||
                z <= 0 || z % HexMetrics.chunkSizeZ != 0)
            {
                Debug.LogError("Unsupported map size.");
                return false;
            }

            // Clear old data
            if (chunks != null)
            {
                for (int i = 0; i < chunks.Length; i++)
                {
                    Destroy(chunks[i].gameObject);
                }
            }

            cellCountX = x;
            cellCountZ = z;

            chunkCountX = cellCountX / HexMetrics.chunkSizeX;
            chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;

            CreateChunks();
            CreateCells();
            return true;
        }

        private void CreateChunks()
        {
            chunks = new HexGridChunk[chunkCountX * chunkCountZ];

            for (int z = 0, i = 0; z < chunkCountZ; z++)
            {
                for (int x = 0; x < chunkCountX; x++)
                {
                    HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
                    chunk.transform.SetParent(transform);
                }
            }
        }

        private void CreateCells()
        {
            cells = new HexCell[cellCountZ * cellCountX];

            for (int z = 0, i = 0; z < cellCountZ; z++)
            {
                for (int x = 0; x < cellCountX; x++)
                {
                    CreateCell(x, z, i++);
                }
            }
        }

        void OnEnable()
        {
            if (!HexMetrics.noiseSource)
            {
                HexMetrics.noiseSource = noiseSource;
                HexMetrics.InitializeHashGrid(seed);
                //HexMetrics.colors = colors;
            }
        }

        private void CreateCell(int x, int z, int i)
        {
            Vector3 position;
            // Note integer division in x
            position.x = (x + (z * 0.5f) - z / 2) * (HexMetrics.innerRadius * 2f);
            position.y = 0f;
            position.z = z * (HexMetrics.outerRadius * 1.5f);

            HexCell cell = cells[i] = Instantiate(cellPrefab);
            cell.transform.localPosition = position;
            cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);

            // Set neighboring HexCells
            // East/west neighbors
            if (x > 0)
            {
                cell.SetNeighbor(HexDirection.W, cells[i - 1]);
            }
            // SE/SW
            if (z > 0)
            {
                // Use the binary AND as a mask, ignoring everything except the first bit.
                // If the result is 0, then it is an even number.
                if ((z & 1) == 0)
                {
                    // For the even rows
                    cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX]);
                    if (x > 0)
                    {
                        cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX - 1]);
                    }
                }
                else
                {
                    // For the odd rows
                    cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX]);
                    if (x < cellCountX - 1)
                    {
                        cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX + 1]);
                    }
                }
            }

            // Create label
            TextMeshProUGUI label = Instantiate(cellLabelPrefab);
            label.rectTransform.anchoredPosition = new Vector2(position.x, position.z);

            // Assign label rect to the HexCell
            cell.uiRect = label.rectTransform;

            // Set initial elevation to 0, this also perturbs y values
            cell.Elevation = 0;

            AddCellToChunk(x, z, cell);
        }

        void AddCellToChunk(int x, int z, HexCell cell)
        {
            int chunkX = x / HexMetrics.chunkSizeX;
            int chunkZ = z / HexMetrics.chunkSizeZ;
            HexGridChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

            int localX = x - chunkX * HexMetrics.chunkSizeX;
            int localZ = z - chunkZ * HexMetrics.chunkSizeZ;
            chunk.AddCell(localX + localZ * HexMetrics.chunkSizeX, cell);
        }

        public HexCell GetCell(Vector3 position)
        {
            position = transform.InverseTransformPoint(position);
            HexCoordinates coordinates = HexCoordinates.FromPosition(position);

            int index = coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;
            return cells[index];
        }

        public HexCell GetCell(HexCoordinates coordinates)
        {
            // Return null if the cell is out of bounds
            int z = coordinates.Z;
            if (z < 0 || z >= cellCountZ) { return null; }

            int x = coordinates.X + z / 2;
            if (x < 0 || x >= cellCountX) { return null; }

            return cells[x + z * cellCountX];
        }

        public void ShowUI(bool visible)
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].ShowUI(visible);
            }
        }

        public void FindPath(HexCell fromCell, HexCell toCell)
        {
            StopAllCoroutines();
            StartCoroutine(Search(fromCell, toCell));
        }

        /// <summary>
        /// Dijkstra's algorithm to find distances between cells
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        IEnumerator Search(HexCell fromCell, HexCell toCell)
        {
            // Initialize the searchFrontier priority queue
            if (searchFrontier == null)
            {
                searchFrontier = new HexCellPriorityQueue();
            }
            else
            {
                searchFrontier.Clear();
            }

            // First set all cell distances to max value
            // to track which distances have been determined.
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Distance = int.MaxValue;
                // Disable all highlights
                cells[i].DisableHighlight();
            }
            // Enable starting highlight
            fromCell.EnableHighlight(Color.blue);
            toCell.EnableHighlight(Color.red);

            WaitForSeconds delay = new WaitForSeconds(1 / 60f);

            // Current cell is distance 0
            fromCell.Distance = 0;
            searchFrontier.Enqueue(fromCell);

            // While a hex is still in the queue
            while (searchFrontier.Count > 0)
            {
                yield return delay;
                HexCell current = searchFrontier.Dequeue();

                // If we have reached the toCell, exit while
                if (current == toCell) 
                {
                    current = current.PathFrom;
                    while (current != fromCell)
                    {
                        current.EnableHighlight(Color.white);
                        current = current.PathFrom;
                    }
                    break; 
                }

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbor = current.GetNeighbor(d);
                    // If the neighbor exists
                    if (neighbor == null)
                    {
                        continue;
                    }

                    // Skip if underwater
                    if (neighbor.IsUnderwater)
                    {
                        continue;
                    }

                    HexEdgeType edgeType = current.GetEdgeType(neighbor);
                    if (edgeType == HexEdgeType.Cliff)
                    {
                        continue;
                    }

                    // Increase search cost if there is no road.
                    int distance = current.Distance;
                    if (current.HasRoadThroughEdge(d))
                    {
                        distance += 1;
                    }
                    else if (current.Walled != neighbor.Walled)
                    {
                        continue;
                    }
                    else
                    {
                        // Add costs for slopes vs flat
                        distance += edgeType == HexEdgeType.Flat ? 5 : 10;
                        // Add costs or features
                        distance += neighbor.UrbanLevel + neighbor.FarmLevel +
                            neighbor.PlantLevel;
                    }

                    if (neighbor.Distance == int.MaxValue)
                    {
                        neighbor.Distance = distance;
                        neighbor.PathFrom = current;
                        // Estimate remaining distance
                        neighbor.SearchHeuristic = 
                            neighbor.coordinates.DistanceTo(toCell.coordinates);
                        searchFrontier.Enqueue(neighbor);
                    }
                    else if (distance < neighbor.Distance)
                    {
                        int oldPriority = neighbor.SearchPriority;
                        neighbor.Distance = distance;
                        neighbor.PathFrom = current;
                        searchFrontier.Change(neighbor, oldPriority);
                    }
                }
            }
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(cellCountX);
            writer.Write(cellCountZ);

            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Save(writer);
            }
        }

        public void Load(BinaryReader reader, int header)
        {
            StopAllCoroutines();

            // Old default size
            int x = 20, z = 15;
            
            if (header >= 1)
            {
                x = reader.ReadInt32();
                z = reader.ReadInt32();
            }

            // If the cell size does not change,
            // do not need to create a new map
            if (x != cellCountX || z != cellCountZ)
            {
                // Abort if map creation failed
                if (!CreateMap(x, z)) { return; }
            }

            // Load each cell's data
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Load(reader);
            }

            // Refresh all chunks
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].Refresh();
            }
        }



    }
}