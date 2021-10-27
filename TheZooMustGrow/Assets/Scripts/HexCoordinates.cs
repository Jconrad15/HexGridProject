using UnityEngine;

namespace TheZooMustGrow
{
    [System.Serializable]
    public struct HexCoordinates
    {
        public int X { get; private set; }
        public int Z { get; private set; }
        public int Y
        {
            get
            {
                return -X - Z;
            }
        }


        public HexCoordinates(int x, int z)
        {
            X = x;
            Z = z;
        }
        

        public static HexCoordinates FromOffsetCoordinates (int x, int z)
        {
            return new HexCoordinates(x - z / 2, z);
        }


        public override string ToString()
        {
            return "(" + X.ToString() + ", " + Y.ToString() + ", " + Z.ToString() + ")";
        }

        public string ToStringOnSeparateLines()
        {
            return X.ToString() + "\n" + Y.ToString() + "\n" + Z.ToString();
        }
    }
}