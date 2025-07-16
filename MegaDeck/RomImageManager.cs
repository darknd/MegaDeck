using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MegaDeck
{
    public static class RomImageManager
    {
        private static readonly string MapPath = "rom_image_map.json";
        private static Dictionary<string, string> _map;

        static RomImageManager()
        {
            Load();
        }

        private static void Load()
        {
            if (File.Exists(MapPath))
            {
                var json = File.ReadAllText(MapPath);
                _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                _map = new Dictionary<string, string>();
            }
        }

        public static void Save()
        {
            var json = JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(MapPath, json);
        }

        public static void SetImage(string cueFileName, string imageFileName)
        {
            _map[cueFileName] = imageFileName;
            Save();
        }

        public static string? GetImage(string cueFileName)
        {
            if (_map.TryGetValue(cueFileName, out var image))
                return image;

            return null;
        }

        public static void RemoveImage(string cueFileName)
        {
            if (_map.ContainsKey(cueFileName))
            {
                _map.Remove(cueFileName);
                Save();
            }
        }

    }
}
