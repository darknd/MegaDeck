using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace MegaDeck
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();

            var config = ConfigManager.LoadConfig();
            SegaCDPathBox.Text = config.RomsDirectory_SegaCD;
            SaturnPathBox.Text = config.RomsDirectory_Saturn;
        }

        private void BrowseSegaCD_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                SegaCDPathBox.Text = dialog.SelectedPath;
        }

        private void BrowseSaturn_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                SaturnPathBox.Text = dialog.SelectedPath;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var config = new AppConfig
            {
                RomsDirectory_SegaCD = SegaCDPathBox.Text,
                RomsDirectory_Saturn = SaturnPathBox.Text
            };

            ConfigManager.SaveConfig(config);

            SaveStatus.Text = "Saved successfully";
            SaveStatus.Visibility = Visibility.Visible;
        }
    }
}
