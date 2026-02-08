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
            PSXPathBox.Text = config.RomsDirectory_PSX;
            PCFXPathBox.Text = config.RomsDirectory_PCFX;
            PCECDPathBox.Text = config.RomsDirectory_PCECD;
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
        private void BrowsePSX_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                PSXPathBox.Text = dialog.SelectedPath;
        }
        private void BrowsePCFX_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                PCFXPathBox.Text = dialog.SelectedPath;
        }
        private void BrowsePCECD_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                PCECDPathBox.Text = dialog.SelectedPath;
        }
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var config = new AppConfig
            {
                RomsDirectory_SegaCD = SegaCDPathBox.Text,
                RomsDirectory_Saturn = SaturnPathBox.Text,
                RomsDirectory_PSX = PSXPathBox.Text,
                RomsDirectory_PCFX = PCFXPathBox.Text,
                RomsDirectory_PCECD = PCECDPathBox.Text
            };

            ConfigManager.SaveConfig(config);

            SaveStatus.Text = "Saved successfully";
            SaveStatus.Visibility = Visibility.Visible;
        }

    }
}
