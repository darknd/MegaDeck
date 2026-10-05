using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MegaDeck.Models;

namespace MegaDeck.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly AppConfig _config;
        private readonly List<RomDirectoryEntry> _entries;

        public SettingsView()
        {
            InitializeComponent();

            _config = ConfigManager.LoadConfig();
            _entries = GameSystem.All
                .Select(s => new RomDirectoryEntry(s.Id, s.DisplayName + ":") { Path = _config.GetRomsDirectory(s.Id) })
                .ToList();
            DirectoryList.ItemsSource = _entries;
        }

        private async void Browse_Click(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not RomDirectoryEntry entry)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var options = new FolderPickerOpenOptions { Title = $"ROM folder - {entry.Label.TrimEnd(':')}" };
            if (Directory.Exists(entry.Path))
                options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(entry.Path);

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            string? path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path != null)
                entry.Path = path;
        }

        private void SaveSettings_Click(object? sender, RoutedEventArgs e)
        {
            foreach (var entry in _entries)
                _config.SetRomsDirectory(entry.SystemId, entry.Path.Trim());

            try
            {
                ConfigManager.SaveConfig(_config);
                SaveStatus.Text = "Saved successfully";
                SaveStatus.Foreground = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                SaveStatus.Text = $"Could not save: {ex.Message}";
                SaveStatus.Foreground = Brushes.OrangeRed;
            }
            SaveStatus.IsVisible = true;
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
            set { _path = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
