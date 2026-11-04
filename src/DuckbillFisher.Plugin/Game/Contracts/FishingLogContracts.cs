using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

/// <summary>Language-independent LogMessage contracts checked against the running client's sheet.</summary>
internal static class FishingLogContracts
{
    // PlaceName 950 is the unknown fishing hole in KR and global clients; never compare translated names.
    public const uint UnknownSpotPlaceName = 950;

    public static int Count => FishingLogBindings.Expected.Count;

    public static FishingLogActor Actor(ILogMessage message, string localName, ushort homeWorld)
    {
        if (message.SourceEntity is not { } source) return FishingLogActor.NoSource;
        var name = source.Name.ExtractText();
        if (!source.IsPlayer || localName.Length == 0 || homeWorld == 0 || name.Length == 0 || source.HomeWorldId == 0)
            return FishingLogActor.Unknown;
        return source.HomeWorldId == homeWorld && name == localName ? FishingLogActor.Local : FishingLogActor.Other;
    }

    /// <summary>Adds valid IDs and returns a language-independent summary of rejected rows ("none" if all pass).</summary>
    public static string Validate(IDataManager data, HashSet<uint> valid)
    {
        var sheet = data.GetExcelSheet<LogMessage>();
        if (sheet is null) return "SheetUnavailable";
        var rejected = new List<string>();
        foreach (var id in FishingLogBindings.Expected.Keys.Order())
        {
            var row = sheet.GetRowOrDefault(id);
            var result = FishingLogBindings.Evaluate(id, row?.Text.ToMacroString());
            if (result.State == FishingLogContractState.Valid) valid.Add(id);
            else rejected.Add($"{id}:{result.State}");
        }
        return rejected.Count == 0 ? "none" : string.Join(',', rejected);
    }
}
