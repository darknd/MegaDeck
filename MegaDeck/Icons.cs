using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MegaDeck;

/// <summary>Iconos pixel-art de Assets/, cargados una sola vez.</summary>
public static class Icons
{
    public static Bitmap App16 { get; } = Load("megadeck16.png");
    public static Bitmap Cd16 { get; } = Load("cd16.png");
    public static Bitmap Cd32 { get; } = Load("cd32.png");
    public static Bitmap Cd48 { get; } = Load("cd48.png");

    private static Bitmap Load(string name) =>
        new(AssetLoader.Open(new Uri($"avares://MegaDeck/Assets/{name}")));
}
