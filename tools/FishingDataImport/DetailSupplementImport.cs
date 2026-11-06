using System.Security.Cryptography;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only extraction of positive published thresholds; never infers a value from level or gear.
internal static class DetailSupplementImport
{
    public static void Run(string[] args)
    {
        if (args.Length != 5) throw new ArgumentException("Usage: --detail-supplement <fishing-sources.json> <source-manifest.json> <KR game> <global game> <output.json>");
        var bytes = File.ReadAllBytes(args[0]);
        using var manifest = JsonDocument.Parse(File.ReadAllText(args[1]));
        var source = manifest.RootElement;
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(hash, source.GetProperty("files").GetProperty("fishing-sources.json").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("DetailSupplementSourceHash");
        var kr = Identities(args[2], Language.Korean);
        var global = Identities(args[3], Language.English);
        using var input = JsonDocument.Parse(bytes);
        var entries = new List<Entry>();
        foreach (var item in input.RootElement.EnumerateObject())
        {
            var id = uint.Parse(item.Name, System.Globalization.CultureInfo.InvariantCulture);
            var values = item.Value.EnumerateArray().Where(row => row.TryGetProperty("minGathering", out var value) && value.ValueKind == JsonValueKind.Number)
                .Select(row => row.GetProperty("minGathering").GetInt32()).ToArray();
            if (values.Any(value => value < 0 || value > 100000)) throw new InvalidDataException($"DetailSupplementRange:{id}");
            var positive = values.Where(value => value > 0).Distinct().ToArray();
            if (positive.Length == 0) continue; // 0/missing is unconfirmed, not a confirmed requirement of zero.
            if (positive.Length != 1 || !kr.TryGetValue(id, out var fish) || !global.TryGetValue(id, out var globalFish) || fish != globalFish)
                throw new InvalidDataException($"DetailSupplementIdentityOrConflict:{id}");
            entries.Add(new(id, fish, positive[0]));
        }
        if (entries.Count == 0) throw new InvalidDataException("DetailSupplementEmpty");
        var result = new
        {
            schemaVersion = 1,
            source = new
            {
                url = source.GetProperty("repository").GetString() + "/blob/" + source.GetProperty("revision").GetString() + "/libs/data/src/lib/json/fishing-sources.json",
                revision = source.GetProperty("revision").GetString(), sha256 = hash, license = "MIT",
                processing = "Positive minGathering only. Zero/missing omitted as unknown. Unique Item/FishParameter identities matched on both installed clients. No multiple-hook guarantee thresholds inferred."
            },
            entries = entries.OrderBy(entry => entry.ItemId).ToArray()
        };
        File.WriteAllText(args[4], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
        Console.WriteLine($"DetailSupplement: confirmed={entries.Count}; conflicts=0; KR/global identities matched; source={hash}");
    }

    private static Dictionary<uint, uint> Identities(string gamePath, Language language)
    {
        using var game = new GameData(Path.Combine(gamePath, "sqpack"), new LuminaOptions { DefaultExcelLanguage = language });
        return game.GetExcelSheet<FishParameter>()!.Where(fish => fish.RowId != 0 && fish.Item.RowId != 0 && fish.Item.Is<Item>())
            .GroupBy(fish => fish.Item.RowId).Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().RowId);
    }

    private sealed record Entry(uint ItemId, uint FishParameterId, int MinimumGathering);
}
