using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Threading;
using Nezabudka.App.Models;
using Nezabudka.App.Services;

namespace Nezabudka.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly NoteStorageService _storage;
    private readonly AppSettingsService _settings;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _reminderTimer;
    private NoteItemViewModel? _selectedNote;
    private string _searchText = string.Empty;
    private string _saveStatusKey = "SaveLoading";
    private AppTheme _currentTheme = AppTheme.Black;
    private bool _isCalculatorOpen;
    private bool _isStatusPickerOpen;
    private bool _isTimePickerOpen;
    private bool _initialized;

    public MainViewModel(NoteStorageService? storage = null, AppSettingsService? settings = null)
    {
        _storage = storage ?? new NoteStorageService();
        _settings = settings ?? new AppSettingsService();
        LocalizationService.Instance.SetLanguage(_settings.LoadLanguage());
        LocalizationService.Instance.LanguageChanged += LanguageChanged;
        _currentTheme = _settings.LoadTheme();
        ThemeService.Apply(_currentTheme);

        NotesView = CollectionViewSource.GetDefaultView(Notes);
        NotesView.Filter = MatchesSearch;
        NotesView.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.UpdatedAt), ListSortDirection.Descending));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            await SaveNowAsync();
        };

        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _reminderTimer.Tick += (_, _) => CheckReminders();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<IReadOnlyList<NoteItemViewModel>>? RemindersDue;

    public ObservableCollection<NoteItemViewModel> Notes { get; } = new();

    public ICollectionView NotesView { get; }

    public CalculatorViewModel Calculator { get; } = new();

    public NoteItemViewModel? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote == value)
            {
                return;
            }

            _selectedNote = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanInsertCalculation));
            IsStatusPickerOpen = false;
            IsTimePickerOpen = false;
        }
    }

    public bool IsCalculatorOpen
    {
        get => _isCalculatorOpen;
        set
        {
            if (_isCalculatorOpen == value)
            {
                return;
            }

            _isCalculatorOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsStatusPickerOpen
    {
        get => _isStatusPickerOpen;
        set
        {
            if (_isStatusPickerOpen == value)
            {
                return;
            }

            _isStatusPickerOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsTimePickerOpen
    {
        get => _isTimePickerOpen;
        set
        {
            if (_isTimePickerOpen == value)
            {
                return;
            }

            _isTimePickerOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsWhiteTheme
    {
        get => _currentTheme == AppTheme.White;
        set
        {
            if (value)
            {
                SetTheme(AppTheme.White);
            }
        }
    }

    public bool IsPinkTheme
    {
        get => _currentTheme == AppTheme.Pink;
        set
        {
            if (value)
            {
                SetTheme(AppTheme.Pink);
            }
        }
    }

    public bool IsBlackTheme
    {
        get => _currentTheme == AppTheme.Black;
        set
        {
            if (value)
            {
                SetTheme(AppTheme.Black);
            }
        }
    }

    public bool IsRussian
    {
        get => LocalizationService.Instance.CurrentLanguage == AppLanguage.Russian;
        set
        {
            if (value)
            {
                SetLanguage(AppLanguage.Russian);
            }
        }
    }

    public bool IsEnglish
    {
        get => LocalizationService.Instance.CurrentLanguage == AppLanguage.English;
        set
        {
            if (value)
            {
                SetLanguage(AppLanguage.English);
            }
        }
    }

    public bool IsChinese
    {
        get => LocalizationService.Instance.CurrentLanguage == AppLanguage.Chinese;
        set
        {
            if (value)
            {
                SetLanguage(AppLanguage.Chinese);
            }
        }
    }

    public bool CanInsertCalculation => SelectedNote is not null && !Calculator.HasError;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            RefreshView();
        }
    }

    public string SaveStatus => LocalizationService.Instance.Get(_saveStatusKey);

    public string VisibleNoteCount => NotesView.Cast<object>().Count().ToString(LocalizationService.Instance.Culture);

    public bool HasVisibleNotes => !NotesView.IsEmpty;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var loadedNotes = await _storage.LoadAsync();
        foreach (var note in loadedNotes.OrderByDescending(note => note.UpdatedAt))
        {
            AddNote(new NoteItemViewModel(note));
        }

        if (Notes.Count == 0)
        {
            CreateNote();
        }
        else
        {
            SelectedNote = Notes[0];
        }

        RefreshView();
        SetSaveStatus("SaveSaved");
        _reminderTimer.Start();
        CheckReminders();
    }

    public void CreateNote()
    {
        var item = new NoteItemViewModel(new NoteData
        {
            Title = string.Empty,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        AddNote(item);
        SelectedNote = item;
        SearchText = string.Empty;
        RefreshView();
        ScheduleSave();
    }

    public void DeleteSelectedNote()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var index = Notes.IndexOf(SelectedNote);
        SelectedNote.Changed -= NoteChanged;
        Notes.Remove(SelectedNote);
        SelectedNote = Notes.Count == 0 ? null : Notes[Math.Clamp(index, 0, Notes.Count - 1)];
        RefreshView();
        ScheduleSave();
    }

    public void ToggleCalculator()
    {
        IsCalculatorOpen = !IsCalculatorOpen;
    }

    public void PressCalculatorKey(string key)
    {
        Calculator.Press(key);
        OnPropertyChanged(nameof(CanInsertCalculation));
    }

    public void SetSelectedStatus(NoteStatus status)
    {
        if (SelectedNote is not null)
        {
            SelectedNote.Status = status;
        }

        IsStatusPickerOpen = false;
    }

    public void AdjustSelectedReminderTime(string part, int delta)
    {
        SelectedNote?.AdjustReminderTime(part, delta);
    }

    public void InsertCalculationIntoNote()
    {
        if (!CanInsertCalculation || SelectedNote is null)
        {
            return;
        }

        var separator = string.IsNullOrWhiteSpace(SelectedNote.Content)
            ? string.Empty
            : Environment.NewLine;
        SelectedNote.Content += $"{separator}{Calculator.Display}";
    }

    public async Task FlushAsync()
    {
        _saveTimer.Stop();
        if (_initialized)
        {
            await SaveNowAsync();
        }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _reminderTimer.Stop();
        LocalizationService.Instance.LanguageChanged -= LanguageChanged;
        foreach (var note in Notes)
        {
            note.Changed -= NoteChanged;
        }
    }

    private void AddNote(NoteItemViewModel note)
    {
        note.Changed += NoteChanged;
        Notes.Add(note);
    }

    private void NoteChanged(NoteItemViewModel note)
    {
        RefreshView();
        ScheduleSave();
    }

    private bool MatchesSearch(object item)
    {
        if (item is not NoteItemViewModel note || string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var comparison = LocalizationService.Instance.Culture.CompareInfo;
        return comparison.IndexOf(note.DisplayTitle, SearchText, System.Globalization.CompareOptions.IgnoreCase) >= 0 ||
               comparison.IndexOf(note.Content, SearchText, System.Globalization.CompareOptions.IgnoreCase) >= 0;
    }

    private void RefreshView()
    {
        NotesView.Refresh();
        OnPropertyChanged(nameof(VisibleNoteCount));
        OnPropertyChanged(nameof(HasVisibleNotes));
    }

    private void ScheduleSave()
    {
        if (!_initialized)
        {
            return;
        }

        SetSaveStatus("SaveSaving");
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async Task SaveNowAsync()
    {
        try
        {
            var snapshot = Notes.Select(note => note.CreateSnapshot()).ToArray();
            await _storage.SaveAsync(snapshot);
            SetSaveStatus("SaveSaved");
        }
        catch (IOException)
        {
            SetSaveStatus("SaveFailed");
        }
        catch (UnauthorizedAccessException)
        {
            SetSaveStatus("SaveAccessDenied");
        }
    }

    private void CheckReminders()
    {
        var now = DateTime.Now;
        var dueNotes = Notes.Where(note => note.ShouldNotify(now)).ToArray();
        if (dueNotes.Length == 0)
        {
            return;
        }

        foreach (var note in dueNotes)
        {
            note.MarkReminderShown();
        }

        RefreshView();
        RemindersDue?.Invoke(dueNotes);
    }

    private void SetTheme(AppTheme theme)
    {
        if (_currentTheme == theme)
        {
            return;
        }

        _currentTheme = theme;
        ThemeService.Apply(theme);
        _settings.SaveTheme(theme);
        OnPropertyChanged(nameof(IsWhiteTheme));
        OnPropertyChanged(nameof(IsPinkTheme));
        OnPropertyChanged(nameof(IsBlackTheme));
    }

    public void SetLanguage(AppLanguage language)
    {
        if (LocalizationService.Instance.CurrentLanguage == language)
        {
            return;
        }

        LocalizationService.Instance.SetLanguage(language);
        _settings.SaveLanguage(language);
    }

    private void LanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsRussian));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsChinese));
        OnPropertyChanged(nameof(SaveStatus));
        OnPropertyChanged(nameof(CanInsertCalculation));
        foreach (var note in Notes)
        {
            note.RefreshLocalization();
        }

        Calculator.RefreshLocalization();
        RefreshView();
    }

    private void SetSaveStatus(string key)
    {
        if (_saveStatusKey == key)
        {
            return;
        }

        _saveStatusKey = key;
        OnPropertyChanged(nameof(SaveStatus));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
