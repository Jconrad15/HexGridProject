using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
	public class HexMapGenerator : MonoBehaviour
	{
		public HexGrid grid;
		private int cellCount;

		public int seed;
		public bool useFixedSeed;

		private HexCellPriorityQueue searchFrontier;
		private int searchFrontierPhase;
		private int xMin, xMax, zMin, zMax;

		[Range(0f, 0.5f)]
		public float jitterProbability = 0.25f;

		[Range(20, 200)]
		public int chunkSizeMin = 30;

		[Range(20, 200)]
		public int chunkSizeMax = 100;

		[Range(5, 95)]
		public int landPercentage = 50;

		[Range(1, 5)]
		public int waterLevel = 3;

		[Range(0f, 1f)]
		public float highRiseProbability = 0.25f;

		[Range(0f, 0.4f)]
		public float sinkProbability = 0.2f;

		[Range(-4, 0)]
		public int elevationMinimum = -2;

		[Range(6, 10)]
		public int elevationMaximum = 8;

		[Range(0, 10)]
		public int mapBorderX = 5;

		[Range(0, 10)]
		public int mapBorderZ = 5;

		public void GenerateMap(int x, int z)
		{
			Random.State originalRandomState = Random.state;

			if (!useFixedSeed)
			{
				seed = Random.Range(0, int.MaxValue);
				seed ^= (int)System.DateTime.Now.Ticks;
				seed ^= (int)Time.unscaledTime;
				seed &= int.MaxValue;
			}
			Random.InitState(seed);

			cellCount = x * z;

			// Generate base map
			grid.CreateMap(x, z);

            // Initiate search frontier
            if (searchFrontier == null)
            {
				searchFrontier = new HexCellPriorityQueue();
            }

            // Set base water level
            for (int i = 0; i < cellCount; i++)
            {
				grid.GetCell(i).WaterLevel = waterLevel;
            }

			// Set land bound constraints
			xMin = mapBorderX;
			xMax = x - mapBorderX;
			zMin = mapBorderZ;
			zMax = z - mapBorderZ;

			CreateLand();
			SetTerrainType();

            // Set all search phase variables in cells to zero
            for (int i = 0; i < cellCount; i++)
            {
				grid.GetCell(i).SearchPhase = 0;
            }

			Random.state = originalRandomState;
		}

		private void CreateLand()
        {
            int landBudget = Mathf.RoundToInt(cellCount * landPercentage * 0.01f);

			for (int guard = 0; landBudget > 0 && guard < 10000; guard++)
			{
				int chunkSize = Random.Range(chunkSizeMin, chunkSizeMax + 1);

				// Determine to raise or sink land
				if (Random.value < sinkProbability)
				{
					landBudget = SinkTerrain(chunkSize, landBudget);
				}
				else
				{
					landBudget = RaiseTerrain(chunkSize, landBudget);
				}
			}

			// Check if the landbudget was used,
			// or the map creation timed out using the guard
			if (landBudget > 0)
            {
				Debug.LogWarning("Failed to use up " + landBudget + "land budget.");
            }
        }

		private int RaiseTerrain(int chunkSize, int budget)
        {
			// Start search for random group of terrain
			searchFrontierPhase += 1;
			HexCell firstCell = GetRandomCell();
			firstCell.SearchPhase = searchFrontierPhase;
			firstCell.Distance = 0;
			firstCell.SearchHeuristic = 0;
			searchFrontier.Enqueue(firstCell);

			HexCoordinates center = firstCell.coordinates;

			int rise = Random.value < highRiseProbability ? 2 : 1;
			int size = 0;
			while (size < chunkSize && searchFrontier.Count > 0)
            {
				HexCell current = searchFrontier.Dequeue();

				int originalElevation = current.Elevation;

				int newElevation = originalElevation + rise;
				if (newElevation > elevationMaximum)
                {
					continue;
                }

				current.Elevation = newElevation;

				// If the current cell has been raised passed the water level and check budget
				if (originalElevation < waterLevel &&
					newElevation >= waterLevel 
					&& --budget == 0)
                {
					break;
                }
				size += 1;

				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = current.GetNeighbor(d);
					if(neighbor && neighbor.SearchPhase < searchFrontierPhase)
                    {
						neighbor.SearchPhase = searchFrontierPhase;
						neighbor.Distance = neighbor.coordinates.DistanceTo(center);
						neighbor.SearchHeuristic = Random.value < jitterProbability ? 1 : 0;
						searchFrontier.Enqueue(neighbor);
                    }

				}
            }
			searchFrontier.Clear();
			return budget;
        }

		private int SinkTerrain(int chunkSize, int budget)
		{
			// Start search for random group of terrain
			searchFrontierPhase += 1;
			HexCell firstCell = GetRandomCell();
			firstCell.SearchPhase = searchFrontierPhase;
			firstCell.Distance = 0;
			firstCell.SearchHeuristic = 0;
			searchFrontier.Enqueue(firstCell);

			HexCoordinates center = firstCell.coordinates;

			int sink = Random.value < highRiseProbability ? 2 : 1;
			int size = 0;
			while (size < chunkSize && searchFrontier.Count > 0)
			{
				HexCell current = searchFrontier.Dequeue();
				int originalElevation = current.Elevation;

				int newElevation = current.Elevation - sink;
				if (newElevation < elevationMinimum)
                {
					continue;
                }

				current.Elevation = newElevation;

				// If the current cell has been lowered passed the water level
				if (originalElevation >= waterLevel &&
					newElevation < waterLevel
					&& --budget == 0)
				{
					budget += 1;
				}
				size += 1;

				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = current.GetNeighbor(d);
					if (neighbor && neighbor.SearchPhase < searchFrontierPhase)
					{
						neighbor.SearchPhase = searchFrontierPhase;
						neighbor.Distance = neighbor.coordinates.DistanceTo(center);
						neighbor.SearchHeuristic = Random.value < jitterProbability ? 1 : 0;
						searchFrontier.Enqueue(neighbor);
					}

				}
			}
			searchFrontier.Clear();
			return budget;
		}

		private void SetTerrainType()
        {
			for (int i = 0; i < cellCount; i++)
			{
				HexCell cell = grid.GetCell(i);
				if (!cell.IsUnderwater)
				{
					cell.TerrainTypeIndex = cell.Elevation - cell.WaterLevel;
				}
			}
        }

		private HexCell GetRandomCell()
        {
			return grid.GetCell(
				Random.Range(xMin, xMax),
				Random.Range(zMin, zMax));
		}

	}
}