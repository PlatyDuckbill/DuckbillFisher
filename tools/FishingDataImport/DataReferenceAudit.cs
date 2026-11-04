using System.Text.Encodings.Web;
using System.Text.Json;
using DuckbillFisher.Core;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only check that every ID the embedded conditions reference resolves to the same sheet facts on
// each installed client (for example KR and global, or a client before and after a patch). The plugin applies
// conditions on any client build and keeps its per-row FishParameter/Item/spot checks; this audit is the
// patch-time evidence. Reads installed sheets only. Any missing reference or cross-client difference fails.
internal static class DataReferenceAudit
{
    public static void Run(string[] args)
    {
        const string usage = "Usage: --data-references <condition-data.json> <output.json> <game-directory> <Language> [<game-directory> <Language>]...";
        if (args.Length < 4 || args.Length % 2 != 0) throw new ArgumentException(usage);
        var dataset = FishingDatasetReader.ReadConditions(File.ReadAllText(args[0]));
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

        var clients = new List<(string Build, Dictionary<string, string> Facts)>();
        for (var i = 2; i < args.Length; i += 2)
        {
            var directory = args[i];
            var language = Enum.Parse<Language>(args[i + 1], ignoreCase: true);
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
            // IDs and numeric columns only; translated names are never compared.
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
            clients.Add((build, facts));
        }

        var failures = new List<string>();
        foreach (var (build, facts) in clients)
        {
            failures.AddRange(facts.Where(pair => pair.Value == "missing").Select(pair => $"Missing: build={build}; {pair.Key}"));
            failures.AddRange(dataset.Fish.Where(fish => !facts[$"FishParameter:{fish.FishParameterId}"].StartsWith($"item={fish.ItemId};", StringComparison.Ordinal))
                .Select(fish => $"PairMismatch: build={build}; item={fish.ItemId}; fishParameter={fish.FishParameterId}"));
        }
        var reference = clients[0];
        foreach (var (build, facts) in clients.Skip(1))
            failures.AddRange(reference.Facts.Where(pair => facts[pair.Key] != pair.Value)
                .Select(pair => $"Different: {reference.Build} vs {build}; {pair.Key}; [{pair.Value}] vs [{facts[pair.Key]}]"));

        foreach (var group in reference.Facts.Keys.GroupBy(key => key[..key.IndexOf(':')]))
            Console.WriteLine($"References: {group.Key}={group.Count()}");
        Console.WriteLine($"DataReferences: dataset={dataset.DataVersion}; clients={string.Join(',', clients.Select(client => client.Build))}; references={reference.Facts.Count}; failures={failures.Count}");
        foreach (var failure in failures.Take(50)) Console.WriteLine(failure);
        var options = new JsonSerializerOptions { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(args[1], JsonSerializer.Serialize(new { dataset = dataset.DataVersion,
            clients = clients.Select(client => new { build = client.Build, facts = client.Facts }), failures }, options) + "\n");
        if (failures.Count > 0) Environment.ExitCode = 1;
    }
}
