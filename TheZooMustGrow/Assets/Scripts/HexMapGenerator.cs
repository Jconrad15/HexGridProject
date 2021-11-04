using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
	public class HexMapGenerator : MonoBehaviour
	{

		public HexGrid grid;


		public void GenerateMap(int x, int z)
		{
			// Generate base map
			grid.CreateMap(x, z);

            for (int i = 0; i < z; i++)
            {
				grid.GetCell(x / 2, i).TerrainTypeIndex = 1;
            }


		}



	}
}