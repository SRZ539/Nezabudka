using System.IO;
using System.Text.Json;
using Nezabudka.App.Models;

namespace Nezabudka.App.Services;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;
    private readonly object _sync = new();

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

    public string LoadDataDirectory()
    {
        var configured = LoadSettings().DataDirectory;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return NoteStorageService.DefaultDataDirectory;
        }

        try
        {
            return Path.GetFullPath(configured);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return NoteStorageService.DefaultDataDirectory;
        }
    }

    public void SaveDataDirectory(string? directory)
    {
        var settings = LoadSettings();
        var normalized = string.IsNullOrWhiteSpace(directory)
            ? null
            : Path.GetFullPath(directory);
        settings.DataDirectory = string.Equals(
            normalized,
            NoteStorageService.DefaultDataDirectory,
            StringComparison.OrdinalIgnoreCase)
                ? null
                : normalized;
        SaveSettings(settings);
    }

    public bool LoadAutoSaveEnabled() => LoadSettings().AutoSaveEnabled;

    public void SaveAutoSaveEnabled(bool value)
    {
        var settings = LoadSettings();
        settings.AutoSaveEnabled = value;
        SaveSettings(settings);
    }

    public bool LoadGlobalHotkeysEnabled() => LoadSettings().GlobalHotkeysEnabled;

    public void SaveGlobalHotkeysEnabled(bool value)
    {
        var settings = LoadSettings();
        settings.GlobalHotkeysEnabled = value;
        SaveSettings(settings);
    }

    private AppSettingsData LoadSettings()
    {
        lock (_sync)
        {
            return LoadSettingsCore();
        }
    }

    private AppSettingsData LoadSettingsCore()
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
        lock (_sync)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = _settingsPath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings));
                File.Move(temporaryPath, _settingsPath, true);
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
    }
}
