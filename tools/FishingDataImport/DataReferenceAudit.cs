using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only check that every ID the embedded conditions reference, plus the plugin's fixed sheet IDs
// (FishingGameIds, linked from the plugin), resolve to the same sheet facts on each installed client, for example
// KR and global. The plugin applies conditions on any client build and keeps its per-row FishParameter/Item/spot
// checks; this audit is the patch-time evidence.
// - Missing references, item/FishParameter pair mismatches and cross-client differences fail the run.
// - With --baseline, changed category digests for the same language are listed as Review lines for a person to
//   check, because a patch can legitimately change sheet facts.
// Reads installed sheets only. Only IDs and numeric columns are compared, never translated names.
internal static class DataReferenceAudit
{
    private sealed record Client(string Build, string Language, Dictionary<string, string> Facts);

    public static void Run(string[] args)
    {
        const string usage = "Usage: --data-references <condition-data.json> <output.json> [--baseline <previous.json>] <game-directory> <Language> [<game-directory> <Language>]...";
        var rest = args.ToList();
        if (rest.Count < 4) throw new ArgumentException(usage);
        var dataPath = rest[0];
        var outputPath = rest[1];
        rest.RemoveRange(0, 2);
        string? baselinePath = null;
        if (rest[0] == "--baseline")
        {
            baselinePath = rest[1];
            rest.RemoveRange(0, 2);
        }
        if (rest.Count < 2 || rest.Count % 2 != 0) throw new ArgumentException(usage);

        var dataset = FishingDatasetReader.ReadConditions(File.ReadAllText(dataPath));
        var items = new SortedSet<uint>();
        var fishParameters = new SortedSet<uint>();
        var spots = new SortedSet<uint>();
        var weathers = new SortedSet<uint>();
        var spearfishing = new SortedSet<uint>();
        foreach (var fish in dataset.Fish)
        {
            items.Add(fish.ItemId);
            fishParameters.Add(fish.FishParameterId);
            if (fish.FishingSpotId != 0) spots.Add(fish.FishingSpotId);
            weathers.UnionWith(fish.WeatherIds);
            weathers.UnionWith(fish.PreviousWeatherIds);
            items.UnionWith(fish.BaitPath);
            foreach (var step in fish.BaitSteps) items.UnionWith(step);
            foreach (var predator in fish.Predators) items.Add(predator.ItemId);
        }
        foreach (var spear in dataset.Spearfishing)
        {
            items.Add(spear.ItemId);
            spearfishing.Add(spear.SpearfishingItemId);
            foreach (var predator in spear.Predators) items.Add(predator.ItemId);
        }

        var clients = new List<Client>();
        for (var i = 0; i < rest.Count; i += 2)
        {
            var directory = rest[i];
            var language = Enum.Parse<Language>(rest[i + 1], ignoreCase: true);
            var build = File.ReadAllText(Path.Combine(directory, "ffxivgame.ver")).Trim();
            using var game = new GameData(Path.Combine(directory, "sqpack"), new LuminaOptions { DefaultExcelLanguage = language });
            var facts = new Dictionary<string, string>(StringComparer.Ordinal);
            var itemSheet = game.GetExcelSheet<Item>(language)!;
            var fishSheet = game.GetExcelSheet<FishParameter>(language)!;
            var spotSheet = game.GetExcelSheet<FishingSpot>(language)!;
            var weatherSheet = game.GetExcelSheet<Weather>(language)!;
            var spearSheet = game.GetExcelSheet<SpearfishingItem>(language)!;
            var territories = game.GetExcelSheet<TerritoryType>(language)!;
            var rates = game.GetExcelSheet<WeatherRate>(language)!;
            foreach (var id in items)
                facts[$"Item:{id}"] = itemSheet.GetRowOrDefault(id) is { } row ? $"category={row.ItemUICategory.RowId};icon={row.Icon}" : "missing";
            foreach (var id in fishParameters)
                facts[$"FishParameter:{id}"] = fishSheet.GetRowOrDefault(id) is { } row ? $"item={row.Item.RowId};inLog={row.IsInLog};ocean={row.OceanStars}" : "missing";
            var rateIds = new SortedSet<uint>();
            foreach (var id in spots)
            {
                if (spotSheet.GetRowOrDefault(id) is not { } row) { facts[$"FishingSpot:{id}"] = "missing"; continue; }
                var rate = territories.GetRowOrDefault(row.TerritoryType.RowId)?.WeatherRate.RowId ?? 0u;
                rateIds.Add(rate);
                facts[$"FishingSpot:{id}"] = $"territory={row.TerritoryType.RowId};place={row.PlaceName.RowId};x={row.X};z={row.Z};radius={row.Radius};" +
                    $"items={string.Join(',', row.Item.Select(item => item.RowId))};weatherRate={rate}";
            }
            foreach (var id in rateIds)
                facts[$"WeatherRate:{id}"] = rates.GetRowOrDefault(id) is { } row
                    ? $"{string.Join(',', row.Weather.Select(weather => weather.RowId))}|{string.Join(',', row.Rate.ToArray())}" : "missing";
            foreach (var id in weathers)
                facts[$"Weather:{id}"] = weatherSheet.GetRowOrDefault(id) is { } row ? $"icon={row.Icon}" : "missing";
            foreach (var id in spearfishing)
                facts[$"SpearfishingItem:{id}"] = spearSheet.GetRowOrDefault(id) is { } row ? $"item={row.Item.RowId}" : "missing";

            // Fixed plugin IDs. Their meaning is not validated at runtime, so patch-time comparison matters most here.
            facts[$"ClassJob:{FishingGameIds.FisherJob}"] = game.GetExcelSheet<ClassJob>(language)!.GetRowOrDefault(FishingGameIds.FisherJob) is { } job
                ? $"category={job.ClassJobCategory.RowId};jobIndex={job.JobIndex};parent={job.ClassJobParent.RowId}" : "missing";
            var statuses = game.GetExcelSheet<Status>(language)!;
            foreach (var id in FishingGameIds.Statuses)
                facts[$"Status:{id}"] = statuses.GetRowOrDefault(id) is { } row ? $"icon={row.Icon};category={row.StatusCategory};maxStacks={row.MaxStacks}" : "missing";
            var actions = game.GetExcelSheet<Lumina.Excel.Sheets.Action>(language)!;
            foreach (var id in FishingGameIds.Actions)
                facts[$"Action:{id}"] = actions.GetRowOrDefault(id) is { } row
                    ? $"icon={row.Icon};job={row.ClassJob.RowId};category={row.ActionCategory.RowId};player={row.IsPlayerAction}" : "missing";
            facts[$"PlaceName:{FishingGameIds.UnknownSpotPlaceName}"] = game.GetExcelSheet<PlaceName>(language)!.HasRow(FishingGameIds.UnknownSpotPlaceName) ? "present" : "missing";
            facts[$"ItemUICategory:{FishingGameIds.BaitCategory}"] = game.GetExcelSheet<ItemUICategory>(language)!.GetRowOrDefault(FishingGameIds.BaitCategory) is { } category
                ? $"icon={category.Icon};orderMajor={category.OrderMajor};orderMinor={category.OrderMinor}" : "missing";
            foreach (var (territory, _) in FishingGameIds.HousingExpansionCorrections)
                facts[$"HousingTerritory:{territory}"] = territories.GetRowOrDefault(territory) is { } row
                    ? $"exVersion={row.ExVersion.RowId};place={row.PlaceName.RowId};use={row.TerritoryIntendedUse.RowId}" : "missing";
            clients.Add(new(build, language.ToString(), facts));
        }

        var failures = new List<string>();
        foreach (var client in clients)
        {
            failures.AddRange(client.Facts.Where(pair => pair.Value == "missing").Select(pair => $"Missing: build={client.Build}; {pair.Key}"));
            failures.AddRange(dataset.Fish.Where(fish => !client.Facts[$"FishParameter:{fish.FishParameterId}"].StartsWith($"item={fish.ItemId};", StringComparison.Ordinal))
                .Select(fish => $"PairMismatch: build={client.Build}; item={fish.ItemId}; fishParameter={fish.FishParameterId}"));
        }
        var reference = clients[0];
        foreach (var client in clients.Skip(1))
            failures.AddRange(reference.Facts.Where(pair => client.Facts[pair.Key] != pair.Value)
                .Select(pair => $"Different: {reference.Build} vs {client.Build}; {pair.Key}; [{pair.Value}] vs [{client.Facts[pair.Key]}]"));

        var digests = clients.Select(client => new { build = client.Build, language = client.Language, categories = Digests(client.Facts) }).ToArray();
        foreach (var group in reference.Facts.Keys.GroupBy(Category))
            Console.WriteLine($"References: {group.Key}={group.Count()}");
        Console.WriteLine($"DataReferences: dataset={dataset.DataVersion}; clients={string.Join(',', clients.Select(client => client.Build))}; references={reference.Facts.Count}; failures={failures.Count}");
        foreach (var failure in failures.Take(50)) Console.WriteLine(failure);

        if (baselinePath is not null)
        {
            using var baseline = JsonDocument.Parse(File.ReadAllText(baselinePath));
            var compared = 0;
            var changed = 0;
            foreach (var current in digests)
            {
                var previous = baseline.RootElement.GetProperty("digests").EnumerateArray()
                    .FirstOrDefault(entry => entry.GetProperty("language").GetString() == current.language);
                if (previous.ValueKind != JsonValueKind.Object) continue;
                foreach (var (category, digest) in current.categories)
                {
                    compared++;
                    var old = previous.GetProperty("categories").TryGetProperty(category, out var value) ? value.GetString() : null;
                    if (old == digest) continue;
                    changed++;
                    Console.WriteLine($"Review: language={current.language}; build={previous.GetProperty("build").GetString()} -> {current.build}; category={category} changed since baseline");
                }
            }
            Console.WriteLine($"Baseline: compared={compared}; changed={changed}");
        }

        var options = new JsonSerializerOptions { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new { dataset = dataset.DataVersion, digests,
            clients = clients.Select(client => new { build = client.Build, language = client.Language, facts = client.Facts }), failures }, options) + "\n");
        if (failures.Count > 0) Environment.ExitCode = 1;
    }

    private static string Category(string key) => key[..key.IndexOf(':')];

    private static SortedDictionary<string, string> Digests(Dictionary<string, string> facts) =>
        new(facts.GroupBy(pair => Category(pair.Key)).ToDictionary(group => group.Key, group => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', group.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")))))),
            StringComparer.Ordinal);
}
