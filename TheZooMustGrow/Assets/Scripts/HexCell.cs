using UnityEngine;

namespace TheZooMustGrow
{
    public class HexCell : MonoBehaviour
    {
        public HexCoordinates coordinates;

        [SerializeField]
        private HexCell[] neighbors;

        public RectTransform uiRect;

        public HexGridChunk chunk;

        public Vector3 Position
        {
            get
            {
                return transform.localPosition;
            }
        }

        public int Elevation
        {
            get { return elevation; }
            set 
            {
                // Return if not changed
                if (elevation == value) { return; }

                elevation = value;

                // Adjust the actual height of the mesh
                Vector3 position = transform.localPosition;
                position.y = value * HexMetrics.elevationStep;
                // Perturb the y value
                position.y +=
                    ((HexMetrics.SampleNoise(position).y * 2f) - 1f) *
                    HexMetrics.elevationPerturbStrength;
                transform.localPosition = position;

                // Adjust the height of the UI label
                Vector3 uiPosition = uiRect.localPosition;
                uiPosition.z = (elevation * -HexMetrics.elevationStep) - HexMetrics.labelOffset;
                uiRect.localPosition = uiPosition;

                Refresh();
            }
        }
        private int elevation = int.MinValue;

        public Color Color
        {
            get { return color; }
            set
            {
                if (color == value) { return; }
                color = value;
                Refresh();
            }
        }
        private Color color;

        /// <summary>
        /// Returns the neighboring HexCell in the provided direction.
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public HexCell GetNeighbor(HexDirection direction)
        {
            return neighbors[(int)direction];
        }

        /// <summary>
        /// Sets the neighboring Hexcell in the provided direction.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="cell"></param>
        public void SetNeighbor(HexDirection direction, HexCell cell)
        {
            neighbors[(int)direction] = cell;
            cell.neighbors[(int)direction.Opposite()] = this;
        }

        public HexEdgeType GetEdgeType(HexDirection direction)
        {
            return HexMetrics.GetEdgeType(elevation, neighbors[(int)direction].elevation);
        }

        public HexEdgeType GetEdgeType(HexCell otherCell)
        {
            return HexMetrics.GetEdgeType(elevation, otherCell.elevation);
        }

        void Refresh()
        {
            // Only refresh the chunk if it has been assigned
            if (chunk)
            {
                chunk.Refresh();
                for (int i = 0; i < neighbors.Length; i++)
                {
                    HexCell neighbor = neighbors[i];
                    if (neighbor != null && neighbor.chunk != chunk)
                    {
                        neighbor.chunk.Refresh();
                    }
                }
            }
        }
    }
}