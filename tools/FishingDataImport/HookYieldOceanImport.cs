using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

internal static class HookYieldOceanImport
{
    public static void Run(string[] args)
    {
        if (args.Length != 6) throw new ArgumentException("Usage: --ocean-hook-yields <indigo.csv> <ruby.csv> <ocean-source.json> <KR game> <global game> <hook-yields.json>");
        using var kr = new GameData(Path.Combine(args[3], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
        using var global = new GameData(Path.Combine(args[4], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.English });
        var krFish = kr.GetExcelSheet<FishParameter>()!.Where(f => f.RowId != 0 && f.Item.RowId != 0 && f.Item.Is<Item>() && f.OceanStars > 0).ToDictionary(f => f.Item.RowId, f => f.RowId);
        var globalFish = global.GetExcelSheet<FishParameter>()!.Where(f => f.RowId != 0 && f.Item.RowId != 0 && f.Item.Is<Item>() && f.OceanStars > 0)
            .Select(f => new { Item = f.Item.RowId, Fish = f.RowId, Name = f.Item.GetValueOrDefault<Item>()?.Name.ExtractText() ?? "" })
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);
        using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
        var root = JsonNode.Parse(File.ReadAllText(args[5]))!.AsObject();
        var sources = root["sources"]!.AsArray();
        var entries = new Dictionary<uint, JsonObject>();
        var skipped = new List<string>();
        for (var route = 0; route < 2; route++)
        {
            var file = args[route];
            var source = manifest.RootElement.GetProperty("sources").EnumerateArray().Single(s => s.GetProperty("url").GetString()!.EndsWith(route == 0 ? "/indigo-EN.csv" : "/ruby-EN.csv", StringComparison.Ordinal));
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            if (!string.Equals(hash, source.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("OceanHookYieldSourceHash");
            var sourceId = route == 0 ? "ocean-indigo-2026-11-06" : "ocean-ruby-2026-11-06";
            foreach (var previous in sources.Where(s => s?["id"]?.GetValue<string>() == sourceId).ToArray()) sources.Remove(previous);
            sources.Add(new JsonObject { ["id"] = sourceId, ["url"] = source.GetProperty("url").GetString(), ["contentSha256"] = hash, ["checkedUtc"] = "2026-11-06" });
            using var csv = new TextFieldParser(file) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
            csv.SetDelimiters(",");
            var headers = csv.ReadFields()!;
            var nameIndex = Array.IndexOf(headers, "FishTranslated");
            var doubleIndex = Array.IndexOf(headers, "DH");
            var tripleIndex = Array.IndexOf(headers, "TH");
            if (nameIndex < 0 || doubleIndex < 0 || tripleIndex < 0) throw new InvalidDataException("OceanHookYieldColumns");
            while (!csv.EndOfData)
            {
                var row = csv.ReadFields()!;
                var name = row[nameIndex].Trim();
                if (!globalFish.TryGetValue(name, out var fish) || !krFish.TryGetValue(fish.Item, out var krId) || krId != fish.Fish)
                { skipped.Add(name); continue; }
                var dh = Range(row[doubleIndex]); var th = Range(row[tripleIndex]);
                if (dh is null && th is null) { skipped.Add(name); continue; }
                var value = new JsonObject { ["itemId"] = fish.Item, ["fishParameterId"] = fish.Fish, ["source"] = sourceId,
                    ["doubleHook"] = dh, ["tripleHook"] = th, ["ocean"] = true };
                if (entries.TryGetValue(fish.Item, out var previous) && (!JsonNode.DeepEquals(previous["doubleHook"], dh) || !JsonNode.DeepEquals(previous["tripleHook"], th)))
                    throw new InvalidDataException($"OceanHookYieldConflict:{fish.Item}");
                entries[fish.Item] = value;
            }
        }
        var regular = root["entries"]!.AsArray().Where(e => e?["ocean"]?.GetValue<bool>() != true).Select(e => e!.DeepClone()).ToArray();
        root["entries"] = new JsonArray(regular.Concat(entries.OrderBy(p => p.Key).Select(p => (JsonNode)p.Value)).ToArray());
        root["dataVersion"] = "regular-and-ocean-hook-yields-2026-11-06";
        root["checkedUtc"] = "2026-11-06";
        root["rights"] = "Fruity Snacks and Tyo'to Tayuun gameplay observations. No public reuse license identified. Individual numeric facts only; no prose, code, images, bite times or strategies redistributed.";
        File.WriteAllText(args[5], root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"OceanHookYields: regular={regular.Length}; ocean={entries.Count}; double={entries.Values.Count(e => e["doubleHook"] is not null)}; triple={entries.Values.Count(e => e["tripleHook"] is not null)}; skipped={string.Join(',', skipped.Distinct())}");
    }

    private static JsonObject? Range(string text)
    {
        var match = Regex.Match(text.Trim(), @"^(?<min>\d+)(?:\s*[-–]\s*(?<max>\d+))?$");
        if (!match.Success) return null;
        var min = int.Parse(match.Groups["min"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var max = match.Groups["max"].Success ? int.Parse(match.Groups["max"].Value, System.Globalization.CultureInfo.InvariantCulture) : min;
        if (min < 1 || max < min || max > 999) throw new InvalidDataException("OceanHookYieldRange");
        return new JsonObject { ["minimum"] = min, ["maximum"] = max };
    }
}
