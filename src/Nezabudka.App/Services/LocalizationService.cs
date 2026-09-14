using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Nezabudka.App.Models;

namespace Nezabudka.App.Services;

/// <summary>Live UI translations. User-authored note titles and text are never translated.</summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> Catalogs = CreateCatalogs();

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Russian;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(CurrentLanguage switch
    {
        AppLanguage.English => "en-US",
        AppLanguage.Chinese => "zh-CN",
        _ => "ru-RU"
    });

    public IEnumerable<string> Keys => Catalogs[CurrentLanguage].Keys;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (Catalogs[CurrentLanguage].TryGetValue(key, out var translation))
        {
            return translation;
        }

        return Catalogs[AppLanguage.Russian].TryGetValue(key, out var fallback)
            ? fallback
            : $"[{key}]";
    }

    public string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);

    public void SetLanguage(AppLanguage language)
    {
        if (!Enum.IsDefined(language))
        {
            language = AppLanguage.Russian;
        }

        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> CreateCatalogs()
    {
        // Every entry supplies all three translations together, preventing partially translated releases.
        (string Key, string Russian, string English, string Chinese)[] entries =
        [
            ("AppName", "Незабудка", "Forget-Me-Not", "勿忘我"),
            ("AppSubtitle", "Заметки и задачи", "Notes and tasks", "笔记与任务"),
            ("Theme", "Тема", "Theme", "主题"),
            ("ThemeWhite", "Белая тема", "White theme", "白色主题"),
            ("ThemePink", "Розовая тема", "Pink theme", "粉色主题"),
            ("ThemeBlack", "Чёрная тема", "Black theme", "黑色主题"),
            ("Language", "Язык", "Language", "语言"),
            ("LanguageTooltip", "Выбрать язык интерфейса", "Choose interface language", "选择界面语言"),
            ("LanguageRussian", "Русский", "Russian", "俄语"),
            ("LanguageEnglish", "Английский", "English", "英语"),
            ("LanguageChinese", "Китайский (упрощённый)", "Chinese (Simplified)", "简体中文"),
            ("Calculator", "Калькулятор", "Calculator", "计算器"),
            ("OpenCalculator", "Открыть калькулятор (Ctrl+K)", "Open calculator (Ctrl+K)", "打开计算器 (Ctrl+K)"),
            ("NewNote", "Новая заметка", "New note", "新建笔记"),
            ("NewNoteShortcut", "Новая заметка (Ctrl+N)", "New note (Ctrl+N)", "新建笔记 (Ctrl+N)"),
            ("Minimize", "Свернуть", "Minimize", "最小化"),
            ("Maximize", "Развернуть", "Maximize", "最大化"),
            ("MinimizeToTray", "Свернуть в трей", "Minimize to tray", "最小化到托盘"),
            ("MyNotes", "Мои заметки", "My notes", "我的笔记"),
            ("SearchTooltip", "Поиск по заголовку и тексту (Ctrl+F)", "Search titles and text (Ctrl+F)", "搜索标题和正文 (Ctrl+F)"),
            ("SearchPlaceholder", "Поиск по заметкам…", "Search notes…", "搜索笔记…"),
            ("NothingFound", "Ничего не найдено", "No notes found", "未找到笔记"),
            ("NoteTitleTooltip", "Заголовок заметки", "Note title", "笔记标题"),
            ("StatusActive", "Активная", "Active", "进行中"),
            ("StatusWaiting", "В ожидании", "Waiting", "待处理"),
            ("StatusCompleted", "Выполнено", "Completed", "已完成"),
            ("StatusClosed", "Закрыта", "Closed", "已关闭"),
            ("Delete", "Удалить", "Delete", "删除"),
            ("Edit", "Редактировать", "Edit", "编辑"),
            ("Cancel", "Отмена", "Cancel", "取消"),
            ("Close", "Закрыть", "Close", "关闭"),
            ("Undo", "Отменить", "Undo", "撤销"),
            ("Redo", "Повторить", "Redo", "重做"),
            ("Cut", "Вырезать", "Cut", "剪切"),
            ("Copy", "Копировать", "Copy", "复制"),
            ("Paste", "Вставить", "Paste", "粘贴"),
            ("SelectAll", "Выделить всё", "Select all", "全选"),
            ("Reminder", "Напоминание", "Reminder", "提醒"),
            ("ReminderHint", "Дата или время сразу включат его", "Choosing a date or time turns it on", "选择日期或时间即可启用提醒"),
            ("Remind", "Напомнить", "Remind me", "提醒我"),
            ("ChooseDate", "Выбрать дату", "Choose date", "选择日期"),
            ("ChooseTime", "Выбрать время", "Choose time", "选择时间"),
            ("SelectTime", "Выбрать время", "Choose time", "选择时间"),
            ("ChooseExactTime", "Выбрать часы, минуты и секунды", "Choose hours, minutes and seconds", "选择时、分、秒"),
            ("ExactTime", "Точное время", "Exact time", "精确时间"),
            ("TimePickerHint", "Часы · минуты · секунды · крутите колесо", "Hours · minutes · seconds · scroll to adjust", "时 · 分 · 秒 · 滚动鼠标滚轮调整"),
            ("ScrollHours", "Колесо мыши меняет часы", "Scroll to change hours", "滚动鼠标滚轮调整小时"),
            ("ScrollMinutes", "Колесо мыши меняет минуты", "Scroll to change minutes", "滚动鼠标滚轮调整分钟"),
            ("ScrollSeconds", "Колесо мыши меняет секунды", "Scroll to change seconds", "滚动鼠标滚轮调整秒数"),
            ("CalendarPrevious", "Предыдущий месяц", "Previous month", "上个月"),
            ("CalendarNext", "Следующий месяц", "Next month", "下个月"),
            ("Done", "Готово", "Done", "完成"),
            ("Reset", "Сбросить", "Reset", "重置"),
            ("SelectNote", "Выберите заметку", "Select a note", "选择一条笔记"),
            ("CreateNoteHint", "или создайте новую сочетанием Ctrl+N", "or create one with Ctrl+N", "或按 Ctrl+N 新建笔记"),
            ("InsertCalculation", "Вставить результат в заметку", "Insert result into note", "将结果插入笔记"),
            ("AutosaveEnabled", "Автосохранение включено", "Autosave is on", "自动保存已开启"),
            ("FooterCredit", "Разработано на C# · KlaOs · альфа 0.1", "Built with C# · KlaOs · alpha 0.1", "使用 C# 开发 · KlaOs · Alpha 0.1"),
            ("TrayHint", "Крестик сворачивает приложение в трей", "Closing the window minimizes to tray", "关闭窗口后最小化到托盘"),
            ("TrayOpen", "Открыть", "Open", "打开"),
            ("TrayExit", "Выйти", "Quit", "退出"),
            ("ReminderDueSingle", "Пора выполнить задачу", "Your task is due", "任务到时间了"),
            ("ReminderDueMultiple", "Пора выполнить задач: {0}", "Tasks due: {0}", "到期任务：{0}"),
            ("DeleteNotePrompt", "Удалить заметку «{0}»?", "Delete the note “{0}”?", "删除笔记“{0}”？"),
            ("DeleteNoteTitle", "Удаление заметки", "Delete note", "删除笔记"),
            ("TrayBackgroundTitle", "Незабудка работает в фоне", "Forget-Me-Not is running in the background", "勿忘我正在后台运行"),
            ("TrayBackgroundMessage", "Напоминания продолжат приходить. Для выхода используйте меню значка в трее.", "Reminders will still arrive. To quit, use the tray icon menu.", "您仍会收到提醒。如需退出，请使用托盘图标菜单。"),
            ("IconLoadError", "Не удалось загрузить иконку Незабудки.", "Could not load the Forget-Me-Not icon.", "无法加载勿忘我图标。"),
            ("SaveLoading", "Загрузка…", "Loading…", "正在加载…"),
            ("SaveSaving", "Сохранение…", "Saving…", "正在保存…"),
            ("SaveSaved", "Все изменения сохранены", "All changes saved", "所有更改已保存"),
            ("SaveFailed", "Не удалось сохранить", "Could not save", "保存失败"),
            ("SaveAccessDenied", "Нет доступа к папке данных", "Cannot access the data folder", "无法访问数据文件夹"),
            ("NoteUntitled", "Без названия", "Untitled", "无标题"),
            ("NoteEmpty", "Пустая заметка", "Empty note", "空白笔记"),
            ("NoteUpdated", "Изменено {0}", "Edited {0}", "修改于 {0}"),
            ("NoteReminder", "Напомнить · {0} {1}", "Reminder · {0} {1}", "提醒 · {0} {1}"),
            ("NoteWaitingReminder", "В ожидании · {0} {1}", "Waiting · {0} {1}", "待处理 · {0} {1}"),
            ("NoteOverdueReminder", "Просрочено · {0} {1}", "Overdue · {0} {1}", "已逾期 · {0} {1}"),
            ("Today", "Сегодня", "Today", "今天"),
            ("Tomorrow", "Завтра", "Tomorrow", "明天"),
            ("UpdatedDateFormat", "dd.MM, HH:mm", "MMM d, HH:mm", "M月d日 HH:mm"),
            ("DateModifiedFormat", "dd.MM, HH:mm", "MMM d, HH:mm", "M月d日 HH:mm"),
            ("ReminderDateFormat", "dd.MM.yyyy", "MMM d, yyyy", "yyyy年M月d日"),
            ("CalculatorReady", "Готов к вычислениям", "Ready to calculate", "准备计算"),
            ("CalculatorPercent", "Проценты", "Percent", "百分比"),
            ("CalculatorError", "Ошибка", "Error", "错误"),
            ("CalculatorCannotCalculate", "Невозможно выполнить операцию", "Cannot perform this operation", "无法执行此运算")
        ];

        return new ReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>>(
            Enum.GetValues<AppLanguage>().ToDictionary(
                language => language,
                language => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(
                    entries.ToDictionary(entry => entry.Key, entry => language switch
                    {
                        AppLanguage.English => entry.English,
                        AppLanguage.Chinese => entry.Chinese,
                        _ => entry.Russian
                    }, StringComparer.Ordinal))));
    }
}
