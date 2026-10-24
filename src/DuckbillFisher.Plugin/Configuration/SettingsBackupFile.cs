using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DuckbillFisher.Plugin;

internal enum BackupIssue { Invalid, Unsupported, FileAccess, RollbackFailed }
internal sealed class BackupException(BackupIssue issue) : Exception(issue.ToString())
{
    public BackupIssue Issue { get; } = issue;
}

internal sealed class SettingsBackupDocument
{
    public required string Product { get; init; }
    public required int FormatVersion { get; init; }
    public required int SettingsVersion { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required CommonSettingsBackup Settings { get; set; }
    public Dictionary<string, string> SoundHashes { get; init; } = [];
}
internal sealed record PreparedSettingsBackup(SettingsBackupDocument Document, Dictionary<SoundSlot, byte[]> Sounds);

/// <summary>Local, bounded JSON/ZIP format. Only four exact WAV entry names are accepted.</summary>
internal static class SettingsBackupFile
{
    public const int FormatVersion = 1;
    public const int FirstSettingsVersion = 21;
    private const int MaxJsonBytes = 4 * 1024 * 1024;
    private const int MaxArchiveBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 32 };

    public static async Task WriteAsync(string path, CommonSettingsBackup settings, bool includeSounds, CancellationToken token)
    {
        var sounds = new Dictionary<SoundSlot, byte[]>();
        if (!settings.IsValid(PreferencesMigration.CurrentVersion)) throw new BackupException(BackupIssue.Invalid);
        // A remembered path is not a selected WAV. Sanitize only the exported snapshot;
        // keep the live preference and older format-v1 imports intact.
        foreach (var slot in ManagedSoundFiles.Slots)
            if (!ManagedSoundFiles.UsesCustomSound(settings, slot)) ManagedSoundFiles.Select(settings, slot, "");
        if (includeSounds)
            foreach (var slot in ManagedSoundFiles.Slots)
            {
                var source = ManagedSoundFiles.Selected(settings, slot);
                if (source.Length == 0) continue;
                sounds[slot] = await ManagedSoundFiles.ReadAsync(source, token);
                ManagedSoundFiles.Select(settings, slot, ManagedSoundFiles.Entry(slot));
            }
        var document = new SettingsBackupDocument
        {
            Product = "DuckbillFisher", FormatVersion = FormatVersion,
            SettingsVersion = PreferencesMigration.CurrentVersion, CreatedUtc = DateTimeOffset.UtcNow,
            Settings = settings,
            SoundHashes = sounds.ToDictionary(pair => ManagedSoundFiles.Entry(pair.Key), pair => Convert.ToHexString(SHA256.HashData(pair.Value))),
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        if (json.Length > MaxJsonBytes) throw new BackupException(BackupIssue.Invalid);
        var destination = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(destination), includeSounds ? ".zip" : ".json", StringComparison.OrdinalIgnoreCase))
            throw new BackupException(BackupIssue.Invalid);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                if (includeSounds)
                {
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true);
                    await using (var content = archive.CreateEntry("settings.json", CompressionLevel.Optimal).Open())
                        await content.WriteAsync(json, token);
                    foreach (var (slot, bytes) in sounds)
                    {
                        await using var content = archive.CreateEntry(ManagedSoundFiles.Entry(slot), CompressionLevel.Optimal).Open();
                        await content.WriteAsync(bytes, token);
                    }
                }
                else await stream.WriteAsync(json, token);
            }
            token.ThrowIfCancellationRequested();
            // Backups are never silently overwritten; the UI asks for a new name.
            File.Move(temporary, destination, false);
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static async Task<PreparedSettingsBackup> ReadAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (stream.Length > MaxArchiveBytes) throw new BackupException(BackupIssue.Invalid);
        SettingsBackupDocument document;
        var sounds = new Dictionary<SoundSlot, byte[]>();
        if (string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            if (archive.Entries.Count is < 1 or > 5 || archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != archive.Entries.Count)
                throw new BackupException(BackupIssue.Invalid);
            var manifest = archive.GetEntry("settings.json") ?? throw new BackupException(BackupIssue.Invalid);
            if (manifest.Length > MaxJsonBytes) throw new BackupException(BackupIssue.Invalid);
            await using (var content = manifest.Open()) document = Parse(await ReadLimitedAsync(content, MaxJsonBytes, token));
            var expected = new HashSet<string>(StringComparer.Ordinal) { "settings.json" };
            foreach (var slot in ManagedSoundFiles.Slots)
            {
                var selected = ManagedSoundFiles.Selected(document.Settings, slot);
                if (selected.Length == 0) continue;
                var entryName = ManagedSoundFiles.Entry(slot);
                if (selected != entryName || !document.SoundHashes.TryGetValue(entryName, out var hash))
                    throw new BackupException(BackupIssue.Invalid);
                expected.Add(entryName);
                var entry = archive.GetEntry(entryName) ?? throw new BackupException(BackupIssue.Invalid);
                if (entry.Length > ManagedSoundFiles.MaxBytes) throw new BackupException(BackupIssue.Invalid);
                await using var content = entry.Open();
                var bytes = await ReadLimitedAsync(content, ManagedSoundFiles.MaxBytes, token);
                ManagedSoundFiles.Validate(bytes);
                if (!string.Equals(hash, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase))
                    throw new BackupException(BackupIssue.Invalid);
                sounds[slot] = bytes;
            }
            if (archive.Entries.Any(entry => !expected.Contains(entry.FullName)) || document.SoundHashes.Count != sounds.Count)
                throw new BackupException(BackupIssue.Invalid);
        }
        else if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            document = Parse(await ReadLimitedAsync(stream, MaxJsonBytes, token));
            if (document.SoundHashes.Count != 0 || ManagedSoundFiles.Slots.Any(slot =>
                ManagedSoundFiles.Selected(document.Settings, slot).StartsWith("sounds/", StringComparison.Ordinal)))
                throw new BackupException(BackupIssue.Invalid);
        }
        else throw new BackupException(BackupIssue.Invalid);
        // All file validation precedes migration; defaults for future added settings live in the DTO.
        document.Settings = document.Settings.Migrate(document.SettingsVersion);
        return new(document, sounds);
    }

    private static SettingsBackupDocument Parse(byte[] bytes)
    {
        var document = JsonSerializer.Deserialize<SettingsBackupDocument>(bytes, Json);
        if (document is null || document.Product != "DuckbillFisher") throw new BackupException(BackupIssue.Invalid);
        if (document.FormatVersion != FormatVersion || document.SettingsVersion < FirstSettingsVersion ||
            !PreferencesMigration.Supports(document.SettingsVersion)) throw new BackupException(BackupIssue.Unsupported);
        if (document.CreatedUtc == default || document.SoundHashes is null || document.Settings is null ||
            !document.Settings.IsValid(document.SettingsVersion)) throw new BackupException(BackupIssue.Invalid);
        return document;
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, int maximum, CancellationToken token)
    {
        using var result = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (result.Length + count > maximum) throw new BackupException(BackupIssue.Invalid);
            result.Write(buffer, 0, count);
        }
        return result.ToArray();
    }
}
