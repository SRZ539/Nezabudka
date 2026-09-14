using System.IO;
using System.Text.Json;
using Nezabudka.App.Models;

namespace Nezabudka.App.Services;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;

    public AppSettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nezabudka",
            "settings.json");
    }

    public AppTheme LoadTheme()
    {
        var settings = LoadSettings();
        return Enum.TryParse<AppTheme>(settings.Theme, true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : AppTheme.Black;
    }

    public AppLanguage LoadLanguage()
    {
        var settings = LoadSettings();
        return Enum.TryParse<AppLanguage>(settings.Language, true, out var language) && Enum.IsDefined(language)
            ? language
            : AppLanguage.Russian;
    }

    public void SaveTheme(AppTheme theme)
    {
        var settings = LoadSettings();
        settings.Theme = theme.ToString();
        SaveSettings(settings);
    }

    public void SaveLanguage(AppLanguage language)
    {
        var settings = LoadSettings();
        settings.Language = language.ToString();
        SaveSettings(settings);
    }

    private AppSettingsData LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettingsData();
            }

            return JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(_settingsPath)) ?? new AppSettingsData();
        }
        catch (JsonException)
        {
            return new AppSettingsData();
        }
        catch (IOException)
        {
            return new AppSettingsData();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettingsData();
        }
    }

    private void SaveSettings(AppSettingsData settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings));
        }
        catch (IOException)
        {
            // A UI preference should never prevent the notebook from working.
        }
        catch (UnauthorizedAccessException)
        {
            // A UI preference should never prevent the notebook from working.
        }
    }

    private sealed class AppSettingsData
    {
        public string Theme { get; set; } = AppTheme.Black.ToString();

        public string Language { get; set; } = AppLanguage.Russian.ToString();
    }
}
