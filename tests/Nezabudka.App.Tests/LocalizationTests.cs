using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Nezabudka.App.Models;
using Nezabudka.App.Services;

namespace Nezabudka.App.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EveryLanguageHasCompleteTranslationsAndMatchingPlaceholders()
    {
        var reference = new LocalizationService();
        var referenceKeys = reference.Keys.OrderBy(key => key).ToArray();

        Assert.NotEmpty(referenceKeys);

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var localized = new LocalizationService();
            localized.SetLanguage(language);
            Assert.Equal(referenceKeys, localized.Keys.OrderBy(key => key));

            foreach (var key in referenceKeys)
            {
                var translated = localized.Get(key);
                Assert.False(string.IsNullOrWhiteSpace(translated), $"{language}: {key} is empty");
                Assert.DoesNotContain($"[{key}]", translated);
                var referenceFormat = CompositeFormat.Parse(reference.Get(key));
                var translatedFormat = CompositeFormat.Parse(translated);
                Assert.Equal(referenceFormat.MinimumArgumentCount, translatedFormat.MinimumArgumentCount);
                Assert.Equal(PlaceholderIndices(reference.Get(key)), PlaceholderIndices(translated));
            }
        }
    }

    [Theory]
    [InlineData(AppLanguage.Russian, "ru-RU", "Незабудка", "Изменено 12,5")]
    [InlineData(AppLanguage.English, "en-US", "Forget-Me-Not", "Edited 12.5")]
    [InlineData(AppLanguage.Chinese, "zh-CN", "勿忘我", "修改于 12.5")]
    public void LanguageControlsTextAndFormatting(AppLanguage language, string culture, string name, string formatted)
    {
        var localized = new LocalizationService();
        localized.SetLanguage(language);

        Assert.Equal(culture, localized.Culture.Name);
        Assert.Equal(name, localized["AppName"]);
        Assert.Equal(formatted, localized.Format("NoteUpdated", 12.5m));
        Assert.Equal("[MissingKey]", localized.Get("MissingKey"));

        var date = new DateTime(2026, 9, 14, 21, 30, 0);
        var dateText = date.ToString(localized.Get("ReminderDateFormat"), localized.Culture);
        Assert.Equal(date.Date, DateTime.ParseExact(dateText, localized.Get("ReminderDateFormat"), localized.Culture).Date);
    }

    [Fact]
    public void LanguageSwitchNotifiesBindingsOnceAndKeepsOtherInstancesIndependent()
    {
        var localized = new LocalizationService();
        var untouched = new LocalizationService();
        var originalCulture = CultureInfo.CurrentCulture;
        var notifications = new List<string?>();
        var changes = 0;
        localized.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        localized.LanguageChanged += (_, _) => changes++;

        localized.SetLanguage(AppLanguage.Chinese);
        localized.SetLanguage(AppLanguage.Chinese);

        Assert.Equal(1, changes);
        Assert.Single(notifications, property => property == "Item[]");
        Assert.Contains(nameof(LocalizationService.Culture), notifications);
        Assert.Contains(nameof(LocalizationService.CurrentLanguage), notifications);
        Assert.Equal(AppLanguage.Russian, untouched.CurrentLanguage);
        Assert.Equal(originalCulture, CultureInfo.CurrentCulture);
    }

    private static IEnumerable<string> PlaceholderIndices(string text) => Regex.Matches(text, @"\{(\d+)(?:[^}]*)\}")
        .Select(match => match.Groups[1].Value)
        .OrderBy(value => value);
}
