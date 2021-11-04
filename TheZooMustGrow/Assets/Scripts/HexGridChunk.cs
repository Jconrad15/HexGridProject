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
        public HexMesh estuaries;

        public HexFeatureManager features;

        Canvas gridCanvas;

        static Color weights1 = new Color(1f, 0f, 0f);
        static Color weights2 = new Color(0f, 1f, 0f);
        static Color weights3 = new Color(0f, 0f, 1f);

        void Awake()
		{
			gridCanvas = GetComponentInChildren<Canvas>();

			cells = new HexCell[HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ];
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
            estuaries.Clear();
            features.Clear();

            for (int i = 0; i < cells.Length; i++)
            {
                Triangulate(cells[i]);
            }

            terrain.Apply();
            rivers.Apply();
            roads.Apply();
            water.Apply();
            waterShore.Apply();
            estuaries.Apply();
            features.Apply();
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

            // Add features if not underwater
            if (!cell.IsUnderwater)
            {
                // Add features to the cell
                if (!cell.HasRiver && !cell.HasRoads)
                {
                    features.AddFeature(cell, cell.Position);
                }

                // Add a special feature to the cell
                if (cell.IsSpecial)
                {
                    features.AddSpecialFeature(cell, cell.Position);
                }
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

                // Add features
                if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
                {
                    features.AddFeature(cell, (center + e.v1 + e.v5) * (1f / 3f));
                }
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
            Vector3 c1 = center + HexMetrics.GetFirstWaterCorner(direction);
            Vector3 c2 = center + HexMetrics.GetSecondWaterCorner(direction);

            water.AddTriangle(center, c1, c2);
            Vector3 indices;
            indices.x = indices.y = indices.z = cell.Index;
            water.AddTriangleCellData(indices, weights1);

            // Connect adjacent water cells
            if (direction <= HexDirection.SE && neighbor != null)
            {
                Vector3 bridge = HexMetrics.GetWaterBridge(direction);
                Vector3 e1 = c1 + bridge;
                Vector3 e2 = c2 + bridge;

                water.AddQuad(c1, c2, e1, e2);
                indices.y = neighbor.Index;
                water.AddQuadCellData(indices, weights1, weights2);

                // Add a triangle to fill gaps between 3 hexagons
                if (direction <= HexDirection.E)
                {
                    HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
                    if (nextNeighbor == null || !nextNeighbor.IsUnderwater)
                    {
                        return;
                    }
                    water.AddTriangle(
                        c2, e2, c2 + HexMetrics.GetWaterBridge(direction.Next()));

                    indices.z = nextNeighbor.Index;
                    water.AddTriangleCellData(
                        indices, weights1, weights2, weights3);
                }
            }
        }

        void TriangulateWaterShore(
            HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center)
        {
            // Perturb the water triangles along the shore
            EdgeVertices e1 = new EdgeVertices(
                center + HexMetrics.GetFirstWaterCorner(direction),
                center + HexMetrics.GetSecondWaterCorner(direction));
            water.AddTriangle(center, e1.v1, e1.v2);
            water.AddTriangle(center, e1.v2, e1.v3);
            water.AddTriangle(center, e1.v3, e1.v4);
            water.AddTriangle(center, e1.v4, e1.v5);

            Vector3 indices;
            indices.x = indices.z = cell.Index;
            indices.y = neighbor.Index;

            water.AddTriangleCellData(indices, weights1);
            water.AddTriangleCellData(indices, weights1);
            water.AddTriangleCellData(indices, weights1);
            water.AddTriangleCellData(indices, weights1);

            // Create edge bridge off of the triangles for the shore
            Vector3 center2 = neighbor.Position;
            center2.y = center.y;
            EdgeVertices e2 = new EdgeVertices(
                center2 + HexMetrics.GetSecondSolidCorner(direction.Opposite()),
                center2 + HexMetrics.GetFirstSolidCorner(direction.Opposite())
            );

            // Check for estuary
            if (cell.HasRiverThroughEdge(direction))
            {
                TriangulateEstuary(e1, e2, cell.IncomingRiver == direction, indices);
            }
            else
            {
                waterShore.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
                waterShore.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
                waterShore.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
                waterShore.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);

                // Add UV coords.  0 on water side, 1 on land side
                waterShore.AddQuadUV(0f, 0f, 0f, 1f);
                waterShore.AddQuadUV(0f, 0f, 0f, 1f);
                waterShore.AddQuadUV(0f, 0f, 0f, 1f);
                waterShore.AddQuadUV(0f, 0f, 0f, 1f);

                waterShore.AddQuadCellData(indices, weights1, weights2);
                waterShore.AddQuadCellData(indices, weights1, weights2);
                waterShore.AddQuadCellData(indices, weights1, weights2);
                waterShore.AddQuadCellData(indices, weights1, weights2);
            }
            // Add corner triangles to fill gaps between 3 hex cells
            HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
            if (nextNeighbor != null)
            {
                Vector3 v3 = nextNeighbor.Position + (nextNeighbor.IsUnderwater ?
                    HexMetrics.GetFirstWaterCorner(direction.Previous()) :
                    HexMetrics.GetFirstSolidCorner(direction.Previous()));
                v3.y = center.y;

                waterShore.AddTriangle( e1.v5, e2.v5, v3);

                waterShore.AddTriangleUV(
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, nextNeighbor.IsUnderwater ? 0f : 1f));

                indices.z = nextNeighbor.Index;
                waterShore.AddTriangleCellData(
                    indices, weights1, weights2, weights3);
            }
        }

        void TriangulateEstuary(
            EdgeVertices e1, EdgeVertices e2, bool incomingRiver, Vector3 indices)
        {
            waterShore.AddTriangle(e2.v1, e1.v2, e1.v1);
            waterShore.AddTriangle(e2.v5, e1.v5, e1.v4);
            waterShore.AddTriangleUV(
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f)
            );
            waterShore.AddTriangleUV(
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f)
            );

            waterShore.AddTriangleCellData(indices, weights2, weights1, weights1);
            waterShore.AddTriangleCellData(indices, weights2, weights1, weights1);

            // Fill in estuary gap
            estuaries.AddQuad(e2.v1, e1.v2, e2.v2, e1.v3);
            estuaries.AddTriangle(e1.v3, e2.v2, e2.v4);
            estuaries.AddQuad(e1.v3, e1.v4, e2.v4, e2.v5);

            estuaries.AddQuadUV(
                new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 0f)
            );
            estuaries.AddTriangleUV(
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f)
            );
            estuaries.AddQuadUV(
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            );

            estuaries.AddQuadCellData(indices, weights2, weights1, weights2, weights1);
            estuaries.AddTriangleCellData(indices, weights1, weights2, weights2);
            estuaries.AddQuadCellData(indices, weights1, weights2);

            // Check if the estuary is flowing towards the open water or the river
            if (incomingRiver)
            {
                estuaries.AddQuadUV2(
                    new Vector2(1.5f, 1f), new Vector2(0.7f, 1.15f),
                    new Vector2(1f, 0.8f), new Vector2(0.5f, 1.1f)
                );
                estuaries.AddTriangleUV2(
                    new Vector2(0.5f, 1.1f),
                    new Vector2(1f, 0.8f),
                    new Vector2(0f, 0.8f)
                );
                estuaries.AddQuadUV2(
                    new Vector2(0.5f, 1.1f), new Vector2(0.3f, 1.15f),
                    new Vector2(0f, 0.8f), new Vector2(-0.5f, 1f)
                );
            }
            else
            {
                estuaries.AddQuadUV2(
                    new Vector2(-0.5f, -0.2f), new Vector2(0.3f, -0.35f),
                    new Vector2(0f, 0f), new Vector2(0.5f, -0.3f)
                );
                estuaries.AddTriangleUV2(
                    new Vector2(0.5f, -0.3f),
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f)
                );
                estuaries.AddQuadUV2(
                    new Vector2(0.5f, -0.3f), new Vector2(0.7f, -0.35f),
                    new Vector2(1f, 0f), new Vector2(1.5f, -0.2f)
                );
            }
        }

        void TriangulateWaterfallInWater(
            Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
            float y1, float y2, float waterY, Vector3 indices)
        {
            v1.y = v2.y = y1;
            v3.y = v4.y = y2;

            v1 = HexMetrics.Perturb(v1);
            v2 = HexMetrics.Perturb(v2);
            v3 = HexMetrics.Perturb(v3);
            v4 = HexMetrics.Perturb(v4);

            float t = (waterY - y2) / (y1 - y2);
            v3 = Vector3.Lerp(v3, v1, t);
            v4 = Vector3.Lerp(v4, v2, t);

            rivers.AddQuadUnperturbed(v1, v2, v3, v4);
            rivers.AddQuadUV(0f, 1f, 0.8f, 1f);
            rivers.AddQuadCellData(indices, weights1, weights2);
        }

        void TriangulateWithoutRiver(
            HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
        {
            TriangulateEdgeFan(center, e, cell.Index);

            // Check if there is also a Road
            if (cell.HasRoads)
            {
                // Determine which interpolator to user
                Vector2 interpolators = GetRoadInterpolators(direction, cell);

                TriangulateRoad(
                    center,
                    Vector3.Lerp(center, e.v1, interpolators.x),
                    Vector3.Lerp(center, e.v5, interpolators.y),
                    e, cell.HasRoadThroughEdge(direction), cell.Index
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

            TriangulateEdgeStrip(m, weights1, cell.Index,
                                 e, weights1, cell.Index);
            TriangulateEdgeFan(center, m, cell.Index);

            // Add features
            if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
            {
                features.AddFeature(cell, (center + e.v1 + e.v5) * (1f / 3f));
            }
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

            TriangulateEdgeStrip(m, weights1, cell.Index,
                                 e, weights1, cell.Index);
            TriangulateEdgeFan(center, m, cell.Index);

            if (!cell.IsUnderwater)
            {
                // Add river quads for river mesh
                bool reversed = cell.HasIncomingRiver;

                Vector3 indices;
                indices.x = indices.y = indices.z = cell.Index;
                
                TriangulateRiverQuad(
                    m.v2, m.v4, e.v2, e.v4, 
                    cell.RiverSurfaceY, 0.6f, reversed, indices);

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

                rivers.AddTriangleCellData(indices, weights1);
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
            TriangulateEdgeStrip(m, weights1, cell.Index,
                                 e, weights1, cell.Index);
            // Second section of the trapezoid (towards center of hexagon)
            terrain.AddTriangle(centerL, m.v1, m.v2);
            terrain.AddQuad(centerL, center, m.v2, m.v3);
            terrain.AddQuad(center, centerR, m.v3, m.v4);
            terrain.AddTriangle(centerR, m.v4, m.v5);

            Vector3 indices;
            indices.x = indices.y = indices.z = cell.Index;
            terrain.AddTriangleCellData(indices, weights1);
            terrain.AddQuadCellData(indices, weights1);
            terrain.AddQuadCellData(indices, weights1);
            terrain.AddTriangleCellData(indices, weights1);

            if (!cell.IsUnderwater)
            {
                // Add river quads for the river mesh
                bool reversed = cell.IncomingRiver == direction;
                TriangulateRiverQuad(
                    centerL, centerR, m.v2, m.v4,
                    cell.RiverSurfaceY, 0.4f, reversed, indices);

                TriangulateRiverQuad(
                    m.v2, m.v4, e.v2, e.v4,
                    cell.RiverSurfaceY, 0.6f, reversed, indices);
            }
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
            float y, float v, bool reversed, Vector3 indices)
        {
            TriangulateRiverQuad(v1, v2, v3, v4, y, y, v, reversed, indices);
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
            float y1, float y2, float v, bool reversed, Vector3 indices)
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
            rivers.AddQuadCellData(indices, weights1, weights2);
        }

        void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, float index)
        {
            terrain.AddTriangle(center, edge.v1, edge.v2);
            terrain.AddTriangle(center, edge.v2, edge.v3);
            terrain.AddTriangle(center, edge.v3, edge.v4);
            terrain.AddTriangle(center, edge.v4, edge.v5);

            Vector3 indices;
            indices.x = indices.y = indices.z = index;
            terrain.AddTriangleCellData(indices, weights1);
            terrain.AddTriangleCellData(indices, weights1);
            terrain.AddTriangleCellData(indices, weights1);
            terrain.AddTriangleCellData(indices, weights1);
        }

        void TriangulateEdgeStrip(
            EdgeVertices e1, Color w1, float index1,
            EdgeVertices e2, Color w2, float index2,
            bool hasRoad = false)
        {
            terrain.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            terrain.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            terrain.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            terrain.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);

            Vector3 indices;
            indices.x = indices.z = index1;
            indices.y = index2;
            terrain.AddQuadCellData(indices, w1, w2);
            terrain.AddQuadCellData(indices, w1, w2);
            terrain.AddQuadCellData(indices, w1, w2);
            terrain.AddQuadCellData(indices, w1, w2);

            if (hasRoad)
            {
                TriangulateRoadSegment(
                    e1.v2, e1.v3, e1.v4, e2.v2, e2.v3, e2.v4, w1, w2, indices);
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
            bool hasRiver = cell.HasRiverThroughEdge(direction);
            bool hasRoad = cell.HasRoadThroughEdge(direction);
            if (hasRiver)
            {
                e2.v3.y = neighbor.StreamBedY;

                Vector3 indices;
                indices.x = indices.z = cell.Index;
                indices.y = neighbor.Index;

                // Check if the river will be in an under water cell
                if (!cell.IsUnderwater)
                {
                    // Is the neighbor cell underwater as well
                    if (!neighbor.IsUnderwater)
                    {
                        // Add quad for river mesh
                        TriangulateRiverQuad(
                            e1.v2, e1.v4, e2.v2, e2.v4,
                            cell.RiverSurfaceY, neighbor.RiverSurfaceY, 0.8f,
                            cell.HasIncomingRiver && cell.IncomingRiver == direction,
                            indices);
                    }
                    else if (cell.Elevation > neighbor.WaterLevel)
                    {
                        // Then there is a waterfall
                        TriangulateWaterfallInWater(
                            e1.v2, e1.v4, e2.v2, e2.v4,
                            cell.RiverSurfaceY, neighbor.RiverSurfaceY,
                            neighbor.WaterSurfaceY, indices);
                    }
                }
                else if (!neighbor.IsUnderwater &&
                         neighbor.Elevation > cell.WaterLevel)
                {
                    TriangulateWaterfallInWater(
                        e2.v4, e2.v2, e1.v4, e1.v2,
                        neighbor.RiverSurfaceY, cell.RiverSurfaceY,
                        cell.WaterSurfaceY, indices);
                }
            }

            // Check edge type
            if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
            {
                TriangulateEdgeTerraces(e1, cell, e2, neighbor, hasRoad);
            }
            else
            {
                TriangulateEdgeStrip(e1, weights1, cell.Index,
                                     e2, weights2, neighbor.Index, hasRoad);
            }

            // Add feature walls as needed
            features.AddWall(e1, cell, e2, neighbor, hasRiver, hasRoad);

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
                Vector3 indices;
                indices.x = bottomCell.Index;
                indices.y = leftCell.Index;
                indices.z = rightCell.Index;
                terrain.AddTriangleCellData(indices, weights1, weights2, weights3);
            }

            // Add wall feature to the corner triangle
            features.AddWall(bottom, bottomCell, left, leftCell, right, rightCell);
        }

        private void TriangulateCornerTerraces(
            Vector3 begin, HexCell beginCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
            Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
            Color w3 = HexMetrics.TerraceLerp(weights1, weights2, 1);
            Color w4 = HexMetrics.TerraceLerp(weights1, weights2, 1);
            
            Vector3 indices;
            indices.x = beginCell.Index;
            indices.y = leftCell.Index;
            indices.z = rightCell.Index;

            // First slope
            terrain.AddTriangle(begin, v3, v4);
            terrain.AddTriangleCellData(indices, weights1, w3, w4);

            // Intermediate slopes
            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended 
                Vector3 v1 = v3;
                Vector3 v2 = v4;
                Color w1 = w3;
                Color w2 = w4;

                // Determine the ending point 
                v3 = HexMetrics.TerraceLerp(begin, left, i);
                v4 = HexMetrics.TerraceLerp(begin, right, i);
                w3 = HexMetrics.TerraceLerp(weights1, weights2, i);
                w4 = HexMetrics.TerraceLerp(weights1, weights3, i);

                // terrain.Add the quad
                terrain.AddQuad(v1, v2, v3, v4);
                terrain.AddQuadCellData(indices, w1, w2, w3, w4);
            }

            // Last slope
            terrain.AddQuad(v3, v4, left, right);
            terrain.AddQuadCellData(indices, w3, w4, weights2, weights3);
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
            Color boundaryWeights = Color.Lerp(weights1, weights3, b);

            Vector3 indices;
            indices.x = beginCell.Index;
            indices.y = leftCell.Index;
            indices.z = rightCell.Index;

            TriangulateBoundaryTriangle(
                begin, weights1, left, weights2, boundary, boundaryWeights, indices);

            // Completion of the top of the corner triangle
            if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
            {
                // If there is slope, add a rotated boundary triangle. 
                TriangulateBoundaryTriangle(
                    left, weights2, right, weights3, boundary, boundaryWeights, indices);
            }
            else
            {
                // Else add a simple triangle
                terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
                terrain.AddTriangleCellData(
                    indices, weights2, weights3, boundaryWeights);
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
            Color boundaryWeights = Color.Lerp(weights1, weights2, b);

            Vector3 indices;
            indices.x = beginCell.Index;
            indices.y = leftCell.Index;
            indices.z = rightCell.Index;

            TriangulateBoundaryTriangle(
                right, weights3, begin, weights1, boundary, boundaryWeights, indices);

            if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
            {
                TriangulateBoundaryTriangle(
                    left, weights2, right, weights3, boundary, boundaryWeights, indices);
            }
            else
            {
                terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
                terrain.AddTriangleCellData(indices, weights2, weights3, boundaryWeights);
            }
        }

        private void TriangulateBoundaryTriangle(
            Vector3 begin, Color beginWeights,
            Vector3 left, Color leftWeights,
            Vector3 boundary, Color boundaryWeights, Vector3 indices)
        {
            Vector3 v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, 1));
            Color w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, 1);

            // First slope
            terrain.AddTriangleUnperturbed(HexMetrics.Perturb(begin), v2, boundary);
            terrain.AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);

            // Intermediate slopes
            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended
                Vector3 v1 = v2;
                Color w1 = w2;

                // Determine ending point
                v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));
                w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, i);

                // Add the triangle
                terrain.AddTriangleUnperturbed(v1, v2, boundary);
                terrain.AddTriangleCellData(indices, w1, w2, boundaryWeights);
            }

            // Last slope
            terrain.AddTriangleUnperturbed(v2, HexMetrics.Perturb(left), boundary);
            terrain.AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
        }

        void TriangulateEdgeTerraces(
            EdgeVertices begin, HexCell beginCell,
            EdgeVertices end, HexCell endCell,
            bool hasRoad)
        {
            EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
            Color w2 = HexMetrics.TerraceLerp(weights1, weights2, 1);

            // First slope
            float i1 = beginCell.Index;
            float i2 = endCell.Index;
            TriangulateEdgeStrip(begin, weights1, i1, e2, w2, i2, hasRoad);

            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended
                EdgeVertices e1 = e2;
                Color w1 = w2;

                // Determine ending point
                e2 = EdgeVertices.TerraceLerp(begin, end, i);
                w2 = HexMetrics.TerraceLerp(weights1, weights2, i);

                // terrain.Add the quad
                TriangulateEdgeStrip(e1, w1, i1, e2, w2, i2, hasRoad);
            }

            // Last slope
            TriangulateEdgeStrip(e2, w2, i1, end, weights2, i2, hasRoad);
        }

        void TriangulateRoadSegment(
            Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 v4, Vector3 v5, Vector3 v6,
            Color w1, Color w2, Vector3 indices)
        {
            roads.AddQuad(v1, v2, v4, v5);
            roads.AddQuad(v2, v3, v5, v6);

            roads.AddQuadUV(0f, 1f, 0f, 0f);
            roads.AddQuadUV(1f, 0f, 0f, 0f);

            roads.AddQuadCellData(indices, w1, w2);
            roads.AddQuadCellData(indices, w1, w2);
        }

        void TriangulateRoad(
            Vector3 center, Vector3 mL, Vector3 mR,
            EdgeVertices e, bool hasRoadThroughCellEdge, float index)
        {
            // Check if the road goes through the cell edge
            if (hasRoadThroughCellEdge)
            {
                Vector3 indices;
                indices.x = indices.y = indices.z = index;

                // Create the road such that it goes from center to edge
                Vector3 mC = Vector3.Lerp(mL, mR, 0.5f);
                TriangulateRoadSegment(
                    mL, mC, mR, e.v2, e.v3, e.v4,
                    weights1, weights1, indices);

                // Add two triangles pointing to the center
                roads.AddTriangle(center, mL, mC);
                roads.AddTriangle(center, mC, mR);

                // Add UVs
                roads.AddTriangleUV(
                    new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f));
                roads.AddTriangleUV(
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));

                roads.AddTriangleCellData(indices, weights1);
                roads.AddTriangleCellData(indices, weights1);
            }
            else
            {
                // Create the road such that it fills a center triangle in this direction
                TriangulateRoadEdge(center, mL, mR, index);
            }
        }

        void TriangulateRoadEdge(
            Vector3 center, Vector3 mL, Vector3 mR, float index)
        {
            roads.AddTriangle(center, mL, mR);
            roads.AddTriangleUV(
                new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            Vector3 indices;
            indices.x = indices.y = indices.z = index;
            roads.AddTriangleCellData(indices, weights1);
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

                if (cell.IncomingRiver == direction.Next() && (
                    cell.HasRoadThroughEdge(direction.Next2()) ||
                    cell.HasRoadThroughEdge(direction.Opposite())))
                {
                    features.AddBridge(roadCenter, center - corner * 0.5f);
                }

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

                Vector3 offset = HexMetrics.GetSolidEdgeMiddle(middle);
                roadCenter += offset * 0.25f;

                // Add a bridge
                if (direction == middle &&
                    cell.HasRoadThroughEdge(direction.Opposite()))
                {
                    features.AddBridge(roadCenter,
                                       center - offset * (HexMetrics.innerToOuter * 0.7f));
                }
            }

            Vector3 mL = Vector3.Lerp(roadCenter, e.v1, interpolators.x);
            Vector3 mR = Vector3.Lerp(roadCenter, e.v5, interpolators.y);
            TriangulateRoad(roadCenter, mL, mR, e, hasRoadThroughEdge, cell.Index);

            // Close the gaps created by moving the center of the river away
            // from the center of the Hex cell
            if (previousHasRiver)
            {
                TriangulateRoadEdge(roadCenter, center, mL, cell.Index);
            }
            if (nextHasRiver)
            {
                TriangulateRoadEdge(roadCenter, mR, center, cell.Index);
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