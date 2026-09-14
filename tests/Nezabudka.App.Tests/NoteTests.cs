using Nezabudka.App.Models;
using Nezabudka.App.Services;
using Nezabudka.App.ViewModels;

namespace Nezabudka.App.Tests;

public sealed class NoteTests
{
    [Fact]
    public async Task StorageRoundTripPreservesNote()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var storagePath = Path.Combine(testDirectory, "notes.json");

        try
        {
            var storage = new NoteStorageService(storagePath);
            var note = new NoteData
            {
                Title = "Проверить отчёт",
                Content = "До встречи в понедельник",
                ReminderAt = new DateTime(2030, 5, 20, 15, 0, 0),
                Status = NoteStatus.Waiting,
                IsCompleted = false
            };

            await storage.SaveAsync(new[] { note });
            var loaded = await storage.LoadAsync();

            var restored = Assert.Single(loaded);
            Assert.Equal(note.Id, restored.Id);
            Assert.Equal("Проверить отчёт", restored.Title);
            Assert.Equal(note.ReminderAt, restored.ReminderAt);
            Assert.Equal(NoteStatus.Waiting, restored.Status);
            Assert.False(restored.IsCompleted);
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
    public void ReminderIsOnlyRaisedForAnOpenUnshownTask()
    {
        var now = DateTime.Now;
        var note = new NoteItemViewModel(new NoteData
        {
            Title = "Позвонить",
            ReminderAt = now.AddMinutes(-1)
        });

        Assert.True(note.ShouldNotify(now));
        Assert.True(note.IsOverdue);

        note.MarkReminderShown();
        Assert.False(note.ShouldNotify(now));

        note.IsCompleted = true;
        Assert.False(note.IsOverdue);
        Assert.Equal("Выполнено", note.ListMeta);
    }

    [Fact]
    public void EmptyTitleAndTextHaveReadableFallbacks()
    {
        var note = new NoteItemViewModel(new NoteData());

        Assert.Equal("Без названия", note.DisplayTitle);
        Assert.Equal("Пустая заметка", note.Preview);
    }

    [Fact]
    public void ChoosingTimeActivatesReminder()
    {
        var note = new NoteItemViewModel(new NoteData());

        note.ReminderTimeText = "23:45";

        Assert.True(note.HasReminder);
        Assert.Equal("23:45:00", note.ReminderTimeText);
        Assert.True(note.ReminderAt > DateTime.Now);
    }

    [Fact]
    public void ClosedTaskDoesNotRaiseReminder()
    {
        var now = DateTime.Now;
        var note = new NoteItemViewModel(new NoteData
        {
            ReminderAt = now.AddMinutes(-1),
            Status = NoteStatus.Closed
        });

        Assert.False(note.ShouldNotify(now));
        Assert.True(note.IsTerminal);
        Assert.Equal(LocalizationService.Instance.Get("StatusClosed"), note.ListMeta);
    }

    [Fact]
    public async Task OldCompletedFlagMigratesToStatus()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var storagePath = Path.Combine(testDirectory, "notes.json");

        try
        {
            var storage = new NoteStorageService(storagePath);
            await storage.SaveAsync(new[] { new NoteData { IsCompleted = true } });

            var migrated = Assert.Single(await storage.LoadAsync());
            Assert.Equal(NoteStatus.Completed, migrated.Status);
            Assert.False(migrated.IsCompleted);
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
