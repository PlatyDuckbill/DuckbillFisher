using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development import only. Runtime code consumes IDs, never translated descriptions.
internal static class FishClassificationImport
{
    public static void Run(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("Usage: --classifications <game-directory> <condition-data.json> <output.json> <audit.json>");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var inputBytes = File.ReadAllBytes(args[1]);
        using var conditions = JsonDocument.Parse(inputBytes);
        var build = conditions.RootElement.GetProperty("gameBuild").GetString()!;
        Require(File.ReadAllText(Path.Combine(args[0], "ffxivgame.ver")).Trim() == build, "KR build mismatch");
        var rules = conditions.RootElement.GetProperty("fish").EnumerateArray()
            .ToDictionary(row => row.GetProperty("itemId").GetUInt32());
        using var game = new GameData(Path.Combine(args[0], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
        var notes = game.GetExcelSheet<FishingNoteInfo>()!.Where(row => row.Item.RowId > 0)
            .GroupBy(row => row.Item.RowId).ToDictionary(group => group.Key, group => group.ToArray());
        var entries = new List<object>();
        var audit = new List<object>();
        var evidence = new StringBuilder();
        var compared = 0;
        var legendary = 0;
        var timingMatches = 0;
        var regular = game.GetExcelSheet<FishParameter>()!.Where(row => row.RowId > 0 && row.IsInLog &&
            row.OceanStars == 0 && row.Item.Is<Item>()).OrderBy(row => row.RowId).ToArray();
        foreach (var row in regular)
        {
            var item = row.Item.GetValueOrDefault<Item>() ?? throw new InvalidDataException("Missing KR item");
            Require(notes.TryGetValue(item.RowId, out var candidates) && candidates.Length == 1, "Ambiguous fishing note");
            var note = candidates![0];
            var description = item.Description.ExtractText();
            var grade = description.Contains("터주왕", StringComparison.Ordinal) ? "Legendary"
                : description.Contains("터주", StringComparison.Ordinal) ? "Big" : null;
            // Corroborate the explicit description label against the inspected KR note group.
            // This check is scoped to this build; it does not define SpecialConditions globally.
            Require((grade is not null) == (note.SpecialConditions == 2), $"Description/note mismatch: {row.RowId}");
            if (rules.TryGetValue(item.RowId, out var rule))
            {
                Require(rule.GetProperty("fishParameterId").GetUInt32() == row.RowId, "Condition ID mismatch");
                Require(rule.GetProperty("bigFish").GetBoolean() == (grade is not null), $"Source grade mismatch: {row.RowId}");
                compared++;
                if (rule.GetProperty("conditionsKnown").GetBoolean() && (note.TimeRestriction == 1 || note.WeatherRestriction == 1))
                {
                    var restricted = rule.GetProperty("startHour").GetDouble() != 0 || rule.GetProperty("endHour").GetDouble() != 24 ||
                        rule.GetProperty("weatherIds").GetArrayLength() > 0 || rule.GetProperty("previousWeatherIds").GetArrayLength() > 0;
                    Require(restricted, $"Positive restriction mismatch: {row.RowId}");
                    timingMatches++;
                }
            }
            evidence.Append(row.RowId).Append('/').Append(item.RowId).Append(':').Append(description).Append('\n');
            if (grade is null) continue;
            if (grade == "Legendary") legendary++;
            entries.Add(new { fishParameterId = row.RowId, itemId = item.RowId, grade });
            audit.Add(new { fishParameterId = row.RowId, itemId = item.RowId, name = item.Name.ExtractText(), grade,
                marker = grade == "Legendary" ? "터주왕" : "터주", note.SpecialConditions });
        }
        Require(entries.Count > 0 && legendary > 0 && compared == rules.Count, "Incomplete classification audit");
        var output = new { schemaVersion = 1, gameBuild = build, dataVersion = "kr-grades-2026-10-16",
            source = "Installed KR Item.Description category labels, corroborated by FishingNoteInfo and pinned condition data",
            conditionDataSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(inputBytes).Replace("\r\n", "\n")))),
            descriptionSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.ToString()))),
            entries };
        File.WriteAllText(args[2], JsonSerializer.Serialize(output, options) + "\n");
        File.WriteAllText(args[3], JsonSerializer.Serialize(new { gameBuild = build, inspected = regular.Length,
            sourceCompared = compared, positiveTimingCompared = timingMatches, bigIncludingLegendary = entries.Count,
            legendary, failures = 0, entries = audit }, options) + "\n");
        Console.WriteLine($"Inspected={regular.Length}; SourceCompared={compared}; PositiveTimingCompared={timingMatches}; BigIncludingLegendary={entries.Count}; Legendary={legendary}; Failed=0");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
