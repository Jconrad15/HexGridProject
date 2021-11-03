using UnityEngine;
using TMPro;
using System;
using System.IO;

namespace TheZooMustGrow
{
	public class SaveLoadMenu : MonoBehaviour
	{
		public TextMeshProUGUI menuLabel, actionButtonLabel;

		public HexGrid hexGrid;

		bool saveMode;

		public TMP_InputField nameInput;

		public RectTransform listContent;
		public SaveLoadItem itemPrefab;

		public void Open(bool saveMode)
		{
			if (saveMode)
            {
				menuLabel.SetText("Save Map");
				actionButtonLabel.SetText("Save");
            }
            else
            {
				menuLabel.SetText("Load Map");
				actionButtonLabel.SetText("Load");
            }

			this.saveMode = saveMode;
			FillList();
			gameObject.SetActive(true);
			HexMapCamera.Locked = true;
		}

		public void Close()
		{
			gameObject.SetActive(false);
			HexMapCamera.Locked = false;
		}

		public void Action()
		{
			string path = GetSelectedPath();
			if (path == null)
			{
				return;
			}
			if (saveMode)
			{
				Save(path);
			}
			else
			{
				Load(path);
			}
			Close();
		}

		public void Delete()
        {
			string path = GetSelectedPath();
			if (path == null) { return; }

			if (File.Exists(path))
			{
				File.Delete(path);
			}
			nameInput.text = "";
			FillList();
		}

		private string GetSelectedPath()
		{
			string mapName = nameInput.text;

			if (mapName.Length == 0) { return null; }

			return Path.Combine(Application.persistentDataPath, mapName + ".map");
		}

        private void Save(string path)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                // Create a header to indicate a version
                int header = 2;
                writer.Write(header);
                hexGrid.Save(writer);
            }
        }

        private void Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("File does not exist " + path);
                return;
            }

            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                // Read in the header
                int header = reader.ReadInt32();
                if (header <= 2)
                {
                    hexGrid.Load(reader, header);
                    HexMapCamera.ValidatePosition();
                }
                else
                {
                    Debug.LogWarning("Unknown map format " + header);
                }
            }
        }

		public void SelectItem(string name)
        {
			nameInput.text = name;
        }

		private void FillList()
		{
			// Remove old items
			for (int i = 0; i < listContent.childCount; i++)
			{
				Destroy(listContent.GetChild(i).gameObject);
			}

			string[] paths = Directory.GetFiles(Application.persistentDataPath, "*.map");

			// Sort to alphabetical order
			Array.Sort(paths);

			// Create item prefabs for each saved map
			for (int i = 0; i < paths.Length; i++)
			{
				SaveLoadItem item = Instantiate(itemPrefab);
				item.menu = this;
				item.MapName = Path.GetFileNameWithoutExtension(paths[i]);
				item.transform.SetParent(listContent, false);
			}


		}

	}
}