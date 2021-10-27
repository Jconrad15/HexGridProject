using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TheZoomMustGrow
{
    public class HexGrid : MonoBehaviour
    {
        public int width = 6;
        public int height = 6;

        public HexCell cellPrefab;
        public TextMeshProUGUI cellLabelPrefab;

        Canvas gridCanvas;

        HexCell[] cells;

        private void Awake()
        {
            gridCanvas = GetComponentInChildren<Canvas>();

            cells = new HexCell[height * width];

            for (int z = 0, i = 0; z < height; z++)
            {
                for (int x =0; x < width; x++)
                {
                    CreateCell(x, z, i++);
                }
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
            cell.transform.SetParent(transform, false);
            cell.transform.localPosition = position;

            // Create label
            TextMeshProUGUI label = Instantiate(cellLabelPrefab);
            label.rectTransform.SetParent(gridCanvas.transform, false);
            label.rectTransform.anchoredPosition =
                new Vector2(position.x, position.z);
            label.SetText(x.ToString() + "\n" + z.ToString());

        }

    }
}