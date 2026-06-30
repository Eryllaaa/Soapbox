using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Soapbox.Builder.SaveSystem
{
    /// <summary>
    /// Reads and writes <see cref="VehicleSaveData"/> as JSON files under
    /// <c>Application.persistentDataPath/Vehicles</c>.
    /// </summary>
    public static class VehicleSaveSystem
    {
        private const string FolderName = "Vehicles";
        private const string Extension = ".json";

        private static string Folder
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, FolderName);
                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>Full path for a vehicle save with the given name.</summary>
        public static string PathFor(string vehicleName) =>
            Path.Combine(Folder, Sanitize(vehicleName) + Extension);

        /// <summary>Writes the data to disk under its (sanitised) name.</summary>
        public static void Save(VehicleSaveData data, string vehicleName)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(PathFor(vehicleName), json);
        }

        /// <summary>Loads a vehicle by name, or null if it does not exist.</summary>
        public static VehicleSaveData Load(string vehicleName)
        {
            string path = PathFor(vehicleName);
            if (!File.Exists(path)) return null;

            return JsonUtility.FromJson<VehicleSaveData>(File.ReadAllText(path));
        }

        /// <summary>Lists the names (without extension) of all saved vehicles.</summary>
        public static IReadOnlyList<string> ListSaves()
        {
            var names = new List<string>();
            foreach (string file in Directory.GetFiles(Folder, "*" + Extension))
                names.Add(Path.GetFileNameWithoutExtension(file));
            return names;
        }

        /// <summary>Deletes a saved vehicle if it exists.</summary>
        public static void Delete(string vehicleName)
        {
            string path = PathFor(vehicleName);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unnamed";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
