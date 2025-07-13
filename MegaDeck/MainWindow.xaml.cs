using System.Windows;

namespace MegaDeck
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void GoToLibrary(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new LibraryPage());
        }

        private void GoToSettings(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage());
        }
    }
}
