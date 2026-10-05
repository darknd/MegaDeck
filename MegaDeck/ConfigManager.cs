using System.Text.Json;

namespace MegaDeck
{
    public static class ConfigManager
    {
        public static AppConfig LoadConfig()
        {
            if (File.Exists(AppPaths.ConfigFile))
            {
                try
                {
                    string json = File.ReadAllText(AppPaths.ConfigFile);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch (JsonException)
                {
                    return new AppConfig();
                }
            }
            return new AppConfig();
        }

        public static void SaveConfig(AppConfig config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.ConfigFile, json);
        }
    }
}
