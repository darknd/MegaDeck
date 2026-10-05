using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Classic.Avalonia.Theme;
using Classic.CommonControls.Dialogs;
using MegaDeck.Models;

namespace MegaDeck.Views
{
    /// <summary>Diálogo de ajustes. Devuelve true en ShowDialog si se ha guardado algún cambio.</summary>
    public partial class SettingsDialog : ClassicWindow
    {
        private readonly AppConfig _config;
        private readonly List<RomDirectoryEntry> _entries;
        private bool _saved;

        public SettingsDialog()
        {
            InitializeComponent();

            _config = ConfigManager.LoadConfig();
            _entries = GameSystem.All
                .Select(s => new RomDirectoryEntry(s.Id, s.DisplayName + ":") { Path = _config.GetRomsDirectory(s.Id) })
                .ToList();
            foreach (var entry in _entries)
                entry.PropertyChanged += (_, _) => ApplyButton.IsEnabled = true;
            DirectoryList.ItemsSource = _entries;
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not RomDirectoryEntry entry)
                return;

            var options = new FolderPickerOpenOptions { Title = $"ROM folder - {entry.Label.TrimEnd(':')}" };
            if (Directory.Exists(entry.Path))
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(entry.Path);

            var folders = await StorageProvider.OpenFolderPickerAsync(options);
            string? path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path != null)
                entry.Path = path;
        }

        private async void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (!ApplyButton.IsEnabled || await SaveAsync())
                Close(_saved);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(_saved);

        private async void OnApplyClick(object? sender, RoutedEventArgs e) => await SaveAsync();

        private async Task<bool> SaveAsync()
        {
            foreach (var entry in _entries)
                _config.SetRomsDirectory(entry.SystemId, entry.Path.Trim());

            try
            {
                ConfigManager.SaveConfig(_config);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowDialog(this, $"Could not save the settings:\n{ex.Message}", "Settings",
                    MessageBoxButtons.Ok, MessageBoxIcon.Error);
                return false;
            }

            _saved = true;
            ApplyButton.IsEnabled = false;
            return true;
        }
    }

    public class RomDirectoryEntry(string systemId, string label) : INotifyPropertyChanged
    {
        public string SystemId { get; } = systemId;
        public string Label { get; } = label;

        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                if (_path == value)
                    return;
                _path = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
