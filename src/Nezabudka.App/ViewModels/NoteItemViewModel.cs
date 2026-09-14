using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Nezabudka.App.Models;
using Nezabudka.App.Services;

namespace Nezabudka.App.ViewModels;

public sealed class NoteItemViewModel : INotifyPropertyChanged
{
    private readonly NoteData _data;

    public NoteItemViewModel(NoteData data)
    {
        _data = data;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<NoteItemViewModel>? Changed;

    public Guid Id => _data.Id;

    public string Title
    {
        get => _data.Title;
        set
        {
            if (_data.Title == value)
            {
                return;
            }

            _data.Title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayTitle));
            Touch();
        }
    }

    public string Content
    {
        get => _data.Content;
        set
        {
            if (_data.Content == value)
            {
                return;
            }

            _data.Content = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
            Touch();
        }
    }

    public NoteStatus Status
    {
        get => _data.Status;
        set
        {
            if (_data.Status == value)
            {
                return;
            }

            _data.Status = value;
            if (!IsTerminal && _data.ReminderAt <= DateTime.Now)
            {
                _data.ReminderShown = false;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsWaiting));
            OnPropertyChanged(nameof(IsClosed));
            OnPropertyChanged(nameof(IsTerminal));
            OnPropertyChanged(nameof(IsOverdue));
            OnPropertyChanged(nameof(ListMeta));
            Touch();
        }
    }

    public bool IsCompleted
    {
        get => Status == NoteStatus.Completed;
        set => Status = value ? NoteStatus.Completed : NoteStatus.Active;
    }

    public bool IsWaiting => Status == NoteStatus.Waiting;

    public bool IsClosed => Status == NoteStatus.Closed;

    public bool IsTerminal => Status is NoteStatus.Completed or NoteStatus.Closed;

    public string StatusDisplay => LocalizationService.Instance.Get(Status switch
    {
        NoteStatus.Waiting => "StatusWaiting",
        NoteStatus.Completed => "StatusCompleted",
        NoteStatus.Closed => "StatusClosed",
        _ => "StatusActive"
    });

    public bool HasReminder
    {
        get => _data.ReminderAt.HasValue;
        set
        {
            if (value == HasReminder)
            {
                return;
            }

            ReminderAt = value ? RoundUpToQuarterHour(DateTime.Now.AddMinutes(30)) : null;
            OnPropertyChanged();
        }
    }

    public DateTime? ReminderDate
    {
        get => _data.ReminderAt?.Date;
        set
        {
            if (value is null)
            {
                ReminderAt = null;
                return;
            }

            var time = _data.ReminderAt?.TimeOfDay ?? RoundUpToQuarterHour(DateTime.Now.AddMinutes(30)).TimeOfDay;
            ReminderAt = value.Value.Date.Add(time);
        }
    }

    public string? ReminderTimeText
    {
        get => _data.ReminderAt?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        set
        {
            if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time))
            {
                return;
            }

            var date = _data.ReminderAt?.Date ?? DateTime.Today;
            if (_data.ReminderAt is null && date.Add(time) <= DateTime.Now)
            {
                date = date.AddDays(1);
            }

            ReminderAt = date.Add(time);
        }
    }

    public string ReminderTimeDisplay => ReminderTimeText ?? LocalizationService.Instance.Get("SelectTime");

    public string ReminderDateDisplay => _data.ReminderAt?.ToString("d", LocalizationService.Instance.Culture)
        ?? LocalizationService.Instance.Get("ChooseDate");

    public string ReminderHourText => (_data.ReminderAt ?? DateTime.Now).ToString("HH", CultureInfo.InvariantCulture);

    public string ReminderMinuteText => (_data.ReminderAt ?? DateTime.Now).ToString("mm", CultureInfo.InvariantCulture);

    public string ReminderSecondText => (_data.ReminderAt ?? DateTime.Now).ToString("ss", CultureInfo.InvariantCulture);

    public DateTime UpdatedAt => _data.UpdatedAt;

    public DateTime? ReminderAt
    {
        get => _data.ReminderAt;
        private set
        {
            if (_data.ReminderAt == value)
            {
                return;
            }

            _data.ReminderAt = value;
            _data.ReminderShown = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReminder));
            OnPropertyChanged(nameof(ReminderDate));
            OnPropertyChanged(nameof(ReminderDateDisplay));
            OnPropertyChanged(nameof(ReminderTimeText));
            OnPropertyChanged(nameof(ReminderTimeDisplay));
            OnPropertyChanged(nameof(ReminderHourText));
            OnPropertyChanged(nameof(ReminderMinuteText));
            OnPropertyChanged(nameof(ReminderSecondText));
            OnPropertyChanged(nameof(IsOverdue));
            OnPropertyChanged(nameof(ListMeta));
            Touch();
        }
    }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? LocalizationService.Instance.Get("NoteUntitled") : Title.Trim();

    public string Preview
    {
        get
        {
            var singleLine = string.Join(" ", Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(singleLine) ? LocalizationService.Instance.Get("NoteEmpty") : singleLine.Trim();
        }
    }

    public bool IsOverdue => !IsTerminal && ReminderAt.HasValue && ReminderAt.Value <= DateTime.Now;

    public string ListMeta
    {
        get
        {
            var localization = LocalizationService.Instance;
            if (IsTerminal)
            {
                return StatusDisplay;
            }

            if (ReminderAt is null)
            {
                return Status == NoteStatus.Waiting
                    ? StatusDisplay
                    : localization.Format("NoteUpdated", UpdatedAt.ToString(localization.Get("UpdatedDateFormat"), localization.Culture));
            }

            var value = ReminderAt.Value;
            var dateText = value.Date == DateTime.Today
                ? localization.Get("Today")
                : value.Date == DateTime.Today.AddDays(1)
                    ? localization.Get("Tomorrow")
                    : value.ToString(localization.Get("ReminderDateFormat"), localization.Culture);

            var formatKey = Status == NoteStatus.Waiting
                ? "NoteWaitingReminder"
                : IsOverdue
                    ? "NoteOverdueReminder"
                    : "NoteReminder";

            return localization.Format(formatKey, dateText, value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }

    public bool ShouldNotify(DateTime now) =>
        !IsTerminal && !_data.ReminderShown && ReminderAt.HasValue && ReminderAt.Value <= now;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(ListMeta));
        OnPropertyChanged(nameof(ReminderTimeDisplay));
        OnPropertyChanged(nameof(ReminderDateDisplay));
    }

    public void AdjustReminderTime(string part, int delta)
    {
        var current = _data.ReminderAt ?? RoundUpToQuarterHour(DateTime.Now.AddMinutes(30));
        var hour = current.Hour;
        var minute = current.Minute;
        var second = current.Second;

        switch (part)
        {
            case "hour":
                hour = Wrap(hour + delta, 24);
                break;
            case "minute":
                minute = Wrap(minute + delta, 60);
                break;
            case "second":
                second = Wrap(second + delta, 60);
                break;
            default:
                return;
        }

        ReminderAt = new DateTime(current.Year, current.Month, current.Day, hour, minute, second);
    }

    public void MarkReminderShown()
    {
        _data.ReminderShown = true;
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(ListMeta));
        Changed?.Invoke(this);
    }

    public NoteData CreateSnapshot() => new()
    {
        Id = _data.Id,
        Title = _data.Title,
        Content = _data.Content,
        CreatedAt = _data.CreatedAt,
        UpdatedAt = _data.UpdatedAt,
        ReminderAt = _data.ReminderAt,
        Status = _data.Status,
        IsCompleted = false,
        ReminderShown = _data.ReminderShown
    };

    private void Touch()
    {
        _data.UpdatedAt = DateTime.Now;
        OnPropertyChanged(nameof(UpdatedAt));
        OnPropertyChanged(nameof(ListMeta));
        Changed?.Invoke(this);
    }

    private static DateTime RoundUpToQuarterHour(DateTime value)
    {
        var minutes = ((value.Minute + 14) / 15) * 15;
        var rounded = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0);
        return rounded.AddMinutes(minutes);
    }

    private static int Wrap(int value, int maximum) => (value % maximum + maximum) % maximum;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
