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
                IsPinned = true,
                IsCompleted = false
            };

            await storage.SaveAsync(new[] { note });
            var loaded = await storage.LoadAsync();

            var restored = Assert.Single(loaded);
            Assert.Equal(note.Id, restored.Id);
            Assert.Equal("Проверить отчёт", restored.Title);
            Assert.Equal(note.ReminderAt, restored.ReminderAt);
            Assert.Equal(NoteStatus.Waiting, restored.Status);
            Assert.True(restored.IsPinned);
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

    [Theory]
    [InlineData("1234", "9", 2, 0, "12934", 3)]
    [InlineData("1234", "99", 1, 2, "1994", 3)]
    [InlineData("", "42", 10, 4, "42", 2)]
    public void TextIsInsertedAtTheCaretOrReplacesSelection(
        string original,
        string inserted,
        int selectionStart,
        int selectionLength,
        string expected,
        int expectedCaret)
    {
        var note = new NoteItemViewModel(new NoteData { Content = original });

        var caret = note.InsertText(inserted, selectionStart, selectionLength);

        Assert.Equal(expected, note.Content);
        Assert.Equal(expectedCaret, caret);
    }

    [Fact]
    public void PinAndTrashStateSurviveSnapshot()
    {
        var note = new NoteItemViewModel(new NoteData { Title = "Important" });

        note.IsPinned = true;
        note.MoveToTrash(new DateTime(2030, 1, 2, 3, 4, 5));
        var snapshot = note.CreateSnapshot();

        Assert.True(snapshot.IsPinned);
        Assert.Equal(new DateTime(2030, 1, 2, 3, 4, 5), snapshot.DeletedAt);

        note.RestoreFromTrash();
        Assert.False(note.IsDeleted);
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
