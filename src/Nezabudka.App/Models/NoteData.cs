using System.Text.Json.Serialization;

namespace Nezabudka.App.Models;

public sealed class NoteData
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public DateTime? ReminderAt { get; set; }

    public NoteStatus Status { get; set; } = NoteStatus.Active;

    public bool IsPinned { get; set; }

    public DateTime? DeletedAt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsCompleted { get; set; }

    public bool ReminderShown { get; set; }
}
