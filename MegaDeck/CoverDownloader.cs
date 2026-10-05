using System.Net;
using System.Text.RegularExpressions;
using MegaDeck.Models;
using SkiaSharp;

namespace MegaDeck
{
    /// <summary>
    /// Descarga carátulas de thumbnails.libretro.com (las mismas que usa RetroArch).
    /// Busca primero por nombre exacto (nombres Redump/No-Intro) y si no, por título aproximado.
    /// </summary>
    public static class CoverDownloader
    {
        private const string BaseUrl = "https://thumbnails.libretro.com/";
        private const int CoverWidth = 300;

        // Orden de preferencia cuando el archivo no indica región.
        private static readonly string[] RegionPreference = ["USA", "Europe", "World", "Japan"];

        // Países que, si no tienen versión propia, deben preferir la versión "Europe".
        private static readonly string[] EuropeanCountries =
            ["Spain", "France", "Germany", "Italy", "UK", "Netherlands", "Sweden", "Portugal", "Scandinavia"];

        private static readonly HttpClient Http = CreateHttpClient();
        private static readonly Dictionary<string, List<string>> IndexCache = new();
        private static readonly SemaphoreSlim IndexLock = new(1, 1);

        public enum Result { Downloaded, NotFound, Skipped }

        public record Summary(int Downloaded, int NotFound, int Skipped, int Failed, string? FirstError);

        /// <summary>
        /// Descarga las carátulas de los juegos que no tienen. <paramref name="progress"/> recibe
        /// (juegos procesados, título actual).
        /// </summary>
        public static async Task<Summary> DownloadMissingAsync(IReadOnlyList<GameInfo> games,
            IProgress<(int Done, string Title)>? progress, CancellationToken cancellationToken)
        {
            int downloaded = 0, notFound = 0, skipped = 0, failed = 0, done = 0;
            string? firstError = null;

            await Parallel.ForEachAsync(games,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
                async (game, ct) =>
                {
                    try
                    {
                        var result = await DownloadAsync(game, overwrite: false, ct);
                        if (result == Result.Downloaded) Interlocked.Increment(ref downloaded);
                        else if (result == Result.NotFound) Interlocked.Increment(ref notFound);
                        else Interlocked.Increment(ref skipped);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref failed);
                        Interlocked.CompareExchange(ref firstError, ex.Message, null);
                    }
                    progress?.Report((Interlocked.Increment(ref done), game.Title));
                });

            return new Summary(downloaded, notFound, skipped, failed, firstError);
        }

        /// <summary>Descarga la carátula de un juego. Con overwrite=false no toca las que ya tiene.</summary>
        public static async Task<Result> DownloadAsync(GameInfo game, bool overwrite, CancellationToken cancellationToken)
        {
            if (!overwrite && RomImageManager.GetImagePath(game.FileName) != null)
                return Result.Skipped;

            var index = await GetIndexAsync(game.System, cancellationToken);
            string? match = FindBestMatch(Path.GetFileNameWithoutExtension(game.FileName), index);
            if (match == null)
                return Result.NotFound;

            string url = BaseUrl + Uri.EscapeDataString(game.System.ThumbnailFolder) + "/Named_Boxarts/" + Uri.EscapeDataString(match) + ".png";
            using var response = await Http.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Result.NotFound;
            response.EnsureSuccessStatusCode();

            byte[] png = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            RomImageManager.AssignCover(game.FileName, ResizeToJpeg(png));
            return Result.Downloaded;
        }

