using System.IO.Compression;

namespace MegaDeck
{
    /// <summary>
    /// Los cores de CD no cargan .zip directamente (necesitan el .cue y todos sus .bin en disco),
    /// así que el juego se descomprime en cache/ antes de lanzarlo. Solo se guarda el último juego
    /// extraído: volver a lanzarlo es inmediato y no se acumulan GB de imágenes de disco.
    /// </summary>
    public static class ZipGameExtractor
    {
        // Orden de preferencia del archivo a lanzar dentro del zip (m3u primero para multidisco).
        private static readonly string[] LaunchExtensions = [".m3u", ".cue", ".ccd", ".toc", ".chd", ".iso", ".pbp"];

        private const string MarkerFileName = ".megadeck-source";

        /// <summary>True si el zip contiene una imagen de disco lanzable. Solo lee el índice del zip.</summary>
        public static bool ContainsGame(string zipPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                return FindLaunchEntry(zip.Entries.Select(e => e.FullName)) != null;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Ruta del juego ya extraído si sigue siendo válido (mismo zip, mismo tamaño y fecha), o null.</summary>
        public static string? GetCachedGame(string zipPath)
        {
            string dir = GetExtractDir(zipPath);
            string marker = Path.Combine(dir, MarkerFileName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != GetSourceStamp(zipPath))
                return null;

            string? launchFile = FindLaunchEntry(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories));
            return launchFile != null && File.Exists(launchFile) ? launchFile : null;
        }

        /// <summary>Extrae el zip y devuelve la ruta del archivo a lanzar. Lanza excepción si falla o se cancela.</summary>
        public static string Extract(string zipPath, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            string dir = GetExtractDir(zipPath);
            DeleteOtherExtractedGames(dir);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var entries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

                long totalBytes = entries.Sum(e => e.Length);
                long freeBytes = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dir))!).AvailableFreeSpace;
                if (totalBytes > freeBytes)
                    throw new IOException($"Not enough disk space. The game needs {FormatSize(totalBytes)} and only {FormatSize(freeBytes)} are free.");

                string root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
                long written = 0;
                var buffer = new byte[1024 * 1024];

                foreach (var entry in entries)
                {
                    // Evita rutas que se salgan de la carpeta de destino ("../").
                    string dest = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!dest.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException($"Invalid path in zip: {entry.FullName}");

                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    using var input = entry.Open();
                    using var output = File.Create(dest);

                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        written += read;
                        progress?.Report(totalBytes == 0 ? 1 : (double)written / totalBytes);
                    }
                }

                string launchFile = FindLaunchEntry(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    ?? throw new InvalidDataException("The zip does not contain a disc image (.cue, .chd, .iso...).");

                // El marcador se escribe al final: si la extracción se interrumpe, no se reutiliza.
                File.WriteAllText(Path.Combine(dir, MarkerFileName), GetSourceStamp(zipPath));
                return launchFile;
            }
            catch
            {
                TryDeleteDirectory(dir);
                throw;
            }
        }

        private static string? FindLaunchEntry(IEnumerable<string> paths)
        {
            var list = paths.ToList();
            foreach (var ext in LaunchExtensions)
            {
                var match = list.Where(p => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (match != null)
                    return match;
            }
            return null;
        }

        private static string GetExtractDir(string zipPath)
        {
            string name = Path.GetFileNameWithoutExtension(zipPath);
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return Path.Combine(AppPaths.ExtractCacheDir, name);
        }

        private static string GetSourceStamp(string zipPath)
        {
            var info = new FileInfo(zipPath);
            return $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }

        private static void DeleteOtherExtractedGames(string keepDir)
        {
            if (!Directory.Exists(AppPaths.ExtractCacheDir))
                return;

            foreach (var dir in Directory.EnumerateDirectories(AppPaths.ExtractCacheDir))
            {
                if (!string.Equals(Path.GetFullPath(dir), Path.GetFullPath(keepDir), StringComparison.Ordinal))
                    TryDeleteDirectory(dir);
            }
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Puede seguir en uso por RetroArch (Windows bloquea los archivos abiertos).
            }
        }

        public static string FormatSize(long bytes) => bytes switch
        {
            >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
            >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
            _ => $"{bytes / 1024.0:0} KB"
        };
    }
}
