using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Classic.Avalonia.Theme;
using Classic.CommonControls.Dialogs;
using MegaDeck.Models;
using MegaDeck.Views;

namespace MegaDeck
{
    public partial class MainWindow : ClassicWindow
    {
        private readonly RomScanner _scanner = new();
        private readonly LibraryNode _rootNode;
        private Dictionary<string, List<GameInfo>> _gamesBySystem = new();
        private List<GameInfo> _displayedGames = new();
        private bool _launching;

        public MainWindow()
        {
            InitializeComponent();

            _rootNode = new LibraryNode
            {
                Name = "MegaDeck",
                Icon = Icons.App16,
                Children = GameSystem.All.Select(s => new LibraryNode { Name = s.DisplayName, Icon = Icons.Cd16, System = s }).ToList()
            };
            SystemTree.ItemsSource = new[] { _rootNode };

            Refresh();
            SystemTree.SelectedItem = _rootNode.Children[0];
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // El TreeView necesita estar en pantalla para poder expandir el nodo raíz.
            if (SystemTree.ContainerFromIndex(0) is TreeViewItem root)
                root.IsExpanded = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F5)
            {
                Refresh();
                e.Handled = true;
            }
        }

        /// <summary>Vuelve a escanear las carpetas de ROMs manteniendo el sistema seleccionado.</summary>
        private void Refresh()
        {
            SetStatus("Scanning ROM folders...");
            _gamesBySystem = _scanner.ScanAll();
            ShowSelectedSystem();
            SetStatus("Ready");
        }

        private GameSystem? SelectedSystem => (SystemTree.SelectedItem as LibraryNode)?.System;

        private void OnSystemSelected(object? sender, SelectionChangedEventArgs e)
        {
            ShowSelectedSystem();
        }

        private void ShowSelectedSystem()
        {
            var system = SelectedSystem;
            _displayedGames = system != null && _gamesBySystem.TryGetValue(system.Id, out var games)
                ? games
                : new List<GameInfo>();

            ContentsHeader.Text = system != null ? $"Contents of '{system.DisplayName}'" : "Select a system";
            StatusSystem.Text = system?.DisplayName ?? "";
            ApplySearch();
        }

        private void OnSearchChanged(object? sender, TextChangedEventArgs e)
        {
            ApplySearch();
        }

