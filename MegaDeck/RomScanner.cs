using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaDeck.Models;

namespace MegaDeck
{
    /// <summary>Busca .cue/.chd/.zip en las carpetas configuradas y genera títulos legibles (con caché).</summary>
    public class RomScanner
    {
        private static readonly string[] RomExtensions = [".cue", ".chd", ".zip"];

        private Dictionary<string, string> _titleCache = new();

        public Dictionary<string, List<GameInfo>> ScanAll()
        {
            LoadCache();
            var config = ConfigManager.LoadConfig();

            bool updated = false;
            var result = new Dictionary<string, List<GameInfo>>();
            foreach (var system in GameSystem.All)
                result[system.Id] = Scan(config.GetRomsDirectory(system.Id), system, ref updated);

            if (updated)
                SaveCache();

            return result;
        }

        private List<GameInfo> Scan(string romFolder, GameSystem system, ref bool cacheUpdated)
        {
            var games = new List<GameInfo>();
            if (string.IsNullOrWhiteSpace(romFolder) || !Directory.Exists(romFolder))
                return games;

            // Se compara la extensión sin distinguir mayúsculas: en Linux "*.cue" no encontraría "GAME.CUE".
            var romFiles = Directory.EnumerateFiles(romFolder, "*", SearchOption.AllDirectories)
                .Where(f => RomExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Where(f => !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || ZipGameExtractor.ContainsGame(f));

            foreach (var rom in romFiles)
            {
                string fileName = Path.GetFileName(rom);

                if (!_titleCache.TryGetValue(fileName, out var title))
                {
                    title = ExtractGameTitle(rom);
                    _titleCache[fileName] = title;
                    cacheUpdated = true;
                }

                games.Add(new GameInfo
                {
                    Title = title,
                    RomPath = rom,
                    System = system,
                    CoverPath = RomImageManager.GetImagePath(fileName)
                });
            }

            games.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
            return games;
        }

        private void LoadCache()
        {
            _titleCache = new Dictionary<string, string>();
            if (!File.Exists(AppPaths.TitleCacheFile))
                return;

            try
            {
                string json = File.ReadAllText(AppPaths.TitleCacheFile);
                _titleCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch (JsonException)
            {
            }
        }

        private void SaveCache()
        {
            File.WriteAllText(AppPaths.TitleCacheFile, JsonSerializer.Serialize(_titleCache, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        private static string ExtractGameTitle(string romFilePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(romFilePath);

            fileName = fileName.Replace("_", " ").Replace("-", " ");
            fileName = Regex.Replace(fileName, @"[\[\(].*?[\]\)]", "");
            fileName = Regex.Replace(fileName, @"Track\s?\d+", "", RegexOptions.IgnoreCase);
            fileName = Regex.Replace(fileName, @"\s+", " ");
            fileName = fileName.Trim();

            fileName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fileName.ToLower());

            fileName = fileName.Replace(" Ii", " II")
                .Replace(" Iii", " III")
                .Replace(" Iv", " IV")
                .Replace(" Usa", " USA");

            return fileName;
        }
    }
}
