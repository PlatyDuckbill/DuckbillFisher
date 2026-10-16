using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuckbillFisher.Core;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only importer: local files and client sheets, never a game process or HTTP.
if (args.Length != 5)
    throw new ArgumentException("Usage: FishingDataImport <manifest> <data.js> <game-directory> <output.json> <audit.json>");
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var source = JsonSerializer.Deserialize<Source>(File.ReadAllText(args[0]), options)
    ?? throw new InvalidDataException("Missing source manifest.");
var bytes = File.ReadAllBytes(args[1]);
if (Convert.ToHexString(SHA256.HashData(bytes)) != source.Sha256)
    throw new InvalidDataException("Source SHA256 mismatch; review the pinned revision separately.");
if (File.ReadAllText(Path.Combine(args[2], "ffxivgame.ver")).Trim() != source.GameBuild)
    throw new InvalidDataException("Unsupported KR game build.");

// Only quote the upstream top-level table keys. Never evaluate JavaScript.
var json = Regex.Replace(System.Text.Encoding.UTF8.GetString(bytes), @"\A\s*const DATA\s*=\s*", "");
json = Regex.Replace(json, @"(?m)^  ([A-Z_]+):", "  \"$1\":").Trim().TrimEnd(';');
using var document = JsonDocument.Parse(json);
var root = document.RootElement;
using var game = new GameData(Path.Combine(args[2], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
var fishSheet = game.GetExcelSheet<FishParameter>()!;
var items = game.GetExcelSheet<Item>()!;
var spots = game.GetExcelSheet<FishingSpot>()!;
var weather = game.GetExcelSheet<Weather>()!;
var fishByItem = fishSheet.Where(row => row.RowId != 0 && row.Item.Is<Item>())
    .GroupBy(row => row.Item.RowId).ToDictionary(group => group.Key, group => group.ToArray());
var rules = new List<FishingRule>();
var audit = new List<object>();
var excluded = new List<object>();
var failures = new List<string>();
foreach (var entry in root.GetProperty("FISH").EnumerateObject())
{
    var raw = entry.Value;
    var patch = raw.GetProperty("patch").GetDouble();
    if (patch < source.MinimumPatch || patch >= source.ExclusiveMaximumPatch)
        continue;
    // Upstream also lists mooch helpers without a recommended location. Keep them in
    // item links/general catalog, but do not invent a location for a detailed rule.
    if (raw.GetProperty("location").ValueKind == JsonValueKind.Null)
    {
        excluded.Add(new { itemId = raw.GetProperty("_id").GetUInt32(), reason = "NoRecommendedSpot" });
        continue;
    }
    try
    {
        var id = raw.GetProperty("_id").GetUInt32();
        Require(entry.Name == id.ToString(System.Globalization.CultureInfo.InvariantCulture), "Source key/ID mismatch");
        Require(fishByItem.TryGetValue(id, out var matches) && matches.Length == 1, "Missing or ambiguous FishParameter");
        var fish = matches![0];
        Require(fish.IsInLog && fish.OceanStars == 0, "Not a regular fishing log entry");
        foreach (var field in new[] { "dataMissing", "gig", "lure" })
            Require(raw.GetProperty(field).ValueKind == JsonValueKind.Null, $"Unsupported {field}");
        Require(raw.GetProperty("snagging").ValueKind is JsonValueKind.Null or JsonValueKind.False, "Snagging required");
        var path = ReadIds(raw.GetProperty("bestCatchPath")); // Nested alternatives intentionally rejected.
        Require(path.Length > 0, "Missing recommended bait path");
        var predators = raw.GetProperty("predators").EnumerateArray().Select(pair =>
        {
            Require(pair.GetArrayLength() == 2, "Invalid predator pair");
            return new FishRequirement(pair[0].GetUInt32(), pair[1].GetInt32());
        }).ToArray();
        Require(predators.All(value => value.Count > 0), "Invalid predator count");
        var duration = raw.GetProperty("intuitionLength");
        double? seconds = duration.ValueKind == JsonValueKind.Null ? null : duration.GetDouble();
        Require(predators.Length == 0 ? seconds is null : seconds is > 0 && double.IsFinite(seconds.Value), "Incomplete intuition condition");
        foreach (var itemId in path.Concat(predators.Select(value => value.ItemId)).Append(id))
        {
            var item = items.GetRow(itemId);
            Require(itemId > 0 && item.Icon != 0 && !string.IsNullOrWhiteSpace(item.Name.ExtractText()), $"Missing KR item {itemId}");
        }
        var spotId = raw.GetProperty("location").GetUInt32();
        var spot = spots.GetRow(spotId);
        Require(spotId > 0 && (spot.Item.Any(value => value.RowId == id) || fish.FishingSpot.RowId == spotId), "Fish/spot mismatch");
        Require(spot.PlaceNameMain.RowId == 0 && spot.TerritoryType.RowId != 0 && !string.IsNullOrWhiteSpace(spot.PlaceName.Value.Name.ExtractText()), "Unsupported spot");
        var territory = spot.TerritoryType.Value;
        var map = territory.Map.Value;
        Require(map.RowId != 0 && map.SizeFactor > 0 && map.TerritoryType.RowId == territory.RowId, "Missing map");
        var originalSpot = root.GetProperty("FISHING_SPOTS").GetProperty(spotId.ToString());
        Require(originalSpot.GetProperty("territory_id").GetUInt32() == territory.RowId, "Territory mismatch");
        var coords = originalSpot.GetProperty("map_coords");
        var x = 1 + spot.X * 41.0 / 2048 / (map.SizeFactor / 100.0);
        var y = 1 + spot.Z * 41.0 / 2048 / (map.SizeFactor / 100.0);
        Require(Math.Abs(x - coords[0].GetDouble()) < 0.00001 && Math.Abs(y - coords[1].GetDouble()) < 0.00001, "Coordinate mismatch");
        var current = ReadIds(raw.GetProperty("weatherSet"));
        var previous = ReadIds(raw.GetProperty("previousWeatherSet"));
        foreach (var weatherId in current.Concat(previous).Distinct())
        {
            var value = weather.GetRow(weatherId);
            Require(value.Icon > 0 && !string.IsNullOrWhiteSpace(value.Name.ExtractText()), "Missing KR weather");
        }
        var rates = territory.WeatherRate.Value;
        Require(rates.Rate.Sum(value => (int)value) == 100, "Invalid KR weather rates");
        // Null/false snagging means no required Snagging condition in this source.
        // fishEyes is the generated modern skill support flag, not the obsolete YAML requirement.
        var rule = new FishingRule {
            ItemId = id, FishParameterId = fish.RowId, FishingSpotId = spotId,
            SourceName = root.GetProperty("ITEMS").GetProperty(entry.Name).GetProperty("name_en").GetString()!,
            StartHour = raw.GetProperty("startHour").GetDouble(), EndHour = raw.GetProperty("endHour").GetDouble(),
            WeatherIds = current, PreviousWeatherIds = previous, BaitPath = path,
            Hookset = raw.GetProperty("hookset").GetString()!, Tug = raw.GetProperty("tug").GetString()!,
            Predators = predators, IntuitionSeconds = seconds,
            FolkloreRequired = raw.GetProperty("folklore").ValueKind != JsonValueKind.Null,
            FishEyesSupported = raw.GetProperty("fishEyes").GetBoolean()
        };
        Require(double.IsFinite(rule.StartHour) && double.IsFinite(rule.EndHour) &&
            rule.StartHour >= 0 && rule.StartHour < 24 && rule.EndHour >= 0 && rule.EndHour <= 24 &&
            rule.StartHour != rule.EndHour, "Invalid time window");
        Require(rule.Hookset is "Precision" or "Powerful" && rule.Tug is "light" or "medium" or "heavy", "Unsupported hookset/tug");
        Require(rule.FolkloreRequired == (fish.GatheringSubCategory.RowId != 0), "KR folklore mismatch");
        rules.Add(rule);
        audit.Add(new { rule.FishParameterId, rule.ItemId, name = items.GetRow(id).Name.ExtractText(), rule.SourceName,
            patch, spotId, territoryId = territory.RowId, mapId = map.RowId, x, y });
    }
    catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or KeyNotFoundException)
    {
        failures.Add($"Item={entry.Name}: {error.Message}");
    }
}
// A failed candidate never silently becomes an unrestricted or partially imported fish.
if (failures.Count > 0)
    throw new InvalidDataException(string.Join(Environment.NewLine, failures));
Require(rules.Count > 0 && rules.Select(rule => rule.FishParameterId).Distinct().Count() == rules.Count, "Empty or duplicate dataset");
var dataset = new { schemaVersion = 1, source.DataVersion, region = "KR", source.GameBuild,
    sourceRepository = source.Repository, sourceRevision = source.Revision, sourceSha256 = source.Sha256,
    sourceFiles = new[] { "private/fishData.yaml", source.File }, fish = rules.OrderBy(rule => rule.FishParameterId).ToArray() };
File.WriteAllText(args[3], JsonSerializer.Serialize(dataset, options) + "\n");
File.WriteAllText(args[4], JsonSerializer.Serialize(new { source.GameBuild, source.Revision, source.Sha256,
    count = rules.Count, fish = audit, excluded }, options) + "\n");
Console.WriteLine($"Imported={rules.Count}; ExcludedNoSpot={excluded.Count}; Failed=0; WithIntuition={rules.Count(rule => rule.Predators.Length > 0)}; WithPreviousWeather={rules.Count(rule => rule.PreviousWeatherIds.Length > 0)}; MaxBaitSteps={rules.Max(rule => rule.BaitPath.Length)}");

static uint[] ReadIds(JsonElement array) => array.EnumerateArray().Select(value =>
{
    Require(value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var id) && id > 0, "Missing ID or unsupported nested alternatives");
    return value.GetUInt32();
}).ToArray();
static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }

internal sealed record Source(string Repository, string Revision, string File, string Sha256,
    string GameBuild, string DataVersion, double MinimumPatch, double ExclusiveMaximumPatch);
