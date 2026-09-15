using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nezabudka.App.Models;

namespace Nezabudka.App.Services;

public sealed class NoteArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task ExportAsync(string path, IEnumerable<NoteData> notes)
    {
        var archive = new NoteArchive
        {
            Notes = notes.Select(Clone).ToList()
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, archive, JsonOptions);
        }

        File.Move(temporaryPath, path, true);
    }

    public async Task<IReadOnlyList<NoteData>> ImportAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length > 100 * 1024 * 1024)
        {
            throw new InvalidDataException("The archive is too large.");
        }

        var archive = await JsonSerializer.DeserializeAsync<NoteArchive>(stream, JsonOptions)
                      ?? throw new InvalidDataException("The archive is empty.");
        if (archive.FormatVersion is < 1 or > NoteArchive.CurrentFormatVersion)
        {
            throw new InvalidDataException($"Unsupported archive format: {archive.FormatVersion}.");
        }

        archive.Notes ??= [];
        if (archive.Notes.Count > 100_000)
        {
            throw new InvalidDataException("The archive contains too many notes.");
        }

        return archive.Notes.Select(Clone).ToArray();
    }

    public static IReadOnlyList<NoteData> Merge(
        IEnumerable<NoteData> current,
        IEnumerable<NoteData> imported)
    {
        var merged = current
            .Select(Clone)
            .GroupBy(note => note.Id)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(note => note.UpdatedAt).First());
        foreach (var note in imported)
        {
            if (!merged.TryGetValue(note.Id, out var existing) || note.UpdatedAt > existing.UpdatedAt)
            {
                merged[note.Id] = Clone(note);
            }
        }

        return merged.Values.OrderByDescending(note => note.UpdatedAt).ToArray();
    }

    internal static NoteData Clone(NoteData note) => new()
    {
        Id = note.Id,
        Title = note.Title ?? string.Empty,
        Content = note.Content ?? string.Empty,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        ReminderAt = note.ReminderAt,
        Status = note.Status,
        IsPinned = note.IsPinned,
        DeletedAt = note.DeletedAt,
        IsCompleted = note.IsCompleted,
        ReminderShown = note.ReminderShown
    };
}
