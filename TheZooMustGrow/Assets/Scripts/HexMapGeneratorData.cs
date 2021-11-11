using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public struct HexMapGeneratorData
    {

		public float jitterProbability;

		public int chunkSizeMin;

		public int chunkSizeMax;

		public int landPercentage;

		public int waterLevel;

		public float highRiseProbability;

		public float sinkProbability;

		public int elevationMinimum;

		public int elevationMaximum;

		public int mapBorderX;

		public int mapBorderZ;

		public int regionBorder;

		public int regionCount;

		public int erosionPercentage;

		public float startingMoisture;

		public float evaporationFactor;

		public float precipitationFactor;

		public float runoffFactor;

		public float seepageFactor;

		public HexDirection windFromDirection;

		public float windStrength;

		public int riverPercentage;

		public float extraLakeProbability;

		public float lowTemperature;

		public float highTemperature;

		public float temperatureJitter;

		public int roadPercentage;

		public int maxRoadLength;

		public int minRoadLength;

		public int urbanPercentage;

		public int minUrbanStamp;

		public int maxUrbanStamp;

		public void SetDefaults()
        {
			jitterProbability = 0.25f;

			chunkSizeMin = 30;

			chunkSizeMax = 100;

			landPercentage = 50;

			waterLevel = 3;

			highRiseProbability = 0.25f;

			sinkProbability = 0.2f;

			elevationMinimum = -2;

			elevationMaximum = 8;

			mapBorderX = 5;

			mapBorderZ = 5;

			regionBorder = 5;

			regionCount = 1;

			erosionPercentage = 50;

			startingMoisture = 0.1f;

			evaporationFactor = 0.5f;

			precipitationFactor = 0.25f;

			runoffFactor = 0.25f;

			seepageFactor = 0.125f;

			windFromDirection = HexDirection.NW;
			windStrength = 4f;

			riverPercentage = 10;

			extraLakeProbability = 0.25f;

			lowTemperature = 0f;

			highTemperature = 1f;

			temperatureJitter = 0.1f;

			roadPercentage = 10;

			maxRoadLength = 20;

			minRoadLength = 6;

			urbanPercentage = 5;

			minUrbanStamp = 4;

			maxUrbanStamp = 8;
		}

	}
}