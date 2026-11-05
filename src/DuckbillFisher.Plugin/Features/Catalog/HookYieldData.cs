using System.Collections.Frozen;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record HookYieldRange(int Minimum, int? Maximum)
{
    public bool Valid => Minimum is >= 1 and <= 999 && (Maximum is null || Maximum >= Minimum && Maximum <= 999);
    public string Display => Maximum is null ? UiText.Get(T.HookYieldAtLeast, Minimum) : Maximum == Minimum
        ? Minimum.ToString(UiText.Culture) : UiText.Get(T.HookYieldRange, Minimum, Maximum);
}
internal sealed record HookYieldEntry(uint ItemId, uint FishParameterId, string Source, HookYieldRange? DoubleHook, HookYieldRange? TripleHook);

internal static class HookYieldData
{
    public static IReadOnlyDictionary<uint, HookYieldEntry> ByItem { get; }
    public static bool Invalid { get; }
    static HookYieldData()
    {
        try
        {
            using var stream = typeof(HookYieldData).Assembly.GetManifestResourceStream("DuckbillFisher.HookYields")
                ?? throw new InvalidDataException("HookYieldsMissing");
            var data = JsonSerializer.Deserialize<Dataset>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("HookYieldsEmpty");
            if (data.SchemaVersion != 1 || data.Sources is not { Length: > 0 } || data.Entries is not { Length: > 0 and <= 4096 } ||
                data.Sources.Any(s => s is null) || data.Entries.Any(e => e is null))
                throw new InvalidDataException("HookYieldSchema");
            var sources = data.Sources.ToDictionary(s => s.Id);
            if (sources.Values.Any(s => !Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                (s.ContentSha256 ?? s.DescriptionSha256) is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit)))
                throw new InvalidDataException("HookYieldSource");
            var rules = FishingData.Dataset.Fish.ToDictionary(f => f.ItemId);
            foreach (var entry in data.Entries)
                if (!sources.ContainsKey(entry.Source) || !rules.TryGetValue(entry.ItemId, out var fish) || fish.FishParameterId != entry.FishParameterId ||
                    entry.DoubleHook is null && entry.TripleHook is null || entry.DoubleHook?.Valid == false || entry.TripleHook?.Valid == false)
                    throw new InvalidDataException("HookYieldIdentity");
            ByItem = data.Entries.ToFrozenDictionary(e => e.ItemId);
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException or InvalidOperationException)
        { Invalid = true; ByItem = FrozenDictionary<uint, HookYieldEntry>.Empty; }
    }
    private sealed record Source(string Id, string Url, string? DescriptionSha256 = null, string? ContentSha256 = null);
    private sealed record Dataset(int SchemaVersion, Source[] Sources, HookYieldEntry[] Entries);
}

internal static class HookYieldText
{
    public static void Draw(uint itemId)
    {
        HookYieldData.ByItem.TryGetValue(itemId, out var entry);
        ImGui.PushTextWrapPos(0);
        ImGui.TextDisabled(UiText.Get(T.HookYieldValues,
            entry?.DoubleHook?.Display ?? UiText.Get(T.ConditionUnknown),
            entry?.TripleHook?.Display ?? UiText.Get(T.ConditionUnknown)));
        ImGui.PopTextWrapPos();
        UiWidgets.Tooltip(UiText.Get(HookYieldData.Invalid ? T.HookYieldUnavailable : T.HookYieldHint));
    }
}
