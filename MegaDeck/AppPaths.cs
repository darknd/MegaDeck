namespace MegaDeck;

/// <summary>
/// Todas las rutas cuelgan de la carpeta del ejecutable (instalación portable),
/// sin depender del directorio de trabajo con el que se lance el programa.
/// </summary>
public static class AppPaths
{
    public static string BaseDir { get; } = AppContext.BaseDirectory;

    public static string ConfigFile => Path.Combine(BaseDir, "config.json");
    public static string TitleCacheFile => Path.Combine(BaseDir, "rom_title_cache.json");
    public static string ImageMapFile => Path.Combine(BaseDir, "rom_image_map.json");
    public static string ImagesDir => Path.Combine(BaseDir, "images");
    public static string ExtractCacheDir => Path.Combine(BaseDir, "cache");

    /// <summary>RetroArch de Windows. También guarda BIOS, saves, shaders y config compartidos con Linux.</summary>
    public static string EngineDir => Path.Combine(BaseDir, "engine");

    /// <summary>RetroArch de Linux (binario, librerías, cores .so, autoconfig).</summary>
    public static string EngineLinuxDir => Path.Combine(BaseDir, "engine-linux");
}
