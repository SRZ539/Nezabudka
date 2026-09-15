namespace Nezabudka.App.Models;

public sealed class AppSettingsData
{
    public string Theme { get; set; } = AppTheme.Black.ToString();

    public string Language { get; set; } = AppLanguage.Russian.ToString();

    public string? DataDirectory { get; set; }

    public bool AutoSaveEnabled { get; set; } = true;

    public bool GlobalHotkeysEnabled { get; set; } = true;
}
