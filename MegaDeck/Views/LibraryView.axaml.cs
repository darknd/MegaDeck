using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MegaDeck.Models;

namespace MegaDeck.Views
{
    public partial class LibraryView : UserControl
    {
        private readonly RomScanner _scanner = new();
        private Dictionary<string, List<GameInfo>> _gamesBySystem = new();
        private List<GameInfo> _displayedGames = new();

        public LibraryView()
        {
            InitializeComponent();
            SystemList.ItemsSource = GameSystem.All;
            Refresh();
        }

        /// <summary>Vuelve a escanear las carpetas de ROMs manteniendo el sistema seleccionado.</summary>
        public void Refresh()
        {
            _gamesBySystem = _scanner.ScanAll();

            if (SystemList.SelectedItem == null)
                SystemList.SelectedIndex = 0; // dispara OnSystemSelected
            else
                ShowSelectedSystem();
        }

        private void OnSystemSelected(object? sender, SelectionChangedEventArgs e)
        {
            ShowSelectedSystem();
        }

        private void ShowSelectedSystem()
        {
            _displayedGames = SystemList.SelectedItem is GameSystem system && _gamesBySystem.TryGetValue(system.Id, out var games)
                ? games
                : new List<GameInfo>();
            ApplySearch();
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            ApplySearch();
        }

        private void ApplySearch()
        {
            string query = SearchBox.Text?.Trim() ?? "";
            RomList.ItemsSource = query.Length == 0
                ? _displayedGames
                : _displayedGames.Where(g => g.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void ViewToggleButton_Changed(object? sender, RoutedEventArgs e)
        {
            bool listView = ViewToggleButton.IsChecked == true;
            RomList.ItemsPanel = (ITemplate<Panel?>)Resources[listView ? "ListViewTemplate" : "GridViewTemplate"]!;
            RomList.ItemTemplate = (IDataTemplate)Resources[listView ? "ListItemTemplate" : "GridItemTemplate"]!;
        }

        private async void OnGameDoubleTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not GameInfo game)
                return;

            LoadingOverlay.IsVisible = true;
            try
            {
                string? error = await Task.Run(() => RetroArchLauncher.Launch(game));
                if (error != null)
                    ShowError(error);
                else
                    await Task.Delay(1000); // deja ver el overlay mientras abre RetroArch
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
            }
        }

        private async void OnAssignCoverClick(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not GameInfo game)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select cover",
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
                RomImageManager.AssignCover(Path.GetFileName(game.RomPath), imagePath);
            }
            catch (Exception ex)
            {
                ShowError($"Could not copy the cover:\n{ex.Message}");
                return;
            }
            Refresh();
        }

        private void OnRemoveCoverClick(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not GameInfo game)
                return;

            RomImageManager.RemoveImage(Path.GetFileName(game.RomPath));
            Refresh();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBanner.IsVisible = true;
        }

        private void OnCloseError(object? sender, RoutedEventArgs e)
        {
            ErrorBanner.IsVisible = false;
        }
    }
}
