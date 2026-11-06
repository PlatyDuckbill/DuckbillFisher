using System.Security.Cryptography;
using System.Text.Json;
using DuckbillFisher.Core;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Microsoft.VisualBasic.FileIO;

internal static class OceanConditionImport
{
    public static void Run(string[] args)
    {
        if (args.Length != 6) throw new ArgumentException("Usage: --ocean-conditions <indigo.csv> <ruby.csv> <source-manifest> <KR game> <global game> <output>");
        using var kr = new GameData(Path.Combine(args[3], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
        using var global = new GameData(Path.Combine(args[4], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.English });
        var krFish = kr.GetExcelSheet<FishParameter>()!.Where(f => f.RowId != 0 && f.Item.Is<Item>() && f.OceanStars > 0).ToDictionary(f => f.Item.RowId);
        var fish = global.GetExcelSheet<FishParameter>()!.Where(f => f.RowId != 0 && f.Item.Is<Item>() && f.OceanStars > 0)
            .GroupBy(f => f.Item.GetValueOrDefault<Item>()!.Value.Name.ExtractText(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);
        var weather = global.GetExcelSheet<Weather>()!.Where(w => w.RowId > 0 && w.Name.ExtractText().Length > 0)
            .GroupBy(w => w.Name.ExtractText()).ToDictionary(g => g.Key, g => g.Select(w => w.RowId).Order().ToArray());
        var spots = global.GetExcelSheet<IKDSpot>()!.Where(s => s.RowId != 0).ToArray();
        using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
        var sources = new List<object>();
        var entries = new Dictionary<(uint Item, uint Spot), OceanFishCondition>();
        for (var route = 0; route < 2; route++)
        {
            var file = args[route];
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            var source = manifest.RootElement.GetProperty("sources").EnumerateArray().Single(s => s.GetProperty("url").GetString()!.EndsWith(route == 0 ? "/indigo-EN.csv" : "/ruby-EN.csv", StringComparison.Ordinal));
            if (!hash.Equals(source.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("OceanConditionHash");
            sources.Add(new { url = source.GetProperty("url").GetString(), sha256 = hash, retrieved = "2026-11-06" });
            using var csv = new TextFieldParser(file) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
            csv.SetDelimiters(",");
            var headers = csv.ReadFields()!;
            string Field(string[] row, string key) { var i = Array.IndexOf(headers, key); return i < 0 ? throw new InvalidDataException("OceanConditionColumn") : row[i].Trim(); }
            while (!csv.EndOfData)
            {
                var row = csv.ReadFields()!;
                if (!fish.TryGetValue(Field(row, "FishTranslated"), out var f) || !krFish.TryGetValue(f.Item.RowId, out var k) ||
                    f.RowId != k.RowId || f.FishingSpot.RowId != k.FishingSpot.RowId) throw new InvalidDataException("OceanConditionIdentity");
                var spectral = Field(row, "TimeFrameDay").Length > 0;
                var stop = spots.Single(s => s.SpotMain.RowId == f.FishingSpot.RowId || s.SpotSub.RowId == f.FishingSpot.RowId);
                var spotId = spectral ? stop.SpotSub.RowId : stop.SpotMain.RowId;
                if (!global.GetExcelSheet<FishingSpot>()!.GetRow(spotId).Item.Any(i => i.RowId == f.Item.RowId) ||
                    !kr.GetExcelSheet<FishingSpot>()!.GetRow(spotId).Item.Any(i => i.RowId == f.Item.RowId)) throw new InvalidDataException("OceanConditionSpotMembership");
                var mask = 14;
                if (spectral)
                {
                    mask = 0;
                    foreach (var (field, time) in new[] { ("TimeFrameNight", 1), ("TimeFrameDay", 2), ("TimeFrameSunset", 3) })
                    {
                        var value = Field(row, field);
                        if (value is not ("Yes" or "No")) throw new InvalidDataException("OceanConditionTime");
                        if (value == "Yes") mask |= 1 << time;
                    }
                    if (mask == 0) throw new InvalidDataException("OceanConditionEmptyTime");
                }
                else if (Field(row, "TimeFrameDay").Length != 0) throw new InvalidDataException($"OceanConditionPool:{Field(row, "FishTranslated")}:{f.FishingSpot.RowId}");
                var allowed = new List<uint>(); var excluded = new List<uint>(); var unknown = new List<uint>();
                if (!spectral)
                    foreach (var column in new[] { "FairSkies", "Clouds", "Fog", "Special1", "Special2", "ClearSkies" })
                    {
                        var name = column switch { "FairSkies" => "Fair Skies", "ClearSkies" => "Clear Skies", "Special1" or "Special2" => Field(row, $"Weather{column}TypeTranslated"), _ => column };
                        if (!weather.TryGetValue(name, out var ids) || ids.Any(id => !kr.GetExcelSheet<Weather>()!.TryGetRow(id, out _))) throw new InvalidDataException($"OceanConditionWeather:{name}");
                        var value = Field(row, $"Weather{column}");
                        if (value == "Yes") allowed.AddRange(ids);
                        else if (value == "???") unknown.AddRange(ids);
                        else if (value == "No") excluded.AddRange(ids);
                        else throw new InvalidDataException("OceanConditionWeatherValue");
                    }
                var fact = new OceanFishCondition(f.Item.RowId, f.RowId, spotId, mask,
                    allowed.Distinct().Order().ToArray(), excluded.Distinct().Order().ToArray(), unknown.Distinct().Order().ToArray(), Field(row, "Intuition").Length > 0);
                if (entries.TryGetValue((f.Item.RowId, spotId), out var previous) && JsonSerializer.Serialize(previous) != JsonSerializer.Serialize(fact)) throw new InvalidDataException("OceanConditionConflict");
                entries[(f.Item.RowId, spotId)] = fact;
            }
        }
        var convention = manifest.RootElement.GetProperty("sources").EnumerateArray().Single(s => s.GetProperty("url").GetString()!.EndsWith("/displayStops.js", StringComparison.Ordinal));
        sources.Add(new { url = convention.GetProperty("url").GetString(), sha256 = convention.GetProperty("sha256").GetString(), retrieved = "2026-11-06" });
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(args[5], JsonSerializer.Serialize(new { schemaVersion = 1, dataVersion = "ocean-conditions-2026-11-06", author = "Tyo'to Tayuun", sources,
            rights = "No public reuse license identified. Individual condition facts only; no prose, code, images or strategies redistributed.",
            processing = "Exact Yes/No flags; ??? remains unknown. Blank time columns mark standard pools, blank weather columns mark spectral pools, as in the source site's displayStops.js. Linked and checked against both installed clients. No bait/achievement/score/intuition-prey inference.",
            entries = entries.Values.OrderBy(e => e.FishingSpotId).ThenBy(e => e.ItemId) }, options) + "\n");
        Console.WriteLine($"OceanConditions: entries={entries.Count}; intuition={entries.Values.Count(e => e.RequiresIntuition)}; unknownWeather={entries.Values.Count(e => e.UnknownWeatherIds.Length > 0)}");
    }
}
