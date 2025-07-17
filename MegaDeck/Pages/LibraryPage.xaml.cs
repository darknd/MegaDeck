using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using MessageBox = System.Windows.Forms.MessageBox;
using Brushes = System.Windows.Media.Brushes;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

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
            Refresh();
        }

        public void Refresh()
        {
            LoadCache();
            LoadRoms("segacd");
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
            if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is GameInfo game)
            {
                LaunchGame(game);
            }
        }

        private void LaunchGame(GameInfo game)
        {
            if (game == null || string.IsNullOrEmpty(game.CuePath))
            {
                MessageBox.Show("Game data is invalid.");
                return;
            }

            // Resolver ruta absoluta del core
            string corePath = game.System switch
            {
                "segacd" => Path.GetFullPath(@".\engine\cores\genesis_plus_gx_libretro.dll"),
                "saturn" => Path.GetFullPath(@".\engine\cores\mednafen_saturn_libretro.dll"),
                _ => null
            };

            if (corePath == null || !File.Exists(corePath))
            {
                MessageBox.Show("Core not found.");
                return;
            }

            // Ruta absoluta del ejecutable RetroArch
            string retroarchPath = Path.GetFullPath(@".\engine\retroarch.exe");

            if (!File.Exists(retroarchPath))
            {
                MessageBox.Show("RetroArch executable not found.");
                return;
            }

            if (!File.Exists(game.CuePath))
            {
                MessageBox.Show("Game file not found.");
                return;
            }

            // Construir argumentos completos
            string args = $"-c \"retroarch.cfg\" -L \"{corePath}\" \"{game.CuePath}\" -f";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = retroarchPath,
                    Arguments = args,
                    UseShellExecute = true, // necesario para rutas absolutas
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(retroarchPath)
                }
            };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching the game:\n{ex.Message}");
            }
        }



        private void OnAssignCoverClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is FrameworkElement fe &&
                fe.Tag is string cuePath)
            {
                string cueName = Path.GetFileName(cuePath);

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.png;*.jpg)|*.png;*.jpg",
                    Title = "Select cover"
                };

                if (dialog.ShowDialog() == true)
                {
                    Directory.CreateDirectory("images");
                    string destFile = Path.Combine("images", Path.GetFileName(dialog.FileName));
                    File.Copy(dialog.FileName, destFile, true);

                    RomImageManager.SetImage(cueName, Path.GetFileName(dialog.FileName));
                    MessageBox.Show("✔ Custom cover assigned.");
                    Refresh();
                }
            }
        }

        private void OnRightClickGame(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string cuePath)
            {
                string cueName = Path.GetFileName(cuePath);

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.png;*.jpg)|*.png;*.jpg",
                    Title = "Select a cover"
                };

                if (dialog.ShowDialog() == true)
                {
                    Directory.CreateDirectory("images");
                    string destFile = Path.Combine("images", Path.GetFileName(dialog.FileName));
                    File.Copy(dialog.FileName, destFile, true);

                    RomImageManager.SetImage(cueName, Path.GetFileName(dialog.FileName));

                    MessageBox.Show("✔ Custom cover assigned.");
                    Refresh(); // Recargar la lista con la nueva imagen
                }
            }
        }


        private void OnRemoveCoverClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is FrameworkElement fe &&
                fe.Tag is string cuePath)
            {
                string cueName = Path.GetFileName(cuePath);

                RomImageManager.RemoveImage(cueName);
                MessageBox.Show("🗑 Custom cover removed.");
                Refresh();
            }
        }

        private List<GameInfo> LoadGamesFrom(string romFolder, string system)
        {
            var gameList = new List<GameInfo>();
            if (string.IsNullOrWhiteSpace(romFolder) || !Directory.Exists(romFolder))
                return gameList;

            var romFiles = Directory.GetFiles(romFolder, "*.cue", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(romFolder, "*.chd", SearchOption.AllDirectories));

            bool updated = false;

            foreach (var cue in romFiles)
            {
                string fileName = Path.GetFileName(cue);
                string title;

                if (_romTitleCache.TryGetValue(fileName, out string cachedTitle))
                {
                    title = cachedTitle;
                }
                else
                {
                    title = ExtractGameTitle(cue);
                    _romTitleCache[fileName] = title;
                    updated = true;
                }

                string imageName = RomImageManager.GetImage(fileName);
                string imagePath = !string.IsNullOrEmpty(imageName) && File.Exists($"images/{imageName}")
                    ? Path.GetFullPath($"images/{imageName}")
                    : "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ac/No_image_available.svg/480px-No_image_available.svg.png";

                gameList.Add(new GameInfo
                {
                    Title = title,
                    CoverUrl = imagePath,
                    CuePath = cue,
                    System = system
                });
            }

            if (updated)
                SaveCache();

            return gameList;
        }

        private void LoadRoms(string filter)
        {
            var config = ConfigManager.LoadConfig();
            var allGames = new List<GameInfo>();

            if (filter == "segacd")
                allGames.AddRange(LoadGamesFrom(config.RomsDirectory_SegaCD, "segacd"));
            else if (filter == "saturn")
                allGames.AddRange(LoadGamesFrom(config.RomsDirectory_Saturn, "saturn"));

            RomList.ItemsSource = allGames;
        }

        private string ExtractGameTitle(string cueFilePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(cueFilePath);

            fileName = fileName.Replace("_", " ").Replace("-", " ");
            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[\[\(].*?[\]\)]", "");
            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"Track\s?\d+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"\s+", " ");
            fileName = fileName.Trim();

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            fileName = culture.TextInfo.ToTitleCase(fileName.ToLower());

            fileName = fileName.Replace(" Ii", " II")
                .Replace(" Iii", " III")
                .Replace(" Iv", " IV")
                .Replace(" Usa", " USA");

            return fileName;
        }

        private void FilterSegaCD_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.LoadConfig();
            var segaCdGames = LoadGamesFrom(config.RomsDirectory_SegaCD, "segacd");
            RomList.ItemsSource = segaCdGames;
        }

        private void FilterSaturn_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.LoadConfig();
            var saturnGames = LoadGamesFrom(config.RomsDirectory_Saturn, "saturn");
            RomList.ItemsSource = saturnGames;
        }

        private void OnSegaCDClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("segacd");
        }

        private void OnSaturnClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("saturn");
        }

        public class GameInfo
        {
            public string Title { get; set; }
            public string CoverUrl { get; set; }
            public string CuePath { get; set; }
            public string System { get; set; }
        }
    }
}