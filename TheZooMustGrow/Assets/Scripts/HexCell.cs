using UnityEngine;

namespace TheZooMustGrow
{
    public class HexCell : MonoBehaviour
    {
        public HexCoordinates coordinates;

        public Color color;

        [SerializeField]
        private HexCell[] neighbors;

        public RectTransform uiRect;

        public int Elevation
        {
            get { return elevation; }
            set 
            { 
                elevation = value;

                // Adjust the actual height of the mesh
                Vector3 position = transform.localPosition;
                position.y = value * HexMetrics.elevationStep;
                transform.localPosition = position;

                // Adjust the height of the UI label
                Vector3 uiPosition = uiRect.localPosition;
                uiPosition.z = elevation * -HexMetrics.elevationStep;
                uiRect.localPosition = uiPosition;
            }
        }
        private int elevation;

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

    }
}