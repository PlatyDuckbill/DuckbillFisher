using System.Reflection;
using System.Security.Cryptography;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record FishBitSample(uint FishParameterId, bool? Value, UiMessage Reason);

/// <summary>
/// A diagnostic read of one fish bit using the exact inspected KR installation.
/// Pointers are acquired and discarded within the current framework update.
/// No offsets, signature scanning, writes, or inferred collection readiness.
/// </summary>
internal sealed class NativeFishCollectionReader
{
    private const uint MaxValidatedFishParameterId = 1522;
    private const string SupportedGameVersion = "2026.10.06.0000.0000";
    private readonly IFramework framework;
    private readonly UiMessage? unsupportedReason;

    public NativeFishCollectionReader(IFramework framework)
    {
        this.framework = framework;
        // File/assembly checks happen once during construction, never in Draw or a native read.
        unsupportedReason = CheckCompatibility();
    }

    public string GameVersion { get; private set; } = "unknown";
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

    private UiMessage? CheckCompatibility()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (processPath is null || !Path.GetFileName(processPath).Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
                return new(T.NativeWrongProcess);
            var versionPath = Path.Combine(Path.GetDirectoryName(processPath)!, "ffxivgame.ver");
            GameVersion = File.ReadAllText(versionPath).Trim();
            if (GameVersion != SupportedGameVersion)
                return new(T.NativeGameMismatch);

            // Check loaded module identity as well as the installed file, including dirty builds.
            if (!Matches(typeof(IPlayerState).Assembly,
                    "d2fe7e59-9fb6-480d-9e6d-00ece6410e34",
                    "D93BE6AB790D89FE980920B2BE03BC0CD41ADDA9AAE104121854C0CC86D5A063") ||
                !Matches(typeof(NativePlayerState).Assembly,
                    "f13b1f1f-fd4a-441a-bdac-b3c5abb3675f",
                    "8CC4C29DA5899F547CAEC065569151DEF545CB190B9A8488A23D9B0FAD03FCF2") ||
                !Matches(typeof(InteropGenerator.Runtime.BitArray).Assembly,
                    "f8f9f456-aaf5-4b57-9551-9fae48c2eb8b",
                    "84638B1F76F3DEEEA0EAA2554559A3E5C925474035C79861BFC0F1B842A6C64F"))
                return new(T.NativeDllMismatch);
            return null;
        }
        catch (Exception ex)
        {
            // Only managed file/metadata checks run here. No native memory is read.
            return new(T.NativeCheckFailed, ex.GetType().Name);
        }
    }

    private static bool Matches(Assembly assembly, string moduleId, string sha256)
    {
        if (assembly.ManifestModule.ModuleVersionId != Guid.Parse(moduleId) || string.IsNullOrEmpty(assembly.Location))
            return false;
        using var stream = File.OpenRead(assembly.Location);
        return Convert.ToHexString(SHA256.HashData(stream)) == sha256;
    }
}
