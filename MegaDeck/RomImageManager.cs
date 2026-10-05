using System.Text.Json;

namespace MegaDeck
{
    /// <summary>Mapa nombre de ROM (.cue/.chd) → archivo de portada dentro de images/.</summary>
    public static class RomImageManager
    {
        private static Dictionary<string, string> _map = Load();
        private static readonly object Lock = new();

        private static Dictionary<string, string> Load()
        {
            if (File.Exists(AppPaths.ImageMapFile))
            {
                try
                {
                    var json = File.ReadAllText(AppPaths.ImageMapFile);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
                catch (JsonException)
                {
                }
            }
            return new Dictionary<string, string>();
        }

        private static void Save()
        {
            var json = JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.ImageMapFile, json);
        }

        /// <summary>
        /// Copia la imagen a images/ con el nombre de la ROM (evita choques entre
        /// portadas que se llamen igual, p. ej. "cover.jpg") y la asigna.
        /// </summary>
        public static void AssignCover(string romFileName, string sourceImagePath)
        {
            Directory.CreateDirectory(AppPaths.ImagesDir);
            string imageFileName = Path.GetFileNameWithoutExtension(romFileName) + Path.GetExtension(sourceImagePath).ToLowerInvariant();
            File.Copy(sourceImagePath, Path.Combine(AppPaths.ImagesDir, imageFileName), true);
            SetImage(romFileName, imageFileName);
        }

        /// <summary>Guarda una carátula descargada (JPEG) como images/&lt;nombre de la ROM&gt;.jpg y la asigna.</summary>
        public static void AssignCover(string romFileName, byte[] jpeg)
        {
            Directory.CreateDirectory(AppPaths.ImagesDir);
            string imageFileName = Path.GetFileNameWithoutExtension(romFileName) + ".jpg";
            File.WriteAllBytes(Path.Combine(AppPaths.ImagesDir, imageFileName), jpeg);
            SetImage(romFileName, imageFileName);
        }

        private static void SetImage(string romFileName, string imageFileName)
        {
            lock (Lock)
            {
                _map[romFileName] = imageFileName;
                Save();
            }
        }

        /// <summary>Ruta absoluta de la portada, o null si no hay o el archivo ya no existe.</summary>
        public static string? GetImagePath(string romFileName)
        {
            string? image;
            lock (Lock)
            {
                if (!_map.TryGetValue(romFileName, out image))
                    return null;
            }

            string path = Path.Combine(AppPaths.ImagesDir, image);
            return File.Exists(path) ? path : null;
        }

        public static void RemoveImage(string romFileName)
        {
            lock (Lock)
            {
                if (_map.Remove(romFileName))
                    Save();
            }
        }
    }
}
