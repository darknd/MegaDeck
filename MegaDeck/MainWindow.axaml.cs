using Avalonia.Controls;
using Avalonia.Interactivity;
using MegaDeck.Views;

namespace MegaDeck
{
    public partial class MainWindow : Window
    {
        private readonly LibraryView _libraryView = new();

        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = _libraryView;
        }

        private void GoToLibrary(object? sender, RoutedEventArgs e)
        {
            _libraryView.Refresh();
            MainContent.Content = _libraryView;
        }

        private void GoToSettings(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = new SettingsView();
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
