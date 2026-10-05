using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Classic.Avalonia.Theme;
using Classic.CommonControls.Dialogs;
using MegaDeck.Models;

namespace MegaDeck.Views
{
    /// <summary>
    /// Pantalla de carga al lanzar un juego: descomprime el .zip si hace falta, arranca RetroArch y
    /// espera unos segundos para detectar si se cierra con error (core, BIOS o imagen que no carga).
    /// </summary>
    public partial class LaunchDialog : ClassicWindow
    {
        // Tiempo vigilando RetroArch tras arrancarlo; si se cierra antes con error, se avisa.
        private static readonly TimeSpan WatchTime = TimeSpan.FromSeconds(3);

        private readonly CancellationTokenSource _cts = new();
        private readonly DispatcherTimer _marquee = new() { Interval = TimeSpan.FromMilliseconds(60) };
        private readonly GameInfo? _game;
        private bool _started;
        private bool _finished;
        private string? _error;

        public LaunchDialog()
        {
            InitializeComponent();
            // Barra "en bucle" para los pasos sin progreso medible (no depende de la animación del tema).
            _marquee.Tick += (_, _) => Progress.Value = Progress.Value >= 100 ? 0 : Progress.Value + 4;
        }

        private LaunchDialog(GameInfo game) : this()
        {
            _game = game;
            Title = $"Loading - {game.Title}";
            TitleText.Text = game.Title;
            SystemText.Text = game.System.DisplayName;
            CoverImage.Source = game.Cover;
            CdIcon.IsVisible = !game.HasCover;
            StepExtract.IsVisible = game.IsZip;
        }

        /// <summary>Lanza el juego mostrando el progreso. Devuelve true si RetroArch está en marcha.</summary>
        public static async Task<bool> LaunchAsync(Window owner, GameInfo game)
        {
            var dialog = new LaunchDialog(game);
            await dialog.ShowDialog(owner);

            if (dialog._error != null)
                await MessageBox.ShowDialog(owner, dialog._error, "MegaDeck", MessageBoxButtons.Ok, MessageBoxIcon.Error);
            return dialog._started;
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (_game == null)
                return;

            try
            {
                string contentPath = _game.IsZip ? await ExtractAsync(_game) : _game.RomPath;

                CancelButton.IsEnabled = false;
                SetStep(StepStart, "Starting RetroArch...");
                var result = await Task.Run(() => RetroArchLauncher.Launch(_game.System, contentPath));
                Done(StepStart);
                if (result.Error != null)
                {
                    _error = result.Error;
                    return;
                }

                SetStep(StepLoad, "Waiting for the game window...");
                var watch = await WatchProcessAsync(result.Process!);
                Done(StepLoad);
                if (watch is { } exitCode && exitCode != 0)
                {
                    _error = $"RetroArch closed unexpectedly (exit code {exitCode}).\n\n" +
                             "The game image may be damaged, or the BIOS for this system\n" +
                             "may be missing from engine/system.";
                    return;
                }
                _started = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _error = $"Could not launch '{_game.Title}':\n{ex.Message}";
            }
            finally
            {
                _marquee.Stop();
                _finished = true;
                Close();
            }
        }

        private async Task<string> ExtractAsync(GameInfo game)
        {
            SetStep(StepExtract, "");

            if (ZipGameExtractor.GetCachedGame(game.RomPath) is { } cached)
            {
                DetailText.Text = "Using the files extracted last time";
                Done(StepExtract);
                return cached;
            }

            long totalBytes = 0;
            try
            {
                using var zip = ZipFile.OpenRead(game.RomPath);
                totalBytes = zip.Entries.Sum(en => en.Length);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                // Extract() dará el error.
            }

            _marquee.Stop();
            Progress.Value = 0;
            var progress = new Progress<double>(fraction =>
            {
                Progress.Value = fraction * 100;
                DetailText.Text = totalBytes > 0
                    ? $"{ZipGameExtractor.FormatSize((long)(fraction * totalBytes))} of {ZipGameExtractor.FormatSize(totalBytes)}"
                    : "";
            });

            string zipPath = game.RomPath;
            string path = await Task.Run(() => ZipGameExtractor.Extract(zipPath, progress, _cts.Token));
            Done(StepExtract);
            return path;
        }

        /// <summary>Vigila RetroArch unos segundos. Devuelve el código de salida si se cierra, o null si sigue abierto.</summary>
        private static async Task<int?> WatchProcessAsync(System.Diagnostics.Process process)
        {
            var end = DateTime.UtcNow + WatchTime;
            while (DateTime.UtcNow < end)
            {
                if (process.HasExited)
                    return process.ExitCode;
                await Task.Delay(100);
            }
            return null;
        }

        private void SetStep(TextBlock step, string detail)
        {
            step.Classes.Add("current");
            step.Text = "»  " + (step.Text ?? "").TrimStart();
            DetailText.Text = detail;
            Progress.Value = 0;
            _marquee.Start();
        }

        private static void Done(TextBlock step)
        {
            step.Classes.Remove("current");
            step.Classes.Add("done");
            step.Text = "✓  " + (step.Text ?? "").TrimStart('»', ' ');
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            CancelButton.IsEnabled = false;
            DetailText.Text = "Cancelling...";
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // Cerrar con la X equivale a Cancelar; la ventana se cierra cuando termina de abortar.
            if (!_finished && _game != null)
            {
                if (CancelButton.IsEnabled)
                    OnCancelClick(null, new RoutedEventArgs());
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}
