using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
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
            LoadCache();
            LoadRoms();
        }

        public void Refresh()
        {
            LoadCache(); // Opcional
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
            string retroarchPath = @".\engine\retroarch.exe";

            if (!File.Exists(retroarchPath))
            {
                MessageBox.Show("RetroArch not found in engine folder.");
                return;
            }

            if (!File.Exists(cueFilePath))
            {
                MessageBox.Show(".cue not found.");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = retroarchPath,
                    Arguments = $"-c \"retroarch.cfg\" -L \"cores\\genesis_plus_gx_libretro.dll\" \"{cueFilePath}\" -f",
                    UseShellExecute = false,
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

        private void OnRemoveCoverClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is FrameworkElement fe &&
                fe.Tag is string cuePath)
            {
                string cueName = Path.GetFileName(cuePath);

                // Eliminar entrada del JSON
                RomImageManager.RemoveImage(cueName);
                MessageBox.Show("🗑 Custom cover removed.");
                Refresh();
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

                    MessageBox.Show("Custom cover assigned.");
                    Refresh(); // Recargar la lista con nueva imagen
                }
            }
        }

        private async void LoadRoms()
        {
            var config = ConfigManager.LoadConfig();
            string romFolder = config.RomsDirectory;

            if (string.IsNullOrWhiteSpace(romFolder) || !Directory.Exists(romFolder))
            {
                MessageBox.Show("The ROMs folder is not properly configured. Please go to Settings and select a folder.");
                return;
            }

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
                    title = ExtractGameTitle(cue);
                    _romTitleCache[fileName] = title;
                    updated = true;
                }

                // Carátula personalizada
                string imageName = RomImageManager.GetImage(fileName);
                string imagePath = !string.IsNullOrEmpty(imageName) && File.Exists($"images/{imageName}")
                    ? Path.GetFullPath($"images/{imageName}")
                    : "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ac/No_image_available.svg/480px-No_image_available.svg.png";

                gameList.Add(new GameInfo
                {
                    Title = title,
                    CoverUrl = imagePath,
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

        public class GameInfo
        {
            public string Title { get; set; }
            public string CoverUrl { get; set; }
            public string CuePath { get; set; }
        }
    }
}
