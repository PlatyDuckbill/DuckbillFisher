using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuckbillFisher.Core;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only, local files and client sheets. Never evaluates JavaScript or reads a game process.
if (args.Length > 0 && args[0] == "--classifications")
{
    FishClassificationImport.Run(args.Skip(1).ToArray());
    return;
}
if (args.Length > 0 && args[0] == "--complete")
{
    FishDataCompletion.Run(args.Skip(1).ToArray());
    return;
}
if (args.Length != 6)
    throw new ArgumentException("Usage: FishingDataImport <manifest> <data.js> <fishData.yaml> <game-directory> <output.json> <audit.json>");
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var source = JsonSerializer.Deserialize<Source>(File.ReadAllText(args[0]), options)!;
var bytes = CheckedBytes(args[1], source.Sha256);
var yaml = System.Text.Encoding.UTF8.GetString(CheckedBytes(args[2], source.YamlSha256));
Require(File.ReadAllText(Path.Combine(args[3], "ffxivgame.ver")).Trim() == source.GameBuild, "Unsupported KR build");
var json = Regex.Replace(System.Text.Encoding.UTF8.GetString(bytes), @"\A\s*const DATA\s*=\s*", "");
json = Regex.Replace(json, @"(?m)^  ([A-Z_]+):", "  \"$1\":").Trim().TrimEnd(';');
using var document = JsonDocument.Parse(json);
var root = document.RootElement;
// Only read block names and the explicit missing-data marker, not the full YAML grammar.
// This marker is lost for some spearfishing rows in the upstream JS generator.
var missingNames = Regex.Split(yaml, @"(?m)^- name: ").Skip(1)
    .Where(block => Regex.IsMatch(block, @"(?m)^  dataMissing: true\s*$"))
    .Select(block => block.Split('\n')[0].Trim().Trim('"', '\'')).ToHashSet(StringComparer.Ordinal);
