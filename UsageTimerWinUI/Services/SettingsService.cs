using System;
using System.IO;
using System.Text.Json;

namespace UsageTimerWinUI.Services
{
    public static class SettingsService
    {
        public static string Theme { get; private set; } = "System"; // "System", "Light", "Dark"
        public static bool UseMica { get; private set; } = true;

        private static readonly string folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "UsageTimerWinUI");

        private static readonly string path = Path.Combine(folder, "settings.json");

        private class SettingsDto
        {
            public string? Theme { get; set; }
            public bool? UseMica { get; set; }
        }

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(folder);
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<SettingsDto>(json);
                if (dto == null) return;

                if (!string.IsNullOrWhiteSpace(dto.Theme))
                    Theme = dto.Theme;

                if (dto.UseMica.HasValue)
                    UseMica = dto.UseMica.Value;
            }
            catch
            {
                // ignore and keep defaults
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(folder);
                var dto = new SettingsDto
                {
                    Theme = Theme,
                    UseMica = UseMica
                };

                File.WriteAllText(path,
                    JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // ignore
            }
        }

        public static void SetTheme(string theme)
        {
            Theme = theme;
            Save();
        }

        public static void SetUseMica(bool value)
        {
            UseMica = value;
            Save();
        }

        public static string GetDataFolder() => folder;
    }
}