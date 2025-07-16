using System.IO;
using System.Text.Json;

namespace MegaDeck
{
    public static class ConfigManager
    {
        private static string ConfigPath => "config.json";

        public static AppConfig LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            return new AppConfig();
        }

        public static void SaveConfig(AppConfig config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
