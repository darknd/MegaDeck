using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace MegaDeck
{
    public partial class SettingsPage : Page
    {
        private AppConfig _config;

        public SettingsPage()
        {
            InitializeComponent();
            _config = ConfigManager.LoadConfig();
            txtRomsPath.Text = _config.RomsDirectory;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select roms folder";
                dialog.SelectedPath = _config.RomsDirectory;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtRomsPath.Text = dialog.SelectedPath;
                }
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            _config.RomsDirectory = txtRomsPath.Text;
            ConfigManager.SaveConfig(_config);
            lblStatus.Text = "✔ Saved successfully";
            lblStatus.Visibility = Visibility.Visible;

            await Task.Delay(2000); // Espera 2 segundos
            lblStatus.Visibility = Visibility.Collapsed;
        }
    }
}
