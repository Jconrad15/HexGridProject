using UnityEngine;
using UnityEngine.EventSystems;
using System.IO;

namespace TheZooMustGrow
{
    public class HexMapEditor : MonoBehaviour
    {
        enum OptionalToggle
        {
            Ignore, Yes, No
        }

        OptionalToggle riverMode, roadMode, walledMode;

        public HexGrid hexGrid;

        private int activeElevation;
        private int activeWaterLevel;

        private int activeUrbanLevel;
        private int activeFarmLevel;
        private int activePlantLevel;
        private int activeSpecialIndex;
        private int activeTerrainTypeIndex;

        bool applyElevation = false;
        bool applyWaterLevel = false;

        bool applyUrbanLevel;
        bool applyFarmLevel;
        bool applyPlantLevel;
        bool applySpecialIndex;

        bool editMode;

        int brushSize;

        bool isDrag;
        HexDirection dragDirection;
        HexCell previousCell;

        public Material terrainMaterial;

        void Awake()
        {
            terrainMaterial.DisableKeyword("GRID_ON");
        }

        private void Update()
        {
            if (Input.GetMouseButton(0) &&
                !EventSystem.current.IsPointerOverGameObject())
            {
                HandleInput();
            }
            else
            {
                previousCell = null;
            }
        }

        private void HandleInput()
        {
            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(inputRay, out hit))
            {
                HexCell currentCell = hexGrid.GetCell(hit.point);

                // Check for a drag
                if (previousCell && previousCell != currentCell)
                {
                    ValidateDrag(currentCell);
                }
                else
                {
                    isDrag = false;
                }

                if (editMode)
                {
                    EditCells(currentCell);
                }
                else
                {
                    hexGrid.FindDistancesTo(currentCell);
                }

                previousCell = currentCell;
            }
            else
            {
                previousCell = null;
            }
        }

        void ValidateDrag(HexCell currentCell)
        {
            for (dragDirection = HexDirection.NE;
                dragDirection <= HexDirection.NW;
                dragDirection++)
            {
                if (previousCell.GetNeighbor(dragDirection) == currentCell)
                {
                    isDrag = true;
                    return;
                }
            }
            isDrag = false;
        }

        private void EditCells(HexCell center)
        {
            int centerX = center.coordinates.X;
            int centerZ = center.coordinates.Z;

            // For the bottom half of the HexCells in the brush size
            for (int r = 0, z = centerZ - brushSize; z <= centerZ; z++, r++)
            {
                for (int x = centerX - r; x <= centerX + brushSize; x++)
                {
                    EditCell(hexGrid.GetCell(new HexCoordinates(x, z)));
                }
            }
            // For the top half excluding the middle row of the HexCells in the brush size
            for (int r = 0, z = centerZ + brushSize; z > centerZ; z--, r++)
            {
                for (int x = centerX - brushSize; x <= centerX + r; x++)
                {
                    EditCell(hexGrid.GetCell(new HexCoordinates(x, z)));
                }
            }
        }

        private void EditCell(HexCell cell)
        {
            if (cell)
            {
                if (activeTerrainTypeIndex >= 0)
                {
                    cell.TerrainTypeIndex = activeTerrainTypeIndex;
                }

                if (applyElevation)
                {
                    cell.Elevation = activeElevation;
                }

                if (applyWaterLevel)
                {
                    cell.WaterLevel = activeWaterLevel;
                }

                if (applySpecialIndex)
                {
                    cell.SpecialIndex = activeSpecialIndex;
                }

                if (applyUrbanLevel)
                {
                    cell.UrbanLevel = activeUrbanLevel;
                }

                if (applyFarmLevel)
                {
                    cell.FarmLevel = activeFarmLevel;
                }

                if (applyPlantLevel)
                {
                    cell.PlantLevel = activePlantLevel;
                }

                if (riverMode == OptionalToggle.No)
                {
                    cell.RemoveRiver();
                }

                if (roadMode == OptionalToggle.No)
                {
                    cell.RemoveRoads();
                }

                if (walledMode != OptionalToggle.Ignore)
                {
                    cell.Walled = walledMode == OptionalToggle.Yes;
                }

                // Check for drags
                if (isDrag)
                {
                    HexCell otherCell = cell.GetNeighbor(dragDirection.Opposite());
                    if (otherCell)
                    {
                        if (riverMode == OptionalToggle.Yes)
                        {
                            otherCell.SetOutgoingRiver(dragDirection);
                        }
                        if (roadMode == OptionalToggle.Yes)
                        {
                            otherCell.AddRoad(dragDirection);
                        }
                    }
                }
            }
        }

        public void SetElevation(float elevation)
        {
            activeElevation = (int)elevation;
        }

        public void SetApplyElevation(bool toggle)
        {
            applyElevation = toggle;
        }

        public void SetBrushSize(float size)
        {
            brushSize = (int)size;
        }

        public void SetRiverMode(int mode)
        {
            riverMode = (OptionalToggle)mode;
        }

        public void SetRoadMode(int mode)
        {
            roadMode = (OptionalToggle)mode;
        }

        public void SetApplyWaterLevel(bool toggle)
        {
            applyWaterLevel = toggle;
        }

        public void SetWaterLevel(float level)
        {
            activeWaterLevel = (int)level;
        }

        public void SetApplyUrbanLevel(bool toggle)
        {
            applyUrbanLevel = toggle;
        }

        public void SetUrbanLevel(float level)
        {
            activeUrbanLevel = (int)level;
        }

        public void SetApplyFarmLevel(bool toggle)
        {
            applyFarmLevel = toggle;
        }

        public void SetFarmLevel(float level)
        {
            activeFarmLevel = (int)level;
        }

        public void SetApplyPlantLevel(bool toggle)
        {
            applyPlantLevel = toggle;
        }

        public void SetPlantLevel(float level)
        {
            activePlantLevel = (int)level;
        }

        public void SetWalledMode(int mode)
        {
            walledMode = (OptionalToggle)mode;
        }

        public void SetApplySpecialIndex(bool toggle)
        {
            applySpecialIndex = toggle;
        }

        public void SetSpecialIndex(float index)
        {
            activeSpecialIndex = (int)index;
        }

        public void SetTerrainTypeIndex(int index)
        {
            activeTerrainTypeIndex = index;
        }

        public void ShowGrid(bool visible)
        {
            if (visible)
            {
                terrainMaterial.EnableKeyword("GRID_ON");
            }
            else
            {
                terrainMaterial.DisableKeyword("GRID_ON");
            }
        }

        public void SetEditMode(bool toggle)
        {
            editMode = toggle;
            hexGrid.ShowUI(!toggle);
        }
    }
}