using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TheZooMustGrow
{
    public class GenerationDataMenu : MonoBehaviour
    {
        public HexMapGeneratorData data = new HexMapGeneratorData();

        private void OnEnable()
        {
            data.SetDefaults();
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

		public void SetJitterProbability(float jitterProbability)
        {
			data.jitterProbability = jitterProbability;
        }

		public void SetChunkSizeMin(Single chunkSizeMin)
		{
			data.chunkSizeMin = (int)chunkSizeMin;
		}

		public void SetChunkSizeMax(Single chunkSizeMax)
		{
			data.chunkSizeMax = (int)chunkSizeMax;
		}

		public void SetLandPercentage(Single landPercentage)
		{
			data.landPercentage = (int)landPercentage;
		}

		public void SetWaterLevel(Single waterLevel)
		{
			data.waterLevel = (int)waterLevel;
		}

		public void SetHighRiseProbability(float highRiseProbability)
		{
			data.highRiseProbability = highRiseProbability;
		}

		public void SetSinkProbability(float sinkProbability)
		{
			data.sinkProbability = sinkProbability;
		}

		public void SetElevationMinimum(Single elevationMinimum)
		{
			data.elevationMinimum = (int)elevationMinimum;
		}

		public void SetElevationMaximum(Single elevationMaximum)
		{
			data.elevationMaximum = (int)elevationMaximum;
		}

		public void SetMapBorderX(Single mapBorderX)
		{
			data.mapBorderX = (int)mapBorderX;
		}

		public void SetMapBorderZ(Single mapBorderZ)
		{
			data.mapBorderZ = (int)mapBorderZ;
		}

		public void SetRegionBorder(Single regionBorder)
		{
			data.regionBorder = (int)regionBorder;
		}

		public void SetRegionCount(Single regionCount)
		{
			data.regionCount = (int)regionCount;
		}

		public void SetErosionPercentage(Single erosionPercentage)
		{
			data.erosionPercentage = (int)erosionPercentage;
		}

		public void SetStartingMoisture(float startingMoisture)
		{
			data.startingMoisture = startingMoisture;
		}

		public void SetEvaporationFactor(float evaporationFactor)
		{
			data.evaporationFactor = evaporationFactor;
		}

		public void SetPrecipitationFactor(float precipitationFactor)
		{
			data.precipitationFactor = precipitationFactor;
		}

		public void SetRunoffFactor(float runoffFactor)
		{
			data.runoffFactor = runoffFactor;
		}

		public void SetSeepageFactor(float seepageFactor)
		{
			data.seepageFactor = seepageFactor;
		}

		public void SetWindFromDirection(Single windFromDirection)
        {
			data.windFromDirection = (HexDirection)windFromDirection;
        }

		public void SetWindStrength(float windStrength)
		{
			data.windStrength = windStrength;
		}

		public void SetRiverPercentage(Single riverPercentage)
		{
			data.riverPercentage = (int)riverPercentage;
		}

		public void SetExtraLakeProbability(float extraLakeProbability)
		{
			data.extraLakeProbability = extraLakeProbability;
		}

		public void SetLowTemperature(float lowTemperature)
		{
			data.lowTemperature = lowTemperature;
		}

		public void SetHighTemperature(float highTemperature)
		{
			data.highTemperature = highTemperature;
		}


	}
}