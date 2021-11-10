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
        private Transform[] columns;
        private int currentCenterColumnIndex = -1;

        public HexUnit unitPrefab;

        public int cellCountX = 20, cellCountZ = 15;

        private int chunkCountX, chunkCountZ;

        public HexCell cellPrefab;
        public TextMeshProUGUI cellLabelPrefab;

        HexCell[] cells;

        public Texture2D noiseSource;

        public HexGridChunk chunkPrefab;
        HexGridChunk[] chunks;

        public int seed;

        HexCellShaderData cellShaderData;

        HexCellPriorityQueue searchFrontier;
        int searchFrontierPhase;

        HexCell currentPathFrom, currentPathTo;

        bool currentPathExists;
        public bool HasPath
        {
            get
            {
                return currentPathExists;
            }
        }

        private List<HexUnit> units = new List<HexUnit>();
        public bool wrapping;

        private void Awake()
        {
            HexMetrics.noiseSource = noiseSource;
            HexMetrics.InitializeHashGrid(seed);
            HexUnit.unitPrefab = unitPrefab;
            cellShaderData = gameObject.AddComponent<HexCellShaderData>();
            cellShaderData.Grid = this;
            CreateMap(cellCountX, cellCountZ, wrapping);
        }

        public bool CreateMap(int x, int z, bool wrapping)
        {
            // Verify sizes
            if (x <= 0 || x % HexMetrics.chunkSizeX != 0 ||
                z <= 0 || z % HexMetrics.chunkSizeZ != 0)
            {
                Debug.LogError("Unsupported map size.");
                return false;
            }

            // Clear old data
            ClearPath();
            ClearUnits();
            if (columns != null)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    Destroy(columns[i].gameObject);
                }
            }

            cellCountX = x;
            cellCountZ = z;
            this.wrapping = wrapping;
            currentCenterColumnIndex = -1; //-1==default value

            // Set Hexmetric wrap size
            HexMetrics.wrapSize = wrapping ? cellCountX : 0;

            chunkCountX = cellCountX / HexMetrics.chunkSizeX;
            chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;

            // Initialize the shader data for this hexGrid
            cellShaderData.Initialize(cellCountX, cellCountZ);

            CreateChunks();
            CreateCells();
            return true;
        }

        private void CreateChunks()
        {
            // Create column info
            columns = new Transform[chunkCountX];
            for (int x = 0; x < chunkCountX; x++)
            {
                columns[x] = new GameObject("Column").transform;
                columns[x].SetParent(transform, false);
            }

            // Create chunk info
            chunks = new HexGridChunk[chunkCountX * chunkCountZ];

            for (int z = 0, i = 0; z < chunkCountZ; z++)
            {
                for (int x = 0; x < chunkCountX; x++)
                {
                    HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
                    chunk.transform.SetParent(columns[x], false);
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
                HexUnit.unitPrefab = unitPrefab;

                // Set Hexmetric wrap size
                HexMetrics.wrapSize = wrapping ? cellCountX : 0;

                ResetVisibility();
            }
        }

        private void CreateCell(int x, int z, int i)
        {
            Vector3 position;
            // Note integer division in x
            position.x = (x + (z * 0.5f) - z / 2) * HexMetrics.innerDiameter;
            position.y = 0f;
            position.z = z * (HexMetrics.outerRadius * 1.5f);

            HexCell cell = cells[i] = Instantiate(cellPrefab);
            cell.transform.localPosition = position;
            cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
            cell.Index = i;
            // Assign shader data component
            cell.ShaderData = cellShaderData;

            cell.Explorable =
                x > 0 && z > 0 && x < cellCountX - 1 && z < cellCountZ - 1;

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

        public HexCell GetCell(int xOffset, int zOffset)
        {
            return cells[xOffset + (zOffset * cellCountX)];
        }

        public HexCell GetCell(int cellIndex)
        {
            return cells[cellIndex];
        }

        public void ShowUI(bool visible)
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].ShowUI(visible);
            }
        }

        public void FindPath(HexCell fromCell, HexCell toCell, HexUnit unit)
        {
            ClearPath();
            currentPathFrom = fromCell;
            currentPathTo = toCell;
            currentPathExists = Search(fromCell, toCell, unit);
            ShowPath(unit.Speed);
        }

        /// <summary>
        /// Dijkstra's algorithm to find distances between cells
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        private bool Search(HexCell fromCell, HexCell toCell, HexUnit unit)
        {
            int speed = unit.Speed;
            searchFrontierPhase += 2;

            // Initialize the searchFrontier priority queue
            if (searchFrontier == null)
            {
                searchFrontier = new HexCellPriorityQueue();
            }
            else
            {
                searchFrontier.Clear();
            }

            // Current cell is distance 0
            fromCell.SearchPhase = searchFrontierPhase;
            fromCell.Distance = 0;
            searchFrontier.Enqueue(fromCell);

            // While a hex is still in the queue
            while (searchFrontier.Count > 0)
            {
                HexCell current = searchFrontier.Dequeue();
                current.SearchPhase += 1;

                // If we have reached the toCell, exit while
                if (current == toCell) 
                {
                    return true;
                }

                int currentTurn = (current.Distance - 1) / speed;

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbor = current.GetNeighbor(d);

                    if (neighbor == null ||
                        neighbor.SearchPhase >searchFrontierPhase)
                    {
                        continue;
                    }

                    if (!unit.IsValidDestination(neighbor))
                    {
                        continue;
                    }

                    int moveCost = unit.GetMoveCost(current, neighbor, d);
                    if (moveCost < 0)
                    {
                        continue;
                    }

                    int distance = current.Distance + moveCost;
                    int turn = (distance - 1) / speed;
                    if (turn > currentTurn)
                    {
                        distance = turn * speed + moveCost;
                    }

                    if (neighbor.SearchPhase < searchFrontierPhase)
                    {
                        neighbor.SearchPhase = searchFrontierPhase;

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
            return false;
        }

        void ShowPath(int speed)
        {
            if (currentPathExists)
            {
                HexCell current = currentPathTo;
                while (current != currentPathFrom)
                {
                    int turn = (current.Distance - 1) / speed;
                    current.SetLabel(turn.ToString());
                    current.EnableHighlight(Color.white);
                    current = current.PathFrom;
                }
            }
            currentPathFrom.EnableHighlight(Color.blue);
            currentPathTo.EnableHighlight(Color.red);
        }

        public void ClearPath()
        {
            if (currentPathExists)
            {
                HexCell current = currentPathTo;
                while (current != currentPathFrom)
                {
                    current.SetLabel(null);
                    current.DisableHighlight();
                    current = current.PathFrom;
                }
                current.DisableHighlight();
                currentPathExists = false;
            }
            else if (currentPathFrom)
            {
                // Clear endpoints from potential invalid paths
                currentPathFrom.DisableHighlight();
                currentPathTo.DisableHighlight();
            }
            currentPathFrom = currentPathTo = null;
        }

        private void ClearUnits()
        {
            for (int i = 0; i < units.Count; i++)
            {
                units[i].Die();
            }
            units.Clear();
        }

        public void AddUnit(HexUnit unit, HexCell location, float orientation)
        {
            units.Add(unit);
            unit.Grid = this;
            unit.transform.SetParent(transform, false);
            unit.Location = location;
            unit.Orientation = orientation;
        }

        public void RemoveUnit(HexUnit unit)
        {
            units.Remove(unit);
            unit.Die();
        }

        public HexCell GetCell(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                return GetCell(hit.point);
            }
            return null;
        }

        public List<HexCell> GetPath()
        {
            if (!currentPathExists) 
            { 
                return null; 
            }

            List<HexCell> path = ListPool<HexCell>.Get();

            for (HexCell c = currentPathTo; c != currentPathFrom; c = c.PathFrom)
            {
                path.Add(c);
            }

            // Also add starting cell
            path.Add(currentPathFrom);

            path.Reverse();

            return path;
        }

        List<HexCell> GetVisibleCells(HexCell fromCell, int range)
        {
            List<HexCell> visibleCells = ListPool<HexCell>.Get();

            searchFrontierPhase += 2;
            if (searchFrontier == null)
            {
                searchFrontier = new HexCellPriorityQueue();
            }
            else
            {
                searchFrontier.Clear();
            }

            range += fromCell.ViewElevation;
            fromCell.SearchPhase = searchFrontierPhase;
            fromCell.Distance = 0;
            searchFrontier.Enqueue(fromCell);
            HexCoordinates fromCoordinates = fromCell.coordinates;
            while (searchFrontier.Count > 0)
            {
                HexCell current = searchFrontier.Dequeue();
                current.SearchPhase += 1;
                visibleCells.Add(current);

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbor = current.GetNeighbor(d);
                    if (
                        neighbor == null ||
                        neighbor.SearchPhase > searchFrontierPhase ||
                        !neighbor.Explorable)
                    {
                        continue;
                    }

                    int distance = current.Distance + 1;
                    if (distance + neighbor.ViewElevation > range ||
                        distance > fromCoordinates.DistanceTo(neighbor.coordinates))
                    {
                        continue;
                    }

                    if (neighbor.SearchPhase < searchFrontierPhase)
                    {
                        neighbor.SearchPhase = searchFrontierPhase;
                        neighbor.Distance = distance;
                        neighbor.SearchHeuristic = 0;
                        searchFrontier.Enqueue(neighbor);
                    }
                    else if (distance < neighbor.Distance)
                    {
                        int oldPriority = neighbor.SearchPriority;
                        neighbor.Distance = distance;
                        searchFrontier.Change(neighbor, oldPriority);
                    }
                }
            }
            return visibleCells;
        }

        public void IncreaseVisibility(HexCell fromCell, int range)
        {
            List<HexCell> cells = GetVisibleCells(fromCell, range);
            for (int i = 0; i < cells.Count; i++)
            {
                cells[i].IncreaseVisibility();
            }
            ListPool<HexCell>.Add(cells);
        }

        public void DecreaseVisibility(HexCell fromCell, int range)
        {
            List<HexCell> cells = GetVisibleCells(fromCell, range);
            for (int i = 0; i < cells.Count; i++)
            {
                cells[i].DecreaseVisibility();
            }
            ListPool<HexCell>.Add(cells);
        }

        public void ResetVisibility()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].ResetVisibility();
            }

            // Update the vision of each unit
            for (int i = 0; i < units.Count; i++)
            {
                HexUnit unit = units[i];
                IncreaseVisibility(unit.Location, unit.VisionRange);
            }
        }

        public void CenterMap(float xPosition)
        {
            int centerColumnIndex = 
                (int)(xPosition / (HexMetrics.innerDiameter * HexMetrics.chunkSizeX));

            if (centerColumnIndex == currentCenterColumnIndex)
            {
                return;
            }

            currentCenterColumnIndex = centerColumnIndex;

            int minColumnIndex = centerColumnIndex - (chunkCountX / 2);
            int maxColumnIndex = centerColumnIndex + (chunkCountX / 2);

            Vector3 position;
            position.y = position.z = 0f;
            for (int i = 0; i < columns.Length; i++)
            {
                // Check if position is too small
                if (i < minColumnIndex)
                {
                    position.x = chunkCountX *
                        (HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
                }
                // Check if position is too large
                else if (i > maxColumnIndex)
                {
                    position.x = chunkCountX *
                        -(HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
                }
                else
                {
                    // Otherwise zero
                    position.x = 0f;
                }

                columns[i].localPosition = position;

            }

        }

        public void Save(BinaryWriter writer)
        {
            // Save the cell counts
            writer.Write(cellCountX);
            writer.Write(cellCountZ);

            // Saving the wrapping toggle
            writer.Write(wrapping);

            // Save the cell data
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Save(writer);
            }

            // Save the unit data
            writer.Write(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                units[i].Save(writer);
            }
        }

        public void Load(BinaryReader reader, int header)
        {
            ClearPath();
            ClearUnits();

            // Old default size
            int x = 20, z = 15;
            
            if (header >= 1)
            {
                x = reader.ReadInt32();
                z = reader.ReadInt32();
            }

            bool wrapping = header >= 5 ? reader.ReadBoolean() : false;
            // If the cell size does not change,
            // do not need to create a new map
            if (x != cellCountX || z != cellCountZ || this.wrapping != wrapping)
            {
                // Abort if map creation failed
                if (!CreateMap(x, z, wrapping)) { return; }
            }

            bool originalImmediateMode = cellShaderData.ImmediateMode;
            cellShaderData.ImmediateMode = true;
            // Load each cell's data
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Load(reader, header);
            }

            // Refresh all chunks
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].Refresh();
            }

            // Load the units
            if (header >= 2)
            {
                int unitCount = reader.ReadInt32();
                for (int i = 0; i < unitCount; i++)
                {
                    HexUnit.Load(reader, this);
                }
            }

            cellShaderData.ImmediateMode = originalImmediateMode;
        }



    }
}