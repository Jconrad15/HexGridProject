using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TheZooMustGrow
{
    public class HexGrid : MonoBehaviour
    {
        public int width = 6;
        public int height = 6;

        public HexCell cellPrefab;
        public TextMeshProUGUI cellLabelPrefab;

        Canvas gridCanvas;

        HexCell[] cells;
        HexMesh hexMesh;

        public Color defaultColor = Color.white;

        public Texture2D noiseSource;

        private void Awake()
        {
            HexMetrics.noiseSource = noiseSource;

            gridCanvas = GetComponentInChildren<Canvas>();
            hexMesh = GetComponentInChildren<HexMesh>();

            cells = new HexCell[height * width];

            for (int z = 0, i = 0; z < height; z++)
            {
                for (int x =0; x < width; x++)
                {
                    CreateCell(x, z, i++);
                }
            }
        }

        void OnEnable()
        {
            HexMetrics.noiseSource = noiseSource;
        }

        private void Start()
        {
            hexMesh.Triangulate(cells);
        }

        private void CreateCell(int x, int z, int i)
        {
            Vector3 position;
            // Note integer division in x
            position.x = (x + (z * 0.5f) - z / 2) * (HexMetrics.innerRadius * 2f);
            position.y = 0f;
            position.z = z * (HexMetrics.outerRadius * 1.5f);

            HexCell cell = cells[i] = Instantiate(cellPrefab);
            cell.transform.SetParent(transform, false);
            cell.transform.localPosition = position;
            cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
            cell.color = defaultColor;

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
                    cell.SetNeighbor(HexDirection.SE, cells[i - width]);
                    if (x > 0)
                    {
                        cell.SetNeighbor(HexDirection.SW, cells[i - width - 1]);
                    }
                }
                else
                {
                    // For the odd rows
                    cell.SetNeighbor(HexDirection.SW, cells[i - width]);
                    if (x < width - 1)
                    {
                        cell.SetNeighbor(HexDirection.SE, cells[i - width + 1]);
                    }
                }
            }

            // Create label
            TextMeshProUGUI label = Instantiate(cellLabelPrefab);
            label.rectTransform.SetParent(gridCanvas.transform, false);
            label.rectTransform.anchoredPosition =
                new Vector2(position.x, position.z);
            label.SetText(cell.coordinates.ToStringOnSeparateLines());

            // Assign label rect to the HexCell
            cell.uiRect = label.rectTransform;
        }

        public HexCell GetCell(Vector3 position)
        {
            position = transform.InverseTransformPoint(position);
            HexCoordinates coordinates = HexCoordinates.FromPosition(position);

            int index = coordinates.X + coordinates.Z * width + coordinates.Z / 2;
            return cells[index];
        }

        public void Refresh()
        {
            hexMesh.Triangulate(cells);
        }

    }
}