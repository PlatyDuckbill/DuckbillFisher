using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace DuckbillFisher.Core;

public enum FishingLogContractState { Valid, Missing, Conflict, Unknown }

public sealed record FishingLogContractResult(uint Id, FishingLogContractState State, string Found);

/// <summary>
/// Language-independent LogMessage contracts. Each entry lists which log parameter refers to which sheet.
/// Expected sets were compared across KR Korean and global English/Japanese/German/French clients
/// (docs/verification/native/GLOBAL-COMPAT.md). Translated text is never compared.
/// </summary>
public static partial class FishingLogBindings
{
    // An empty set means the text names no parameter; only the row's existence is checked at runtime.
    // Patch-time review of those rows is a development step (FishingDataImport --log-contracts).
    public static FrozenDictionary<uint, FrozenSet<string>> Expected { get; } = new Dictionary<uint, string[]>
    {
        [1110] = ["PlaceName:lnum1"], [1111] = [], [1112] = [], [1113] = [], [1114] = ["Item:lnum1"], [1115] = ["PlaceName:lnum1"],
        [1116] = [], [1117] = [], [1118] = ["Item:lnum1"], [1119] = [], [1120] = [], [1121] = ["Item:lnum1"],
        [1127] = [], [1129] = [], [1130] = ["PlaceName:lnum1"], [3511] = [], [3512] = ["Item:lnum1"],
        [3513] = ["PlaceName:lnum1"], [3515] = ["Item:lnum1"], [3525] = ["Item:lnum1"], [3527] = [],
        [3528] = ["Item:lnum1"], [3559] = ["Item:lnum1"], [5528] = ["Item:lnum1"], [5556] = ["Item:lnum1"],
        [5557] = ["Item:lnum1", "Item:lnum2"], [5558] = ["Item:lnum1"], [5559] = [], [5584] = [],
    }.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet(StringComparer.Ordinal));

    // Only macro names observed in the compared clients count as bindings: sheet(Name,lnumN,...)
    // and the language noun macros ennoun/denoun/frnoun(Name,count,lnumN,...).
    [GeneratedRegex(@"<(?:sheet|ennoun|denoun|frnoun)\((\w+),(?:\d+,)?(lnum\d+)[,)]", RegexOptions.CultureInvariant)]
    private static partial Regex BindingPattern();

    public static FrozenSet<string> Extract(string macro) =>
        BindingPattern().Matches(macro).Select(match => $"{match.Groups[1].Value}:{match.Groups[2].Value}")
            .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Valid when every binding found in this language is expected and no parameter maps to two sheets.
    /// A language may omit an expected binding from its text; the server parameter still exists.
    /// </summary>
    public static FishingLogContractResult Evaluate(uint id, string? macro)
    {
        if (!Expected.TryGetValue(id, out var expected)) return new(id, FishingLogContractState.Unknown, "");
        if (macro is null) return new(id, FishingLogContractState.Missing, "");
        var found = Extract(macro);
        var text = string.Join('+', found.Order(StringComparer.Ordinal));
        var parameters = found.Select(binding => binding[(binding.IndexOf(':') + 1)..]).ToArray();
        var conflict = found.Any(binding => !expected.Contains(binding)) || parameters.Distinct(StringComparer.Ordinal).Count() != parameters.Length ||
            id == 3559 && !macro.Contains("num(lnum4)", StringComparison.Ordinal);
        return new(id, conflict ? FishingLogContractState.Conflict : FishingLogContractState.Valid, text);
    }
}
