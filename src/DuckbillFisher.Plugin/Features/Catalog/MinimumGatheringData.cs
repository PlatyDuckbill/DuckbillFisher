using System.Collections.Frozen;
using System.Text.Json;

namespace DuckbillFisher.Plugin;

internal sealed record MinimumGatheringEntry(uint ItemId, uint FishParameterId, int MinimumGathering);

internal static class MinimumGatheringData
{
    public static IReadOnlyDictionary<uint, MinimumGatheringEntry> ByItem { get; }
    public static bool Invalid { get; }

    static MinimumGatheringData()
    {
        try
        {
            using var stream = typeof(MinimumGatheringData).Assembly.GetManifestResourceStream("DuckbillFisher.MinimumGathering")
                ?? throw new InvalidDataException("MinimumGatheringMissing");
            var dataset = JsonSerializer.Deserialize<Dataset>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("MinimumGatheringEmpty");
            if (dataset.SchemaVersion != 1 || dataset.Entries is not { Length: > 0 } || dataset.Source is not { License: "MIT" } source ||
                source.Revision is not { Length: 40 } || !source.Revision.All(Uri.IsHexDigit) || source.Sha256 is not { Length: 64 } || !source.Sha256.All(Uri.IsHexDigit) ||
                !Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                throw new InvalidDataException("MinimumGatheringSchema");
            if (dataset.Entries.Any(entry => entry is null || entry.ItemId == 0 || entry.FishParameterId == 0 || entry.MinimumGathering is < 1 or > 100000) ||
                dataset.Entries.Select(entry => entry.ItemId).Distinct().Count() != dataset.Entries.Length)
                throw new InvalidDataException("MinimumGatheringIdentity");
            ByItem = dataset.Entries.ToFrozenDictionary(entry => entry.ItemId);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or IOException)
        { Invalid = true; ByItem = FrozenDictionary<uint, MinimumGatheringEntry>.Empty; }
    }

    private sealed record Dataset(int SchemaVersion, Source Source, MinimumGatheringEntry[] Entries);
    private sealed record Source(string Url, string Revision, string Sha256, string License);
}
