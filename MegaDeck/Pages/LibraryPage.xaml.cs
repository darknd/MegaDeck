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
        private List<GameInfo> _allGames = new List<GameInfo>();
        private List<GameInfo> _segaCdGames = new List<GameInfo>();
        private List<GameInfo> _saturnGames = new List<GameInfo>();
        private List<GameInfo> _psxGames = new List<GameInfo>();
        private List<GameInfo> _pcfxGames = new List<GameInfo>();
        private List<GameInfo> _pcecdGames = new List<GameInfo>();
        private List<GameInfo> _displayedGames = new List<GameInfo>();


        public LibraryPage()
        {
            InitializeComponent();
            Refresh();
        }

        public void Refresh()
        {
            LoadCache();
            var config = ConfigManager.LoadConfig();

            _segaCdGames = LoadGamesFrom(config.RomsDirectory_SegaCD, "segacd");
            _saturnGames = LoadGamesFrom(config.RomsDirectory_Saturn, "saturn");
            _psxGames = LoadGamesFrom(config.RomsDirectory_PSX, "psx");
            _pcfxGames = LoadGamesFrom(config.RomsDirectory_PCFX, "pcfx");
            _pcecdGames = LoadGamesFrom(config.RomsDirectory_PCECD, "pcecd");

            ShowGames(_segaCdGames);
        }

        private void ShowGames(List<GameInfo> games)
        {
            _displayedGames = new List<GameInfo>(games);
            RomList.ItemsSource = _displayedGames;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();
            var filtered = _displayedGames
                .Where(g => g.Title.ToLower().Contains(query))
                .ToList();

            RomList.ItemsSource = filtered;
        }

        private void OnSegaCDChecked(object sender, RoutedEventArgs e)
        {
            BtnSegaSaturn.IsChecked = false;
            ShowGames(_segaCdGames);
        }

        private void OnSaturnChecked(object sender, RoutedEventArgs e)
        {
            BtnSegaCD.IsChecked = false;
            ShowGames(_saturnGames);
        }

        private void OnPSXChecked(object sender, RoutedEventArgs e)
        {
            BtnPSX.IsChecked = false;
            ShowGames(_psxGames);
        }

        private void OnPCFXChecked(object sender, RoutedEventArgs e)
        {
            BtnPCFX.IsChecked = false;
            ShowGames(_pcfxGames);
        }
        private void OnPCECDChecked(object sender, RoutedEventArgs e)
        {
            BtnPCECD.IsChecked = false;
            ShowGames(_pcecdGames);
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

        private async void OnGameClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || !(sender is FrameworkElement fe) || !(fe.DataContext is GameInfo game))
                return;

            try
            {
                // Mostrar overlay
                LoadingOverlay.Visibility = Visibility.Visible;

                // Lanzar el juego en un hilo aparte para no bloquear la UI
                await Task.Run(() => LaunchGame(game));
            }
            finally
            {
                // Ocultar overlay después de lanzar
                LoadingOverlay.Visibility = Visibility.Collapsed;
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
                "psx" => Path.GetFullPath(@".\engine\cores\mednafen_psx_libretro.dll"),
                "pcfx" => Path.GetFullPath(@".\engine\cores\mednafen_pcfx_libretro.dll"),
                "pcecd" => Path.GetFullPath(@".\engine\cores\mednafen_pce_libretro.dll"),
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
            _allGames.Clear();

            if (filter == "segacd")
                _allGames.AddRange(LoadGamesFrom(config.RomsDirectory_SegaCD, "segacd"));
            else if (filter == "saturn")
                _allGames.AddRange(LoadGamesFrom(config.RomsDirectory_Saturn, "saturn"));
            else if (filter == "psx")
                _allGames.AddRange(LoadGamesFrom(config.RomsDirectory_PSX, "psx"));
            else if (filter == "pcfx")
                _allGames.AddRange(LoadGamesFrom(config.RomsDirectory_PCFX, "pcfx"));
            else if (filter == "pcecd")
                _allGames.AddRange(LoadGamesFrom(config.RomsDirectory_PCECD, "pcecd"));

            RomList.ItemsSource = _allGames;
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
        private void FilterPSX_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.LoadConfig();
            var psxGames = LoadGamesFrom(config.RomsDirectory_PSX, "psx");
            RomList.ItemsSource = psxGames;
        }
        private void FilterPCFX_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.LoadConfig();
            var pcfxGames = LoadGamesFrom(config.RomsDirectory_PCFX, "pcfx");
            RomList.ItemsSource = pcfxGames;
        }
        private void FilterPCECD_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.LoadConfig();
            var pcecdGames = LoadGamesFrom(config.RomsDirectory_PCECD, "pcecd");
            RomList.ItemsSource = pcecdGames;
        }

        private void OnSegaCDClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("segacd");
        }
        private void OnSaturnClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("saturn");
        }
        private void OnPSXClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("psx");
        }
        private void OnPCFXClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("pcfx");
        }
        private void OnPCECDClick(object sender, RoutedEventArgs e)
        {
            LoadRoms("pcecd");
        }

        private void ViewToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            RomList.ItemsPanel = (ItemsPanelTemplate)Resources["ListViewTemplate"];
            RomList.ItemTemplate = (DataTemplate)Resources["ListItemTemplate"];
        }

        private void ViewToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            RomList.ItemsPanel = (ItemsPanelTemplate)Resources["GridViewTemplate"];
            RomList.ItemTemplate = (DataTemplate)Resources["GameItemTemplate"]; 
        }

        private void OnSystemButtonUnchecked(object sender, RoutedEventArgs e)
        {
            // Opcional: dejar todos desmarcados
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