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
    private NoteStorageService _storage;
    private readonly AppSettingsService _settings;
    private readonly NoteArchiveService _archiveService = new();
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _reminderTimer;
    private NoteItemViewModel? _selectedNote;
    private string _searchText = string.Empty;
    private string _saveStatusKey = "SaveLoading";
    private AppTheme _currentTheme = AppTheme.Black;
    private bool _isCalculatorOpen;
    private bool _isStatusPickerOpen;
    private bool _isTimePickerOpen;
    private bool _isAutoSaveEnabled;
    private bool _isGlobalHotkeysEnabled;
    private bool _hasUnsavedChanges;
    private NoteItemViewModel? _lastDeletedNote;
    private bool _initialized;

    public MainViewModel(NoteStorageService? storage = null, AppSettingsService? settings = null)
    {
        _settings = settings ?? new AppSettingsService();
        _storage = storage ?? new NoteStorageService(Path.Combine(_settings.LoadDataDirectory(), "notes.json"));
        _isAutoSaveEnabled = _settings.LoadAutoSaveEnabled();
        _isGlobalHotkeysEnabled = _settings.LoadGlobalHotkeysEnabled();
        LocalizationService.Instance.SetLanguage(_settings.LoadLanguage());
        LocalizationService.Instance.LanguageChanged += LanguageChanged;
        _currentTheme = _settings.LoadTheme();
        ThemeService.Apply(_currentTheme);

        NotesView = CollectionViewSource.GetDefaultView(Notes);
        NotesView.Filter = MatchesSearch;
        NotesView.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.IsPinned), ListSortDirection.Descending));
        NotesView.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.UpdatedAt), ListSortDirection.Descending));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            await SaveNowAsync();
        };

        _reminderTimer = new DispatcherTimer();
        _reminderTimer.Tick += (_, _) =>
        {
            _reminderTimer.Stop();
            CheckReminders();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<IReadOnlyList<NoteItemViewModel>>? RemindersDue;

    public event Action<bool>? GlobalHotkeysSettingChanged;

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

    public bool IsAutoSaveEnabled
    {
        get => _isAutoSaveEnabled;
        set
        {
            if (_isAutoSaveEnabled == value)
            {
                return;
            }

            _isAutoSaveEnabled = value;
            _settings.SaveAutoSaveEnabled(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoSaveDisplay));
            if (value && HasUnsavedChanges)
            {
                ScheduleSave();
            }
            else if (!value)
            {
                _saveTimer.Stop();
                SetSaveStatus(HasUnsavedChanges ? "SaveUnsaved" : "SaveSaved");
            }
        }
    }

    public bool IsGlobalHotkeysEnabled
    {
        get => _isGlobalHotkeysEnabled;
        set
        {
            if (_isGlobalHotkeysEnabled == value)
            {
                return;
            }

            _isGlobalHotkeysEnabled = value;
            _settings.SaveGlobalHotkeysEnabled(value);
            OnPropertyChanged();
            GlobalHotkeysSettingChanged?.Invoke(value);
        }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set
        {
            if (_hasUnsavedChanges == value)
            {
                return;
            }

            _hasUnsavedChanges = value;
            OnPropertyChanged();
        }
    }

    public bool CanUndoDelete => _lastDeletedNote is not null;

    public IEnumerable<NoteItemViewModel> DeletedNotes => Notes
        .Where(note => note.IsDeleted)
        .OrderByDescending(note => note.DeletedAt);

    public int DeletedNoteCount => Notes.Count(note => note.IsDeleted);

    public bool HasDeletedNotes => DeletedNoteCount > 0;

    public string TrashHeader => LocalizationService.Instance.Format("TrashWithCount", DeletedNoteCount);

    public string AutoSaveDisplay => LocalizationService.Instance.Get(
        IsAutoSaveEnabled ? "AutosaveEnabled" : "AutosaveDisabled");

    public string DataDirectory => Path.GetDirectoryName(_storage.StoragePath) ?? string.Empty;

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
        try
        {
            await _storage.CreateBackupAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Loading must remain possible even if an optional startup backup cannot be created.
        }
        var loadedNotes = await _storage.LoadAsync();
        foreach (var note in loadedNotes.OrderByDescending(note => note.UpdatedAt))
        {
            AddNote(new NoteItemViewModel(note));
        }

        var firstVisible = Notes.FirstOrDefault(note => !note.IsDeleted);
        if (firstVisible is null)
        {
            CreateNote();
        }
        else
        {
            SelectedNote = firstVisible;
        }

        RefreshView();
        SetSaveStatus("SaveSaved");
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

        var deleted = SelectedNote;
        deleted.MoveToTrash();
        _lastDeletedNote = deleted;
        OnPropertyChanged(nameof(CanUndoDelete));
        NotifyTrashChanged();
        SelectedNote = NotesView.Cast<NoteItemViewModel>().FirstOrDefault(note => note != deleted);
        RefreshView();
        ScheduleSave();
    }

    public void UndoLastDelete()
    {
        if (_lastDeletedNote is null)
        {
            return;
        }

        var note = _lastDeletedNote;
        _lastDeletedNote = null;
        note.RestoreFromTrash();
        SelectedNote = note;
        OnPropertyChanged(nameof(CanUndoDelete));
        NotifyTrashChanged();
        RefreshView();
        ScheduleSave();
    }

    public void RestoreFromTrash(NoteItemViewModel note)
    {
        if (!Notes.Contains(note) || !note.IsDeleted)
        {
            return;
        }

        note.RestoreFromTrash();
        if (_lastDeletedNote == note)
        {
            _lastDeletedNote = null;
            OnPropertyChanged(nameof(CanUndoDelete));
        }

        SelectedNote = note;
        NotifyTrashChanged();
        RefreshView();
        ScheduleSave();
    }

    public void EmptyTrash()
    {
        var deleted = Notes.Where(note => note.IsDeleted).ToArray();
        foreach (var note in deleted)
        {
            note.Changed -= NoteChanged;
            Notes.Remove(note);
        }

        _lastDeletedNote = null;
        OnPropertyChanged(nameof(CanUndoDelete));
        NotifyTrashChanged();
        RefreshView();
        ScheduleSave();
    }

    public void ToggleSelectedPin()
    {
        if (SelectedNote is null)
        {
            return;
        }

        SelectedNote.IsPinned = !SelectedNote.IsPinned;
        RefreshView();
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

    public int InsertCalculationIntoNote(int selectionStart, int selectionLength)
    {
        if (!CanInsertCalculation || SelectedNote is null)
        {
            return selectionStart;
        }

        return SelectedNote.InsertText(Calculator.Display, selectionStart, selectionLength);
    }

    public async Task SaveAsync()
    {
        _saveTimer.Stop();
        if (_initialized)
        {
            await SaveNowAsync();
        }
    }

    public async Task ExportAsync(string path)
    {
        await SaveAsync();
        await _archiveService.ExportAsync(path, CreateSnapshot());
    }

    public async Task<int> ImportAsync(string path, NoteImportMode mode)
    {
        var imported = await _archiveService.ImportAsync(path);
        await _storage.CreateBackupAsync();
        var combined = mode == NoteImportMode.Merge
            ? NoteArchiveService.Merge(CreateSnapshot(), imported)
            : imported;
        ReplaceNotes(combined);
        HasUnsavedChanges = true;
        await SaveNowAsync();
        return imported.Count;
    }

    public async Task ChangeDataDirectoryAsync(string directory)
    {
        var normalizedDirectory = Path.GetFullPath(directory);
        if (string.Equals(normalizedDirectory, DataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(normalizedDirectory);
        await SaveAsync();

        var nextStorage = new NoteStorageService(Path.Combine(normalizedDirectory, "notes.json"));
        await nextStorage.CreateBackupAsync();
        var notesAlreadyThere = await nextStorage.LoadAsync();
        var combined = NoteArchiveService.Merge(notesAlreadyThere, CreateSnapshot());
        await nextStorage.SaveAsync(combined);
        _storage = nextStorage;
        _settings.SaveDataDirectory(normalizedDirectory);
        ReplaceNotes(combined);
        HasUnsavedChanges = false;
        OnPropertyChanged(nameof(DataDirectory));
        SetSaveStatus("SaveSaved");
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
        NotifyTrashChanged();
        ScheduleReminderCheck();
        ScheduleSave();
    }

    private bool MatchesSearch(object item)
    {
        if (item is not NoteItemViewModel note || note.IsDeleted)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
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

        HasUnsavedChanges = true;
        _saveTimer.Stop();
        if (IsAutoSaveEnabled)
        {
            SetSaveStatus("SaveSaving");
            _saveTimer.Start();
        }
        else
        {
            SetSaveStatus("SaveUnsaved");
        }
    }

    private async Task SaveNowAsync()
    {
        try
        {
            var snapshot = Notes.Select(note => note.CreateSnapshot()).ToArray();
            await _storage.SaveAsync(snapshot);
            HasUnsavedChanges = false;
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
            ScheduleReminderCheck();
            return;
        }

        foreach (var note in dueNotes)
        {
            note.MarkReminderShown();
        }

        RefreshView();
        RemindersDue?.Invoke(dueNotes);
        ScheduleReminderCheck();
    }

    private void ScheduleReminderCheck()
    {
        _reminderTimer.Stop();
        if (!_initialized)
        {
            return;
        }

        var nextReminder = Notes
            .Where(note => !note.IsDeleted && note.ShouldNotify(DateTime.MaxValue))
            .Select(note => note.ReminderAt!.Value)
            .DefaultIfEmpty(DateTime.MaxValue)
            .Min();
        if (nextReminder == DateTime.MaxValue)
        {
            return;
        }

        var delay = nextReminder - DateTime.Now;
        _reminderTimer.Interval = delay <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(100)
            : delay > TimeSpan.FromDays(1)
                ? TimeSpan.FromDays(1)
                : delay;
        _reminderTimer.Start();
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
        OnPropertyChanged(nameof(AutoSaveDisplay));
        OnPropertyChanged(nameof(TrashHeader));
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

    private IReadOnlyList<NoteData> CreateSnapshot() =>
        Notes.Select(note => note.CreateSnapshot()).ToArray();

    private void ReplaceNotes(IEnumerable<NoteData> notes)
    {
        foreach (var note in Notes)
        {
            note.Changed -= NoteChanged;
        }

        Notes.Clear();
        foreach (var note in notes.OrderByDescending(note => note.UpdatedAt))
        {
            AddNote(new NoteItemViewModel(note));
        }

        _lastDeletedNote = null;
        OnPropertyChanged(nameof(CanUndoDelete));
        NotifyTrashChanged();
        RefreshView();
        SelectedNote = NotesView.Cast<NoteItemViewModel>().FirstOrDefault();
    }

    private void NotifyTrashChanged()
    {
        OnPropertyChanged(nameof(DeletedNotes));
        OnPropertyChanged(nameof(DeletedNoteCount));
        OnPropertyChanged(nameof(HasDeletedNotes));
        OnPropertyChanged(nameof(TrashHeader));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
