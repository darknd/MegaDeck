using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HtmlAgilityPack;


namespace MegaDeck
{
    public partial class LibraryPage : Page
    {
        private Border _hoveredItem = null;
        private readonly string _cachePath = "rom_title_cache.json";
        private Dictionary<string, string> _romTitleCache = new Dictionary<string, string>();

        public LibraryPage()
        {
            InitializeComponent();
            LoadCache();
            LoadRoms();
        }

        private void LoadCache()
        {
            if (File.Exists(_cachePath))
            {
                try
                {
                    string json = File.ReadAllText(_cachePath);
                    _romTitleCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                                     ?? new Dictionary<string, string>();
                }
                catch
                {
                    _romTitleCache = new Dictionary<string, string>();
                }
            }
        }

        private void SaveCache()
        {
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(_romTitleCache, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        private void OnMouseEnterGame(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                _hoveredItem = border;
                border.BorderBrush = Brushes.DeepSkyBlue;
            }
        }

        private void OnMouseLeaveGame(object sender, MouseEventArgs e)
        {
            if (sender is Border border && _hoveredItem == border)
            {
                border.BorderBrush = Brushes.Transparent;
                _hoveredItem = null;
            }
        }

        private void OnGameClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.Tag is string cuePath)
            {
                LaunchGame(cuePath);
            }
        }

        private void LaunchGame(string cueFilePath)
        {
            string fusionPath = @"C:\Users\fjafo\Documents\SynologyDrive\MegaDeck\MegaDeck\engine\Fusion.exe";

            if (!File.Exists(fusionPath))
            {
                MessageBox.Show("No se encontró Fusion en la carpeta 'engine'.");
                return;
            }

            if (!File.Exists(cueFilePath))
            {
                MessageBox.Show("No se encontró el archivo .cue del juego.");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fusionPath,
                    Arguments = $"\"{cueFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(fusionPath)
                }
            };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al lanzar el juego:\n{ex.Message}");
            }
        }

        private async void LoadRoms()
        {
            string romFolder = @"C:\roms";

            if (!Directory.Exists(romFolder))
                Directory.CreateDirectory(romFolder);

            var cueFiles = Directory.GetFiles(romFolder, "*.cue", SearchOption.AllDirectories);
            var gameList = new List<GameInfo>();
            bool updated = false;

            foreach (var cue in cueFiles)
            {
                string fileName = Path.GetFileName(cue);
                string title;

                if (_romTitleCache.TryGetValue(fileName, out string cachedTitle))
                {
                    title = cachedTitle;
                }
                else
                {
                    title = ExtractGameTitle(cue); // O puedes mejorar esto con búsqueda online si quieres
                    _romTitleCache[fileName] = title;
                    updated = true;
                }

                gameList.Add(new GameInfo
                {
                    Title = title,
                    CoverUrl =
                        "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ac/No_image_available.svg/480px-No_image_available.svg.png",
                    CuePath = cue
                });
            }

            if (updated)
                SaveCache();

            RomList.ItemsSource = gameList;
        }

        private string ExtractGameTitle(string cueFilePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(cueFilePath);

            // Sustituye guiones y underscores por espacio
            fileName = fileName.Replace("_", " ").Replace("-", " ");

            // Elimina paréntesis o corchetes con su contenido
            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[\[\(].*?[\]\)]", "");

            // Elimina "Track XX"
            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"Track\s?\d+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Elimina múltiples espacios
            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"\s+", " ");

            // Recorta espacios al inicio y final
            fileName = fileName.Trim();

            // Capitaliza estilo "Title Case"
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            fileName = culture.TextInfo.ToTitleCase(fileName.ToLower());

            // Repara excepciones comunes (opcional)
            fileName = fileName.Replace(" Ii", " II")
                .Replace(" Iii", " III")
                .Replace(" Iv", " IV")
                .Replace(" Usa", " USA");

            return fileName;
        }

        public class GameInfo
        {
            public string Title { get; set; }
            public string CoverUrl { get; set; }
            public string CuePath { get; set; }
        }
    }
}