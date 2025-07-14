using System.Windows;

namespace MegaDeck
{
    public partial class MainWindow : Window
    {
        private LibraryPage _libraryPage = new LibraryPage();

        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(_libraryPage); // Página principal
        }

        private void GoToLibrary(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(_libraryPage);
        }

        private void GoToSettings(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage()); // crea tu SettingsPage.xaml
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

}
