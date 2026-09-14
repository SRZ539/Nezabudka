using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nezabudka.App.Models;

namespace Nezabudka.App.Services;

public sealed class NoteStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public NoteStorageService(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nezabudka",
            "notes.json");
    }

    public string StoragePath { get; }

    public async Task<IReadOnlyList<NoteData>> LoadAsync()
    {
        if (!File.Exists(StoragePath))
        {
            return Array.Empty<NoteData>();
        }

        try
        {
            await using var stream = File.OpenRead(StoragePath);
            var notes = await JsonSerializer.DeserializeAsync<List<NoteData>>(stream, JsonOptions)
                        ?? new List<NoteData>();

            foreach (var note in notes.Where(note => note.IsCompleted && note.Status == NoteStatus.Active))
            {
                note.Status = NoteStatus.Completed;
                note.IsCompleted = false;
            }

            return notes;
        }
        catch (JsonException)
        {
            PreserveUnreadableFile();
            return Array.Empty<NoteData>();
        }
        catch (IOException)
        {
            return Array.Empty<NoteData>();
        }
    }

    public async Task SaveAsync(IEnumerable<NoteData> notes)
    {
        await _saveLock.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(StoragePath)!;
            Directory.CreateDirectory(directory);

            var temporaryPath = StoragePath + ".tmp";
            var json = JsonSerializer.Serialize(notes, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, StoragePath, true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void PreserveUnreadableFile()
    {
        try
        {
            var backupPath = StoragePath + $".unreadable-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(StoragePath, backupPath, false);
        }
        catch (IOException)
        {
            // The original file stays untouched if a backup cannot be created.
        }
    }
}
