using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LocalizationSmoke;

internal static class VisualChecks
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static IReadOnlyList<DependencyObject> Descendants(DependencyObject root)
    {
        var found = new HashSet<DependencyObject>();
        void Visit(DependencyObject value)
        {
            if (!found.Add(value))
            {
                return;
            }

            if (value is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(value); index++)
                {
                    Visit(VisualTreeHelper.GetChild(value, index));
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(value).OfType<DependencyObject>())
            {
                Visit(child);
            }

            if (value is Popup { Child: { } popupChild })
            {
                Visit(popupChild);
            }

            if (value is FrameworkElement { ContextMenu: { } menu })
            {
                Visit(menu);
            }
        }

        Visit(root);
        return found.ToArray();
    }

    public static IEnumerable<string> Strings(DependencyObject root)
    {
        foreach (var item in Descendants(root))
        {
            if (item is TextBlock { Text: { Length: > 0 } text })
            {
                yield return text;
            }

            if (item is ContentControl { Content: string content })
            {
                yield return content;
            }

            if (item is HeaderedItemsControl { Header: string header })
            {
                yield return header;
            }

            if (item is FrameworkElement { ToolTip: string tooltip })
            {
                yield return tooltip;
            }
        }
    }

    public static void Layout(FrameworkElement root, double width, double height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        DrainDispatcher();
        root.UpdateLayout();
    }

    public static void DrainDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    public static void Render(FrameworkElement root, string outputFile)
    {
        Require(root.ActualWidth > 0 && root.ActualHeight > 0, $"Empty rendering surface: {outputFile}");
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputFile);
        encoder.Save(stream);
    }

    public static void VerifyButtonFits(FrameworkElement button, FrameworkElement surface)
    {
        Require(button.ActualWidth > 0 && button.ActualHeight > 0, $"Control has no layout: {button.Name}");
        var bounds = button.TransformToAncestor(surface).TransformBounds(new Rect(button.RenderSize));
        Require(bounds.Left >= -1 && bounds.Top >= -1 &&
                bounds.Right <= surface.ActualWidth + 1 && bounds.Bottom <= surface.ActualHeight + 1,
            $"Control lies outside minimum window: {button.Name} at {bounds}");

        foreach (var label in Descendants(button).OfType<TextBlock>())
        {
            if (string.IsNullOrEmpty(label.Text) || label.TextWrapping != TextWrapping.NoWrap)
            {
                continue;
            }

            var text = new FormattedText(label.Text, label.Language.GetEquivalentCulture(),
                label.FlowDirection, new Typeface(label.FontFamily, label.FontStyle,
                    label.FontWeight, label.FontStretch), label.FontSize, Brushes.Black, 1);
            Require(text.Width <= label.ActualWidth + 3,
                $"Clipped button label: '{label.Text}' needs {text.Width:F1}px but has {label.ActualWidth:F1}px");
        }
    }
}

internal sealed class BindingErrorCollector : TraceListener
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public override void Write(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _messages.Add(message);
        }
    }

    public override void WriteLine(string? message) => Write(message);
}