using var game = new GameData(Path.Combine(args[3], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
var items = game.GetExcelSheet<Item>()!;
var spots = game.GetExcelSheet<FishingSpot>()!;
var weathers = game.GetExcelSheet<Weather>()!;
var fishByItem = game.GetExcelSheet<FishParameter>()!.Where(row => row.RowId > 0 && row.IsInLog && row.OceanStars == 0 && row.Item.Is<Item>())
    .GroupBy(row => row.Item.RowId).ToDictionary(group => group.Key, group => group.ToArray());
var spearByItem = game.GetExcelSheet<SpearfishingItem>()!.Where(row => row.RowId > 0 && row.Item.RowId > 0)
    .GroupBy(row => row.Item.RowId).ToDictionary(group => group.Key, group => group.ToArray());
var rules = new List<FishingRule>();
var spearRules = new List<SpearfishingRule>();
var audit = new List<object>();
var excluded = new List<object>();
var failures = new List<string>();
var coordinateDifferences = new Dictionary<uint, object>();
var matchedMissingNames = new HashSet<string>();
foreach (var entry in root.GetProperty("FISH").EnumerateObject())
{
    try
    {
        var raw = entry.Value;
        var id = raw.GetProperty("_id").GetUInt32();
        Require(entry.Name == id.ToString(System.Globalization.CultureInfo.InvariantCulture), "Source key/ID mismatch");
        var name = root.GetProperty("ITEMS").GetProperty(entry.Name).GetProperty("name_en").GetString()!;
        var missing = missingNames.Contains(name) || raw.GetProperty("dataMissing").ValueKind != JsonValueKind.Null;
        if (missingNames.Contains(name)) matchedMissingNames.Add(name);
        var predators = raw.GetProperty("predators").EnumerateArray().Select(pair =>
        {
            Require(pair.GetArrayLength() == 2, "Invalid predator pair");
            return new FishRequirement(pair[0].GetUInt32(), pair[1].GetInt32());
        }).ToArray();
        Require(predators.All(value => value.Count > 0 && value.ItemId > 0), "Invalid predator requirement");
        var gig = raw.GetProperty("gig");
        if (gig.ValueKind != JsonValueKind.Null)
        {
            if (!spearByItem.TryGetValue(id, out var spear)) { excluded.Add(new { itemId = id, reason = "NoKRSpearfishingItem" }); continue; }
            Require(spear.Length == 1, "Ambiguous KR spear Item");
            CheckItem(id);
            foreach (var predator in predators) CheckItem(predator.ItemId);
            var size = gig.GetString()!;
            Require(size is "Small" or "Normal" or "Large" or "UNKNOWN", "Unsupported gig value");
            spearRules.Add(new(id, spear[0].RowId, raw.GetProperty("location").GetUInt32(), name, size,
                !missing && size != "UNKNOWN", predators));
            continue;
        }
        if (!fishByItem.TryGetValue(id, out var fishRows)) { excluded.Add(new { itemId = id, reason = "NoKRRegularLogEntry" }); continue; }
        Require(fishRows.Length == 1, "Ambiguous FishParameter");
        var fish = fishRows[0];
        CheckItem(id);
        var steps = raw.GetProperty("bestCatchPath").EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Array ? ReadIds(value) : new[] { value.GetUInt32() }).ToArray();
        Require(steps.Length > 0 && steps.All(step => step.Length > 0), "Empty bait path/alternative");
        foreach (var itemId in steps.SelectMany(step => step).Concat(predators.Select(value => value.ItemId))) CheckItem(itemId);
        var current = ReadIds(raw.GetProperty("weatherSet"));
        var previous = ReadIds(raw.GetProperty("previousWeatherSet"));
        foreach (var weatherId in current.Concat(previous).Distinct())
        {
            var value = weathers.GetRow(weatherId);
            Require(value.Icon > 0 && !string.IsNullOrWhiteSpace(value.Name.ExtractText()), "Missing KR weather");
        }
        var spotId = raw.GetProperty("location").ValueKind == JsonValueKind.Null ? 0 : raw.GetProperty("location").GetUInt32();
        if (spotId != 0)
        {
            var spot = spots.GetRow(spotId);
            Require(spot.Item.Any(value => value.RowId == id) || fish.FishingSpot.RowId == spotId, "Fish/spot mismatch");
            Require(spot.PlaceNameMain.RowId == 0 && spot.TerritoryType.RowId != 0, "Unsupported ordinary spot");
            var territory = spot.TerritoryType.Value;
            var map = territory.Map.Value;
            Require(map.RowId > 0 && map.SizeFactor > 0 && map.TerritoryType.RowId == territory.RowId, "Missing map");
            var original = root.GetProperty("FISHING_SPOTS").GetProperty(spotId.ToString());
            Require(original.GetProperty("territory_id").GetUInt32() == territory.RowId, "Territory mismatch");
            var coords = original.GetProperty("map_coords");
            var x = 1 + spot.X * 41.0 / 2048 / (map.SizeFactor / 100.0);
            var y = 1 + spot.Z * 41.0 / 2048 / (map.SizeFactor / 100.0);
            // Conditions use the validated spot ID; map pins always use current KR sheet coordinates.
            // Some upstream coordinates overflow. Keep the difference visible instead of copying them.
            if (Math.Abs(x - coords[0].GetDouble()) >= 0.00001 || Math.Abs(y - coords[1].GetDouble()) >= 0.00001)
                coordinateDifferences[spotId] = new { spotId, mapId = map.RowId, krX = x, krY = y,
                    sourceX = coords[0].GetDouble(), sourceY = coords[1].GetDouble() };
            Require(territory.WeatherRate.Value.Rate.Sum(value => (int)value) == 100, "Invalid weather rates");
        }
        var hookset = raw.GetProperty("hookset").GetString() ?? "";
        var tug = raw.GetProperty("tug").GetString() ?? "";
        var lure = raw.GetProperty("lure").GetString() ?? "";
        Require(hookset is "" or "Precision" or "Powerful", "Unsupported hookset");
        Require(tug is "" or "light" or "medium" or "heavy", "Unsupported tug");
        Require(lure is "" or "Modest" or "Ambitious", "Unsupported lure");
        var duration = raw.GetProperty("intuitionLength");
        double? seconds = duration.ValueKind == JsonValueKind.Null ? null : duration.GetDouble();
        Require(seconds is null || (seconds > 0 && double.IsFinite(seconds.Value) && predators.Length > 0), "Invalid intuition duration");
        var folklore = raw.GetProperty("folklore").ValueKind != JsonValueKind.Null;
        Require(folklore == (fish.GatheringSubCategory.RowId != 0), "KR folklore mismatch");
        if (folklore) CheckItem(fish.GatheringSubCategory.Value.Item.RowId);
        var rule = new FishingRule {
            ItemId = id, FishParameterId = fish.RowId, FishingSpotId = spotId, SourceName = name,
            StartHour = raw.GetProperty("startHour").GetDouble(), EndHour = raw.GetProperty("endHour").GetDouble(),
            WeatherIds = current, PreviousWeatherIds = previous, BaitSteps = steps, Hookset = hookset, Tug = tug,
            Predators = predators, IntuitionSeconds = seconds, FolkloreRequired = folklore,
            FishEyesSupported = raw.GetProperty("fishEyes").GetBoolean(), ConditionsKnown = !missing && spotId > 0,
            SnaggingRequired = raw.GetProperty("snagging").ValueKind == JsonValueKind.True,
            Lure = lure, BigFish = raw.GetProperty("bigFish").GetBoolean(), Patch = raw.GetProperty("patch").GetDouble()
        };
        Require(double.IsFinite(rule.StartHour) && double.IsFinite(rule.EndHour) && rule.StartHour >= 0 && rule.StartHour < 24 &&
            rule.EndHour >= 0 && rule.EndHour <= 24 && rule.StartHour != rule.EndHour, "Invalid time window");
        rules.Add(rule);
        audit.Add(new { rule.FishParameterId, rule.ItemId, rule.SourceName, rule.FishingSpotId, rule.ConditionsKnown,
            hooksetKnown = hookset != "", tugKnown = tug != "", durationKnown = predators.Length == 0 || seconds is not null });
    }
    catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or KeyNotFoundException)
    { failures.Add($"Item={entry.Name}: {error.Message}"); }
}
Require(missingNames.SetEquals(matchedMissingNames), "Not all YAML missing-data markers were matched to source items");
if (failures.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, failures));
Require(rules.Count > 0 && rules.Select(rule => rule.FishParameterId).Distinct().Count() == rules.Count, "Empty or duplicate rules");
var dataset = new { schemaVersion = 2, source.DataVersion, region = "KR", source.GameBuild,
    sourceRepository = source.Repository, sourceRevision = source.Revision, sourceSha256 = source.Sha256, source.YamlSha256,
    sourceFiles = new[] { "private/fishData.yaml", source.File }, fish = rules.OrderBy(rule => rule.FishParameterId).ToArray(),
    spearfishing = spearRules.OrderBy(rule => rule.SpearfishingItemId).ToArray() };
