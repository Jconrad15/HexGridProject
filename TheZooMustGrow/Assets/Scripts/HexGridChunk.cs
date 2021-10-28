using UnityEngine;
using UnityEngine.UI;

namespace TheZooMustGrow
{
    public class HexGridChunk : MonoBehaviour
    {
		HexCell[] cells;

        HexMesh hexMesh;
		Canvas gridCanvas;

		void Awake()
		{
			gridCanvas = GetComponentInChildren<Canvas>();
			hexMesh = GetComponentInChildren<HexMesh>();

			cells = new HexCell[HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ];
            ShowUI(false);
        }

		public void AddCell (int index, HexCell cell)
        {
			cells[index] = cell;
			cell.chunk = this;
			cell.transform.SetParent(transform, false);
			cell.uiRect.SetParent(gridCanvas.transform, false);
        }

        // Don't have to immediately triangulate when a chunk is refreshed.
        // Instead, we can take note that an update is needed,
        // and triangulate once editing is finished.
        public void Refresh()
		{
            enabled = true;
		}

		void LateUpdate()
		{
			hexMesh.Triangulate(cells);
			enabled = false;
		}

		public void ShowUI(bool visible)
        {
			gridCanvas.gameObject.SetActive(visible);
        }

	}
}