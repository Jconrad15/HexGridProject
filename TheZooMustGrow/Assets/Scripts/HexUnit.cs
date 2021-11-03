using UnityEngine;

namespace TheZooMustGrow
{
    public class HexUnit : MonoBehaviour
    {

        HexCell location;
		public HexCell Location
		{
			get
			{
				return location;
			}
			set
			{
				location = value;
				value.Unit = this;
				transform.localPosition = value.Position;
			}
		}

		float orientation;
		public float Orientation
		{
			get
			{
				return orientation;
			}
			set
			{
				orientation = value;
				transform.localRotation = Quaternion.Euler(0f, value, 0f);
			}
		}

		/// <summary>
		/// Updates the position of the unit.
		/// </summary>
		public void ValidateLocation()
		{
			transform.localPosition = location.Position;
		}

		/// <summary>
		/// Destroy the unit and remove properties.
		/// </summary>
		public void Die()
        {
			location.Unit = null;
			Destroy(gameObject);
        }
	}
}