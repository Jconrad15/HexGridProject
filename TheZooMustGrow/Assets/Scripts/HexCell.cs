using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI;

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
                    (elevation + HexMetrics.waterElevationOffset) *
                    HexMetrics.elevationStep;
            }
        }

        public HexDirection RiverBeginOrEndDirection
        {
            get
            {
                return hasIncomingRiver ? incomingRiver : outgoingRiver;
            }
        }

        bool IsValidRiverDestination(HexCell neighbor)
        {
            return neighbor && (
                elevation >= neighbor.elevation || waterLevel == neighbor.elevation
            );
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

        private int waterLevel;
        public int WaterLevel
        {
            get
            {
                return waterLevel;
            }
            set
            {
                if (waterLevel == value) { return; }
                waterLevel = value;
                ValidateRivers();
                Refresh();
            }
        }

        public bool IsUnderwater
        {
            get
            {
                return waterLevel > elevation;
            }
        }

        public float WaterSurfaceY
        {
            get
            {
                return (waterLevel + HexMetrics.waterElevationOffset) *
                    HexMetrics.elevationStep;
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

                RefreshPosition();

                // Remove rivers if they change to flowing uphill due to elevation change
                ValidateRivers();

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

        private int terrainTypeIndex;
        public int TerrainTypeIndex
        {
            get
            {
                return terrainTypeIndex;
            }
            set
            {
                if (terrainTypeIndex != value)
                {
                    terrainTypeIndex = value;
                    Refresh();
                }
            }
        }

/*        private Color color;
        public Color Color
        {
            get { return HexMetrics.colors[terrainTypeIndex]; }
        }*/

        // Features
        private int urbanLevel;
        public int UrbanLevel
        {
            get
            {
                return urbanLevel;
            }
            set
            {
                if (urbanLevel != value)
                {
                    urbanLevel = value;
                    RefreshSelfOnly();
                }
            }
        }

        private int farmLevel;
        public int FarmLevel
        {
            get
            {
                return farmLevel;
            }
            set
            {
                if (farmLevel != value)
                {
                    farmLevel = value;
                    RefreshSelfOnly();
                }
            }
        }

        private int plantLevel;
        public int PlantLevel
        {
            get
            {
                return plantLevel;
            }
            set
            {
                if (plantLevel != value)
                {
                    plantLevel = value;
                    RefreshSelfOnly();
                }
            }
        }

        private bool walled;
        public bool Walled
        {
            get
            {
                return walled;
            }
            set
            {
                if (walled != value)
                {
                    walled = value;
                    Refresh();
                }
            }
        }

        private int specialIndex;
        public int SpecialIndex
        {
            get
            {
                return specialIndex;
            }
            set
            {
                if (specialIndex != value && !HasRiver)
                {
                    specialIndex = value;
                    RemoveRoads();
                    RefreshSelfOnly();
                }
            }
        }

        public bool IsSpecial
        {
            get
            {
                return specialIndex > 0;
            }
        }

        private int distance;
        public int Distance
        {
            get
            {
                return distance;
            }
            set
            {
                distance = value;
                UpdateDistanceLabel();
            }
        }

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

        void RefreshPosition()
        {
            // Adjust the actual height of the mesh
            Vector3 position = transform.localPosition;
            position.y = elevation * HexMetrics.elevationStep;
            // Perturb the y value
            position.y +=
                ((HexMetrics.SampleNoise(position).y * 2f) - 1f) *
                HexMetrics.elevationPerturbStrength;
            transform.localPosition = position;

            // Adjust the height of the UI label
            Vector3 uiPosition = uiRect.localPosition;
            uiPosition.z = (elevation * -HexMetrics.elevationStep) - HexMetrics.labelOffset;
            uiRect.localPosition = uiPosition;
        }

        public void SetOutgoingRiver(HexDirection direction)
        {
            if (hasOutgoingRiver && outgoingRiver == direction)
            { return; }

            HexCell neighbor = GetNeighbor(direction);
            // Abort if there is no neighbor
            // Abort if the neighbor is uphill
            if (!IsValidRiverDestination(neighbor))
            {
                return;
            }

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
            // Remove special features
            specialIndex = 0;

            // Set the incoming river of the next cell
            neighbor.RemoveIncomingRiver();
            neighbor.hasIncomingRiver = true;
            neighbor.incomingRiver = direction.Opposite();
            neighbor.specialIndex = 0;

            // Remove the roads if a river is created
            SetRoad((int)direction, false);
        }

        void ValidateRivers()
        {
            if ( hasOutgoingRiver &&
                !IsValidRiverDestination(GetNeighbor(outgoingRiver)))
            {
                RemoveOutgoingRiver();
            }
            if (hasIncomingRiver &&
                !GetNeighbor(incomingRiver).IsValidRiverDestination(this))
            {
                RemoveIncomingRiver();
            }
        }

        public bool HasRoadThroughEdge(HexDirection direction)
        {
            return roads[(int)direction];
        }

        public void AddRoad(HexDirection direction)
        {
            if (!roads[(int)direction] &&
                !HasRiverThroughEdge(direction) &&
                !IsSpecial && !GetNeighbor(direction).IsSpecial &&
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

        void UpdateDistanceLabel()
        {
            TextMeshProUGUI label = uiRect.GetComponent<TextMeshProUGUI>();
            string distanceText = distance == int.MaxValue ? "" : distance.ToString();
            label.SetText(distanceText);
        }

        /// <summary>
        /// Disables the highlight from the cell label.
        /// </summary>
        public void DisableHighlight()
        {
            Image highlight = uiRect.GetChild(0).GetComponent<Image>();
            highlight.enabled = false;
        }

        /// <summary>
        /// Enables the highlight from the cell label.
        /// </summary>
        public void EnableHighlight(Color color)
        {
            Image highlight = uiRect.GetChild(0).GetComponent<Image>();
            highlight.color = color;
            highlight.enabled = true;
        }

        public void Save(BinaryWriter writer)
        {
            // The integer values only cover a small value range.
            // They stay inside the 0–255 range each.
            // This means that only the first byte of each integer will be used.
            writer.Write((byte)terrainTypeIndex);
            writer.Write((byte)elevation);
            writer.Write((byte)waterLevel);
            writer.Write((byte)urbanLevel);
            writer.Write((byte)farmLevel);
            writer.Write((byte)plantLevel);
            writer.Write((byte)specialIndex);
            writer.Write(walled);

            if (hasIncomingRiver)
            {
                writer.Write((byte)(incomingRiver + 128));
            }
            else
            {
                writer.Write((byte)0);
            }

            if (hasOutgoingRiver)
            {
                writer.Write((byte)(outgoingRiver + 128));
            }
            else
            {
                writer.Write((byte)0);
            }

            int roadFlags = 0;
            for (int i = 0; i < roads.Length; i++)
            {
                if (roads[i])
                {
                    roadFlags |= 1 << i;
                }
            }
            writer.Write((byte)roadFlags);

        }

        public void Load(BinaryReader reader)
        {
            terrainTypeIndex = reader.ReadByte();
            elevation = reader.ReadByte();
            RefreshPosition();
            waterLevel = reader.ReadByte();
            urbanLevel = reader.ReadByte();
            farmLevel = reader.ReadByte();
            plantLevel = reader.ReadByte();
            specialIndex = reader.ReadByte();
            walled = reader.ReadBoolean();

            byte riverData = reader.ReadByte();
            if (riverData >= 128)
            {
                hasIncomingRiver = true;
                incomingRiver = (HexDirection)(riverData - 128);
            }
            else
            {
                hasIncomingRiver = false;
            }

            riverData = reader.ReadByte();
            if (riverData >= 128)
            {
                hasOutgoingRiver = true;
                outgoingRiver = (HexDirection)(riverData - 128);
            }
            else
            {
                hasOutgoingRiver = false;
            }

            int roadFlags = reader.ReadByte();
            for (int i = 0; i < roads.Length; i++)
            {
                roads[i] = (roadFlags & (1 << i)) != 0;
            }
        }

    }
}