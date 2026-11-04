using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DuckbillFisher.Core;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only patch review of the LogMessage contracts the plugin uses. Reads installed client sheets;
// never reads a game process. Conflicts fail the run. Text changes since a baseline are listed for a person
// to review, because rows without parameter bindings cannot be checked from their text at runtime.
internal static class LogContractAudit
{
    private sealed record Row(string Build, string Language, uint Id, string State, string Found, string Sha256, string Macro);

    public static void Run(string[] args)
    {
        const string usage = "Usage: --log-contracts <output.json> [--baseline <previous.json>] <game-directory> <Language[,Language...]> [<game-directory> <languages>]...";
        var rest = args.ToList();
        if (rest.Count < 3) throw new ArgumentException(usage);
        var output = rest[0];
        rest.RemoveAt(0);
        string? baseline = null;
        if (rest[0] == "--baseline")
        {
            if (rest.Count < 4) throw new ArgumentException(usage);
            baseline = rest[1];
            rest.RemoveRange(0, 2);
        }
        if (rest.Count % 2 != 0) throw new ArgumentException(usage);

        var rows = new List<Row>();
        for (var i = 0; i < rest.Count; i += 2)
        {
            var directory = rest[i];
            var build = File.ReadAllText(Path.Combine(directory, "ffxivgame.ver")).Trim();
            var languages = rest[i + 1].Split(',').Select(name => Enum.Parse<Language>(name, ignoreCase: true)).ToArray();
            using var game = new GameData(Path.Combine(directory, "sqpack"), new LuminaOptions { DefaultExcelLanguage = languages[0] });
            foreach (var language in languages)
            {
                var sheet = game.GetExcelSheet<LogMessage>(language) ?? throw new InvalidOperationException($"LogMessage unavailable: {build}/{language}");
                foreach (var id in FishingLogBindings.Expected.Keys.Order())
                {
                    var macro = sheet.GetRowOrDefault(id)?.Text.ToMacroString();
                    var result = FishingLogBindings.Evaluate(id, macro);
                    rows.Add(new(build, language.ToString(), id, result.State.ToString(), result.Found,
                        macro is null ? "" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(macro))), macro ?? ""));
                }
            }
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(output, JsonSerializer.Serialize(rows, options) + "\n");

        foreach (var group in rows.GroupBy(row => (row.Build, row.Language)))
            Console.WriteLine($"LogContracts: build={group.Key.Build}; language={group.Key.Language}; valid={group.Count(row => row.State == "Valid")}/{group.Count()}");
        var failed = rows.Where(row => row.State != "Valid").ToArray();
        foreach (var row in failed)
            Console.WriteLine($"Rejected: build={row.Build}; language={row.Language}; id={row.Id}; state={row.State}; found={row.Found}");

        if (baseline is not null)
        {
            var previous = JsonSerializer.Deserialize<Row[]>(File.ReadAllText(baseline), options)!
                .ToDictionary(row => (row.Language, row.Id), row => row.Sha256);
            var changed = rows.Where(row => previous.TryGetValue((row.Language, row.Id), out var sha) && sha != row.Sha256).ToArray();
            foreach (var row in changed)
                Console.WriteLine($"Review: language={row.Language}; id={row.Id}; bound={FishingLogBindings.Expected[row.Id].Count > 0}; text changed since baseline");
            Console.WriteLine($"Baseline: compared={rows.Count(row => previous.ContainsKey((row.Language, row.Id)))}; changed={changed.Length}");
        }
        if (failed.Length > 0) Environment.ExitCode = 1;
    }
}
