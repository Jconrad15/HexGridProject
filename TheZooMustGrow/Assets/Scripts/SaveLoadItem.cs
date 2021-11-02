using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheZooMustGrow
{
	public class SaveLoadItem : MonoBehaviour
	{

		public SaveLoadMenu menu;

		public string MapName
		{
			get
			{
				return mapName;
			}
			set
			{
				mapName = value;
				transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText(value);
			}
		}

		string mapName;

		public void Select()
		{
			menu.SelectItem(mapName);
		}
	}
}