        private void ApplySearch()
        {
            string query = SearchBox.Text?.Trim() ?? "";
            var visible = query.Length == 0
                ? _displayedGames
                : _displayedGames.Where(g => g.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            GameList.ItemsSource = visible;
            StatusCount.Text = $"{visible.Count} object(s)";
        }

        // ---- Vistas ----

        private void OnIconsViewClick(object? sender, RoutedEventArgs e) => SetView(details: false);

        private void OnDetailsViewClick(object? sender, RoutedEventArgs e) => SetView(details: true);

        private void SetView(bool details)
        {
            GameList.ItemsPanel = (ITemplate<Panel?>)Resources[details ? "DetailsPanel" : "IconsPanel"]!;
            GameList.ItemTemplate = (IDataTemplate)Resources[details ? "DetailsItemTemplate" : "IconItemTemplate"]!;
            GameList.Classes.Set("icons", !details);
            ColumnHeaders.IsVisible = details;

            MenuIcons.IsChecked = !details;
            MenuDetails.IsChecked = details;
            ToolIcons.IsChecked = !details;
            ToolDetails.IsChecked = details;
        }

        // ---- Lanzar juegos ----

        private void OnGameDoubleTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as Control)?.DataContext is GameInfo game)
                _ = LaunchAsync(game);
        }

        private void OnGameListKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && GameList.SelectedItem is GameInfo game)
            {
                _ = LaunchAsync(game);
                e.Handled = true;
            }
        }

        private void OnPlayClick(object? sender, RoutedEventArgs e)
        {
            if (GetTargetGame(sender) is { } game)
                _ = LaunchAsync(game);
        }

        private async Task LaunchAsync(GameInfo game)
        {
            if (_launching)
                return;
            _launching = true;

            try
            {
                SetStatus($"Loading {game.Title}...");
                bool started = await LaunchDialog.LaunchAsync(this, game);
                SetStatus(started ? $"Running {game.Title}" : "Ready");
            }
            finally
            {
                _launching = false;
            }
        }

        // ---- Portadas ----

        private async void OnAssignCoverClick(object? sender, RoutedEventArgs e)
        {
            if (GetTargetGame(sender) is not { } game)
                return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Select cover - {game.Title}",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Image files") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }
                ]
            });

            string? imagePath = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (imagePath == null)
                return;

            try
            {
                RomImageManager.AssignCover(game.FileName, imagePath);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowDialog(this, $"Could not copy the cover:\n{ex.Message}", "MegaDeck",
                    MessageBoxButtons.Ok, MessageBoxIcon.Error);
                return;
            }
            Refresh();
        }

        private async void OnDownloadCoverClick(object? sender, RoutedEventArgs e)
        {
            if (GetTargetGame(sender) is not { } game)
                return;

            if (game.HasCover && await MessageBox.ShowDialog(this, $"'{game.Title}' already has a cover.\nDo you want to replace it?",
                    "Download cover", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != MessageBoxResult.Yes)
                return;

            SetStatus($"Downloading cover for {game.Title}...");
            try
            {
                var result = await Task.Run(() => CoverDownloader.DownloadAsync(game, overwrite: true, CancellationToken.None));
                if (result == CoverDownloader.Result.NotFound)
                    await MessageBox.ShowDialog(this, $"No cover was found for '{game.FileName}'.", "Download cover",
                        MessageBoxButtons.Ok, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowDialog(this, $"Could not download the cover:\n{ex.Message}", "Download cover",
                    MessageBoxButtons.Ok, MessageBoxIcon.Error);
            }
            Refresh();
        }

        /// <summary>Descarga las carátulas que faltan del sistema seleccionado, o de todos si está seleccionado "MegaDeck".</summary>
        private async void OnDownloadCoversClick(object? sender, RoutedEventArgs e)
        {
            var system = SelectedSystem;
            var games = system != null
                ? _gamesBySystem.GetValueOrDefault(system.Id, [])
                : _gamesBySystem.Values.SelectMany(g => g).ToList();

            if (games.Count == 0)
            {
                await MessageBox.ShowDialog(this, "There are no games to download covers for.\nSet the ROM folders in Tools > Settings.",
                    "Download Covers", MessageBoxButtons.Ok, MessageBoxIcon.Information);
                return;
            }

            if (await CoverDownloadDialog.RunAsync(this, games, system?.DisplayName ?? "all systems"))
                Refresh();
        }

        private void OnRemoveCoverClick(object? sender, RoutedEventArgs e)
        {
            if (GetTargetGame(sender) is not { } game)
                return;

            RomImageManager.RemoveImage(game.FileName);
            Refresh();
        }

        /// <summary>El juego del menú contextual, o el seleccionado si viene del menú File / la barra.</summary>
        private GameInfo? GetTargetGame(object? sender) =>
            (sender as Control)?.DataContext as GameInfo ?? GameList.SelectedItem as GameInfo;

        // ---- Menús ----

        private void OnRefreshClick(object? sender, RoutedEventArgs e) => Refresh();

        private async void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            if (await new SettingsDialog().ShowDialog<bool>(this))
                Refresh();
        }

        private async void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            await MessageBox.ShowDialog(this,
                "MegaDeck\nVersion 0.3\n\nDaRKND 2026\n\nRetroArch launcher for Sega CD, Saturn,\nPlayStation, PC-FX and PC Engine CD.",
                "About MegaDeck", MessageBoxButtons.Ok, MessageBoxIcon.Information);
        }

        private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

        private void SetStatus(string text) => StatusText.Text = text;
    }
}