        /// <summary>Las originales son PNG de ~500 KB; reducidas a 300 px en JPEG ocupan ~25 KB.</summary>
        private static byte[] ResizeToJpeg(byte[] image)
        {
            using var original = SKBitmap.Decode(image) ?? throw new IOException("The downloaded cover is not a valid image.");
            int height = (int)Math.Round(original.Height * (double)CoverWidth / original.Width);
            using var resized = original.Width > CoverWidth
                ? original.Resize(new SKImageInfo(CoverWidth, height), SKFilterQuality.High)
                : original.Copy();

            // JPEG no tiene transparencia: se pinta sobre blanco.
            using var surface = SKSurface.Create(new SKImageInfo(resized.Width, resized.Height));
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.DrawBitmap(resized, 0, 0);
            using var data = surface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 88);
            return data.ToArray();
        }

        /// <summary>Lista de carátulas (sin ".png") del sistema. Se descarga una vez por sesión.</summary>
        private static async Task<List<string>> GetIndexAsync(GameSystem system, CancellationToken cancellationToken)
        {
            await IndexLock.WaitAsync(cancellationToken);
            try
            {
                if (IndexCache.TryGetValue(system.Id, out var cached))
                    return cached;

                string html = await Http.GetStringAsync(BaseUrl + Uri.EscapeDataString(system.ThumbnailFolder) + "/Named_Boxarts/", cancellationToken);
                var names = Regex.Matches(html, "href=\"([^\"]+)\\.png\"")
                    .Select(m => Uri.UnescapeDataString(m.Groups[1].Value))
                    .ToList();

                IndexCache[system.Id] = names;
                return names;
            }
            finally
            {
                IndexLock.Release();
            }
        }

        /// <summary>
        /// 1) Nombre exacto. 2) Mismo título sin etiquetas, eligiendo la versión cuyas etiquetas
        /// (región, disco...) más se parecen a las del archivo.
        /// </summary>
        public static string? FindBestMatch(string romName, IReadOnlyList<string> index)
        {
            // libretro sustituye estos caracteres por "_" en los nombres de archivo.
            string libretroName = Regex.Replace(romName, "[&*/:`<>?\\\\|\"]", "_");
            var exact = index.FirstOrDefault(n => string.Equals(n, libretroName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            string title = NormalizeTitle(romName);
            if (title.Length == 0)
                return null;

            var romTags = GetTags(romName);
            if (romTags.Overlaps(EuropeanCountries))
                romTags.Add("Europe");
            return index
                .Where(n => NormalizeTitle(n) == title)
                .OrderByDescending(n => ScoreTags(romTags, GetTags(n)))
                .ThenBy(n => n.Length)
                .FirstOrDefault();
        }

        private static int ScoreTags(HashSet<string> romTags, HashSet<string> candidateTags)
        {
            int score = 10 * candidateTags.Count(romTags.Contains) - candidateTags.Count(t => !romTags.Contains(t));

            // Sin región en el archivo: preferir USA, luego Europa...
            if (!romTags.Overlaps(RegionPreference))
            {
                int region = Array.FindIndex(RegionPreference, r => candidateTags.Contains(r));
                if (region >= 0)
                    score += 5 - region;
            }
            return score;
        }

        /// <summary>Etiquetas entre paréntesis/corchetes: "(USA, Europe) (Disc 1)" → {usa, europe, disc 1}.</summary>
        private static HashSet<string> GetTags(string name)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(name.Replace('_', ' '), @"[\(\[]([^\)\]]*)[\)\]]"))
            {
                foreach (var part in m.Groups[1].Value.Split(','))
                {
                    string tag = Regex.Replace(part.Trim(), @"\s+", " ");
                    if (tag.Length > 0)
                        tags.Add(tag);
                }
            }
            return tags;
        }

        /// <summary>"Adventures of Batman _ Robin, The (USA)" → "adventuresofbatmanrobin".</summary>
        private static string NormalizeTitle(string name)
        {
            string t = name.Replace('_', ' ').Replace('-', ' ');
            t = Regex.Replace(t, @"[\(\[].*?[\)\]]", " ");
            t = Regex.Replace(t, @"track\s?\d+", " ", RegexOptions.IgnoreCase);
            t = t.ToLowerInvariant().Replace("&", " and ");
            t = Regex.Replace(t, @"\b(the|and)\b", " ");
            return Regex.Replace(t, "[^a-z0-9]", "");
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MegaDeck/0.3");
            return client;
        }
    }
}
