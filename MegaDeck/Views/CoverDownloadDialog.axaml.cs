using Avalonia.Controls;
using Avalonia.Interactivity;
using Classic.Avalonia.Theme;
using Classic.CommonControls.Dialogs;
using MegaDeck.Models;

namespace MegaDeck.Views
{
    /// <summary>Descarga las carátulas que faltan de una lista de juegos y muestra un resumen al terminar.</summary>
    public partial class CoverDownloadDialog : ClassicWindow
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IReadOnlyList<GameInfo> _games = [];
        private CoverDownloader.Summary? _summary;
        private Exception? _error;
        private bool _finished;

        public CoverDownloadDialog()
        {
            InitializeComponent();
        }

        private CoverDownloadDialog(IReadOnlyList<GameInfo> games, string scope) : this()
        {
            _games = games;
            HeaderText.Text = $"Downloading covers for {scope} from the libretro thumbnail server. Games that already have a cover are skipped.";
            CountText.Text = $"0 of {games.Count}";
        }

        /// <summary>Devuelve true si se ha descargado alguna carátula.</summary>
        public static async Task<bool> RunAsync(Window owner, IReadOnlyList<GameInfo> games, string scope)
        {
            var dialog = new CoverDownloadDialog(games, scope);
            await dialog.ShowDialog(owner);

            if (dialog._error != null)
            {
                await MessageBox.ShowDialog(owner, $"Could not download the covers:\n{dialog._error.Message}",
                    "Download Covers", MessageBoxButtons.Ok, MessageBoxIcon.Error);
                return false;
            }
            if (dialog._summary is not { } s)
                return false; // cancelado

            string message = $"{s.Downloaded} cover(s) downloaded.\n{s.NotFound} game(s) not found.\n{s.Skipped} game(s) already had a cover.";
            if (s.Failed > 0)
                message += $"\n\n{s.Failed} download(s) failed: {s.FirstError}";
            await MessageBox.ShowDialog(owner, message, "Download Covers", MessageBoxButtons.Ok,
                s.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            return s.Downloaded > 0;
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (_games.Count == 0)
                return; // instancia del diseñador

            var progress = new Progress<(int Done, string Title)>(p =>
            {
                Progress.Value = 100.0 * p.Done / _games.Count;
                CurrentText.Text = p.Title;
                CountText.Text = $"{p.Done} of {_games.Count}";
            });

            try
            {
                _summary = await Task.Run(() => CoverDownloader.DownloadMissingAsync(_games, progress, _cts.Token));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _error = ex;
            }
            finally
            {
                _finished = true;
                Close();
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            CancelButton.IsEnabled = false;
            CurrentText.Text = "Cancelling...";
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!_finished)
            {
                if (CancelButton.IsEnabled)
                    OnCancelClick(null, new RoutedEventArgs());
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}
