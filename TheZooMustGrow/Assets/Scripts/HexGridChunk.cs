using UnityEngine;
using UnityEngine.UI;

namespace TheZooMustGrow
{
    public class HexGridChunk : MonoBehaviour
    {
		HexCell[] cells;

        public HexMesh terrain;
        public HexMesh rivers;
        public HexMesh roads;
        public HexMesh water;
        public HexMesh waterShore;

        Canvas gridCanvas;

		void Awake()
		{
			gridCanvas = GetComponentInChildren<Canvas>();

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
			Triangulate();
			enabled = false;
		}

		public void ShowUI(bool visible)
        {
			gridCanvas.gameObject.SetActive(visible);
        }

        public void Triangulate()
        {
            terrain.Clear();
            rivers.Clear();
            roads.Clear();
            water.Clear();
            waterShore.Clear();

            for (int i = 0; i < cells.Length; i++)
            {
                Triangulate(cells[i]);
            }

            terrain.Apply();
            rivers.Apply();
            roads.Apply();
            water.Apply();
            waterShore.Apply();
        }

        /// <summary>
        /// Triangulate the whole hexagon slice by slice.
        /// </summary>
        /// <param name="cell"></param>
        void Triangulate(HexCell cell)
        {
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                Triangulate(d, cell);
            }
        }

        /// <summary>
        /// Triangulate a triangle slice of the hexagon.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="cell"></param>
        void Triangulate(HexDirection direction, HexCell cell)
        {
            Vector3 center = cell.Position;
            EdgeVertices e = new EdgeVertices(
                center + HexMetrics.GetFirstSolidCorner(direction),
                center + HexMetrics.GetSecondSolidCorner(direction)
            );

            // Check if there is a river
            if (cell.HasRiver)
            {
                if (cell.HasRiverThroughEdge(direction))
                {
                    e.v3.y = cell.StreamBedY;

                    // Check if this the beginning or end of a river
                    if (cell.HasRiverBeginOrEnd)
                    {
                        TriangulateWithRiverBeginOrEnd(direction, cell, center, e);
                    }
                    else
                    {
                        TriangulateWithRiver(direction, cell, center, e);
                    }
                }
                else
                {
                    // This is a non river edge in the river tile.
                    TriangulateAdjacentToRiver(direction, cell, center, e);
                }
            }
            else
            {
                TriangulateWithoutRiver(direction, cell, center, e);
            }

            // terrain.Add a connection to neighboring hex cell if it is NE, E, and SE
            if (direction <= HexDirection.SE)
            {
                TriangulateConnection(direction, cell, e);
            }

            // Check if the cell is submerged
            if (cell.IsUnderwater)
            {
                TriangulateWater(direction, cell, center);
            }
        }

        void TriangulateWater(HexDirection direction, HexCell cell, Vector3 center)
        {
            center.y = cell.WaterSurfaceY;

            // Determine if this is open water or shore water
            HexCell neighbor = cell.GetNeighbor(direction);
            if (neighbor != null && !neighbor.IsUnderwater)
            {
                TriangulateWaterShore(direction, cell, neighbor, center);
            }
            else
            {
                TriangulateOpenWater(direction, cell, neighbor, center);
            }
        }

        void TriangulateOpenWater(
            HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center)
        {
            Vector3 c1 = center + HexMetrics.GetFirstSolidCorner(direction);
            Vector3 c2 = center + HexMetrics.GetSecondSolidCorner(direction);

            water.AddTriangle(center, c1, c2);

            // Connect adjacent water cells
            if (direction <= HexDirection.SE && neighbor != null)
            {
                Vector3 bridge = HexMetrics.GetBridge(direction);
                Vector3 e1 = c1 + bridge;
                Vector3 e2 = c2 + bridge;

                water.AddQuad(c1, c2, e1, e2);

                // Add a triangle to fill gaps between 3 hexagons
                if (direction <= HexDirection.E)
                {
                    HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
                    if (nextNeighbor == null || !nextNeighbor.IsUnderwater)
                    {
                        return;
                    }
                    water.AddTriangle(
                        c2, e2, c2 + HexMetrics.GetBridge(direction.Next())
                    );
                }
            }
        }

