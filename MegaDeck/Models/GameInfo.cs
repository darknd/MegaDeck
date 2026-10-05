using Avalonia.Media.Imaging;

namespace MegaDeck.Models;

public class GameInfo
{
    public required string Title { get; init; }
    public required string RomPath { get; init; }
    public required GameSystem System { get; init; }
    public string? CoverPath { get; init; }

    public string FileName => Path.GetFileName(RomPath);
    public string FileType => Path.GetExtension(RomPath).TrimStart('.').ToUpperInvariant();
    public bool IsZip => FileType == "ZIP";

    private Bitmap? _cover;
    private bool _coverLoaded;

    /// <summary>Se carga la primera vez que la UI la pide, reducida para no gastar memoria.</summary>
    public Bitmap? Cover
    {
        get
        {
            if (!_coverLoaded)
            {
                _coverLoaded = true;
                _cover = LoadCover();
            }
            return _cover;
        }
    }

    public bool HasCover => Cover != null;

    private Bitmap? LoadCover()
    {
        if (CoverPath == null)
            return null;

        try
        {
            // Se lee a memoria y se cierra el archivo para no bloquearlo (en Windows
            // impediría sobrescribir la portada).
            using var stream = new MemoryStream(File.ReadAllBytes(CoverPath));
            return Bitmap.DecodeToWidth(stream, 300);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
