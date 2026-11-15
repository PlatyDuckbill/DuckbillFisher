using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only inventory. Unknowns are reported, never converted into yields.
internal static class HookYieldAudit
{
    public static void Run(string[] args)
    {
        if (args.Length != 5)
            throw new ArgumentException("Usage: --hook-yield-audit <conditions.json> <hook-yields.json> <KR game> <global game> <output.csv>");
        using var conditions = JsonDocument.Parse(File.ReadAllText(args[0]));
        using var yields = JsonDocument.Parse(File.ReadAllText(args[1]));
        var fish = conditions.RootElement.GetProperty("fish").EnumerateArray().ToDictionary(f => f.GetProperty("itemId").GetUInt32());
        var predators = fish.Values.SelectMany(f => f.GetProperty("predators").EnumerateArray())
            .Select(p => p.GetProperty("itemId").GetUInt32()).ToHashSet();
        var root = yields.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("HookYieldSchema");
        var sources = root.GetProperty("sources").EnumerateArray().ToDictionary(s => s.GetProperty("id").GetString()!);
        foreach (var source in sources.Values)
        {
            var hash = source.TryGetProperty("contentSha256", out var content) ? content.GetString() : source.GetProperty("descriptionSha256").GetString();
            if (!Uri.TryCreate(source.GetProperty("url").GetString(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("HookYieldSource");
        }
        var entries = root.GetProperty("entries").EnumerateArray().ToDictionary(e => e.GetProperty("itemId").GetUInt32());
        if (sources.Count == 0 || entries.Count is < 1 or > 4096) throw new InvalidDataException("HookYieldSchema");
        var kr = Read(args[2], Language.Korean);
        var global = Read(args[3], Language.English);
        var globalById = global.ToDictionary(f => f.Id);
        if (kr.Length != global.Length) throw new InvalidDataException("HookYieldClientRowCount");
        foreach (var row in kr)
            if (!globalById.TryGetValue(row.Id, out var other) || row.Item != other.Item || row.Kind != other.Kind || row.Ocean != other.Ocean || row.InLog != other.InLog)
                throw new InvalidDataException($"HookYieldClientIdentity:{row.Id}");
        var rows = kr.ToDictionary(f => f.Id);
        foreach (var (item, entry) in entries)
        {
            var id = entry.GetProperty("fishParameterId").GetUInt32();
            var ocean = entry.TryGetProperty("ocean", out var flag) && flag.GetBoolean();
            if (!rows.TryGetValue(id, out var row) || row.Item != item || row.Kind != "Item" || row.Ocean != ocean ||
                string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(globalById[id].Name) ||
                !sources.ContainsKey(entry.GetProperty("source").GetString()!) ||
                !ocean && (!fish.TryGetValue(item, out var rule) || rule.GetProperty("fishParameterId").GetUInt32() != id) ||
                State(entry, "doubleHook") == "Unknown" && State(entry, "tripleHook") == "Unknown")
                throw new InvalidDataException($"HookYieldEntryIdentity:{id}:{item}");
        }
        var csv = new StringBuilder("fishParameterId,itemId,itemKind,category,nameKr,nameEn,inLog,intuitionPrerequisite,doubleState,doubleMinimum,doubleMaximum,tripleState,tripleMinimum,tripleMaximum,source,reason\n");
        var totals = new Dictionary<string, int[]>();
        foreach (var row in kr.OrderBy(f => f.Id))
        {
            var hasRule = fish.TryGetValue(row.Item, out var rule) && rule.GetProperty("fishParameterId").GetUInt32() == row.Id && row.Kind == "Item";
            var category = row.Id == 0 || row.Item == 0 || string.IsNullOrWhiteSpace(row.Name) ? "Unresolved"
                : row.Kind == "EventItem" ? "EventItem" : row.Ocean ? "Ocean" : hasRule ? "RegularConditions" : "OtherItem";
            var hasEntry = entries.TryGetValue(row.Item, out var entry) && entry.GetProperty("fishParameterId").GetUInt32() == row.Id && row.Kind == "Item";
            var dh = hasEntry ? State(entry, "doubleHook") : "Unknown";
            var th = hasEntry ? State(entry, "tripleHook") : "Unknown";
            var prerequisite = hasRule && predators.Contains(row.Item);
            Count(category); if (prerequisite) Count("IntuitionPrerequisites");
            var reason = dh != "Unknown" && th != "Unknown" ? "KnownBoth"
                : category == "Unresolved" ? "ClientRowUnresolved"
                : category is "EventItem" or "OtherItem" ? "OutsideRegularConditionDataset_NoNumericEvidence"
                : hasEntry ? "ActionQuantityUnconfirmed" : "NoYieldEntry";
            var fields = new[] { row.Id.ToString(), row.Item.ToString(), row.Kind, category, row.Name, globalById[row.Id].Name,
                row.InLog ? "true" : "false", prerequisite ? "true" : "false", dh, Number("doubleHook", "minimum"), Number("doubleHook", "maximum"),
                th, Number("tripleHook", "minimum"), Number("tripleHook", "maximum"), hasEntry ? entry.GetProperty("source").GetString()! : "", reason };
            csv.AppendLine(string.Join(',', fields.Select(s => '"' + s.Replace("\"", "\"\"") + '"')));

            string Number(string action, string bound) => hasEntry && entry.GetProperty(action).ValueKind != JsonValueKind.Null &&
                entry.GetProperty(action).GetProperty(bound).ValueKind != JsonValueKind.Null ? entry.GetProperty(action).GetProperty(bound).GetInt32().ToString() : "";
            void Count(string group)
            {
                if (!totals.TryGetValue(group, out var counts)) totals[group] = counts = new int[5];
                counts[0]++; if (dh != "Unknown") counts[1]++; if (th != "Unknown") counts[2]++;
                if (dh == "Unknown" && th == "Unknown") counts[3]++;
                if ((dh == "Unknown") != (th == "Unknown")) counts[4]++;
            }
        }
        var output = Path.GetFullPath(args[4]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, csv.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        var summary = new
        {
            format = "hook-yield-audit/v1", dataVersion = root.GetProperty("dataVersion").GetString(),
            conditionsSha256 = Hash(args[0]), hookYieldsSha256 = Hash(args[1]), csvSha256 = Hash(output),
            clientRows = kr.Length, identityMismatches = 0, entries = entries.Count,
            counts = totals.ToDictionary(p => p.Key, p => new { rows = p.Value[0], doubleKnown = p.Value[1], tripleKnown = p.Value[2], bothUnknown = p.Value[3], oneUnknown = p.Value[4] })
        };
        File.WriteAllText(Path.ChangeExtension(output, ".summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine(JsonSerializer.Serialize(summary));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n"))));
    private static string State(JsonElement entry, string key)
    {
        var range = entry.GetProperty(key);
        if (range.ValueKind == JsonValueKind.Null) return "Unknown";
        var min = range.GetProperty("minimum").GetInt32();
        int? max = range.GetProperty("maximum").ValueKind == JsonValueKind.Null ? null : range.GetProperty("maximum").GetInt32();
        if (min is < 1 or > 999 || max is not null && (max < min || max > 999)) throw new InvalidDataException("HookYieldRange");
        return max is null ? "AtLeast" : max == min ? "Exact" : "Range";
    }
    private sealed record Row(uint Id, uint Item, string Kind, string Name, bool InLog, bool Ocean);
    private static Row[] Read(string path, Language language)
    {
        using var game = new GameData(Path.Combine(path, "sqpack"), new LuminaOptions { DefaultExcelLanguage = language });
        return game.GetExcelSheet<FishParameter>()!.Select(f => new Row(f.RowId, f.Item.RowId,
            f.Item.Is<Item>() ? "Item" : f.Item.Is<EventItem>() ? "EventItem" : "Unsupported",
            f.Item.Is<Item>() ? f.Item.GetValueOrDefault<Item>()?.Name.ExtractText() ?? ""
                : f.Item.Is<EventItem>() ? f.Item.GetValueOrDefault<EventItem>()?.Name.ExtractText() ?? "" : "", f.IsInLog, f.OceanStars > 0)).ToArray();
    }
}
