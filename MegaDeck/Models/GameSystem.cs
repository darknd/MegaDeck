namespace MegaDeck.Models;

/// <summary>
/// Sistema soportado. <see cref="Core"/> es el nombre del core libretro sin
/// el sufijo "_libretro" ni la extensión (.dll / .so). <see cref="ThumbnailFolder"/> es el nombre
/// del sistema en thumbnails.libretro.com.
/// </summary>
public sealed record GameSystem(string Id, string DisplayName, string Core, string ThumbnailFolder)
{
    public static readonly IReadOnlyList<GameSystem> All =
    [
        new("segacd", "Sega CD", "genesis_plus_gx", "Sega - Mega-CD - Sega CD"),
        new("pcecd", "PC-Engine CD", "mednafen_pce", "NEC - PC Engine CD - TurboGrafx-CD"),
        new("pcfx", "PC-FX", "mednafen_pcfx", "NEC - PC-FX"),
        new("saturn", "Sega Saturn", "mednafen_saturn", "Sega - Saturn"),
        new("psx", "Playstation", "mednafen_psx", "Sony - PlayStation"),
    ];
}
