using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuckbillFisher.Core;

/// <summary>Shared offline/runtime contract. Validation never supplies missing fishing facts.</summary>
public static class FishingDatasetReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter<FishGrade>(allowIntegerValues: false) },
    };

    // Match the importer's existing LF-normalized conditionDataSha256 contract.
    public static string ContentHash(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Replace("\r\n", "\n"))));

    public static FishingDataset ReadConditions(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            CheckDuplicateFields(root, "$");
            RequireFields(root, "$", "schemaVersion", "region", "gameBuild", "dataVersion",
                "sourceRepository", "sourceRevision", "fish", "spearfishing");
            RequireArray(root.GetProperty("fish"), "$.fish");
            var index = 0;
            foreach (var row in root.GetProperty("fish").EnumerateArray())
            {
                RequireFields(row, $"$.fish[{index++}]", "itemId", "fishParameterId", "fishingSpotId", "sourceName",
                    "startHour", "endHour", "weatherIds", "previousWeatherIds", "baitPath", "baitSteps",
                    "hookset", "tug", "predators", "intuitionSeconds", "folkloreRequired", "fishEyesSupported",
                    "conditionsKnown", "snaggingRequired", "lure", "bigFish", "patch");
            }
            RequireArray(root.GetProperty("spearfishing"), "$.spearfishing");
            index = 0;
            foreach (var row in root.GetProperty("spearfishing").EnumerateArray())
                RequireFields(row, $"$.spearfishing[{index++}]", "itemId", "spearfishingItemId", "sourceSpotId",
                    "sourceName", "gig", "conditionsKnown", "predators");

            var data = root.Deserialize<FishingDataset>(Options)!;
            Require(data.SchemaVersion == 2, FishingDataError.UnsupportedSchema, "$.schemaVersion");
            Require(data.Region == "KR", FishingDataError.InvalidValue, "$.region");
            Require(!string.IsNullOrWhiteSpace(data.GameBuild) && !string.IsNullOrWhiteSpace(data.DataVersion),
                FishingDataError.InvalidValue, "$.gameBuild/dataVersion");
            Require(IsHttps(data.SourceRepository) && IsHex(data.SourceRevision, 40), FishingDataError.InvalidValue, "$.sourceRepository/sourceRevision");
            Require(data.Fish.Length > 0, FishingDataError.InvalidValue, "$.fish");
            var items = new HashSet<uint>();
            var fishIds = new HashSet<uint>();
            for (index = 0; index < data.Fish.Length; index++)
            {
                var fish = data.Fish[index];
                var path = $"$.fish[{index}]";
                Require(fish is not null, FishingDataError.InvalidValue, path);
                Require(fish!.ItemId > 0 && fish.FishParameterId > 0, FishingDataError.InvalidValue, path);
                Require(items.Add(fish.ItemId) && fishIds.Add(fish.FishParameterId), FishingDataError.DuplicateIdentity, path);
                Require(!fish.ConditionsKnown || fish.FishingSpotId > 0, FishingDataError.InvalidValue, path + ".fishingSpotId");
                Require(double.IsFinite(fish.StartHour) && double.IsFinite(fish.EndHour) &&
                    fish.StartHour is >= 0 and < 24 && fish.EndHour is >= 0 and <= 24 && fish.StartHour != fish.EndHour,
                    FishingDataError.InvalidValue, path + ".startHour/endHour");
                Require(ValidIds(fish.WeatherIds) && ValidIds(fish.PreviousWeatherIds), FishingDataError.InvalidValue, path + ".weatherIds/previousWeatherIds");
                Require(fish.BaitSteps.Length > 0 && fish.BaitSteps.All(step => step is { Length: > 0 } && ValidIds(step)) &&
                    (fish.BaitPath.Length == 0 || (fish.BaitPath.Length == fish.BaitSteps.Length &&
                     !fish.BaitPath.Where((id, i) => !fish.BaitSteps[i].Contains(id)).Any())),
                    FishingDataError.InconsistentReference, path + ".baitSteps/baitPath");
                Require(fish.Hookset is "" or "Precision" or "Powerful" && fish.Tug is "" or "light" or "medium" or "heavy" &&
                    fish.Lure is "" or "Modest" or "Ambitious", FishingDataError.InvalidValue, path + ".hookset/tug/lure");
                Require(ValidPredators(fish.Predators, fish.ItemId), FishingDataError.InvalidValue, path + ".predators");
                Require(fish.IntuitionSeconds is null || (double.IsFinite(fish.IntuitionSeconds.Value) && fish.IntuitionSeconds > 0 && fish.Predators.Length > 0),
                    FishingDataError.InvalidValue, path + ".intuitionSeconds");
                Require(double.IsFinite(fish.Patch) && fish.Patch >= 0, FishingDataError.InvalidValue, path + ".patch");
            }
            var spearIds = new HashSet<uint>();
            var spearItems = new HashSet<uint>();
            for (index = 0; index < data.Spearfishing.Length; index++)
            {
                var fish = data.Spearfishing[index];
                var path = $"$.spearfishing[{index}]";
                Require(fish is not null, FishingDataError.InvalidValue, path);
                Require(fish!.ItemId > 0 && fish.SpearfishingItemId > 0, FishingDataError.InvalidValue, path);
                Require(spearItems.Add(fish.ItemId) && spearIds.Add(fish.SpearfishingItemId), FishingDataError.DuplicateIdentity, path);
                Require(fish.Gig is "Small" or "Normal" or "Large" or "UNKNOWN" && (!fish.ConditionsKnown || fish.Gig != "UNKNOWN"),
                    FishingDataError.InvalidValue, path + ".gig");
                Require(ValidPredators(fish.Predators, fish.ItemId), FishingDataError.InvalidValue, path + ".predators");
            }
            foreach (var source in data.SupplementalSources)
                Require(source is not null && !string.IsNullOrWhiteSpace(source.Name) && IsHttps(source.Repository) && IsHex(source.Revision, 40),
                    FishingDataError.InvalidValue, "$.supplementalSources");
            return data;
        }
        catch (JsonException ex) { throw new FishingDataValidationException(FishingDataError.InvalidJson, ex.Path ?? "$"); }
    }

    public static FishGradeDataset ReadGrades(string json, FishingDataset conditions, string conditionHash)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            CheckDuplicateFields(root, "$");
            RequireFields(root, "$", "schemaVersion", "gameBuild", "dataVersion", "conditionDataSha256", "entries");
            RequireArray(root.GetProperty("entries"), "$.entries");
            var index = 0;
            foreach (var row in root.GetProperty("entries").EnumerateArray())
            {
                var path = $"$.entries[{index++}]";
                RequireFields(row, path, "fishParameterId", "itemId", "grade");
                Require(row.GetProperty("grade").ValueKind == JsonValueKind.String &&
                    row.GetProperty("grade").GetString() is "Big" or "Legendary", FishingDataError.InvalidValue, path + ".grade");
            }
            var data = root.Deserialize<FishGradeDataset>(Options)!;
            Require(data.SchemaVersion == 1, FishingDataError.UnsupportedSchema, "$.schemaVersion");
            Require(data.GameBuild == conditions.GameBuild && data.DataVersion == conditions.DataVersion + "-grades" &&
                IsHex(data.ConditionDataSha256, 64) && data.ConditionDataSha256.Equals(conditionHash, StringComparison.OrdinalIgnoreCase),
                FishingDataError.SourceMismatch, "$.conditionDataSha256/gameBuild/dataVersion");
            Require(data.Entries.Length > 0, FishingDataError.InvalidValue, "$.entries");
            var items = new HashSet<uint>();
            var ids = new HashSet<uint>();
            var rules = conditions.Fish.ToDictionary(fish => fish.FishParameterId);
            var rulesByItem = conditions.Fish.ToDictionary(fish => fish.ItemId);
            for (index = 0; index < data.Entries.Length; index++)
            {
                var entry = data.Entries[index];
                var path = $"$.entries[{index}]";
                Require(entry is not null && entry.ItemId > 0 && entry.FishParameterId > 0 && entry.Grade is FishGrade.Big or FishGrade.Legendary,
                    FishingDataError.InvalidValue, path);
                Require(items.Add(entry!.ItemId) && ids.Add(entry.FishParameterId), FishingDataError.DuplicateIdentity, path);
                // The grade source covers the whole guide; an earlier condition subset need not contain every grade.
                Require(!rules.TryGetValue(entry.FishParameterId, out var rule) || (rule.ItemId == entry.ItemId && rule.BigFish),
                    FishingDataError.InconsistentReference, path);
                Require(!rulesByItem.TryGetValue(entry.ItemId, out var itemRule) || itemRule.FishParameterId == entry.FishParameterId,
                    FishingDataError.InconsistentReference, path);
            }
            Require(conditions.Fish.Where(fish => fish.BigFish).All(fish => ids.Contains(fish.FishParameterId)),
                FishingDataError.InconsistentReference, "$.entries");
            return data;
        }
        catch (JsonException ex) { throw new FishingDataValidationException(FishingDataError.InvalidJson, ex.Path ?? "$"); }
    }

    private static bool IsHex(string text, int length) => text.Length == length && text.All(Uri.IsHexDigit);
    private static bool IsHttps(string text) => Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    private static bool ValidIds(uint[] ids) => ids.All(id => id > 0) && ids.Distinct().Count() == ids.Length;
    private static bool ValidPredators(FishRequirement[] predators, uint self) =>
        predators.All(value => value is not null && value.ItemId > 0 && value.ItemId != self && value.Count > 0) &&
        predators.Select(value => value.ItemId).Distinct().Count() == predators.Length;

    private static void Require(bool condition, FishingDataError error, string path)
    {
        if (!condition) throw new FishingDataValidationException(error, path);
    }

    private static void RequireFields(JsonElement value, string path, params string[] fields)
    {
        Require(value.ValueKind == JsonValueKind.Object, FishingDataError.InvalidValue, path);
        foreach (var field in fields)
            Require(value.TryGetProperty(field, out _), FishingDataError.MissingField, path + "." + field);
    }

    private static void RequireArray(JsonElement value, string path) =>
        Require(value.ValueKind == JsonValueKind.Array, FishingDataError.InvalidValue, path);

    private static void CheckDuplicateFields(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name), FishingDataError.DuplicateField, path + "." + property.Name);
                CheckDuplicateFields(property.Value, path + "." + property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in value.EnumerateArray()) CheckDuplicateFields(child, $"{path}[{index++}]");
        }
    }
}