        void TriangulateWaterShore(
            HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center)
        {
            // Perturb the water triangles along the shore
            EdgeVertices e1 = new EdgeVertices(
                center + HexMetrics.GetFirstSolidCorner(direction),
                center + HexMetrics.GetSecondSolidCorner(direction));
            water.AddTriangle(center, e1.v1, e1.v2);
            water.AddTriangle(center, e1.v2, e1.v3);
            water.AddTriangle(center, e1.v3, e1.v4);
            water.AddTriangle(center, e1.v4, e1.v5);

            // Create edge bridge off of the triangles for the shore
            Vector3 bridge = HexMetrics.GetBridge(direction);
            EdgeVertices e2 = new EdgeVertices(
                e1.v1 + bridge,
                e1.v5 + bridge
            );
            waterShore.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            waterShore.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            waterShore.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            waterShore.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);

            // Add UV coords.  0 on water side, 1 on land side
            waterShore.AddQuadUV(0f, 0f, 0f, 1f);
            waterShore.AddQuadUV(0f, 0f, 0f, 1f);
            waterShore.AddQuadUV(0f, 0f, 0f, 1f);
            waterShore.AddQuadUV(0f, 0f, 0f, 1f);

            // Add corner triangles to fill gaps between 3 hex cells
            HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
            if (nextNeighbor != null)
            {
                waterShore.AddTriangle(
                    e1.v5, e2.v5, e1.v5 + HexMetrics.GetBridge(direction.Next())
                );
                waterShore.AddTriangleUV(
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, nextNeighbor.IsUnderwater ? 0f : 1f)
                );
            }
        }

        void TriangulateWithoutRiver(
            HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
        {
            TriangulateEdgeFan(center, e, cell.Color);

            // Check if there is also a Road
            if (cell.HasRoads)
            {
                // Determine which interpolator to user
                Vector2 interpolators = GetRoadInterpolators(direction, cell);

                TriangulateRoad(
                    center,
                    Vector3.Lerp(center, e.v1, interpolators.x),
                    Vector3.Lerp(center, e.v5, interpolators.y),
                    e, cell.HasRoadThroughEdge(direction)
                );
            }
        }

        void TriangulateAdjacentToRiver(
            HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
        {
            // Check for road
            if (cell.HasRoads)
            {
                TriangulateRoadAdjacentToRiver(direction, cell, center, e);
            }


            // Need to determine both what kind or river we have, and its relative orientation.
            if (cell.HasRiverThroughEdge(direction.Next()))
            {
                if (cell.HasRiverThroughEdge(direction.Previous()))
                {
                    center += HexMetrics.GetSolidEdgeMiddle(direction) *
                        (HexMetrics.innerToOuter * 0.5f);
                }
                else if (cell.HasRiverThroughEdge(direction.Previous2()))
                {
                    center += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
                }
            }
            else if (cell.HasRiverThroughEdge(direction.Previous()) &&
                     cell.HasRiverThroughEdge(direction.Next2()))
            {
                center += HexMetrics.GetSecondSolidCorner(direction) * 0.25f;
            }

            EdgeVertices m = new EdgeVertices(
                Vector3.Lerp(center, e.v1, 0.5f),
                Vector3.Lerp(center, e.v5, 0.5f));

            TriangulateEdgeStrip(m, cell.Color, e, cell.Color);
            TriangulateEdgeFan(center, m, cell.Color);

        }

        void TriangulateWithRiverBeginOrEnd(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
        {
            EdgeVertices m = new EdgeVertices(
                Vector3.Lerp(center, e.v1, 0.5f),
                Vector3.Lerp(center, e.v5, 0.5f)
            );

            // Make sure that the channel does not become too shallow too fast
            // so, the center point is not lowered
            m.v3.y = e.v3.y;

            TriangulateEdgeStrip(m, cell.Color, e, cell.Color);
            TriangulateEdgeFan(center, m, cell.Color);

            // Add river quads for river mesh
            bool reversed = cell.HasIncomingRiver;
            TriangulateRiverQuad(
                m.v2, m.v4, e.v2, e.v4, cell.RiverSurfaceY, 0.6f, reversed
            );

            center.y = m.v2.y = m.v4.y = cell.RiverSurfaceY;
            rivers.AddTriangle(center, m.v2, m.v4);
            if (reversed)
            {
                rivers.AddTriangleUV(
                    new Vector2(0.5f, 0.4f), new Vector2(1f, 0.2f), new Vector2(0f, 0.2f)
                );
            }
            else
            {
                rivers.AddTriangleUV(
                    new Vector2(0.5f, 0.4f), new Vector2(0f, 0.6f), new Vector2(1f, 0.6f)
                );
            }
        }

        void TriangulateWithRiver(
            HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
        {
            // To create a channel straight across the cell part,
            // we have to stretch the center into a line.
            // This line needs to have the same width as the channel.

            Vector3 centerL, centerR;

            // If the river cuts straight across the hexagon
            if (cell.HasRiverThroughEdge(direction.Opposite()))
            {
                centerL = center +
                        HexMetrics.GetFirstSolidCorner(direction.Previous()) * 0.25f;
                centerR = center +
                        HexMetrics.GetSecondSolidCorner(direction.Next()) * 0.25f;
            }
            // If the river bends by one
            else if (cell.HasRiverThroughEdge(direction.Next()))
            {
                centerL = center;
                centerR = Vector3.Lerp(center, e.v5, 2f / 3f);
            }
            else if (cell.HasRiverThroughEdge(direction.Previous()))
            {
                centerL = Vector3.Lerp(center, e.v1, 2f / 3f);
                centerR = center;
            }
            // If the river bends by two
            else if (cell.HasRiverThroughEdge(direction.Next2()))
            {
                centerL = center;
                centerR = center +
                          (HexMetrics.GetSolidEdgeMiddle(direction.Next()) *
                          (0.5f * HexMetrics.innerToOuter));
            }
            else
            {
                centerL = center +
                          (HexMetrics.GetSolidEdgeMiddle(direction.Previous()) *
                          (0.5f * HexMetrics.innerToOuter));
                centerR = center;
            }

            center = Vector3.Lerp(centerL, centerR, 0.5f);

            // Middle line
            EdgeVertices m = new EdgeVertices(
                Vector3.Lerp(centerL, e.v1, 0.5f),
                Vector3.Lerp(centerR, e.v5, 0.5f),
                1f / 6f);

            // Lower Y to make river bottoms
            m.v3.y = center.y = e.v3.y;

            // Fill the space between the middle and edge lines
            TriangulateEdgeStrip(m, cell.Color, e, cell.Color);

            // Second section of the trapezoid (towards center of hexagon)
            terrain.AddTriangle(centerL, m.v1, m.v2);
            terrain.AddTriangleColor(cell.Color);
            terrain.AddQuad(centerL, center, m.v2, m.v3);
            terrain.AddQuadColor(cell.Color);
            terrain.AddQuad(center, centerR, m.v3, m.v4);
            terrain.AddQuadColor(cell.Color);
            terrain.AddTriangle(centerR, m.v4, m.v5);
            terrain.AddTriangleColor(cell.Color);

            // Add river quads for the river mesh
            bool reversed = cell.IncomingRiver == direction;
            TriangulateRiverQuad(centerL, centerR, m.v2, m.v4, cell.RiverSurfaceY, 0.4f, reversed);
            TriangulateRiverQuad(m.v2, m.v4, e.v2, e.v4, cell.RiverSurfaceY, 0.6f, reversed);
        }

        /// <summary>
        /// Create river quad using single height y.
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        /// <param name="v4"></param>
        /// <param name="y"></param>
        /// <param name="reversed"></param>
        void TriangulateRiverQuad(
            Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
            float y, float v, bool reversed)
        {
            TriangulateRiverQuad(v1, v2, v3, v4, y, y, v, reversed);
        }

        /// <summary>
        /// Create river quad using two heights y1 and y2.
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        /// <param name="v4"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="reversed"></param>
        void TriangulateRiverQuad(
            Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
            float y1, float y2, float v, bool reversed)
        {
            // The U coordinate is 0 at the left of the river and
            // 1 at the right, when looking downstream.
            // And the V coordinate should go from 0 to 1 in the
            // direction that the river is flowing.

            v1.y = v2.y = y1;
            v3.y = v4.y = y2;
            rivers.AddQuad(v1, v2, v3, v4);
            if (reversed)
            {
                rivers.AddQuadUV(1f, 0f, 0.8f - v, 0.6f - v);
            }
            else
            {
                rivers.AddQuadUV(0f, 1f, v, v + 0.2f);
            }
        }

        void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, Color color)
        {
            terrain.AddTriangle(center, edge.v1, edge.v2);
            terrain.AddTriangleColor(color);
            terrain.AddTriangle(center, edge.v2, edge.v3);
            terrain.AddTriangleColor(color);
            terrain.AddTriangle(center, edge.v3, edge.v4);
            terrain.AddTriangleColor(color);
            terrain.AddTriangle(center, edge.v4, edge.v5);
            terrain.AddTriangleColor(color);
        }

        void TriangulateEdgeStrip(
            EdgeVertices e1, Color c1,
            EdgeVertices e2, Color c2,
            bool hasRoad = false)
        {
            terrain.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            terrain.AddQuadColor(c1, c2);
            terrain.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            terrain.AddQuadColor(c1, c2);
            terrain.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            terrain.AddQuadColor(c1, c2);
            terrain.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
            terrain.AddQuadColor(c1, c2);

            if (hasRoad)
            {
                TriangulateRoadSegment(e1.v2, e1.v3, e1.v4, e2.v2, e2.v3, e2.v4);
            }
        }

        /// <summary>
        /// Triangulates the quad bridge and triangle corners that connect HexCells.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="cell"></param>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        void TriangulateConnection(
            HexDirection direction, HexCell cell, EdgeVertices e1)
        {
            HexCell neighbor = cell.GetNeighbor(direction);
            if (neighbor == null) { return; }

            Vector3 bridge = HexMetrics.GetBridge(direction);
            bridge.y = neighbor.Position.y - cell.Position.y;
            EdgeVertices e2 = new EdgeVertices(
                e1.v1 + bridge,
                e1.v5 + bridge
            );

            // Check for river through connection
            if (cell.HasRiverThroughEdge(direction))
            {
                e2.v3.y = neighbor.StreamBedY;

                // Add quad for river mesh
                TriangulateRiverQuad(
                    e1.v2, e1.v4, e2.v2, e2.v4,
                    cell.RiverSurfaceY, neighbor.RiverSurfaceY, 0.8f,
                    cell.HasIncomingRiver && cell.IncomingRiver == direction);
            }

            // Check edge type
            if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
            {
                TriangulateEdgeTerraces(e1, cell, e2, neighbor, cell.HasRoadThroughEdge(direction));
            }
            else
            {
                TriangulateEdgeStrip(e1, cell.Color, e2, neighbor.Color,
                    cell.HasRoadThroughEdge(direction));
            }

            // terrain.Add triangular holes/corners for all NE and E neighbors
            HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
            if (direction <= HexDirection.E && nextNeighbor != null)
            {
                // Modify for elevation
                Vector3 v5 = e1.v5 + HexMetrics.GetBridge(direction.Next());
                v5.y = nextNeighbor.Position.y;

                // Determine which cell is the lowest
                if (cell.Elevation <= neighbor.Elevation)
                {
                    if (cell.Elevation <= nextNeighbor.Elevation)
                    {
                        TriangulateCorner(e1.v5, cell, e2.v5, neighbor, v5, nextNeighbor);
                    }
                    else
                    {
                        TriangulateCorner(v5, nextNeighbor, e1.v5, cell, e2.v5, neighbor);
                    }
                }
                else if (neighbor.Elevation <= nextNeighbor.Elevation)
                {
                    TriangulateCorner(e2.v5, neighbor, v5, nextNeighbor, e1.v5, cell);
                }
                else
                {
                    TriangulateCorner(v5, nextNeighbor, e1.v5, cell, e2.v5, neighbor);
                }
            }
        }

        void TriangulateCorner(
            Vector3 bottom, HexCell bottomCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            HexEdgeType leftEdgeType = bottomCell.GetEdgeType(leftCell);
            HexEdgeType rightEdgeType = bottomCell.GetEdgeType(rightCell);

            // Check edge types for different orientations
            // E.g., cliff-cliff-slope
            if (leftEdgeType == HexEdgeType.Slope)
            {
                if (rightEdgeType == HexEdgeType.Slope)
                {
                    TriangulateCornerTerraces(
                        bottom, bottomCell, left, leftCell, right, rightCell
                    );
                }
                else if (rightEdgeType == HexEdgeType.Flat)
                {
                    TriangulateCornerTerraces(
                        left, leftCell, right, rightCell, bottom, bottomCell
                    );
                }
                else
                {
                    TriangulateCornerTerracesCliff(
                        bottom, bottomCell, left, leftCell, right, rightCell
                    );
                }
            }
            else if (rightEdgeType == HexEdgeType.Slope)
            {
                if (leftEdgeType == HexEdgeType.Flat)
                {
                    TriangulateCornerTerraces(
                        right, rightCell, bottom, bottomCell, left, leftCell
                    );
                }
                else
                {
                    TriangulateCornerCliffTerraces(
                        bottom, bottomCell, left, leftCell, right, rightCell
                    );
                }
            }
            else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
            {
                if (leftCell.Elevation < rightCell.Elevation)
                {
                    TriangulateCornerCliffTerraces(
                        right, rightCell, bottom, bottomCell, left, leftCell
                    );
                }
                else
                {
                    TriangulateCornerTerracesCliff(
                        left, leftCell, right, rightCell, bottom, bottomCell
                    );
                }
            }
            else
            {
                terrain.AddTriangle(bottom, left, right);
                terrain.AddTriangleColor(bottomCell.Color, leftCell.Color, rightCell.Color);
            }
        }

        private void TriangulateCornerTerraces(
            Vector3 begin, HexCell beginCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
            Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
            Color c3 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, 1);
            Color c4 = HexMetrics.TerraceLerp(beginCell.Color, rightCell.Color, 1);

            // First slope
            terrain.AddTriangle(begin, v3, v4);
            terrain.AddTriangleColor(beginCell.Color, c3, c4);

            // Intermediate slopes
            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended 
                Vector3 v1 = v3;
                Vector3 v2 = v4;
                Color c1 = c3;
                Color c2 = c4;

                // Determine the ending point 
                v3 = HexMetrics.TerraceLerp(begin, left, i);
                v4 = HexMetrics.TerraceLerp(begin, right, i);
                c3 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);
                c4 = HexMetrics.TerraceLerp(beginCell.Color, rightCell.Color, i);

                // terrain.Add the quad
                terrain.AddQuad(v1, v2, v3, v4);
                terrain.AddQuadColor(c1, c2, c3, c4);
            }

            // Last slope
            terrain.AddQuad(v3, v4, left, right);
            terrain.AddQuadColor(c3, c4, leftCell.Color, rightCell.Color);
        }

        void TriangulateCornerTerracesCliff(
            Vector3 begin, HexCell beginCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            float b = 1f / (rightCell.Elevation - beginCell.Elevation);
            // Triangulating top to bottom may cause boundary interpolators to be negative
            // Make sure this is always positive.
            if (b < 0) { b = -b; }

            Vector3 boundary = Vector3.Lerp(HexMetrics.Perturb(begin), HexMetrics.Perturb(right), b);
            Color boundaryColor = Color.Lerp(beginCell.Color, rightCell.Color, b);

            TriangulateBoundaryTriangle(
                begin, beginCell, left, leftCell, boundary, boundaryColor
            );

            // Completion of the top of the corner triangle
            if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
            {
                // If there is slope, add a rotated boundary triangle. 
                TriangulateBoundaryTriangle(
                    left, leftCell, right, rightCell, boundary, boundaryColor
                );
            }
            else
            {
                // Else add a simple triangle
                terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
                terrain.AddTriangleColor(leftCell.Color, rightCell.Color, boundaryColor);
            }

        }

        void TriangulateCornerCliffTerraces(
            Vector3 begin, HexCell beginCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            float b = 1f / (leftCell.Elevation - beginCell.Elevation);
            // Triangulating top to bottom may cause boundary interpolators to be negative
            // Make sure this is always positive.
            if (b < 0) { b = -b; }

            Vector3 boundary = Vector3.Lerp(HexMetrics.Perturb(begin), HexMetrics.Perturb(left), b);
            Color boundaryColor = Color.Lerp(beginCell.Color, leftCell.Color, b);

            TriangulateBoundaryTriangle(
                right, rightCell, begin, beginCell, boundary, boundaryColor
            );

            if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
            {
                TriangulateBoundaryTriangle(
                    left, leftCell, right, rightCell, boundary, boundaryColor
                );
            }
            else
            {
                terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
                terrain.AddTriangleColor(leftCell.Color, rightCell.Color, boundaryColor);
            }
        }

        private void TriangulateBoundaryTriangle(
            Vector3 begin, HexCell beginCell,
            Vector3 left, HexCell leftCell,
            Vector3 boundary, Color boundaryColor)
        {
            Vector3 v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, 1));
            Color c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, 1);

            // First slope
            terrain.AddTriangleUnperturbed(HexMetrics.Perturb(begin), v2, boundary);
            terrain.AddTriangleColor(beginCell.Color, c2, boundaryColor);

            // Intermediate slopes
            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended
                Vector3 v1 = v2;
                Color c1 = c2;

                // Determine ending point
                v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));
                c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);

                // terrain.terrain.Add the triangle
                terrain.AddTriangleUnperturbed(v1, v2, boundary);
                terrain.AddTriangleColor(c1, c2, boundaryColor);
            }

            // Last slope
            terrain.AddTriangleUnperturbed(v2, HexMetrics.Perturb(left), boundary);
            terrain.AddTriangleColor(c2, leftCell.Color, boundaryColor);
        }

        void TriangulateEdgeTerraces(
            EdgeVertices begin, HexCell beginCell,
            EdgeVertices end, HexCell endCell,
            bool hasRoad)
        {
            EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
            Color c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, 1);

            // First slope
            TriangulateEdgeStrip(begin, beginCell.Color, e2, c2, hasRoad);

            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended
                EdgeVertices e1 = e2;
                Color c1 = c2;

                // Determine ending point
                e2 = EdgeVertices.TerraceLerp(begin, end, i);
                c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, i);

                // terrain.Add the quad
                TriangulateEdgeStrip(e1, c1, e2, c2, hasRoad);
            }

            // Last slope
            TriangulateEdgeStrip(e2, c2, end, endCell.Color, hasRoad);
        }

        void TriangulateRoadSegment(
            Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 v4, Vector3 v5, Vector3 v6)
        {
            roads.AddQuad(v1, v2, v4, v5);
            roads.AddQuad(v2, v3, v5, v6);

            // Set UV -- V=0 and U=1 in the middle
            roads.AddQuadUV(0f, 1f, 0f, 0f);
            roads.AddQuadUV(1f, 0f, 0f, 0f);
        }

        void TriangulateRoad(
            Vector3 center, Vector3 mL, Vector3 mR,
            EdgeVertices e, bool hasRoadThroughCellEdge)
        {
            // Check if the road goes through the cell edge
            if (hasRoadThroughCellEdge)
            {
                // Create the road such that it goes from center to edge
                Vector3 mC = Vector3.Lerp(mL, mR, 0.5f);
                TriangulateRoadSegment(mL, mC, mR, e.v2, e.v3, e.v4);

                // Add two triangles pointing to the center
                roads.AddTriangle(center, mL, mC);
                roads.AddTriangle(center, mC, mR);

                // Add UVs
                roads.AddTriangleUV(
                    new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f));
                roads.AddTriangleUV(
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            }
            else
            {
                // Create the road such that it fills a center triangle in this direction
                TriangulateRoadEdge(center, mL, mR);
            }
        }

        void TriangulateRoadEdge(Vector3 center, Vector3 mL, Vector3 mR)
        {
            roads.AddTriangle(center, mL, mR);
            roads.AddTriangleUV(
                new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f)
            );
        }

        void TriangulateRoadAdjacentToRiver(
            HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
        {
            bool hasRoadThroughEdge = cell.HasRoadThroughEdge(direction);
            bool previousHasRiver = cell.HasRiverThroughEdge(direction.Previous());
            bool nextHasRiver = cell.HasRiverThroughEdge(direction.Next());

            Vector2 interpolators = GetRoadInterpolators(direction, cell);
            Vector3 roadCenter = center;

            // If the cell includes a beginning or end of a river
            if (cell.HasRiverBeginOrEnd)
            {
                // Push the river center away from the Hex center
                roadCenter += HexMetrics.GetSolidEdgeMiddle(
                    cell.RiverBeginOrEndDirection.Opposite()
                ) * (1f / 3f);
            }
            // If the cell includes a straight river
            else if (cell.IncomingRiver == cell.OutgoingRiver.Opposite())
            {
                Vector3 corner;
                if (previousHasRiver)
                {
                    // If there is no road through the edge or the next edge
                    // break out of method before tiangulating road on the 
                    // other side of the river
                    if (!hasRoadThroughEdge &&
                        !cell.HasRoadThroughEdge(direction.Next()))
                    {
                        return;
                    }
                    corner = HexMetrics.GetSecondSolidCorner(direction);
                }
                else
                {
                    // If there is no road through the edge or the next edge
                    // break out of method before tiangulating road on the 
                    // other side of the river
                    if (!hasRoadThroughEdge &&
                        !cell.HasRoadThroughEdge(direction.Previous()))
                    {
                        return;
                    }
                    corner = HexMetrics.GetFirstSolidCorner(direction);
                }

                // To shift the road so it ends up adjacent to the river,
                // we have to move the road center half of the way towards
                // that corner. Then, we have to also move the cell center
                // a quarter of the way in that direction.
                roadCenter += corner * 0.5f;
                center += corner * 0.25f;
            }
            // If the cell includes a zigzagging river in the previous direction
            else if (cell.IncomingRiver == cell.OutgoingRiver.Previous())
            {
                // Move the road center by using one of the corners of the incoming
                // river direction. Which corner it is depends on the flow direction.
                // Push the road center away from that corner with a factor of 0.2.
                roadCenter -= HexMetrics.GetFirstCorner(cell.IncomingRiver) * 0.2f;
            }
            // If the cell includes a zigzagging river in the next direction
            else if (cell.IncomingRiver == cell.OutgoingRiver.Next())
            {
                roadCenter -= HexMetrics.GetFirstCorner(cell.IncomingRiver) * 0.2f;
            }
            // Inside of a curved river (next and previous)
            else if (previousHasRiver && nextHasRiver)
            {
                // Prune Roads
                if (!hasRoadThroughEdge) { return; }

                // Pull the road center towards the current cell edge,
                // shortening the road by a lot. A factor of 0.7 is fine.
                // The cell center has to move as well, with a factor of 0.5.
                Vector3 offset = HexMetrics.GetSolidEdgeMiddle(direction) *
                                 HexMetrics.innerToOuter;
                roadCenter += offset * 0.7f;
                center += offset * 0.5f;
            }
            // Outside of a curved river
            else
            {
                HexDirection middle;
                if (previousHasRiver)
                {
                    middle = direction.Next();
                }
                else if (nextHasRiver)
                {
                    middle = direction.Previous();
                }
                else
                {
                    middle = direction;
                }
                // Prune roads
                if (!cell.HasRoadThroughEdge(middle) &&
                    !cell.HasRoadThroughEdge(middle.Previous()) &&
                    !cell.HasRoadThroughEdge(middle.Next()))
                {
                    return;
                }

                roadCenter += HexMetrics.GetSolidEdgeMiddle(middle) * 0.25f;
            }

            Vector3 mL = Vector3.Lerp(roadCenter, e.v1, interpolators.x);
            Vector3 mR = Vector3.Lerp(roadCenter, e.v5, interpolators.y);
            TriangulateRoad(roadCenter, mL, mR, e, hasRoadThroughEdge);

            // Close the gaps created by moving the center of the river away
            // from the center of the Hex cell
            if (previousHasRiver)
            {
                TriangulateRoadEdge(roadCenter, center, mL);
            }
            if (nextHasRiver)
            {
                TriangulateRoadEdge(roadCenter, mR, center);
            }

        }

        /// <summary>
        /// Determine which interpolator to use for a Road.  
        /// Can help with center bulges.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        Vector2 GetRoadInterpolators(HexDirection direction, HexCell cell)
        {
            // X component is the interpolator for the left point,
            // Y component is the interpolator for the right point.
            Vector2 interpolators;

            // If the road is in this direction
            if (cell.HasRoadThroughEdge(direction))
            {
                // Place interpolator points halfway
                interpolators.x = interpolators.y = 0.5f;
            }
            else
            {
                interpolators.x =
                    cell.HasRoadThroughEdge(direction.Previous()) ? 0.5f : 0.25f;
                interpolators.y =
                    cell.HasRoadThroughEdge(direction.Next()) ? 0.5f : 0.25f;
            }

            return interpolators;
        }
    }
}