using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nezabudka.App.Models;
using Nezabudka.App.Services;
using Nezabudka.App.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace Nezabudka.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly System.Drawing.Icon? _appIcon;
    private readonly System.Windows.Forms.NotifyIcon? _trayIcon;
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private readonly GlobalHotkeyService? _globalHotkeys;
    private NoteItemViewModel? _pendingDeletion;
    private int _lastEditorSelectionStart;
    private int _lastEditorSelectionLength;
    private bool _allowClose;
    private bool _hasThemeSnapshot;
    private bool _trayHintShown;

    public MainWindow() : this(new MainViewModel(), true)
    {
    }

    public MainWindow(MainViewModel viewModel, bool enableDesktopIntegration = false)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        ConfigureKeyboardShortcuts();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _localization.LanguageChanged += Localization_LanguageChanged;
        ApplyLanguage();
        ConfigureTextMenu(SearchBox);
        ConfigureTextMenu(TitleEditor);
        ConfigureTextMenu(ContentEditor);
        if (!enableDesktopIntegration)
        {
            return;
        }

        _globalHotkeys = new GlobalHotkeyService();
        _globalHotkeys.ToggleWindowRequested += ToggleMainWindow;
        _globalHotkeys.NewNoteRequested += CreateNoteFromGlobalHotkey;
        _viewModel.GlobalHotkeysSettingChanged += GlobalHotkeysSettingChanged;
        SourceInitialized += MainWindow_SourceInitialized;

        _appIcon = LoadAppIcon();

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = _localization.Get("AppName"),
            Visible = true,
            ContextMenuStrip = CreateTrayMenu()
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        _trayIcon.BalloonTipClicked += (_, _) => ShowMainWindow();
        _viewModel.RemindersDue += ShowReminderNotification;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private System.Windows.Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(_localization.Get("TrayOpen"), null, (_, _) => ShowMainWindow());
        menu.Items.Add(_localization.Get("NewNote"), null, (_, _) => Dispatcher.Invoke(() =>
        {
            ShowMainWindow();
            _viewModel.CreateNote();
        }));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(_localization.Get("TrayExit"), null, async (_, _) => await ExitApplicationAsync());
        return menu;
    }

    private void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void ToggleMainWindow()
    {
        if (IsVisible && IsActive)
        {
            Hide();
            return;
        }

        ShowMainWindow();
    }

    private void CreateNoteFromGlobalHotkey()
    {
        ShowMainWindow();
        _viewModel.CreateNote();
        Dispatcher.BeginInvoke(() =>
        {
            TitleEditor.Focus();
            TitleEditor.SelectAll();
        });
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _globalHotkeys?.Attach(this);
        ApplyGlobalHotkeySetting(_viewModel.IsGlobalHotkeysEnabled, showError: true);
    }

    private void GlobalHotkeysSettingChanged(bool enabled) => ApplyGlobalHotkeySetting(enabled, showError: true);

    private void ApplyGlobalHotkeySetting(bool enabled, bool showError)
    {
        if (_globalHotkeys is null || _globalHotkeys.SetEnabled(enabled) || !enabled)
        {
            return;
        }

        _viewModel.IsGlobalHotkeysEnabled = false;
        if (showError)
        {
            MessageBox.Show(
                _localization.Get("GlobalHotkeysUnavailable"),
                _localization.Get("GlobalHotkeysSetting"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    internal void RestoreFromSecondaryLaunch()
    {
        ShowMainWindow();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        var resourceUri = new Uri(
            "pack://application:,,,/Nezabudka;component/Assets/nezabudka.ico",
            UriKind.Absolute);
        var resource = System.Windows.Application.GetResourceStream(resourceUri)
                       ?? throw new InvalidOperationException(LocalizationService.Instance.Get("IconLoadError"));

        using var stream = resource.Stream;
        using var icon = new System.Drawing.Icon(stream);
        return (System.Drawing.Icon)icon.Clone();
    }

    private void ShowReminderNotification(IReadOnlyList<NoteItemViewModel> notes)
    {
        if (_trayIcon is null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            _trayIcon.BalloonTipTitle = notes.Count == 1
                ? _localization.Get("ReminderDueSingle")
                : _localization.Format("ReminderDueMultiple", notes.Count);
            _trayIcon.BalloonTipText = notes.Count == 1
                ? notes[0].DisplayTitle
                : string.Join(Environment.NewLine, notes.Take(3).Select(note => $"• {note.DisplayTitle}"));
            _trayIcon.ShowBalloonTip(7000);
        });
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CreateNote();
    }

    private void ContentEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        _lastEditorSelectionStart = ContentEditor.SelectionStart;
        _lastEditorSelectionLength = ContentEditor.SelectionLength;
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        ApplyLanguage();
        if (_trayIcon is not null)
        {
            _trayIcon.Text = _localization.Get("AppName");
            var oldMenu = _trayIcon.ContextMenuStrip;
            _trayIcon.ContextMenuStrip = CreateTrayMenu();
            oldMenu?.Dispose();
        }

        if (_pendingDeletion is not null)
        {
            DeletePrompt.Text = _localization.Format("DeleteNotePrompt", _pendingDeletion.DisplayTitle);
        }
    }

    private void ApplyLanguage()
    {
        Language = XmlLanguage.GetLanguage(_localization.Culture.Name);
        var family = _localization.CurrentLanguage == AppLanguage.Chinese
            ? (System.Windows.Media.FontFamily)FindResource("ChineseFont")
            : (System.Windows.Media.FontFamily)FindResource("LatinFont");
        System.Windows.Application.Current.Resources["InterfaceFont"] = family;
        ReminderCalendar.Language = Language;
        ReminderCalendar.FirstDayOfWeek = _localization.Culture.DateTimeFormat.FirstDayOfWeek;
        LocalizeCalendarNavigation();
        Dispatcher.BeginInvoke(LocalizeCalendarNavigation, DispatcherPriority.Loaded);
    }

    private void ConfigureTextMenu(System.Windows.Controls.TextBox textBox)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        foreach (var (key, command) in new[]
        {
            ("Undo", ApplicationCommands.Undo), ("Redo", ApplicationCommands.Redo),
            ("Cut", ApplicationCommands.Cut), ("Copy", ApplicationCommands.Copy),
            ("Paste", ApplicationCommands.Paste), ("SelectAll", ApplicationCommands.SelectAll)
        })
        {
            var item = CreateLocalizedMenuItem(key);
            item.Command = command;
            item.CommandTarget = textBox;
            menu.Items.Add(item);
        }

        textBox.ContextMenu = menu;
    }

    private System.Windows.Controls.MenuItem CreateLocalizedMenuItem(string key)
    {
        var item = new System.Windows.Controls.MenuItem();
        item.SetBinding(HeaderedItemsControl.HeaderProperty, new System.Windows.Data.Binding($"[{key}]")
        {
            Source = _localization,
            Mode = BindingMode.OneWay
        });
        return item;
    }

    private void ReminderCalendar_Loaded(object sender, RoutedEventArgs e) => LocalizeCalendarNavigation();

    private void LocalizeCalendarNavigation()
    {
        ReminderCalendar.ApplyTemplate();
        var calendarItem = FindVisualChild<System.Windows.Controls.Primitives.CalendarItem>(ReminderCalendar);
        if (calendarItem is null)
        {
            return;
        }

        calendarItem.ApplyTemplate();
        foreach (var (name, key) in new[] { ("PART_PreviousButton", "CalendarPrevious"), ("PART_NextButton", "CalendarNext") })
        {
            if (calendarItem.Template.FindName(name, calendarItem) is System.Windows.Controls.Button button)
            {
                button.Content = key == "CalendarPrevious" ? "‹" : "›";
                button.ToolTip = _localization.Get(key);
                System.Windows.Automation.AutomationProperties.SetName(button, _localization.Get(key));
            }
        }
    }

    private void DatePickerDone_Click(object sender, RoutedEventArgs e) => DatePickerButton.IsChecked = false;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedNote))
        {
            DatePickerButton.IsChecked = false;
            ReminderCalendar.DisplayDate = _viewModel.SelectedNote?.ReminderDate ?? DateTime.Today;
        }
    }

    private void ThemeRadio_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton { IsChecked: true })
        {
            return;
        }

        CaptureThemeSnapshot();
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (!_hasThemeSnapshot)
        {
            WindowFrame.BeginAnimation(OpacityProperty, SmoothAnimation(0.82, 1, 300));
            return;
        }

        Dispatcher.BeginInvoke(BeginThemeCrossFade, DispatcherPriority.Render);
    }

    private void CaptureThemeSnapshot()
    {
        if (!IsLoaded || AppSurface.ActualWidth < 1 || AppSurface.ActualHeight < 1)
        {
            return;
        }

        ThemeTransitionSnapshot.BeginAnimation(OpacityProperty, null);
        ThemeTransitionSnapshot.Visibility = Visibility.Collapsed;
        ThemeTransitionSnapshot.Source = null;

        var dpi = VisualTreeHelper.GetDpi(AppSurface);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(AppSurface.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(AppSurface.ActualHeight * dpi.DpiScaleY)),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        bitmap.Render(AppSurface);
        bitmap.Freeze();

        ThemeTransitionSnapshot.Source = bitmap;
        ThemeTransitionSnapshot.Opacity = 1;
        ThemeTransitionSnapshot.Visibility = Visibility.Collapsed;
        _hasThemeSnapshot = true;
    }

    private void BeginThemeCrossFade()
    {
        ThemeTransitionSnapshot.Visibility = Visibility.Visible;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(340))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        fade.Completed += (_, _) =>
        {
            ThemeTransitionSnapshot.Visibility = Visibility.Collapsed;
            ThemeTransitionSnapshot.Source = null;
            _hasThemeSnapshot = false;
        };
        ThemeTransitionSnapshot.BeginAnimation(OpacityProperty, fade);
    }

    private void Calculator_Click(object sender, RoutedEventArgs e)
    {
        ToggleCalculatorAnimated();
    }

    private void CalculatorClose_Click(object sender, RoutedEventArgs e)
    {
        CloseCalculatorAnimated();
    }

    private void CalculatorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string key })
        {
            _viewModel.PressCalculatorKey(key);
        }
    }

    private void StatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string statusName } &&
            Enum.TryParse<NoteStatus>(statusName, true, out var status))
        {
            _viewModel.SetSelectedStatus(status);
        }
    }

    private void TimeStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string instruction })
        {
            return;
        }

        var parts = instruction.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out var delta))
        {
            _viewModel.AdjustSelectedReminderTime(parts[0], delta);
        }
    }

    private void TimePickerDone_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsTimePickerOpen = false;
    }

    private void TimePart_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string part } && e.Delta != 0)
        {
            _viewModel.AdjustSelectedReminderTime(part, e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    private void InsertCalculation_Click(object sender, RoutedEventArgs e)
    {
        var caret = _viewModel.InsertCalculationIntoNote(
            _lastEditorSelectionStart,
            _lastEditorSelectionLength);
        Dispatcher.BeginInvoke(() =>
        {
            ContentEditor.Focus();
            ContentEditor.Select(Math.Clamp(caret, 0, ContentEditor.Text.Length), 0);
        });
    }

    private void PinSelectedNote_Click(object sender, RoutedEventArgs e) => _viewModel.ToggleSelectedPin();

    private void ClearReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote is not null)
        {
            _viewModel.SelectedNote.HasReminder = false;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
        WindowFrame.Margin = maximized ? new Thickness(0) : new Thickness(10);
        WindowFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(18);
        MaximizeGlyph.Text = maximized ? "❐" : "□";
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote is null)
        {
            return;
        }

        ConfirmAndDelete(_viewModel.SelectedNote);
    }

    private void EditNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { DataContext: NoteItemViewModel note })
        {
            return;
        }

        _viewModel.SelectedNote = note;
        AnimateEditorIn();
        Dispatcher.BeginInvoke(() =>
        {
            TitleEditor.Focus();
            TitleEditor.SelectAll();
        });
    }

    private void DeleteNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: NoteItemViewModel note })
        {
            ConfirmAndDelete(note);
        }
    }

    private void TogglePinNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: NoteItemViewModel note })
        {
            _viewModel.SelectedNote = note;
            _viewModel.ToggleSelectedPin();
        }
    }

    private void NoteItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: NoteItemViewModel note } item)
        {
            item.IsSelected = true;
            item.Focus();
            item.ContextMenu ??= CreateNoteContextMenu();
            item.ContextMenu.DataContext = note;
        }
    }

    private System.Windows.Controls.ContextMenu CreateNoteContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var pinItem = new System.Windows.Controls.MenuItem { Tag = "PinCommand" };
        pinItem.Click += TogglePinNoteMenuItem_Click;
        var editItem = CreateLocalizedMenuItem("Edit");
        editItem.Click += EditNoteMenuItem_Click;

        var deleteItem = CreateLocalizedMenuItem("Delete");
        deleteItem.SetResourceReference(ForegroundProperty, "DangerBrush");
        deleteItem.Click += DeleteNoteMenuItem_Click;

        menu.Opened += (_, _) =>
        {
            if (menu.DataContext is NoteItemViewModel note)
            {
                pinItem.Header = _localization.Get(note.IsPinned ? "UnpinNote" : "PinNote");
            }
        };
        menu.Items.Add(pinItem);
        menu.Items.Add(editItem);
        menu.Items.Add(new Separator { Margin = new Thickness(8, 3, 8, 3) });
        menu.Items.Add(deleteItem);
        return menu;
    }

    private void NoteItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: NoteItemViewModel note })
        {
            _viewModel.SelectedNote = note;
            Dispatcher.BeginInvoke(() => ContentEditor.Focus());
            e.Handled = true;
        }
    }

    private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedNote is not null)
        {
            Dispatcher.BeginInvoke(AnimateEditorIn);
        }
    }

    private void NotesList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(NotesList);
        if (scrollViewer is null || e.Delta == 0)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta / 3.0));
        e.Handled = true;
    }

    private void ConfirmAndDelete(NoteItemViewModel note)
    {
        _viewModel.SelectedNote = note;
        _viewModel.IsStatusPickerOpen = false;
        _viewModel.IsTimePickerOpen = false;
        DatePickerButton.IsChecked = false;
        _pendingDeletion = note;
        DeletePrompt.Text = _localization.Format("DeleteNotePrompt", note.DisplayTitle);
        DeleteConfirmation.Visibility = Visibility.Visible;
        System.Windows.Input.KeyboardNavigation.SetTabNavigation(DeleteConfirmation, KeyboardNavigationMode.Cycle);
        CancelDeleteButton.Focus();
    }

    private void CancelDelete_Click(object sender, RoutedEventArgs e) => CloseDeleteConfirmation();

    private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingDeletion is { } note && _viewModel.Notes.Contains(note))
        {
            _viewModel.SelectedNote = note;
            _viewModel.DeleteSelectedNote();
            AnimateUndoPanelIn();
        }

        CloseDeleteConfirmation();
    }

    private void UndoDelete_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.UndoLastDelete();
        Dispatcher.BeginInvoke(AnimateEditorIn);
    }

    private void AnimateUndoPanelIn()
    {
        UndoDeletePanel.BeginAnimation(OpacityProperty, SmoothAnimation(0, 1, 220));
        UndoDeleteTranslate.BeginAnimation(TranslateTransform.YProperty, SmoothAnimation(14, 0, 250));
    }

    private async void ExportNotes_Click(object sender, RoutedEventArgs e)
    {
        DataMenuButton.IsChecked = false;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _localization.Get("ExportNotes"),
            Filter = _localization.Get("ArchiveFileFilter"),
            DefaultExt = ".nezabudka",
            AddExtension = true,
            FileName = $"Nezabudka-{DateTime.Now:yyyy-MM-dd}.nezabudka"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _viewModel.ExportAsync(dialog.FileName);
            ShowInformation("ExportCompleted", "ExportNotes");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowOperationError(exception);
        }
    }

    private async void ImportNotes_Click(object sender, RoutedEventArgs e)
    {
        DataMenuButton.IsChecked = false;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localization.Get("ImportNotes"),
            Filter = _localization.Get("ArchiveFileFilter"),
            DefaultExt = ".nezabudka",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var choice = MessageBox.Show(
            _localization.Get("ImportModePrompt"),
            _localization.Get("ImportNotes"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (choice == MessageBoxResult.Cancel)
        {
            return;
        }

        try
        {
            var count = await _viewModel.ImportAsync(
                dialog.FileName,
                choice == MessageBoxResult.Yes ? NoteImportMode.Merge : NoteImportMode.Replace);
            MessageBox.Show(
                _localization.Format("ImportCompleted", count),
                _localization.Get("ImportNotes"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ShowOperationError(exception);
        }
    }

    private async void ChangeDataFolder_Click(object sender, RoutedEventArgs e)
    {
        DataMenuButton.IsChecked = false;
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _localization.Get("ChooseDataFolder"),
            UseDescriptionForTitle = true,
            InitialDirectory = _viewModel.DataDirectory,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        try
        {
            await _viewModel.ChangeDataDirectoryAsync(dialog.SelectedPath);
            ShowInformation("DataFolderChanged", "DataManagement");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowOperationError(exception);
        }
    }

    private void RestoreTrashNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: NoteItemViewModel note })
        {
            _viewModel.RestoreFromTrash(note);
            DataMenuButton.IsChecked = false;
            Dispatcher.BeginInvoke(AnimateEditorIn);
        }
    }

    private void EmptyTrash_Click(object sender, RoutedEventArgs e)
    {
        var choice = MessageBox.Show(
            _localization.Get("EmptyTrashPrompt"),
            _localization.Get("EmptyTrashTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Yes)
        {
            _viewModel.EmptyTrash();
        }
    }

    private void ShowInformation(string messageKey, string titleKey) => MessageBox.Show(
        _localization.Get(messageKey),
        _localization.Get(titleKey),
        MessageBoxButton.OK,
        MessageBoxImage.Information);

    private void ShowOperationError(Exception _) => MessageBox.Show(
        _localization.Get("OperationFailed"),
        _localization.Get("DataManagement"),
        MessageBoxButton.OK,
        MessageBoxImage.Error);

    private void CloseDeleteConfirmation()
    {
        _pendingDeletion = null;
        DeleteConfirmation.Visibility = Visibility.Collapsed;
        NotesList.Focus();
    }

    private void ToggleCalculatorAnimated()
    {
        if (_viewModel.IsCalculatorOpen)
        {
            CloseCalculatorAnimated();
            return;
        }

        _viewModel.IsCalculatorOpen = true;
        Dispatcher.BeginInvoke(AnimateCalculatorIn);
    }

    private void AnimateCalculatorIn()
    {
        CalculatorPanel.BeginAnimation(OpacityProperty, SmoothAnimation(0, 1, 220));
        CalculatorScale.BeginAnimation(ScaleTransform.ScaleXProperty, SmoothAnimation(0.96, 1, 240));
        CalculatorScale.BeginAnimation(ScaleTransform.ScaleYProperty, SmoothAnimation(0.96, 1, 240));
        CalculatorTranslate.BeginAnimation(TranslateTransform.XProperty, SmoothAnimation(22, 0, 240));
    }

    private void CloseCalculatorAnimated()
    {
        if (!_viewModel.IsCalculatorOpen)
        {
            return;
        }

        var fadeOut = SmoothAnimation(1, 0, 150);
        fadeOut.Completed += (_, _) =>
        {
            _viewModel.IsCalculatorOpen = false;
            CalculatorPanel.BeginAnimation(OpacityProperty, null);
            CalculatorPanel.Opacity = 1;
        };
        CalculatorPanel.BeginAnimation(OpacityProperty, fadeOut);
        CalculatorTranslate.BeginAnimation(TranslateTransform.XProperty, SmoothAnimation(0, 15, 150));
    }

    private void AnimateEditorIn()
    {
        EditorPanel.BeginAnimation(OpacityProperty, SmoothAnimation(0.48, 1, 230));
        EditorTranslate.BeginAnimation(TranslateTransform.YProperty, SmoothAnimation(8, 0, 240));
    }

    private static DoubleAnimation SmoothAnimation(double from, double to, int milliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DeleteConfirmation.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                CloseDeleteConfirmation();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Escape && DatePickerButton.IsChecked == true)
        {
            DatePickerButton.IsChecked = false;
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Escape &&
            (_viewModel.IsCalculatorOpen || _viewModel.IsStatusPickerOpen || _viewModel.IsTimePickerOpen))
        {
            CloseCalculatorAnimated();
            _viewModel.IsStatusPickerOpen = false;
            _viewModel.IsTimePickerOpen = false;
            e.Handled = true;
            return;
        }

        if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case System.Windows.Input.Key.N:
                _viewModel.CreateNote();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.F:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.K:
                ToggleCalculatorAnimated();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.S:
                _ = _viewModel.SaveAsync();
                e.Handled = true;
                break;
        }
    }

    private void ConfigureKeyboardShortcuts()
    {
        AddKeyboardShortcut(System.Windows.Input.Key.N, () => _viewModel.CreateNote());
        AddKeyboardShortcut(System.Windows.Input.Key.F, () =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        });
        AddKeyboardShortcut(System.Windows.Input.Key.K, ToggleCalculatorAnimated);
        AddKeyboardShortcut(System.Windows.Input.Key.S, () => _ = _viewModel.SaveAsync());
    }

    private void AddKeyboardShortcut(System.Windows.Input.Key key, Action action)
    {
        var command = new System.Windows.Input.RoutedCommand();
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            command,
            (_, _) => action(),
            (_, args) => args.CanExecute = true));
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            command,
            key,
            System.Windows.Input.ModifierKeys.Control));
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();

        if (!_trayHintShown && _trayIcon is not null)
        {
            _trayHintShown = true;
            _trayIcon.BalloonTipTitle = _localization.Get("TrayBackgroundTitle");
            _trayIcon.BalloonTipText = _localization.Get("TrayBackgroundMessage");
            _trayIcon.ShowBalloonTip(5000);
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_viewModel.HasUnsavedChanges && !_viewModel.IsAutoSaveEnabled)
        {
            var choice = MessageBox.Show(
                _localization.Get("SaveBeforeExitPrompt"),
                _localization.Get("UnsavedChangesTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (choice == MessageBoxResult.Cancel)
            {
                return;
            }

            if (choice == MessageBoxResult.Yes)
            {
                await _viewModel.SaveAsync();
            }
        }
        else
        {
            await _viewModel.FlushAsync();
        }

        _allowClose = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
        }
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _localization.LanguageChanged -= Localization_LanguageChanged;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.GlobalHotkeysSettingChanged -= GlobalHotkeysSettingChanged;
        if (_globalHotkeys is not null)
        {
            _globalHotkeys.ToggleWindowRequested -= ToggleMainWindow;
            _globalHotkeys.NewNoteRequested -= CreateNoteFromGlobalHotkey;
            _globalHotkeys.Dispose();
        }
        _viewModel.Dispose();
        _trayIcon?.Dispose();
        _appIcon?.Dispose();
    }
}
