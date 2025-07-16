using System.Windows;

namespace MegaDeck
{
    public partial class MainWindow : Window
    {
        private LibraryPage _libraryPage = new LibraryPage();

         public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(_libraryPage); 
        }

        private void GoToLibrary(object sender, RoutedEventArgs e)
        {
            _libraryPage.Refresh(); 
            MainFrame.Navigate(_libraryPage);
        }

        private void GoToSettings(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage()); 
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

}
