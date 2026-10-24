namespace DuckbillFisher.Plugin;

internal enum SoundSlot { SpawnAlert, Bite, BigFish, LegendaryFish }

/// <summary>Four stable, owned WAV files. Never uses a filename supplied by a backup.</summary>
internal sealed class ManagedSoundFiles(string configDirectory)
{
    private readonly string directory = Path.GetFullPath(Path.Combine(configDirectory, "sounds"));
    private readonly SemaphoreSlim writes = new(1, 1);
    public static readonly SoundSlot[] Slots = Enum.GetValues<SoundSlot>();
    public const int MaxBytes = CustomAlertSound.MaxMegabytes * 1024 * 1024;

    public static string Name(SoundSlot slot) => slot switch
    {
        SoundSlot.SpawnAlert => "spawn-alert.wav",
        SoundSlot.Bite => "bite.wav",
        SoundSlot.BigFish => "big-fish.wav",
        SoundSlot.LegendaryFish => "legendary-fish.wav",
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
    public static string Entry(SoundSlot slot) => "sounds/" + Name(slot);
    public string FilePath(SoundSlot slot) => Path.Combine(directory, Name(slot));
    public bool IsManaged(SoundSlot slot, string path) => string.Equals(FilePath(slot), path, StringComparison.OrdinalIgnoreCase);

    public static bool UsesCustomSound(CommonSettingsBackup settings, SoundSlot slot) => (slot switch
    {
        SoundSlot.SpawnAlert => settings.SpawnAlertSound,
        SoundSlot.Bite => settings.Timer.BiteSound.Sound,
        SoundSlot.BigFish => settings.Timer.BigFishSound.Sound,
        _ => settings.Timer.LegendaryFishSound.Sound,
    }) == CustomAlertSound.Choice;
    public static bool UsesCustomSound(FishPreferencesView settings, SoundSlot slot) => (slot switch
    {
        SoundSlot.SpawnAlert => settings.SpawnAlertSound,
        SoundSlot.Bite => settings.Timer.BiteSound.Sound,
        SoundSlot.BigFish => settings.Timer.BigFishSound.Sound,
        _ => settings.Timer.LegendaryFishSound.Sound,
    }) == CustomAlertSound.Choice;

    public static string Selected(CommonSettingsBackup settings, SoundSlot slot) => slot switch
    {
        SoundSlot.SpawnAlert => settings.CustomAlertSoundPath,
        SoundSlot.Bite => settings.Timer.BiteSound.CustomPath,
        SoundSlot.BigFish => settings.Timer.BigFishSound.CustomPath,
        _ => settings.Timer.LegendaryFishSound.CustomPath,
    };
    public static string Selected(FishPreferencesView settings, SoundSlot slot) => slot switch
    {
        SoundSlot.SpawnAlert => settings.CustomAlertSoundPath,
        SoundSlot.Bite => settings.Timer.BiteSound.CustomPath,
        SoundSlot.BigFish => settings.Timer.BigFishSound.CustomPath,
        _ => settings.Timer.LegendaryFishSound.CustomPath,
    };
    public static void Select(CommonSettingsBackup settings, SoundSlot slot, string path)
    {
        switch (slot)
        {
            case SoundSlot.SpawnAlert: settings.CustomAlertSoundPath = path; break;
            case SoundSlot.Bite: settings.Timer = settings.Timer with { BiteSound = settings.Timer.BiteSound with { CustomPath = path } }; break;
            case SoundSlot.BigFish: settings.Timer = settings.Timer with { BigFishSound = settings.Timer.BigFishSound with { CustomPath = path } }; break;
            case SoundSlot.LegendaryFish: settings.Timer = settings.Timer with { LegendaryFishSound = settings.Timer.LegendaryFishSound with { CustomPath = path } }; break;
        }
    }

    public static async Task<byte[]> ReadAsync(string path, CancellationToken token)
    {
        if (path.Length > 4096 || path.Contains('"') || path.Any(char.IsControl) ||
            !Path.IsPathFullyQualified(path) || path.StartsWith(@"\", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("InvalidWavePath");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            81920, FileOptions.Asynchronous);
        if (stream.Length is < 12 or > MaxBytes) throw new InvalidDataException("WaveSizeLimit");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, token);
        Validate(bytes);
        return bytes;
    }

    public static void Validate(byte[] bytes)
    {
        if (bytes.Length is < 12 or > MaxBytes || !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8)) throw new InvalidDataException("InvalidWave");
    }

    public async Task<string> StoreAsync(SoundSlot slot, byte[] bytes, CancellationToken token)
    {
        Validate(bytes);
        await writes.WaitAsync(token);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(directory);
            var destination = FilePath(slot);
            temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            return destination;
        }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            writes.Release();
        }
    }

    public async Task<byte[]?> PreviousAsync(SoundSlot slot, CancellationToken token)
    {
        var path = FilePath(slot);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                81920, FileOptions.Asynchronous);
            if (stream.Length > MaxBytes) throw new InvalidDataException("ExistingWaveSizeLimit");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, token);
            return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task RollbackAsync(SoundSlot slot, byte[]? bytes)
    {
        // Old owned files may be corrupt, so rollback deliberately does not validate their contents.
        await writes.WaitAsync();
        string? temporary = null;
        try
        {
            var destination = FilePath(slot);
            if (bytes is null) { File.Delete(destination); return; }
            temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            writes.Release();
        }
    }
}
