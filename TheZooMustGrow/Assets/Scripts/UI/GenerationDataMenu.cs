using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TheZooMustGrow
{
    public class GenerationDataMenu : MonoBehaviour
    {
        public HexMapGeneratorData generationData = new HexMapGeneratorData();

		[SerializeField]
		private HexMapGenerator hexMapGenerator;

        private void OnEnable()
        {
            generationData.SetDefaults();
        }

        public void Open()
		{
			gameObject.SetActive(true);
			HexMapCamera.Locked = true;
		}

		public void Close()
		{
			gameObject.SetActive(false);
			HexMapCamera.Locked = false;
		}

		private void ProvideGenerationData()
        {
			hexMapGenerator.generatorData = generationData;
        }

		public void SetJitterProbability(float jitterProbability)
        {
			generationData.jitterProbability = jitterProbability;
			ProvideGenerationData();
        }

		public void SetChunkSizeMin(Single chunkSizeMin)
		{
			generationData.chunkSizeMin = (int)chunkSizeMin;
			ProvideGenerationData();
		}

		public void SetChunkSizeMax(Single chunkSizeMax)
		{
			generationData.chunkSizeMax = (int)chunkSizeMax;
			ProvideGenerationData();
		}

		public void SetLandPercentage(Single landPercentage)
		{
			generationData.landPercentage = (int)landPercentage;
			ProvideGenerationData();
		}

		public void SetWaterLevel(Single waterLevel)
		{
			generationData.waterLevel = (int)waterLevel;
		    ProvideGenerationData();
        }

		public void SetHighRiseProbability(float highRiseProbability)
		{
			generationData.highRiseProbability = highRiseProbability;
		    ProvideGenerationData();
        }

		public void SetSinkProbability(float sinkProbability)
		{
			generationData.sinkProbability = sinkProbability;
		    ProvideGenerationData();
        }

		public void SetElevationMinimum(Single elevationMinimum)
		{
			generationData.elevationMinimum = (int)elevationMinimum;
		    ProvideGenerationData();
        }

		public void SetElevationMaximum(Single elevationMaximum)
		{
			generationData.elevationMaximum = (int)elevationMaximum;
		    ProvideGenerationData();
        }

		public void SetMapBorderX(Single mapBorderX)
		{
			generationData.mapBorderX = (int)mapBorderX;
		    ProvideGenerationData();
        }

		public void SetMapBorderZ(Single mapBorderZ)
		{
			generationData.mapBorderZ = (int)mapBorderZ;
		    ProvideGenerationData();
        }

		public void SetRegionBorder(Single regionBorder)
		{
			generationData.regionBorder = (int)regionBorder;
		    ProvideGenerationData();
        }

		public void SetRegionCount(Single regionCount)
		{
			generationData.regionCount = (int)regionCount;
		    ProvideGenerationData();
        }

		public void SetErosionPercentage(Single erosionPercentage)
		{
			generationData.erosionPercentage = (int)erosionPercentage;
		    ProvideGenerationData();
        }

		public void SetStartingMoisture(float startingMoisture)
		{
			generationData.startingMoisture = startingMoisture;
		    ProvideGenerationData();
        }

		public void SetEvaporationFactor(float evaporationFactor)
		{
			generationData.evaporationFactor = evaporationFactor;
		    ProvideGenerationData();
        }

		public void SetPrecipitationFactor(float precipitationFactor)
		{
			generationData.precipitationFactor = precipitationFactor;
		    ProvideGenerationData();
        }

		public void SetRunoffFactor(float runoffFactor)
		{
			generationData.runoffFactor = runoffFactor;
		    ProvideGenerationData();
        }

		public void SetSeepageFactor(float seepageFactor)
		{
			generationData.seepageFactor = seepageFactor;
		    ProvideGenerationData();
        }

		public void SetWindFromDirection(Single windFromDirection)
        {
			generationData.windFromDirection = (HexDirection)windFromDirection;
            ProvideGenerationData();
        }

		public void SetWindStrength(float windStrength)
		{
			generationData.windStrength = windStrength;
		    ProvideGenerationData();
        }

		public void SetRiverPercentage(Single riverPercentage)
		{
			generationData.riverPercentage = (int)riverPercentage;
		    ProvideGenerationData();
        }

		public void SetExtraLakeProbability(float extraLakeProbability)
		{
			generationData.extraLakeProbability = extraLakeProbability;
		    ProvideGenerationData();
        }

		public void SetLowTemperature(float lowTemperature)
		{
			generationData.lowTemperature = lowTemperature;
		    ProvideGenerationData();
        }

		public void SetHighTemperature(float highTemperature)
		{
			generationData.highTemperature = highTemperature;
		    ProvideGenerationData();
        }

		public void SetTemperatureJitter(float temperatureJitter)
        {
			generationData.temperatureJitter = temperatureJitter;
			ProvideGenerationData();
        }

		public void SetRoadPercentage(Single roadPercentage)
		{
			generationData.roadPercentage = (int)roadPercentage;
			ProvideGenerationData();
		}

		public void SetMaxRoadLength(Single maxRoadLength)
		{
			generationData.maxRoadLength = (int)maxRoadLength;
			ProvideGenerationData();
		}

		public void SetMinRoadLength(Single minRoadLength)
		{
			generationData.minRoadLength = (int)minRoadLength;
			ProvideGenerationData();
		}

		public void SetUrbanPercentage(Single urbanPercentage)
		{
			generationData.urbanPercentage = (int)urbanPercentage;
			ProvideGenerationData();
		}

		public void SetMinUrbanStamp(Single minUrbanStamp)
		{
			generationData.minUrbanStamp= (int)minUrbanStamp;
			ProvideGenerationData();
		}

		public void SetMaxUrbanStamp(Single maxUrbanStamp)
		{
			generationData.maxUrbanStamp = (int)maxUrbanStamp;
			ProvideGenerationData();
		}
	}
}