using UnityEngine;
using UnityEngine.EventSystems;

namespace TheZooMustGrow
{
	public class HexGameUI : MonoBehaviour
	{
		public HexGrid grid;

		private HexCell currentCell;
		private HexUnit selectedUnit;

		public void SetEditMode(bool toggle)
		{
			enabled = !toggle;
			grid.ShowUI(!toggle);
			grid.ClearPath();
			// Toggle edit mode shaders
			if (toggle)
            {
				Shader.EnableKeyword("HEX_MAP_EDIT_MODE");
            }
            else
            {
				Shader.DisableKeyword("HEX_MAP_EDIT_MODE");
            }
		}

		private bool UpdateCurrentCell()
        {
			HexCell cell =
				grid.GetCell(Camera.main.ScreenPointToRay(Input.mousePosition));

			if (cell != currentCell)
            {
				currentCell = cell;
				return true;
            }
			return false;
        }

		private void DoSelection()
        {
			grid.ClearPath();
			UpdateCurrentCell();
			if (currentCell)
            {
				selectedUnit = currentCell.Unit;
            }
        }

        private void Update()
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
				// Select a unit
				if (Input.GetMouseButtonDown(0))
                {
					DoSelection();
                }
				else if (selectedUnit)
                {
					// Otherwise move when right click
					if (Input.GetMouseButtonDown(1))
                    {
						DoMove();
                    }
					// Otherwise pathfind to current cell
                    else
                    {
						DoPathfinding();
                    }
                }
            }
        }

		private void DoPathfinding()
		{
			if (UpdateCurrentCell())
			{
				if (currentCell && selectedUnit.IsValidDestination(currentCell))
				{
					grid.FindPath(selectedUnit.Location, currentCell, 24);
				}
                else
                {
					grid.ClearPath();
                }
			}
		}

		private void DoMove()
		{
			if (grid.HasPath)
			{
				selectedUnit.Travel(grid.GetPath());
				grid.ClearPath();
			}
		}

	}
}