File.WriteAllText(args[4], JsonSerializer.Serialize(dataset, options) + "\n");
File.WriteAllText(args[5], JsonSerializer.Serialize(new { source.GameBuild, source.Revision, regular = rules.Count,
    spear = spearRules.Count, spearMissing = spearRules.Count(rule => !rule.ConditionsKnown), excluded,
    coordinateDifferences = coordinateDifferences.Values, fish = audit }, options) + "\n");
Console.WriteLine($"Regular={rules.Count}; WithSpot={rules.Count(rule => rule.FishingSpotId > 0)}; Spear={spearRules.Count}; SpearMissing={spearRules.Count(rule => !rule.ConditionsKnown)}; Excluded={excluded.Count}; CoordinateDifferences={coordinateDifferences.Count}; Failed=0");

void CheckItem(uint id)
{
    var item = items.GetRow(id);
    Require(id > 0 && item.Icon > 0 && !string.IsNullOrWhiteSpace(item.Name.ExtractText()), $"Missing KR item {id}");
}
static uint[] ReadIds(JsonElement array) => array.EnumerateArray().Select(value =>
{
    Require(value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var id) && id > 0, "Invalid ID or unsupported nesting");
    return value.GetUInt32();
}).ToArray();
static byte[] CheckedBytes(string path, string hash)
{
    var bytes = File.ReadAllBytes(path);
    Require(Convert.ToHexString(SHA256.HashData(bytes)) == hash, $"Source SHA256 mismatch: {path}");
    return bytes;
}
static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
internal sealed record Source(string Repository, string Revision, string File, string Sha256, string YamlSha256,
    string GameBuild, string DataVersion);
