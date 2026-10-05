namespace MegaDeck.Models;

/// <summary>
/// Sistema soportado. <see cref="Core"/> es el nombre del core libretro sin
/// el sufijo "_libretro" ni la extensión (.dll / .so).
/// </summary>
public sealed record GameSystem(string Id, string DisplayName, string Core)
{
    public static readonly IReadOnlyList<GameSystem> All =
    [
        new("segacd", "Sega CD", "genesis_plus_gx"),
        new("pcecd", "PC-Engine CD", "mednafen_pce"),
        new("pcfx", "PC-FX", "mednafen_pcfx"),
        new("saturn", "Sega Saturn", "mednafen_saturn"),
        new("psx", "Playstation", "mednafen_psx"),
    ];
}
