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

        #region RIVER PROPERTIES
        bool hasIncomingRiver, hasOutgoingRiver;
        HexDirection incomingRiver, outgoingRiver;

        public bool HasIncomingRiver
        {
            get { return hasIncomingRiver; }
        }

        public bool HasOutgoingRiver
        {
            get { return hasOutgoingRiver; }
        }

        public HexDirection IncomingRiver
        {
            get { return incomingRiver; }
        }

        public HexDirection OutgoingRiver
        {
            get { return outgoingRiver; }
        }

        public bool HasRiver
        {
            get { return hasIncomingRiver || hasOutgoingRiver; }
        }

        public bool HasRiverBeginOrEnd
        {
            get
            {
                return hasIncomingRiver != hasOutgoingRiver;
            }
        }

        public float StreamBedY
        {
            get
            {
                return (elevation + HexMetrics.streamBedElevationOffset) *
                        HexMetrics.elevationStep;
            }
        }

        public float RiverSurfaceY
        {
            get
            {
                return
                    (elevation + HexMetrics.riverSurfaceElevationOffset) *
                    HexMetrics.elevationStep;
            }
        }
        #endregion

        [SerializeField]
        bool[] roads;

        public bool HasRoads
        {
            get
            {
                for (int i = 0; i < roads.Length; i++)
                {
                    if (roads[i])
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public HexDirection RiverBeginOrEndDirection
        {
            get
            {
                return hasIncomingRiver ? incomingRiver : outgoingRiver;
            }
        }

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

                // Remove rivers if they change to flowing uphill due to elevation change
                if (hasOutgoingRiver &&
                    elevation < GetNeighbor(outgoingRiver).elevation)
                {
                    RemoveOutgoingRiver();
                }

                if (hasIncomingRiver &&
                    elevation > GetNeighbor(incomingRiver).elevation)
                {
                    RemoveIncomingRiver();
                }

                // Remove Roads if they become too steep
                for (int i = 0; i < roads.Length; i++)
                {
                    if (roads[i] && GetElevationDifference((HexDirection)i) > 1)
                    {
                        SetRoad(i, false);
                    }
                }

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

        /// <summary>
        /// Returns true if there is a river going through 
        /// the edge at the provided direction. 
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public bool HasRiverThroughEdge(HexDirection direction)
        {
            return
                hasIncomingRiver && incomingRiver == direction ||
                hasOutgoingRiver && outgoingRiver == direction;
        }

        public void RemoveOutgoingRiver()
        {
            if (!hasOutgoingRiver)
            {
                return;
            }
            hasOutgoingRiver = false;
            RefreshSelfOnly();

            // Also need to remove the neighbors incoming river
            HexCell neighbor = GetNeighbor(outgoingRiver);
            neighbor.hasIncomingRiver = false;
            neighbor.RefreshSelfOnly();
        }

        public void RemoveIncomingRiver()
        {
            if (!hasIncomingRiver)
            {
                return;
            }
            hasIncomingRiver = false;
            RefreshSelfOnly();

            HexCell neighbor = GetNeighbor(incomingRiver);
            neighbor.hasOutgoingRiver = false;
            neighbor.RefreshSelfOnly();
        }

        public void RemoveRiver()
        {
            RemoveOutgoingRiver();
            RemoveIncomingRiver();
        }

        void RefreshSelfOnly()
        {
            chunk.Refresh();
        }

        public void SetOutgoingRiver(HexDirection direction)
        {
            if (hasOutgoingRiver && outgoingRiver == direction)
            { return; }

            HexCell neighbor = GetNeighbor(direction);
            // Abort if there is no neighbor
            // Abort if the neighbor is uphill
            if (!neighbor || elevation < neighbor.elevation)
            { return; }

            // Clear any previous outgoing river
            RemoveOutgoingRiver();
            // Clear any previous incoming river if it overlaps the new outgoing river
            if (hasIncomingRiver && incomingRiver == direction)
            {
                RemoveIncomingRiver();
            }

            // Set the outgoing river
            hasOutgoingRiver = true;
            outgoingRiver = direction;

            // Set the incoming river of the next cell
            neighbor.RemoveIncomingRiver();
            neighbor.hasIncomingRiver = true;
            neighbor.incomingRiver = direction.Opposite();

            // Remove the roads if a river is created
            SetRoad((int)direction, false);
        }

        public bool HasRoadThroughEdge(HexDirection direction)
        {
            return roads[(int)direction];
        }

        public void AddRoad(HexDirection direction)
        {
            if (!roads[(int)direction] &&
                !HasRiverThroughEdge(direction) &&
                GetElevationDifference(direction) <= 1)
            {
                SetRoad((int)direction, true);
            }
        }


        public void RemoveRoads()
        {
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (roads[i])
                {
                    SetRoad(i, false);
                }
            }
        }

        void SetRoad (int index, bool state)
        {
            roads[index] = state;
            // Also disable neighbor's roads
            neighbors[index].roads[(int)((HexDirection)index).Opposite()] = state;
            neighbors[index].RefreshSelfOnly();
            RefreshSelfOnly();
        }

        public int GetElevationDifference(HexDirection direction)
        {
            int difference = elevation - GetNeighbor(direction).elevation;
            return difference >= 0 ? difference : -difference;
        }

    }
}