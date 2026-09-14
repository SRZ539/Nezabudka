using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Nezabudka.App;
using Nezabudka.App.Models;
using Nezabudka.App.Services;
using Nezabudka.App.ViewModels;
using static LocalizationSmoke.VisualChecks;

namespace LocalizationSmoke;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Run(args);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(string[] args)
    {
        string? screenshots = null;
        if (args.Length == 2 && args[0] == "--screenshots")
        {
            screenshots = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(screenshots);
        }
        else
        {
            Require(args.Length == 0, "Usage: LocalizationSmoke [--screenshots <directory>]");
        }

        var app = new App();
        app.InitializeComponent();

        var bindingErrors = new BindingErrorCollector();
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        var dataDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.LocalizationSmoke", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(dataDirectory, "settings.json");
        var settings = new AppSettingsService(settingsPath);
        using var viewModel = new MainViewModel(
            new NoteStorageService(Path.Combine(dataDirectory, "notes.json")),
            settings);
        var window = new MainWindow(viewModel, enableDesktopIntegration: false);
        var surface = (FrameworkElement)window.Content;
        var note = new NoteItemViewModel(new NoteData
        {
            Title = "Sample note — 123",
            Content = "User-authored content stays unchanged when the interface language changes.",
            Status = NoteStatus.Waiting,
            ReminderAt = new DateTime(2030, 5, 20, 15, 27, 43)
        });
        viewModel.Notes.Add(note);
        viewModel.SelectedNote = note;

        // Construct the actual contextual note menu without simulating a mouse or opening it.
        var menuFactory = typeof(MainWindow).GetMethod("CreateNoteContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(menuFactory is not null, "Missing note context menu factory");
        var noteMenu = (ContextMenu)menuFactory!.Invoke(window, null)!;
        noteMenu.DataContext = note;
        Layout(surface, window.MinWidth, window.MinHeight);

        var localization = LocalizationService.Instance;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var languageButtonName = language switch
            {
                AppLanguage.English => "EnglishLanguageButton",
                AppLanguage.Chinese => "ChineseLanguageButton",
                _ => "RussianLanguageButton"
            };
            ((RadioButton)window.FindName(languageButtonName)).IsChecked = true;
            DrainDispatcher();
            Require(localization.CurrentLanguage == language, $"Language button did not switch to {language}");
            Layout(surface, window.MinWidth, window.MinHeight);

            var strings = Strings(surface).Concat(Strings(noteMenu)).Distinct().ToArray();
            Require(window.Title == localization.Get("AppName"), $"Window title did not switch to {language}");
            foreach (var key in new[] { "NewNote", "MyNotes", "Language", "Reminder", "ExactTime", "DeleteNoteTitle", "FooterCredit" })
            {
                Require(strings.Contains(localization.Get(key)), $"Missing or stale {language} label: {key}");
            }

            Require(note.StatusDisplay == localization.Get("StatusWaiting"), $"Note status did not refresh: {language}");
            Require(note.Title == "Sample note — 123" &&
                    note.Content == "User-authored content stays unchanged when the interface language changes.",
                "Switching language changed user-authored text");

            Require(strings.All(value => !value.StartsWith('[') || !value.EndsWith(']')),
                $"Missing resource placeholder in {language} UI");
            if (language != AppLanguage.Russian)
            {
                var leftovers = strings.Where(value => value != "Русский" && value.Any(character => character is >= '\u0400' and <= '\u04ff')).ToArray();
                var origins = DescribeCyrillic(surface).Concat(DescribeCyrillic(noteMenu)).ToArray();
                Require(leftovers.Length == 0, $"Untranslated Russian in {language}: {string.Join(" | ", origins)}");
            }

            foreach (var name in new[]
                     {
                         "BrandPanel", "ThemeSelector", "CalculatorButton", "NewNoteButton", "WindowButtons",
                         "RussianLanguageButton", "EnglishLanguageButton", "ChineseLanguageButton",
                         "StatusPickerButton", "DatePickerButton", "TimePickerButton"
                     })
            {
                Require(window.FindName(name) is FrameworkElement, $"Missing UI control {name}");
                VerifyButtonFits((FrameworkElement)window.FindName(name), surface);
            }

            var calendar = (Calendar)window.FindName("ReminderCalendar");
            Require(calendar.Language.GetEquivalentCulture().Name == localization.Culture.Name,
                $"Calendar language is stale: {language}: {calendar.Language}");

            foreach (var popup in Descendants(surface).OfType<Popup>().Where(popup => popup.Child is FrameworkElement))
            {
                var child = (FrameworkElement)popup.Child;
                child.Measure(new Size(600, 620));
                child.Arrange(new Rect(new Point(), child.DesiredSize));
                child.UpdateLayout();
                Require(child.ActualWidth > 0 && child.ActualHeight > 0, "Empty localized popup");
            }

            if (language == AppLanguage.Chinese)
            {
                VerifyChineseGlyphs(localization);
            }

            if (screenshots is not null)
            {
                Render(surface, Path.Combine(screenshots, $"{localization.Culture.Name}-minimum.png"));
                var popupIndex = 0;
                foreach (var popup in Descendants(surface).OfType<Popup>().Where(popup => popup.Child is FrameworkElement))
                {
                    Render((FrameworkElement)popup.Child, Path.Combine(screenshots, $"{localization.Culture.Name}-popup-{popupIndex++}.png"));
                }
            }

            Console.WriteLine($"PASS {localization.Culture.Name}: language button, live labels, preserved user text, minimum layout, popup layout, calendar culture");
        }

        Require(bindingErrors.Messages.Count == 0, "WPF binding errors:" + Environment.NewLine + string.Join(Environment.NewLine, bindingErrors.Messages));
        Require(settings.LoadLanguage() == AppLanguage.Chinese, "Language selection was not persisted");
        window.Close();
        app.Shutdown();
        Directory.Delete(dataDirectory, recursive: true);
        Console.WriteLine("PASS isolated WPF localization smoke check; no desktop window, tray, or user data accessed");
    }

    private static void VerifyChineseGlyphs(LocalizationService localization)
    {
        var font = new FontFamily(new Uri("pack://application:,,,/Nezabudka;component/Assets/Fonts/"), "./#Noto Sans SC");
        var typeface = new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Require(typeface.TryGetGlyphTypeface(out var glyphs), "Bundled Noto Sans SC could not load");
        var chineseCharacters = localization.Keys.SelectMany(key => localization.Get(key))
            .Where(character => character is >= '\u3400' and <= '\u9fff').Distinct().ToArray();
        var missing = chineseCharacters.Where(character => !glyphs.CharacterToGlyphMap.TryGetValue(character, out var glyph) || glyph == 0).ToArray();
        Require(missing.Length == 0, $"Bundled Chinese font lacks glyphs: {new string(missing)}");
        Console.WriteLine($"PASS bundled Chinese font: {chineseCharacters.Length} translated Han characters have glyphs");
    }

    private static IEnumerable<string> DescribeCyrillic(DependencyObject root)
    {
        foreach (var item in Descendants(root))
        {
            if (item is ContentControl { Content: string content } && content.Any(character => character is >= '\u0400' and <= '\u04ff'))
            {
                yield return $"{item.GetType().Name}.Content={content}";
            }

            if (item is FrameworkElement { ToolTip: string tooltip } && tooltip.Any(character => character is >= '\u0400' and <= '\u04ff'))
            {
                yield return $"{item.GetType().Name}.ToolTip={tooltip}";
            }
        }
    }
}
