using System.Collections.Frozen;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using NativeFishGuide = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentFishGuide;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record FishBitSample(uint FishParameterId, bool? Value, UiMessage Reason);

internal enum FishGuideSnapshotState
{
    NotSampled, Unavailable, WrongMode, Loading, InvalidEntries, Incomplete, BitMismatch, ConflictingObservation, Complete,
}

internal sealed record FishGuideSnapshot(FishGuideSnapshotState State, int Entries = 0, int Matched = 0, int Mismatches = 0)
{
    public IReadOnlySet<uint> Caught { get; init; } = FrozenSet<uint>.Empty;
    public IReadOnlySet<uint> Uncaught { get; init; } = FrozenSet<uint>.Empty;
}

/// <summary>
/// Fish-bit diagnostics and validated game-guide snapshots for the inspected KR installation.
/// Pointers are acquired and discarded within the current framework update.
/// No offsets, signature scanning, writes, or inferred collection readiness.
/// </summary>
internal sealed class NativeFishCollectionReader
{
    private const uint MaxValidatedFishParameterId = 1522;
    private readonly IFramework framework;
    private readonly NativeRuntimeCompatibility runtime;
    private readonly UiMessage? unsupportedReason;

    public NativeFishCollectionReader(IFramework framework)
    {
        this.framework = framework;
        // File/assembly checks happen once during construction, never in Draw or a native read.
        runtime = NativeRuntimeCompatibility.Inspect();
        unsupportedReason = runtime.Failure;
    }

    public string GameVersion => runtime.GameVersion;
    public string RuntimeDiagnostic => runtime.Diagnostic;
    public UiMessage Compatibility => unsupportedReason ?? new(T.NativeCompatible);
    public bool IsCompatible => unsupportedReason is null;

    public static bool Supports(FishGuideRow row) => row.IsDisplayable && row.IsInLog && row.ItemType == "Item" &&
        row.FishParameterId is > 0 and <= MaxValidatedFishParameterId;

    public unsafe FishBitSample Read(FishGuideRow row, ulong expectedCharacter, bool fishGuideVisible)
    {
        FishBitSample Unknown(UiMessage reason) => new(row.FishParameterId, null, reason);
        if (unsupportedReason is not null)
            return Unknown(unsupportedReason);
        if (!framework.IsInFrameworkUpdateThread)
            return Unknown(new(T.NativeWrongThread));
        if (!fishGuideVisible)
            return Unknown(new(T.NativeGuideRequired));
        if (!Supports(row))
            return Unknown(new(T.NativeRowUnsupported));
        if (expectedCharacter == 0)
            return Unknown(new(T.NativePlayerRequired));

        // Installed metadata confirms this is the generated getter for the resolved static address.
        // A null address is rejected before dereference; no pointer survives this call.
        var state = NativePlayerState.StaticAddressPointers.pInstance;
        if (state == null)
            return Unknown(new(T.NativeAddressMissing));
        if (!state->IsLoaded || state->ContentId != expectedCharacter)
            return Unknown(new(T.NativeCharacterChanged));

        // This read is deliberately not wrapped in try/catch: invalid pointers are not recoverable
        // managed errors. Compatibility, scope, lifetime, and row eligibility are checked first.
        var value = state->IsFishCaught(row.FishParameterId);
        return new(row.FishParameterId, value, new(T.NativeRawSample));
    }

    // This is the game's completed, unfiltered guide list, not a network-receipt flag.
    // Never infer Uncaught from a bare false bit or from sheet/UI visibility alone.
    public unsafe FishGuideSnapshot ReadGuideSnapshot(IReadOnlyDictionary<uint, uint> expectedItems,
        ulong expectedCharacter, nint agentAddress)
    {
        if (!IsCompatible || !framework.IsInFrameworkUpdateThread || expectedCharacter == 0 || agentAddress == 0 ||
            expectedItems.Count == 0 || expectedItems.Keys.Any(id => id is 0 or > MaxValidatedFishParameterId))
            return new(FishGuideSnapshotState.Unavailable);
        var state = NativePlayerState.StaticAddressPointers.pInstance;
        if (state == null || !state->IsLoaded || state->ContentId != expectedCharacter)
            return new(FishGuideSnapshotState.Unavailable);

        // The caller obtains this borrowed agent via IGameGui for the visible FishGuide2 addon.
        // No pointer or native container survives this call; no native function is invoked here.
        var agent = (NativeFishGuide*)agentAddress;
        if (agent->Mode != 0)
            return new(FishGuideSnapshotState.WrongMode);
        var data = agent->Data;
        if (data == null || data->Agent != agent || !data->IsFilteredEntriesLoaded)
            return new(FishGuideSnapshotState.Loading);
        var first = data->Entries.First;
        var last = data->Entries.Last;
        var end = data->Entries.End;
        var entrySize = (nuint)sizeof(NativeFishGuide.FishGuideDataBase.FishGuideListEntry);
        if (first == null || (nuint)last < (nuint)first || (nuint)end < (nuint)last ||
            ((nuint)last - (nuint)first) % entrySize != 0 || ((nuint)end - (nuint)first) % entrySize != 0 ||
            ((nuint)end - (nuint)first) / entrySize > 4096)
            return new(FishGuideSnapshotState.InvalidEntries);
        var count = (int)(((nuint)last - (nuint)first) / entrySize);
        if (count < expectedItems.Count)
            return new(FishGuideSnapshotState.Incomplete, count);

        var seen = new HashSet<uint>();
        var caught = new HashSet<uint>();
        var uncaught = new HashSet<uint>();
        var mismatches = 0;
        for (var index = 0; index < count; index++)
        {
            var entry = first[index];
            if (!expectedItems.TryGetValue(entry.Id, out var expectedItem))
                continue;
            if (entry.ItemId != expectedItem || !seen.Add(entry.Id))
                return new(FishGuideSnapshotState.InvalidEntries, count, seen.Count);
            if (entry.IsCaught != state->IsFishCaught(entry.Id))
                mismatches++;
            (entry.IsCaught ? caught : uncaught).Add(entry.Id);
        }
        if (!state->IsLoaded || state->ContentId != expectedCharacter)
            return new(FishGuideSnapshotState.Unavailable);
        if (seen.Count != expectedItems.Count)
            return new(FishGuideSnapshotState.Incomplete, count, seen.Count);
        if (mismatches != 0)
            return new(FishGuideSnapshotState.BitMismatch, count, seen.Count, mismatches);
        return new(FishGuideSnapshotState.Complete, count, seen.Count)
        {
            Caught = caught.ToFrozenSet(), Uncaught = uncaught.ToFrozenSet(),
        };
    }

}
