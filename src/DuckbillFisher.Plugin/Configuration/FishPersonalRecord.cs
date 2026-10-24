namespace DuckbillFisher.Plugin;

/// <summary>Manual, character-shared annotations. Never inferred from casts, catches or collection.</summary>
public sealed record FishPersonalRecord
{
    public const int MaxNoteLength = 1000;
    public const int MaxAttempts = 1_000_000;
    public const int MaxEntries = 4096;
    public string Note { get; init; } = "";
    public int Attempts { get; init; }
    public Guid? TimerPresetId { get; init; }

    public static bool ValidNote(string? note) => note is not null && note.Length <= MaxNoteLength &&
        !note.Any(character => char.IsControl(character) && character is not ('\n' or '\r' or '\t'));
    public bool IsValid() => ValidNote(Note) && Attempts is >= 0 and <= MaxAttempts && TimerPresetId != Guid.Empty;
    public bool IsEmpty() => Note.Length == 0 && Attempts == 0 && TimerPresetId is null;
    public static bool ValidEntries(Dictionary<uint, FishPersonalRecord>? entries) =>
        entries is { Count: <= MaxEntries } && entries.All(pair => pair.Key != 0 && pair.Value is not null && pair.Value.IsValid());

    public static bool RemoveMissingPresets(Dictionary<uint, FishPersonalRecord> entries, CastingTimerSettings timer)
    {
        var existing = timer.Presets.Select(preset => preset.Id).ToHashSet();
        var changed = false;
        foreach (var (fishId, record) in entries.ToArray())
        {
            var value = record.TimerPresetId is { } id && !existing.Contains(id) ? record with { TimerPresetId = null } : record;
            if (value.IsEmpty()) { entries.Remove(fishId); changed = true; }
            else if (value != record) { entries[fishId] = value; changed = true; }
        }
        return changed;
    }
}
