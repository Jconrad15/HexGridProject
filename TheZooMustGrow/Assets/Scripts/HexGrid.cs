using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TheZooMustGrow
{
    public class HexGrid : MonoBehaviour
    {
        public int chunkCountX = 4, chunkCountZ = 3;

        private int cellCountX, cellCountZ;

        public HexCell cellPrefab;
        public TextMeshProUGUI cellLabelPrefab;

        HexCell[] cells;

        public Color defaultColor = Color.white;

        public Texture2D noiseSource;

        public HexGridChunk chunkPrefab;
        HexGridChunk[] chunks;


        private void Awake()
        {
            HexMetrics.noiseSource = noiseSource;

            cellCountX = chunkCountX * HexMetrics.chunkSizeX;
            cellCountZ = chunkCountZ * HexMetrics.chunkSizeZ;

            CreateChunks();
            CreateCells();
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
            HexMetrics.noiseSource = noiseSource;
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
            cell.Color = defaultColor;

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
            label.rectTransform.anchoredPosition =
                new Vector2(position.x, position.z);
            label.SetText(cell.coordinates.ToStringOnSeparateLines());

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


    }
}