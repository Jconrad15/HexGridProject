using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HexMesh : MonoBehaviour
    {
        // The lists that HexMesh uses are effectively temporary buffers.
        // They are only used during triangulation.
        // And chunks are triangulated one at a time.
        // So we really only need one set of lists,
        // not one set per hex mesh object.
        // We can do so by making the lists static.
        static List<Vector3> vertices = new List<Vector3>();
        static List<Color> colors = new List<Color>();
        static List<int> triangles = new List<int>();

        Mesh hexMesh;
        MeshCollider meshCollider;

        private void Awake()
        {
            GetComponent<MeshFilter>().mesh = hexMesh = new Mesh();
            meshCollider = gameObject.AddComponent<MeshCollider>();
            hexMesh.name = "Hex Mesh";
        }

        public void Triangulate(HexCell[] cells)
        {
            hexMesh.Clear();
            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            for (int i = 0; i < cells.Length; i++)
            {
                Triangulate(cells[i]);
            }

            hexMesh.vertices = vertices.ToArray();
            hexMesh.colors = colors.ToArray();
            hexMesh.triangles = triangles.ToArray();
            hexMesh.RecalculateNormals();
            meshCollider.sharedMesh = hexMesh;
        }

        void Triangulate(HexCell cell)
        {
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                Triangulate(d, cell);
            }
        }

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
            }
            else
            {
                TriangulateEdgeFan(center, e, cell.Color);
            }

            // Add a connection to neighboring hex cell if it is NE, E, and SE
            if (direction <= HexDirection.SE)
            {
                TriangulateConnection(direction, cell, e);
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

            TriangulateEdgeStrip(m, cell.Color, e, cell.Color);
            TriangulateEdgeFan(center, m, cell.Color);
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
            AddTriangle(centerL, m.v1, m.v2);
            AddTriangleColor(cell.Color);
            AddQuad(centerL, center, m.v2, m.v3);
            AddQuadColor(cell.Color);
            AddQuad(center, centerR, m.v3, m.v4);
            AddQuadColor(cell.Color);
            AddTriangle(centerR, m.v4, m.v5);
            AddTriangleColor(cell.Color);
        }

        void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, Color color)
        {
            AddTriangle(center, edge.v1, edge.v2);
            AddTriangleColor(color);
            AddTriangle(center, edge.v2, edge.v3);
            AddTriangleColor(color);
            AddTriangle(center, edge.v3, edge.v4);
            AddTriangleColor(color);
            AddTriangle(center, edge.v4, edge.v5);
            AddTriangleColor(color);
        }

        void TriangulateEdgeStrip(
            EdgeVertices e1, Color c1,
            EdgeVertices e2, Color c2)
        {
            AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            AddQuadColor(c1, c2);
            AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            AddQuadColor(c1, c2);
            AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            AddQuadColor(c1, c2);
            AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
            AddQuadColor(c1, c2);
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
            }

            // Check edge type
            if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
            {
                TriangulateEdgeTerraces(e1, cell, e2, neighbor);
            }
            else
            {
                TriangulateEdgeStrip(e1, cell.Color, e2, neighbor.Color);
            }

            // Add triangular holes/corners for all NE and E neighbors
            HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
            if (direction <= HexDirection.E && nextNeighbor != null)
            {
                // Modify for elevation
                Vector3 v5 = e1.v5 + HexMetrics.GetBridge(direction.Next());
                v5.y = nextNeighbor.Position.y;

                // Determine which cell is the lowest
                if (cell.Elevation <= neighbor.Elevation)
                {
                    if(cell.Elevation <= nextNeighbor.Elevation)
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
                AddTriangle(bottom, left, right);
                AddTriangleColor(bottomCell.Color, leftCell.Color, rightCell.Color);
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
            AddTriangle(begin, v3, v4);
            AddTriangleColor(beginCell.Color, c3, c4);

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

                // Add the quad
                AddQuad(v1, v2, v3, v4);
                AddQuadColor(c1, c2, c3, c4);
            }

            // Last slope
            AddQuad(v3, v4, left, right);
            AddQuadColor(c3, c4, leftCell.Color, rightCell.Color);
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
            
            Vector3 boundary = Vector3.Lerp(Perturb(begin), Perturb(right), b);
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
                AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
                AddTriangleColor(leftCell.Color, rightCell.Color, boundaryColor);
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

            Vector3 boundary = Vector3.Lerp(Perturb(begin), Perturb(left), b);
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
                AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
                AddTriangleColor(leftCell.Color, rightCell.Color, boundaryColor);
            }
        }

        private void TriangulateBoundaryTriangle(
            Vector3 begin, HexCell beginCell,
            Vector3 left, HexCell leftCell,
            Vector3 boundary, Color boundaryColor)
        {
            Vector3 v2 = Perturb(HexMetrics.TerraceLerp(begin, left, 1));
            Color c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, 1);

            // First slope
            AddTriangleUnperturbed(Perturb(begin), v2, boundary);
            AddTriangleColor(beginCell.Color, c2, boundaryColor);

            // Intermediate slopes
            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended
                Vector3 v1 = v2;
                Color c1 = c2;

                // Determine ending point
                v2 = Perturb(HexMetrics.TerraceLerp(begin, left, i));
                c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);

                // Add the triangle
                AddTriangleUnperturbed(v1, v2, boundary);
                AddTriangleColor(c1, c2, boundaryColor);
            }

            // Last slope
            AddTriangleUnperturbed(v2, Perturb(left), boundary);
            AddTriangleColor(c2, leftCell.Color, boundaryColor);
        }

        void TriangulateEdgeTerraces(
            EdgeVertices begin, HexCell beginCell,
            EdgeVertices end, HexCell endCell)
        {
            EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
            Color c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, 1);

            // First slope
            TriangulateEdgeStrip(begin, beginCell.Color, e2, c2);

            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                // Start the next portion where the last portion ended
                EdgeVertices e1 = e2;
                Color c1 = c2;

                // Determine ending point
                e2 = EdgeVertices.TerraceLerp(begin, end, i);
                c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, i);

                // Add the quad
                TriangulateEdgeStrip(e1, c1, e2, c2);
            }

            // Last slope
            TriangulateEdgeStrip(e2, c2, end, endCell.Color);
        }

        void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int vertexIndex = vertices.Count;
            vertices.Add(Perturb(v1));
            vertices.Add(Perturb(v2));
            vertices.Add(Perturb(v3));

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
        }

        void AddTriangleUnperturbed(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int vertexIndex = vertices.Count;
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
        }

        /// <summary>
        /// Add the same color to each vertex.
        /// </summary>
        /// <param name="color"></param>
        void AddTriangleColor(Color color)
        {
            // Three vertices per triangle
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
        }

        /// <summary>
        /// Add a different color to each vertex.  
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="c3"></param>
        void AddTriangleColor(Color c1, Color c2, Color c3)
        {
            colors.Add(c1);
            colors.Add(c2);
            colors.Add(c3);
        }

        void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
        {
            int vertexIndex = vertices.Count;

            vertices.Add(Perturb(v1));
            vertices.Add(Perturb(v2));
            vertices.Add(Perturb(v3));
            vertices.Add(Perturb(v4));

            // two triangles in the quad
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 3);
        }

        void AddQuadColor(Color c1, Color c2, Color c3, Color c4)
        {
            colors.Add(c1);
            colors.Add(c2);
            colors.Add(c3);
            colors.Add(c4);
        }

        void AddQuadColor(Color c1, Color c2)
        {
            colors.Add(c1);
            colors.Add(c1);
            colors.Add(c2);
            colors.Add(c2);
        }

        void AddQuadColor(Color color)
        {
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
        }

        private Vector3 Perturb(Vector3 position)
        {
            Vector4 sample = HexMetrics.SampleNoise(position);
            position.x += ((sample.x * 2f) - 1f) * HexMetrics.cellPerturbStrength;
            //position.y += ((sample.y * 2f) - 1f) * HexMetrics.cellPerturbStrength;
            position.z += ((sample.z * 2f) - 1f) * HexMetrics.cellPerturbStrength;
            return position;
        }
    }
}