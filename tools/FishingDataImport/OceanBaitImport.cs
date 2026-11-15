using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Microsoft.VisualBasic.FileIO;

internal static class OceanBaitImport
{
    public static void Run(string[] args)
    {
        if (args.Length != 7) throw new ArgumentException("Usage: --ocean-baits <indigo.csv> <ruby.csv> <guidance.json> <conditions.json> <KR game> <global game> <output.json>");
        using var kr = new GameData(Path.Combine(args[4], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
        using var global = new GameData(Path.Combine(args[5], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.English });
        var fish = global.GetExcelSheet<FishParameter>()!.Where(f => f.RowId != 0 && f.Item.Is<Item>() && f.OceanStars > 0)
            .ToDictionary(f => f.Item.GetValueOrDefault<Item>()!.Value.Name.ExtractText(), StringComparer.OrdinalIgnoreCase);
        var items = global.GetExcelSheet<Item>()!.Where(i => i.RowId != 0 && i.Name.ExtractText().Length > 0)
            .GroupBy(i => i.Name.ExtractText(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);
        using var conditions = JsonDocument.Parse(File.ReadAllText(args[3]));
        var rules = JsonSerializer.Deserialize<OceanFishCondition[]>(conditions.RootElement.GetProperty("entries"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var root = JsonNode.Parse(File.ReadAllText(args[2]))!.AsObject();
        var targets = new Dictionary<(uint Spot, uint Item), object>();
        var missing = 0; var mooch = 0;
        for (var route = 0; route < 2; route++)
        {
            var source = root["sources"]!.AsArray().Single(s => s!["url"]!.GetValue<string>().EndsWith(route == 0 ? "/indigo-EN.csv" : "/ruby-EN.csv", StringComparison.Ordinal))!;
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[route])));
            if (!hash.Equals(source["sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("OceanBaitSourceHash");
            using var csv = new TextFieldParser(args[route]) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
            csv.SetDelimiters(",");
            var headers = csv.ReadFields()!;
            string Field(string[] row, string key)
            {
                var index = Array.IndexOf(headers, key);
                return index >= 0 ? row[index].Trim() : throw new InvalidDataException($"OceanBaitColumn:{key}");
            }
            while (!csv.EndOfData)
            {
                var row = csv.ReadFields()!;
                var name = Field(row, "FishTranslated");
                if (!fish.TryGetValue(name, out var parameter)) throw new InvalidDataException($"OceanBaitFish:{name}");
                var spectral = Field(row, "TimeFrameDay").Length > 0;
                var memberships = rules.Where(r => r.ItemId == parameter.Item.RowId).ToArray();
                // The two-pool Junior Jinbei identity is resolved by the client's actual stop membership.
                var spot = global.GetExcelSheet<IKDSpot>()!.Single(s => s.RowId != 0 && (s.SpotMain.RowId == parameter.FishingSpot.RowId || s.SpotSub.RowId == parameter.FishingSpot.RowId));
                var spotId = spectral ? spot.SpotSub.RowId : spot.SpotMain.RowId;
                var rule = memberships.Single(r => r.FishingSpotId == spotId && r.FishParameterId == parameter.RowId);
                var krParameter = kr.GetExcelSheet<FishParameter>()!.GetRow(parameter.RowId);
                if (!krParameter.Item.Is<Item>() || krParameter.Item.RowId != rule.ItemId ||
                    !kr.GetExcelSheet<FishingSpot>()!.GetRow(spotId).Item.Any(i => i.RowId == rule.ItemId) ||
                    !global.GetExcelSheet<FishingSpot>()!.GetRow(spotId).Item.Any(i => i.RowId == rule.ItemId)) throw new InvalidDataException("OceanBaitIdentity");
                var preferred = Field(row, "BestBaitTranslated");
                var baitId = 0u; var kind = "Unknown";
                if (preferred.Length > 0)
                {
                    if (!items.TryGetValue(preferred, out var bait)) throw new InvalidDataException($"OceanBaitItem:{preferred}");
                    baitId = bait.RowId;
                    kind = bait.ItemUICategory.RowId == FishingGameIds.BaitCategory ? "Direct"
                        : fish.Values.Any(f => f.Item.RowId == baitId) ? "Mooch" : throw new InvalidDataException("OceanBaitKind");
                    var krBait = kr.GetExcelSheet<Item>()!.GetRow(baitId);
                    if (krBait.ItemUICategory.RowId != bait.ItemUICategory.RowId || krBait.Name.ExtractText().Length == 0) throw new InvalidDataException("OceanBaitItemIdentity");
                }
                else missing++;
                if (kind == "Mooch") mooch++;
                if (!int.TryParse(Field(row, "Points"), out var points) || points is < 1 or > 1000) throw new InvalidDataException("OceanBaitPoints");
                var trigger = !spectral && (name.StartsWith("Spectral ", StringComparison.Ordinal) || name == "Spectresaur");
                var tug = Field(row, "Bite");
                if (tug is not ("!" or "!!" or "!!!") || rule.RequiresIntuition != (Field(row, "Intuition").Length > 0)) throw new InvalidDataException("OceanBaitCondition");
                var species = Field(row, "Species");
                var group = OceanFishingGoal.Default;
                if (species.Length > 0 && (!Enum.TryParse(species, out group) || group == OceanFishingGoal.Default || !Enum.IsDefined(group)))
                    throw new InvalidDataException($"OceanBaitGroup:{species}");
                if (!targets.TryAdd((spotId, rule.ItemId), new { fishItemId = rule.ItemId, fishParameterId = rule.FishParameterId,
                    fishingSpotId = spotId, baitItemId = baitId, baitKind = kind, points, spectralTrigger = trigger, tug, group = group.ToString() })) throw new InvalidDataException("OceanBaitDuplicate");
            }
        }
        if (targets.Count != rules.Length) throw new InvalidDataException("OceanBaitCoverage");
        root["targets"] = JsonSerializer.SerializeToNode(targets.OrderBy(p => p.Key.Spot).ThenBy(p => p.Key.Item).Select(p => p.Value));
        root["dataVersion"] = "ocean-guidance-2026-11-16";
        root["checkedUtc"] = "2026-11-16";
        root["targetProcessing"] = "Preferred bait, single-fish points, tug, explicit spectral-trigger identities and Species group facts only from pinned CSV snapshots, checked 2026-11-16. Item/FishParameter/pool/bait-kind matched against both clients. A preferred fish is a mooch source, not inventory bait. Blank preferred bait remains Unknown; blank Species remains Default (unclassified). Groups do not establish achievement IDs, thresholds or player/party progress. No bite times, catch rates, prose or source code redistributed. Existing 52 basic rows are preserved.";
        File.WriteAllText(args[6], root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
        Console.WriteLine($"OceanBaits: targets={targets.Count}; missingBait={missing}; mooch={mooch}; originalBasicRows={root["entries"]!.AsArray().Count}");
    }
}
