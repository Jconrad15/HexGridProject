using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
	public class HexMapGenerator : MonoBehaviour
	{
		[SerializeField]
		private CloudManager cloudManager;

		private int farmPercentage = 10;
		private int maxFarmStamp = 4;
		private int minFarmStamp = 2;

		public HexGrid grid;
		private int cellCount;
		private int landCells;

		public int seed;
		public bool useFixedSeed;

		private HexCellPriorityQueue searchFrontier;
		private int searchFrontierPhase;

		private List<MapRegion> regions;
		struct MapRegion
		{
			public int xMin, xMax, zMin, zMax;
		}

		private List<ClimateData> climate = new List<ClimateData>();
		private List<ClimateData> nextClimate = new List<ClimateData>();
		private List<HexDirection> flowDirections = new List<HexDirection>();
		private List<HexDirection> roadDirections = new List<HexDirection>();
		private List<HexDirection> featureLocations = new List<HexDirection>();

		struct ClimateData
        {
			public float clouds;
			public float moisture;
        }

		public HexMapGeneratorData generatorData;

		public enum HemisphereMode
		{
			Both, North, South
		}

		public HemisphereMode hemisphere;

		private int temperatureJitterChannel;

		static float[] temperatureBands = { 0.1f, 0.3f, 0.6f };
		static float[] moistureBands = { 0.12f, 0.28f, 0.85f };

		struct Biome
        {
			public int terrain, plant;

			public Biome (int terrain, int plant)
            {
				this.terrain = terrain;
				this.plant = plant;
            }
        }

		// Biome matrix
		// X-axis == moisture	Y-axis == temperature
		static Biome[] biomes = {
			new Biome(0, 0), new Biome(4, 0), new Biome(4, 0), new Biome(4, 0),
			new Biome(0, 0), new Biome(2, 0), new Biome(2, 1), new Biome(2, 2),
			new Biome(0, 0), new Biome(1, 0), new Biome(1, 1), new Biome(1, 2),
			new Biome(0, 0), new Biome(1, 1), new Biome(1, 2), new Biome(1, 3)};

        private void OnEnable()
        {
			generatorData = new HexMapGeneratorData();
			generatorData.SetDefaults();
        }

        public void GenerateMap(int x, int z, bool wrapping)
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
			grid.CreateMap(x, z, wrapping);

            // Initiate search frontier
            if (searchFrontier == null)
            {
				searchFrontier = new HexCellPriorityQueue();
            }

            // Set base water level
            for (int i = 0; i < cellCount; i++)
            {
				grid.GetCell(i).WaterLevel = generatorData.waterLevel;
            }

			// Set land bound constraints
			CreateRegions();

			CreateLand();
			ErodeLand();
			CreateClimate();
			CreateRivers();
			CreateRoads();
			SetTerrainType();

			CreateUrbans();
			CreateFarms();
			CreateWalls();

			cloudManager.GenerateNewClouds(seed);

            // Set all search phase variables in cells to zero
            for (int i = 0; i < cellCount; i++)
            {
				grid.GetCell(i).SearchPhase = 0;
            }

			Random.state = originalRandomState;
		}

		private void CreateLand()
        {
            int landBudget = Mathf.RoundToInt(cellCount * generatorData.landPercentage * 0.01f);
			landCells = landBudget;

			for (int guard = 0; guard < 10000; guard++)
			{
				bool sink = Random.value < generatorData.sinkProbability;

				// Loop through regions
				for (int i = 0; i < regions.Count; i++)
				{
					MapRegion region = regions[i];

					int chunkSize = Random.Range(generatorData.chunkSizeMin, generatorData.chunkSizeMax + 1);

					// Determine to raise or sink land
					if (sink)
					{
						landBudget = SinkTerrain(chunkSize, landBudget, region);
					}
					else
					{
						landBudget = RaiseTerrain(chunkSize, landBudget, region);
						
						// Check if landBudget is met
						if (landBudget == 0)
                        {
							return;
                        }
					}
				}
			}

			// Check if the landbudget was used,
			// or the map creation timed out using the guard
			if (landBudget > 0)
            {
				Debug.LogWarning("Failed to use up " + landBudget + "land budget.");
				landCells -= landBudget;
            }
        }

		private int RaiseTerrain(int chunkSize, int budget, MapRegion region)
        {
			// Start search for random group of terrain
			searchFrontierPhase += 1;
			HexCell firstCell = GetRandomCell(region);
			firstCell.SearchPhase = searchFrontierPhase;
			firstCell.Distance = 0;
			firstCell.SearchHeuristic = 0;
			searchFrontier.Enqueue(firstCell);

			HexCoordinates center = firstCell.coordinates;

			int rise = Random.value < generatorData.highRiseProbability ? 2 : 1;
			int size = 0;
			while (size < chunkSize && searchFrontier.Count > 0)
            {
				HexCell current = searchFrontier.Dequeue();

				int originalElevation = current.Elevation;

				int newElevation = originalElevation + rise;
				if (newElevation > generatorData.elevationMaximum)
                {
					continue;
                }

				current.Elevation = newElevation;

				// If the current cell has been raised passed the water level and check budget
				if (originalElevation < generatorData.waterLevel &&
					newElevation >= generatorData.waterLevel 
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
						neighbor.SearchHeuristic = Random.value < generatorData.jitterProbability ? 1 : 0;
						searchFrontier.Enqueue(neighbor);
                    }

				}
            }
			searchFrontier.Clear();
			return budget;
        }

		private int SinkTerrain(int chunkSize, int budget, MapRegion region)
		{
			// Start search for random group of terrain
			searchFrontierPhase += 1;
			HexCell firstCell = GetRandomCell(region);
			firstCell.SearchPhase = searchFrontierPhase;
			firstCell.Distance = 0;
			firstCell.SearchHeuristic = 0;
			searchFrontier.Enqueue(firstCell);

			HexCoordinates center = firstCell.coordinates;

			int sink = Random.value < generatorData.highRiseProbability ? 2 : 1;
			int size = 0;
			while (size < chunkSize && searchFrontier.Count > 0)
			{
				HexCell current = searchFrontier.Dequeue();
				int originalElevation = current.Elevation;

				int newElevation = current.Elevation - sink;
				if (newElevation < generatorData.elevationMinimum)
                {
					continue;
                }

				current.Elevation = newElevation;

				// If the current cell has been lowered passed the water level
				if (originalElevation >= generatorData.waterLevel &&
					newElevation < generatorData.waterLevel
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
						neighbor.SearchHeuristic = Random.value < generatorData.jitterProbability ? 1 : 0;
						searchFrontier.Enqueue(neighbor);
					}

				}
			}
			searchFrontier.Clear();
			return budget;
		}

		private void SetTerrainType()
		{
			temperatureJitterChannel = Random.Range(0, 4);

			int rockDesertElevation =
				generatorData.elevationMaximum - (generatorData.elevationMaximum - generatorData.waterLevel) / 2;

			for (int i = 0; i < cellCount; i++)
			{
				HexCell cell = grid.GetCell(i);

				float temperature = DetermineTemperature(cell);
				float moisture = climate[i].moisture;

				if (!cell.IsUnderwater)
				{
					int t = 0;
                    for (; t < temperatureBands.Length; t++)
                    {
						if (temperature < temperatureBands[t])
                        {
							break;
                        }
                    }

					int m = 0;
					for (; m < moistureBands.Length; m++)
					{
						if (moisture < moistureBands[m])
						{
							break;
						}
					}

					Biome cellBiome = biomes[t * 4 + m];

					// If sand is at high elevation, change to rock
					if (cellBiome.terrain == 0)
					{
						if (cell.Elevation >= rockDesertElevation)
						{
							cellBiome.terrain = 3;
						}
					}
					// If max elevation, set to snow
					else if (cell.Elevation == generatorData.elevationMaximum)
					{
						cellBiome.terrain = 4;
					}

					//  Tweak plant levels
					if (cellBiome.terrain == 4)
					{
						// Remove plants from snow
						cellBiome.plant = 0;
					}
					else if (cellBiome.plant < 3 && cell.HasRiver)
					{
						// Max plants on rivers
						cellBiome.plant += 1;
					}


					cell.TerrainTypeIndex = cellBiome.terrain;
					cell.PlantLevel = cellBiome.plant;
				}
				else
				{
					// For underwater cells
					int terrain;
					if (cell.Elevation == generatorData.waterLevel - 1)
					{
						// For shallow water determine coast type
						int cliffs = 0, slopes = 0;
						for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
						{
							HexCell neighbor = cell.GetNeighbor(d);
							if (!neighbor)
							{
								continue;
							}
							int delta = neighbor.Elevation - cell.WaterLevel;
							if (delta == 0)
							{
								slopes += 1;
							}
							else if (delta > 0)
							{
								cliffs += 1;
							}
						}

						if (cliffs + slopes > 3)
						{
							terrain = 1;
						}
						else if (cliffs > 0)
						{
							terrain = 3;
						}
						else if (slopes > 0)
						{
							terrain = 0;
						}
						else
						{
							terrain = 1;
						}
					}
					else if (cell.Elevation >= generatorData.waterLevel)
					{
						// Grass in higher elevation lakes
						terrain = 1;
					}
					else if (cell.Elevation < 0)
					{
						terrain = 3;
					}
					else
					{
						terrain = 2;
					}


					if (terrain == 1 && temperature < temperatureBands[0])
					{
						terrain = 2;
					}

					cell.TerrainTypeIndex = terrain;
				}
			}
		}

		private HexCell GetRandomCell(MapRegion region)
        {
			return grid.GetCell(
				Random.Range(region.xMin, region.xMax),
				Random.Range(region.zMin, region.zMax));
		}

		private void CreateRegions()
        {
			if (regions == null)
            {
				regions = new List<MapRegion>();
            }
            else
            {
				regions.Clear();
            }

			int borderX = grid.wrapping ? generatorData.regionBorder : generatorData.mapBorderX;
			MapRegion region;
			switch (generatorData.regionCount)
			{
				default:
					if (grid.wrapping)
					{
						borderX = 0;
					}
					region.xMin = borderX;
					region.xMax = grid.cellCountX - borderX;
					region.zMin = generatorData.mapBorderZ;
					region.zMax = grid.cellCountZ - generatorData.mapBorderZ;
					regions.Add(region);
					break;
				case 2:
					if (Random.value < 0.5f)
					{
						region.xMin = borderX;
						region.xMax = grid.cellCountX / 2 - generatorData.regionBorder;
						region.zMin = generatorData.mapBorderZ;
						region.zMax = grid.cellCountZ - generatorData.mapBorderZ;
						regions.Add(region);
						region.xMin = grid.cellCountX / 2 + generatorData.regionBorder;
						region.xMax = grid.cellCountX - borderX;
						regions.Add(region);
					}
					else
					{
						if (grid.wrapping)
						{
							borderX = 0;
						}
						region.xMin = borderX;
						region.xMax = grid.cellCountX - borderX;
						region.zMin = generatorData.mapBorderZ;
						region.zMax = grid.cellCountZ / 2 - generatorData.regionBorder;
						regions.Add(region);
						region.zMin = grid.cellCountZ / 2 + generatorData.regionBorder;
						region.zMax = grid.cellCountZ - generatorData.mapBorderZ;
						regions.Add(region);
					}
					break;
				case 3:
					region.xMin = borderX;
					region.xMax = grid.cellCountX / 3 - generatorData.regionBorder;
					region.zMin = generatorData.mapBorderZ;
					region.zMax = grid.cellCountZ - generatorData.mapBorderZ;
					regions.Add(region);
					region.xMin = grid.cellCountX / 3 + generatorData.regionBorder;
					region.xMax = grid.cellCountX * 2 / 3 - generatorData.regionBorder;
					regions.Add(region);
					region.xMin = grid.cellCountX * 2 / 3 + generatorData.regionBorder;
					region.xMax = grid.cellCountX - borderX;
					regions.Add(region);
					break;
				case 4:
					region.xMin = borderX;
					region.xMax = grid.cellCountX / 2 - generatorData.regionBorder;
					region.zMin = generatorData.mapBorderZ;
					region.zMax = grid.cellCountZ / 2 - generatorData.regionBorder;
					regions.Add(region);
					region.xMin = grid.cellCountX / 2 + generatorData.regionBorder;
					region.xMax = grid.cellCountX - borderX;
					regions.Add(region);
					region.zMin = grid.cellCountZ / 2 + generatorData.regionBorder;
					region.zMax = grid.cellCountZ - generatorData.mapBorderZ;
					regions.Add(region);
					region.xMin = borderX;
					region.xMax = grid.cellCountX / 2 - generatorData.regionBorder;
					regions.Add(region);
					break;
			}
		}

		private void ErodeLand()
        {
			List<HexCell> erodibleCells = ListPool<HexCell>.Get();
            for (int i = 0; i < cellCount; i++)
            {
				HexCell cell = grid.GetCell(i);
				if (IsErodible(cell))
                {
					erodibleCells.Add(cell);
                }
            }

			// Determine the number to leave as erodible
			int targetErodibleCount = 
				(int)(erodibleCells.Count * (100 - generatorData.erosionPercentage) * 0.01f);

			while (erodibleCells.Count > targetErodibleCount)
			{
				// Get random erodible cell to erode
				int index = Random.Range(0, erodibleCells.Count);

				HexCell cell = erodibleCells[index];
				HexCell targetCell = GetErosionTarget(cell);

				// Move elevation from erodible cell to erosion target
				cell.Elevation -= 1;
				targetCell.Elevation += 1;

				// Remove the cell if it is no longer erodible
				if (IsErodible(cell) == false)
				{
					erodibleCells[index] = erodibleCells[erodibleCells.Count - 1];
					erodibleCells.RemoveAt(erodibleCells.Count - 1);
				}

				// Check the cell's neighbors to see if they became erodible
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);
					if (neighbor && neighbor.Elevation == cell.Elevation + 2 &&
						!erodibleCells.Contains(neighbor))
					{
						erodibleCells.Add(neighbor);
					}
				}

				if (IsErodible(targetCell) && !erodibleCells.Contains(targetCell))
				{
					erodibleCells.Add(targetCell);
				}

				// Check if the erosion target's neighbors became erodible or not erodible
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = targetCell.GetNeighbor(d);
					if (neighbor && neighbor != cell &&
						neighbor.Elevation == targetCell.Elevation + 1 &&
						!IsErodible(neighbor))
					{
						erodibleCells.Remove(neighbor);
					}
				}

			}

			// Re-add the temp list to the pool
			ListPool<HexCell>.Add(erodibleCells);
        }

		/// <summary>
		/// Returns true if a cell is high enough compared to neighbors to be erodible.
		/// </summary>
		/// <param name="cell"></param>
		/// <returns></returns>
		private bool IsErodible(HexCell cell)
		{
			int erodibleElevation = cell.Elevation - 2;
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				HexCell neighbor = cell.GetNeighbor(d);
				if (neighbor && neighbor.Elevation <= erodibleElevation)
				{
					return true;
				}
			}
			return false;
		}

		private HexCell GetErosionTarget(HexCell cell)
        {
			// Get list from list pool
			List<HexCell> candidates = ListPool<HexCell>.Get();
			
			int erodibleElevation = cell.Elevation - 2;
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				HexCell neighbor = cell.GetNeighbor(d);
				if (neighbor && neighbor.Elevation <= erodibleElevation)
				{
					candidates.Add(neighbor);
				}
			}

			// Choose random target from candidate list
			HexCell target = candidates[Random.Range(0, candidates.Count)];
			
			// Return list to list pool
			ListPool<HexCell>.Add(candidates);

			return target;
		}

		private void CreateClimate()
        {
			climate.Clear();
			nextClimate.Clear();

			ClimateData initialData = new ClimateData();
			initialData.moisture = generatorData.startingMoisture;
			ClimateData clearData = new ClimateData();
			for (int i = 0; i < cellCount; i++)
            {
				climate.Add(initialData);
				nextClimate.Add(clearData);
            }

			for (int cycle = 0; cycle < 40; cycle++)
			{
				for (int i = 0; i < cellCount; i++)
				{
					EvolveClimate(i);
				}

				// Swap the two climate lists each cycle
				List<ClimateData> swap = climate;
				climate = nextClimate;
				nextClimate = swap;
			}
		}

		private void EvolveClimate(int cellIndex)
        {
			HexCell cell = grid.GetCell(cellIndex);
			ClimateData cellClimate = climate[cellIndex];

			// Evaporate water from moisture to clouds
			if (cell.IsUnderwater)
            {
				cellClimate.moisture = 1f;
				cellClimate.clouds += generatorData.evaporationFactor;
            }
            else
            {
				float evaporation = cellClimate.moisture * generatorData.evaporationFactor;
				cellClimate.moisture -= evaporation;
				cellClimate.clouds += evaporation;
			}

			// Precipitate water from clouds to moisture
			float precipitation = cellClimate.clouds * generatorData.precipitationFactor;
			cellClimate.clouds -= precipitation;
			cellClimate.moisture += precipitation;

			float cloudMaximum = 1f - cell.ViewElevation / (generatorData.elevationMaximum + 1f);
			// If there is more clouds than allowed, precipitate
			if (cellClimate.clouds > cloudMaximum)
			{
				cellClimate.moisture += cellClimate.clouds - cloudMaximum;
				cellClimate.clouds = cloudMaximum;
			}


			// Disperse clouds and moisture
			HexDirection mainDispersalDirection = generatorData.windFromDirection.Opposite();
			float cloudDispersal = cellClimate.clouds * (1f / (5f + generatorData.windStrength));
			float runoff = cellClimate.moisture * generatorData.runoffFactor * (1f / 6f);
			float seepage = cellClimate.moisture * generatorData.seepageFactor * (1f / 6f);
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				HexCell neighbor = cell.GetNeighbor(d);
				if (!neighbor)
				{
					continue;
				}
				
				ClimateData neighborClimate = nextClimate[neighbor.Index];

				// Disperse clouds
				if (d == mainDispersalDirection)
				{
					neighborClimate.clouds += cloudDispersal * generatorData.windStrength;
				}
				else
				{
					neighborClimate.clouds += cloudDispersal;
				}
				//Disperse moisture
				int elevationDelta = neighbor.ViewElevation - cell.ViewElevation;
				// If neighbor is lower, then runoff
				if (elevationDelta < 0)
                {
					cellClimate.moisture -= runoff;
					neighborClimate.moisture += runoff;
                }
				// If neighbor is the same elevation, then seepage
				else if (elevationDelta == 0)
                {
					cellClimate.moisture -= seepage;
					neighborClimate.moisture += seepage;
                }

				nextClimate[neighbor.Index] = neighborClimate;
			}

			ClimateData nextCellClimate = nextClimate[cellIndex];
			nextCellClimate.moisture += cellClimate.moisture;
			// Limit moisture to 1
			if (nextCellClimate.moisture > 1f)
			{
				nextCellClimate.moisture = 1f;
			}

			nextClimate[cellIndex] = nextCellClimate;
			climate[cellIndex] = new ClimateData();
		}

		private void CreateRoads()
        {
			List<HexCell> roadOrigins = ListPool<HexCell>.Get();

			// Determine a weight for each cell
			for (int i = 0; i < cellCount; i++)
            {
				HexCell cell = grid.GetCell(i);
				// No roads underwater
				if (cell.IsUnderwater)
                {
					continue;
                }

				// Random road weighting
				float weight = Random.Range(0f, 1f);
				
				if (weight > 0.8f)
				{
					roadOrigins.Add(cell);
					roadOrigins.Add(cell);
				}
				if (weight > 0.6f)
				{
					roadOrigins.Add(cell);
				}
				if (weight > 0.4f)
                {
                    roadOrigins.Add(cell);
                }
			}

			int roadBudget = Mathf.RoundToInt(landCells * generatorData.roadPercentage * 0.01f);

            // Select road origin cells
            while (roadBudget > 0 && roadOrigins.Count > 0)
			{
				int index = Random.Range(0, roadOrigins.Count);
				int lastIndex = roadOrigins.Count - 1;
				HexCell origin = roadOrigins[index];
				roadOrigins[index] = roadOrigins[lastIndex];
				roadOrigins.RemoveAt(lastIndex);

				// Check to create the road
				roadBudget -= CreateRoad(origin);
			}

			if (roadBudget > 0)
			{
				Debug.LogWarning("Failed to use up road budget.");
			}

			ListPool<HexCell>.Add(roadOrigins);
		}

		private int CreateRoad(HexCell origin)
        {
			int length = 1;

			HexCell cell = origin;
			HexDirection direction = HexDirection.NE;

			int maxRoadDistance = Random.Range(
				generatorData.minRoadLength, generatorData.maxRoadLength + 1);

			while (cell.IsUnderwater == false &&
				   cell.DoAllNeighborsHaveRoads() == false &&
				   length <= maxRoadDistance)
			{
				roadDirections.Clear();

				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);

					if (!neighbor)
					{
						continue;
					}

					// If neighbor is underwater, exit
					if (neighbor.IsUnderwater == true)
                    {
						continue;
                    }

					// If neighbor already has road through this edge, exit
					if (neighbor == origin || neighbor.HasRoadThroughEdge(d.Opposite()))
					{
						continue;
					}

					// Skip neighbor cell if it is steep uphill
					int delta = neighbor.Elevation - cell.Elevation;
					if (delta > 1)
					{
						continue;
					}

					// If no roads in next sharp directions
					// 'weight' the cell list with the neighbor tile
					HexCell neighborNext = cell.GetNeighbor(d.Next());
					HexCell neighborPrevious = cell.GetNeighbor(d.Next());
					if (neighborNext && neighborPrevious)
					{
						if (cell.GetNeighbor(d.Next()).HasRoads == false &&
							cell.GetNeighbor(d.Previous()).HasRoads == false)
						{
							roadDirections.Add(d);
							roadDirections.Add(d);
						}
					}

					roadDirections.Add(d);
				}

				// If no place to bring road to, exit
				if (roadDirections.Count == 0)
				{
					// If road is only 1 long, return 0
					if (length == 1)
					{
						return 0;
					}

					break;
				}

				direction = roadDirections[Random.Range(0, roadDirections.Count)];

				cell.AddRoad(direction);
				length += 1;

				cell = cell.GetNeighbor(direction);
			}

			return length;
		}

		private void CreateFarms()
		{
			List<HexCell> farmCenter = ListPool<HexCell>.Get();

			// Determine a weight for each cell
			for (int i = 0; i < cellCount; i++)
			{
				HexCell cell = grid.GetCell(i);

				// No farm centers underwater or on snow
				if (cell.IsUnderwater || cell.TerrainTypeIndex == 5)
				{
					continue;
				}

				float roadCount = cell.GetRoadCount();

				// Add farm center weight if road and small or no urban level
				if (roadCount > 1 && cell.UrbanLevel <= 1)
				{
					farmCenter.Add(cell);
					farmCenter.Add(cell);
				}

				// Add farm center weight based on number of neighbors with roads
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbor = cell.GetNeighbor(d);
					if (neighbor)
					{
						if (neighbor.HasRoads)
						{
							farmCenter.Add(cell);
						}
					}
				}

			}

			int farmBudget = Mathf.RoundToInt(landCells * farmPercentage * 0.01f);

			// Select farm center cells
			while (farmBudget > 0 && farmCenter.Count > 0)
			{
				int index = Random.Range(0, farmCenter.Count);
				int lastIndex = farmCenter.Count - 1;
				HexCell origin = farmCenter[index];
				farmCenter[index] = farmCenter[lastIndex];
				farmCenter.RemoveAt(lastIndex);

				// Check to create farm
				// If less than max farm level
				if (origin.FarmLevel < 3)
				{
					farmBudget -= CreateFarm(origin);
				}
			}
			if (farmBudget > 0)
			{
				Debug.LogWarning("Failed to use up farm budget.");
			}

			ListPool<HexCell>.Add(farmCenter);
		}

		private int CreateFarm(HexCell farmCenter)
		{
			int maxCounter = cellCount;
			int counter = 0;

			int size = 1;

			HexCell cell = farmCenter;
			HexDirection direction = HexDirection.NE;

			int farmStamp = Random.Range(
				minFarmStamp, maxFarmStamp);

			while (cell.IsUnderwater == false &&
				   size <= farmStamp &&
				   counter <= maxCounter)
			{
				featureLocations.Clear();

				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);

					if (!neighbor)
					{
						continue;
					}

					// If neighbor is underwater, exit
					if (neighbor.IsUnderwater == true)
					{
						continue;
					}

					// If neighbor is snow, exit
					if (neighbor.TerrainTypeIndex == 6)
                    {
						continue;
                    }

					// If neighbor has max farm, exit
					if (neighbor.FarmLevel == 3)
					{
						continue;
					}

					// Skip neighbor cell if it is steep uphill
					int delta = neighbor.Elevation - cell.Elevation;
					if (delta > 1)
					{
						continue;
					}

					// Weight farm if no urban
					if (neighbor.FarmLevel == 0)
					{
						featureLocations.Add(d);
					}
					
					// Weight farm if road
					if (neighbor.HasRoads == true)
                    {
						featureLocations.Add(d);
					}

					featureLocations.Add(d);
				}

				// If no place to place urban, exit
				if (featureLocations.Count == 0)
				{
					// If urban is only 1 long, return 0
					if (size == 1)
					{
						return 0;
					}

					break;
				}

				direction = featureLocations[Random.Range(0, featureLocations.Count)];

				// Increase size if farm level increased from 0
				if (cell.FarmLevel == 0)
				{
					size += 1;
				}
				cell.FarmLevel += 1;

				cell = cell.GetNeighbor(direction);
			}

			return size;
		}

		private void CreateUrbans()
		{
			List<HexCell> urbanCenters = ListPool<HexCell>.Get();

			// Determine a weight for each cell
			for (int i = 0; i < cellCount; i++)
			{
				HexCell cell = grid.GetCell(i);
				
				// No urban underwater
				if (cell.IsUnderwater || cell.TerrainTypeIndex == 5)
				{
					continue;
				}

				float roadCount = cell.GetRoadCount();

				if (roadCount > 5)
				{
					urbanCenters.Add(cell);
					urbanCenters.Add(cell);
				}
				if (roadCount > 3)
				{
					urbanCenters.Add(cell);
				}
				if (roadCount > 1)
				{
					urbanCenters.Add(cell);
				}
			}

			int urbanBudget = Mathf.RoundToInt(landCells * generatorData.urbanPercentage * 0.01f);

			// Select urban center cells
			while (urbanBudget > 0 && urbanCenters.Count > 0)
			{
				int index = Random.Range(0, urbanCenters.Count);
				int lastIndex = urbanCenters.Count - 1;
				HexCell origin = urbanCenters[index];
				urbanCenters[index] = urbanCenters[lastIndex];
				urbanCenters.RemoveAt(lastIndex);

				// Check to create urban
				// If less than max urban level
				if (origin.UrbanLevel < 3)
				{
					urbanBudget -= CreateUrban(origin);
				}
			}
			if (urbanBudget > 0)
			{
				Debug.LogWarning("Failed to use up urban budget.");
			}

			ListPool<HexCell>.Add(urbanCenters);
		}

		private int CreateUrban(HexCell urbanCenter)
		{
			int maxCounter = cellCount;
			int counter = 0;

			int size = 1;

			HexCell cell = urbanCenter;
			HexDirection direction = HexDirection.NE;

			int urbanStamp = Random.Range(
				generatorData.minUrbanStamp, generatorData.maxUrbanStamp + 1);

			while (cell.IsUnderwater == false &&
				   size <= urbanStamp &&
				   counter <= maxCounter)
			{
				featureLocations.Clear();

				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);

					if (!neighbor)
					{
						continue;
					}

					// If neighbor is underwater, exit
					if (neighbor.IsUnderwater == true)
					{
						continue;
					}

					// If neighbor has max urban, exit
					if (neighbor.UrbanLevel == 3)
					{
						continue;
					}

					// Skip neighbor cell if it is steep uphill
					int delta = neighbor.Elevation - cell.Elevation;
					if (delta > 1)
					{
						continue;
					}

					// Weight urban if on road
					if (neighbor.GetRoadCount() > 0)
                    {
						featureLocations.Add(d);
						featureLocations.Add(d);
					}

					featureLocations.Add(d);
				}

				// If no place to place urban, exit
				if (featureLocations.Count == 0)
				{
					// If urban is only 1 long, return 0
					if (size == 1)
					{
						return 0;
					}

					break;
				}

				direction = featureLocations[Random.Range(0, featureLocations.Count)];

				// Increase size if urban level increased from 0
				if (cell.UrbanLevel == 0)
				{
					size += 1;
				}
				cell.UrbanLevel += 1;

				cell = cell.GetNeighbor(direction);
			}

			return size;
		}

		private void CreateWalls()
        {
			// First wall pass based on urban and farm level
            for (int i = 0; i < cellCount; i++)
            {
				HexCell cell = grid.GetCell(i);

				// No underwater walls
				if (cell.IsUnderwater) { continue; }

				// Skip if already has a wall
				if (cell.Walled) { continue; }

				// Cache values
				int urbanLevel = cell.UrbanLevel;
				int farmLevel = cell.FarmLevel;
				
				// Place wall if urban level is high enough
				if (urbanLevel > 1)
                {
					CreateWall(cell);
					continue;
				}
				else if (urbanLevel == 1 && farmLevel > 1)
                {
					CreateWall(cell);
					continue;
                }

				// Chance for walls on single urbanlevel with neighboring urban levels
				bool addWall = false;
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);
					// Skip neighbor if it does not exist
					if (!neighbor) { continue; }

					if (urbanLevel > 1 || (urbanLevel == 1 && farmLevel > 1))
                    {
						addWall = true;
						break;
                    }
				}

				if (addWall && (urbanLevel > 0 || farmLevel > 0))
                {
					CreateWall(cell);
					continue;
                }
			}

			// Second wall pass
			List<HexCell> additionalCells = ListPool<HexCell>.Get();
			// Select additional cells for walls
			for (int i = 0; i < cellCount; i++)
            {
				HexCell cell = grid.GetCell(i);

				// No underwater walls
				if (cell.IsUnderwater) { continue; }

				// Skip if already has a wall
				if (cell.Walled) { continue; }


				// If two neighbors have walls, add a wall
				int neighborsWithWalls = 0;
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);
					// Skip neighbor if it does not exist
					if (!neighbor) { continue; }

					if (neighbor.Walled)
					{
						neighborsWithWalls += 1;
					}
				}

				if (neighborsWithWalls >= 3)
                {
					additionalCells.Add(cell);
                }
				else if (neighborsWithWalls == 2)
                {
					if (Random.value > 0.5f)
                    {
						additionalCells.Add(cell);
                    }
                }

			}

            // Add walls to each additionl cell
            for (int i = 0; i < additionalCells.Count; i++)
            {
				additionalCells[i].Walled = true;
            }

			ListPool<HexCell>.Add(additionalCells);
		}

		private void CreateWall(HexCell location)
        {
			// Decrease plant level if large in walled cells
			if (location.PlantLevel >= 2)
            {
				location.PlantLevel -= 1;
            }

			location.Walled = true;
		}

		private void CreateRivers()
        {
			List<HexCell> riverOrigins = ListPool<HexCell>.Get();

			// Determine a weight for each cell
			for (int i = 0; i < cellCount; i++)
			{
				HexCell cell = grid.GetCell(i);
				if (cell.IsUnderwater)
				{
					continue;
				}
				ClimateData currentClimateData = climate[i];
				float weight =
					currentClimateData.moisture * (cell.Elevation - generatorData.waterLevel) /
					(generatorData.elevationMaximum - generatorData.waterLevel);
				if (weight > 0.75f)
				{
					riverOrigins.Add(cell);
					riverOrigins.Add(cell);
				}
				if (weight > 0.5f)
				{
					riverOrigins.Add(cell);
				}
				if (weight > 0.25f)
				{
					riverOrigins.Add(cell);
				}
			}

			int riverBudget = Mathf.RoundToInt(landCells * generatorData.riverPercentage * 0.01f);

			// Select river origin cells
			while (riverBudget > 0 && riverOrigins.Count > 0)
			{
				int index = Random.Range(0, riverOrigins.Count);
				int lastIndex = riverOrigins.Count - 1;
				HexCell origin = riverOrigins[index];
				riverOrigins[index] = riverOrigins[lastIndex];
				riverOrigins.RemoveAt(lastIndex);

				// Check to create the river
				if (!origin.HasRiver)
                {
					bool isValidOrigin = true;

					for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
					{
						HexCell neighbor = origin.GetNeighbor(d);

						// Don't place river origin next to existing river or water
						if (neighbor && (neighbor.HasRiver || neighbor.IsUnderwater))
						{
							isValidOrigin = false;
							break;
						}
					}

					if (isValidOrigin)
					{
						riverBudget -= CreateRiver(origin);
					}
				}

			}

			if (riverBudget > 0)
			{
				Debug.LogWarning("Failed to use up river budget.");
			}


			ListPool<HexCell>.Add(riverOrigins);
		}

		private int CreateRiver(HexCell origin)
		{
			int length = 1;

			HexCell cell = origin;
			HexDirection direction = HexDirection.NE;

			while (!cell.IsUnderwater)
            {
				int minNeighborElevation = int.MaxValue;
				flowDirections.Clear();
				
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbor = cell.GetNeighbor(d);

					if (!neighbor)
					{
						continue;
					}

					// Set min neighbor elevation which helps place lakes
					if (neighbor.Elevation < minNeighborElevation)
					{
						minNeighborElevation = neighbor.Elevation;
					}

					// If neighbor already has river, exit
					if (neighbor == origin || neighbor.HasIncomingRiver)
					{
						continue;
					}

					// Skip neighbor cell if it is uphill
					int delta = neighbor.Elevation - cell.Elevation;
					if (delta > 0)
                    {
						continue;
                    }

					// If the neighbor has and outgoing river but no incoming river,
					// then it is an origin point, merge the rivers and return
					if (neighbor.HasOutgoingRiver)
					{
						cell.SetOutgoingRiver(d);
						return length;
					}

					// If downhill 'weight' the cell list with the neighbor tile
					if (delta < 0)
					{
						flowDirections.Add(d);
						flowDirections.Add(d);
						flowDirections.Add(d);
					}

					// If not multiple sharp turns in a row,
					// 'weight' the cell list with the neighbor tile
					if (length == 1 ||
						(d != direction.Next2() && d != direction.Previous2()))
                    {
						flowDirections.Add(d);
                    }

					flowDirections.Add(d);
				}

				// If no place to flow to, make lake or exit
				if (flowDirections.Count == 0)
				{
					// If river is only 1 long, return 0
					if (length == 1)
					{
						return 0;
					}

					if (minNeighborElevation >= cell.Elevation)
					{
						cell.WaterLevel = minNeighborElevation;
						if (minNeighborElevation == cell.Elevation)
						{
							cell.Elevation = minNeighborElevation - 1;
						}
					}
					break;

				}

				direction = flowDirections[Random.Range(0, flowDirections.Count)];
				
				cell.SetOutgoingRiver(direction);
				length += 1;

				// Create extra lakes
				if (minNeighborElevation >= cell.Elevation &&
					Random.value < generatorData.extraLakeProbability)
                {
					cell.WaterLevel = cell.Elevation;
					cell.Elevation -= 1;
                }

				cell = cell.GetNeighbor(direction);
            }


			return length;
		}

		private float DetermineTemperature(HexCell cell)
        {
			float latitude = (float)cell.coordinates.Z / grid.cellCountZ;

			if (hemisphere == HemisphereMode.Both)
			{
				latitude *= 2f;
				if (latitude > 1f)
				{
					latitude = 2f - latitude;
				}
			}
			else if (hemisphere == HemisphereMode.North)
			{
				latitude = 1f - latitude;
			}

			float temperature = Mathf.LerpUnclamped(
				generatorData.lowTemperature,
				generatorData.highTemperature,
				latitude);

			// Scale temperature by elevation
			temperature *= 1f - (cell.ViewElevation - generatorData.waterLevel) /
					(generatorData.elevationMaximum - generatorData.waterLevel + 1f);

			// Add noise to the temperature
			float jitter = 
				HexMetrics.SampleNoise(cell.Position * 0.1f)[temperatureJitterChannel];
			temperature += (jitter * 2f - 1f) * generatorData.temperatureJitter;

			return temperature;

        }
	}
}