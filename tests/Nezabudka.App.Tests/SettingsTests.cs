using Nezabudka.App.Models;
using Nezabudka.App.Services;

namespace Nezabudka.App.Tests;

public sealed class SettingsTests
{
    [Theory]
    [InlineData(AppLanguage.Russian)]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Chinese)]
    public void SavingLanguageAndThemePreservesBothPreferences(AppLanguage language)
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            var settings = new AppSettingsService(settingsPath);
            settings.SaveTheme(AppTheme.Pink);
            settings.SaveLanguage(language);

            var reloaded = new AppSettingsService(settingsPath);
            Assert.Equal(AppTheme.Pink, reloaded.LoadTheme());
            Assert.Equal(language, reloaded.LoadLanguage());

            reloaded.SaveTheme(AppTheme.White);
            var reloadedAgain = new AppSettingsService(settingsPath);
            Assert.Equal(language, reloadedAgain.LoadLanguage());
            Assert.Equal(AppTheme.White, reloadedAgain.LoadTheme());
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    [Theory]
    [InlineData("{\"Theme\":\"Pink\"}", AppTheme.Pink)]
    [InlineData("{\"Theme\":\"Pink\",\"Language\":\"Unsupported\"}", AppTheme.Pink)]
    [InlineData("{\"Theme\":\"999\",\"Language\":\"999\"}", AppTheme.Black)]
    [InlineData("{not-json", AppTheme.Black)]
    public void OldOrInvalidSettingsHaveSafeLanguageDefault(string content, AppTheme expectedTheme)
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            Directory.CreateDirectory(testDirectory);
            File.WriteAllText(settingsPath, content);
            var settings = new AppSettingsService(settingsPath);

            Assert.Equal(AppLanguage.Russian, settings.LoadLanguage());
            Assert.Equal(expectedTheme, settings.LoadTheme());
            settings.SaveLanguage(AppLanguage.Chinese);
            Assert.Equal(expectedTheme, settings.LoadTheme());
            Assert.Equal(AppLanguage.Chinese, settings.LoadLanguage());
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    [Fact]
    public void ThemeChoiceSurvivesRestart()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            var settings = new AppSettingsService(settingsPath);
            settings.SaveTheme(AppTheme.Pink);

            var reloadedSettings = new AppSettingsService(settingsPath);
            Assert.Equal(AppTheme.Pink, reloadedSettings.LoadTheme());
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    [Fact]
    public void StorageAndBehaviorPreferencesSurviveRestart()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(testDirectory, "settings.json");
        var dataDirectory = Path.Combine(testDirectory, "PortableNotes");

        try
        {
            var settings = new AppSettingsService(settingsPath);
            settings.SaveDataDirectory(dataDirectory);
            settings.SaveAutoSaveEnabled(false);
            settings.SaveGlobalHotkeysEnabled(false);

            var reloaded = new AppSettingsService(settingsPath);
            Assert.Equal(Path.GetFullPath(dataDirectory), reloaded.LoadDataDirectory());
            Assert.False(reloaded.LoadAutoSaveEnabled());
            Assert.False(reloaded.LoadGlobalHotkeysEnabled());
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }
}
