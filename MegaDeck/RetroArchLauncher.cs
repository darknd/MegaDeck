using System.Diagnostics;
using System.Text;
using MegaDeck.Models;

namespace MegaDeck
{
    /// <summary>
    /// Lanza RetroArch incluido en la carpeta del programa:
    /// engine/ en Windows y engine-linux/ en Linux.
    /// </summary>
    public static class RetroArchLauncher
    {
        /// <summary>
        /// Lanza <paramref name="contentPath"/> (la ROM, o el .cue/.chd extraído de un .zip)
        /// con el core del sistema. Devuelve un mensaje de error o null si ha arrancado.
        /// </summary>
        public static string? Launch(GameSystem system, string contentPath)
        {
            if (!File.Exists(contentPath))
                return "Game file not found.";

            if (OperatingSystem.IsWindows())
                return LaunchWindows(system, contentPath);
            if (OperatingSystem.IsLinux())
                return LaunchLinux(system, contentPath);

            return "This operating system is not supported.";
        }

        private static string? LaunchWindows(GameSystem system, string contentPath)
        {
            string engine = AppPaths.EngineDir;
            string retroarch = Path.Combine(engine, "retroarch.exe");
            string core = Path.Combine(engine, "cores", $"{system.Core}_libretro.dll");

            return Start(retroarch, core, engine, ["-c", Path.Combine(engine, "retroarch.cfg")], contentPath);
        }

        private static string? LaunchLinux(GameSystem system, string contentPath)
        {
            string engine = AppPaths.EngineLinuxDir;
            string retroarch = Path.Combine(engine, "bin", "retroarch");
            string core = Path.Combine(engine, "cores", $"{system.Core}_libretro.so");

            if (File.Exists(retroarch))
                EnsureExecutable(retroarch);

            string pathsCfg;
            try
            {
                pathsCfg = WriteLinuxPathsConfig();
            }
            catch (Exception ex)
            {
                return $"Could not write RetroArch config:\n{ex.Message}";
            }

            return Start(retroarch, core, engine,
                ["-c", Path.Combine(engine, "retroarch.cfg"), "--appendconfig", pathsCfg], contentPath);
        }

        private static string? Start(string retroarch, string core, string workingDir, string[] configArgs, string contentPath)
        {
            if (!File.Exists(retroarch))
                return $"RetroArch executable not found:\n{retroarch}";
            if (!File.Exists(core))
                return $"Core not found:\n{core}";

            var startInfo = new ProcessStartInfo
            {
                FileName = retroarch,
                WorkingDirectory = workingDir,
                UseShellExecute = false
            };
            foreach (var arg in configArgs)
                startInfo.ArgumentList.Add(arg);
            startInfo.ArgumentList.Add("-L");
            startInfo.ArgumentList.Add(core);
            startInfo.ArgumentList.Add(contentPath);
            startInfo.ArgumentList.Add("-f");

            try
            {
                Process.Start(startInfo);
                return null;
            }
            catch (Exception ex)
            {
                return $"Error launching the game:\n{ex.Message}";
            }
        }

        /// <summary>
        /// En Windows el retroarch.cfg usa rutas ":\..." relativas al exe. En Linux eso
        /// apuntaría a engine-linux/bin, así que se generan rutas absolutas en cada lanzamiento.
        /// BIOS, saves, states, shaders y overrides se comparten con la versión de Windows (engine/).
        /// </summary>
        private static string WriteLinuxPathsConfig()
        {
            string shared = AppPaths.EngineDir;
            string linux = AppPaths.EngineLinuxDir;

            var dirs = new (string Key, string Path)[]
            {
                ("system_directory", Path.Combine(shared, "system")),
                ("savefile_directory", Path.Combine(shared, "saves")),
                ("savestate_directory", Path.Combine(shared, "states")),
                ("rgui_config_directory", Path.Combine(shared, "config")),
                ("input_remapping_directory", Path.Combine(shared, "config", "remaps")),
                ("video_shader_dir", Path.Combine(shared, "shaders")),
                ("content_database_path", Path.Combine(shared, "database", "rdb")),
                ("cursor_directory", Path.Combine(shared, "database", "cursors")),
                ("screenshot_directory", Path.Combine(shared, "screenshots")),
                ("libretro_directory", Path.Combine(linux, "cores")),
                ("libretro_info_path", Path.Combine(linux, "info")),
                ("joypad_autoconfig_dir", Path.Combine(linux, "autoconfig")),
                ("assets_directory", Path.Combine(linux, "assets")),
                ("core_assets_directory", Path.Combine(linux, "downloads")),
                ("playlist_directory", Path.Combine(linux, "playlists")),
                ("thumbnails_directory", Path.Combine(linux, "thumbnails")),
                ("log_dir", Path.Combine(linux, "logs")),
                ("cache_directory", Path.Combine(linux, "cache")),
                ("content_history_path", Path.Combine(linux, "content_history.lpl")),
                ("content_favorites_path", Path.Combine(linux, "content_favorites.lpl")),
                ("content_image_history_path", Path.Combine(linux, "content_image_history.lpl")),
                ("content_music_history_path", Path.Combine(linux, "content_music_history.lpl")),
                ("content_video_history_path", Path.Combine(linux, "content_video_history.lpl")),
            };

            var sb = new StringBuilder();
            foreach (var (key, path) in dirs)
            {
                // RetroArch ignora los directorios que no existen (p. ej. states/ la primera vez).
                if (!path.EndsWith(".lpl"))
                    Directory.CreateDirectory(path);
                sb.Append(key).Append(" = \"").Append(path).Append("\"\n");
            }

            string cfgPath = Path.Combine(linux, "megadeck_paths.cfg");
            File.WriteAllText(cfgPath, sb.ToString());
            return cfgPath;
        }

        private static void EnsureExecutable(string path)
        {
            if (OperatingSystem.IsWindows())
                return;

            var mode = File.GetUnixFileMode(path);
            const UnixFileMode exec = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                try
                {
                    File.SetUnixFileMode(path, mode | exec);
                }
                catch (UnauthorizedAccessException)
                {
                    // Start() dará el error correspondiente.
                }
            }
        }
    }
}
