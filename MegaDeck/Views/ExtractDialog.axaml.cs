using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Classic.Avalonia.Theme;
using Classic.CommonControls.Dialogs;
using MegaDeck.Models;

namespace MegaDeck.Views
{
    /// <summary>Descomprime un juego .zip mostrando el progreso. Devuelve la ruta a lanzar o null.</summary>
    public partial class ExtractDialog : ClassicWindow
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly GameInfo? _game;
        private long _totalBytes;
        private string? _result;
        private Exception? _error;

        public ExtractDialog()
        {
            InitializeComponent();
        }

        private ExtractDialog(GameInfo game) : this()
        {
            _game = game;
            FileText.Text = $"Extracting '{game.FileName}'";
            try
            {
                using var zip = ZipFile.OpenRead(game.RomPath);
                _totalBytes = zip.Entries.Sum(en => en.Length);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                // Extract() dará el error.
            }
            UpdateProgress(0);
        }

        public static async Task<string?> ExtractAsync(Window owner, GameInfo game)
        {
            var dialog = new ExtractDialog(game);
            await dialog.ShowDialog(owner);

            if (dialog._error != null)
            {
                await MessageBox.ShowDialog(owner, $"Could not extract '{game.FileName}':\n{dialog._error.Message}",
                    "MegaDeck", MessageBoxButtons.Ok, MessageBoxIcon.Error);
            }
            return dialog._result;
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (_game == null)
                return;

            var progress = new Progress<double>(UpdateProgress);
            try
            {
                string zipPath = _game.RomPath;
                _result = await Task.Run(() => ZipGameExtractor.Extract(zipPath, progress, _cts.Token));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _error = ex;
            }
            Close();
        }

        private void UpdateProgress(double fraction)
        {
            Progress.Value = fraction * 100;
            SizeText.Text = _totalBytes > 0
                ? $"{ZipGameExtractor.FormatSize((long)(fraction * _totalBytes))} of {ZipGameExtractor.FormatSize(_totalBytes)}"
                : "";
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _cts.Cancel();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // Cerrar con la X cancela; la ventana se cierra cuando la extracción termina de abortar.
            if (_result == null && _error == null && !_cts.IsCancellationRequested && _game != null)
            {
                _cts.Cancel();
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}
