using System.Collections.Frozen;
using Dalamud.Game;
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
/// Fish-bit diagnostics and validated game-guide snapshots on the running KR or global client.
/// Pointers are acquired and discarded within the current framework update.
/// No hardcoded offsets, own signatures, writes, or inferred collection readiness.
/// </summary>
internal sealed class NativeFishCollectionReader
{
    // Highest readable FishParameter ID, taken from the loaded ClientStructs layout instead of a pinned constant.
    private static readonly uint MaxFishParameterId = ReadBitLimit();
    private readonly IFramework framework;

    public NativeFishCollectionReader(IFramework framework, ClientLanguage language)
    {
        this.framework = framework;
        // Report-only; file/assembly reads happen once during construction, never in Draw or a native read.
        RuntimeEnvironment = NativeEnvironment.Inspect(language);
    }

    public NativeEnvironment RuntimeEnvironment { get; }
    public string EnvironmentDiagnostic => $"{RuntimeEnvironment.Diagnostic}; FishBitLimit={MaxFishParameterId}";

    /// <summary>Collection reads need the PlayerState static address that ClientStructs resolved in the running game.</summary>
    public unsafe bool IsAvailable => NativePlayerState.StaticAddressPointers.pInstance != null;
    public UiMessage Availability => IsAvailable ? new(T.NativeRawSample) : new(T.NativeAddressMissing);

    public static bool Supports(FishGuideRow row) => row.IsDisplayable && row.IsInLog && row.ItemType == "Item" &&
        row.FishParameterId > 0 && row.FishParameterId <= MaxFishParameterId;

    /// <summary>
    /// Reads the declared bit count and buffer size from a zeroed local copy of the loaded PlayerState layout.
    /// No game memory is touched. The generated BitArray accepts index == BitCount (1522 in KR/global 7.57),
    /// so the limit keeps that ID while never exceeding the fixed buffer.
    /// </summary>
    private static unsafe uint ReadBitLimit()
    {
        var layout = default(NativePlayerState);
        var declared = layout.CaughtFishBitArray.BitCount;
        var buffer = layout.CaughtFish.Length * 8 - 1;
        return (uint)Math.Max(0, Math.Min(declared, buffer));
    }

    // Same bit order as InteropGenerator BitArray.Get (byte id/8, bit id%8). The span is bounded by the
    // loaded layout, and callers have already limited id to MaxFishParameterId.
    private static unsafe bool IsCaught(NativePlayerState* state, uint id) =>
        (state->CaughtFish[(int)(id >> 3)] & (1 << (int)(id & 7))) != 0;

    public unsafe FishBitSample Read(FishGuideRow row, ulong expectedCharacter, bool fishGuideVisible)
    {
        FishBitSample Unknown(UiMessage reason) => new(row.FishParameterId, null, reason);
        if (!framework.IsInFrameworkUpdateThread)
            return Unknown(new(T.NativeWrongThread));
        if (!fishGuideVisible)
            return Unknown(new(T.NativeGuideRequired));
        if (!Supports(row))
            return Unknown(new(T.NativeRowUnsupported));
        if (expectedCharacter == 0)
            return Unknown(new(T.NativePlayerRequired));

        // Generated getter for the static address ClientStructs resolved in the running game.
        // A null address is rejected before dereference; no pointer survives this call.
        var state = NativePlayerState.StaticAddressPointers.pInstance;
        if (state == null)
            return Unknown(new(T.NativeAddressMissing));
        if (!state->IsLoaded || state->ContentId != expectedCharacter)
            return Unknown(new(T.NativeCharacterChanged));

        // This read is deliberately not wrapped in try/catch: invalid pointers are not recoverable
        // managed errors. Address, scope, lifetime, and row eligibility are checked first.
        var value = IsCaught(state, row.FishParameterId);
        return new(row.FishParameterId, value, new(T.NativeRawSample));
    }

    // This is the game's completed, unfiltered guide list, not a network-receipt flag.
    // Never infer Uncaught from a bare false bit or from sheet/UI visibility alone.
    public unsafe FishGuideSnapshot ReadGuideSnapshot(IReadOnlyDictionary<uint, uint> expectedItems,
        ulong expectedCharacter, nint agentAddress)
    {
        if (!framework.IsInFrameworkUpdateThread || expectedCharacter == 0 || agentAddress == 0 ||
            expectedItems.Count == 0 || expectedItems.Keys.Any(id => id == 0 || id > MaxFishParameterId))
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
            if (entry.IsCaught != IsCaught(state, entry.Id))
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
