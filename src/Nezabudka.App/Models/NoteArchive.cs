namespace Nezabudka.App.Models;

public sealed class NoteArchive
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public string AppVersion { get; set; } = "0.2.0-alpha";

    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;

    public List<NoteData> Notes { get; set; } = [];
}

public enum NoteImportMode
{
    Merge,
    Replace
}
