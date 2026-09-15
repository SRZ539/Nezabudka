using Nezabudka.App.Models;
using Nezabudka.App.Services;

namespace Nezabudka.App.Tests;

public sealed class ArchiveTests
{
    [Fact]
    public async Task ArchiveRoundTripPreservesPortableFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "notes.nezabudka");
        try
        {
            var note = new NoteData
            {
                Title = "Pinned",
                Content = "Portable",
                IsPinned = true,
                Status = NoteStatus.Waiting,
                ReminderAt = new DateTime(2035, 4, 5, 6, 7, 8)
            };
            var service = new NoteArchiveService();

            await service.ExportAsync(path, new[] { note });
            var imported = Assert.Single(await service.ImportAsync(path));

            Assert.Equal(note.Id, imported.Id);
            Assert.Equal(note.Content, imported.Content);
            Assert.True(imported.IsPinned);
            Assert.Equal(note.ReminderAt, imported.ReminderAt);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void MergeIsIdempotentAndKeepsNewestVersion()
    {
        var id = Guid.NewGuid();
        var oldNote = new NoteData { Id = id, Title = "Old", UpdatedAt = new DateTime(2030, 1, 1) };
        var newNote = new NoteData { Id = id, Title = "New", UpdatedAt = new DateTime(2030, 1, 2) };

        var merged = NoteArchiveService.Merge(new[] { oldNote }, new[] { newNote, newNote });

        var note = Assert.Single(merged);
        Assert.Equal("New", note.Title);
    }

    [Fact]
    public async Task BackupRotationKeepsRequestedCount()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Nezabudka.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new NoteStorageService(Path.Combine(directory, "notes.json"));
            await storage.SaveAsync(new[] { new NoteData { Title = "Backup" } });
            for (var index = 0; index < 4; index++)
            {
                await storage.CreateBackupAsync(2);
            }

            Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "Backups"), "notes-*.json").Length);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
