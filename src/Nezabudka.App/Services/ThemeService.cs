using System.Windows;
using Nezabudka.App.Models;

namespace Nezabudka.App.Services;

public static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        var resources = System.Windows.Application.Current.Resources;
        var dictionaries = resources.MergedDictionaries;
        var themeDictionary = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/Nezabudka;component/Themes/{theme}Theme.xaml",
                UriKind.Absolute)
        };

        var existingIndex = dictionaries
            .Select((dictionary, index) => new { dictionary, index })
            .FirstOrDefault(item => item.dictionary.Source?.OriginalString.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true)
            ?.index;

        if (existingIndex.HasValue)
        {
            dictionaries[existingIndex.Value] = themeDictionary;
        }
        else
        {
            dictionaries.Insert(0, themeDictionary);
        }
    }
}
