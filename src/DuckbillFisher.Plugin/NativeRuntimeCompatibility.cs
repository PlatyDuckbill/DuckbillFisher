using System.Reflection;
using System.Security.Cryptography;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Exact, inspected KR runtime combinations. File checks only; no native access or network.</summary>
internal sealed record NativeRuntimeCompatibility(string GameVersion, string DalamudVersion,
    string? Profile, string MismatchedModules, UiMessage? Failure)
{
    private const string SupportedGameVersion = "2026.10.06.0000.0000";

    // Each row is one complete distribution, not independently interchangeable module allowlists.
    // Sources, hashes and the 15.0.3.7 layout/IL comparison: validation/NATIVE-RUNTIME-15.0.3.7.md.
    private static readonly RuntimeProfile[] Profiles =
    [
        new("15.0.3.6",
            new("d2fe7e59-9fb6-480d-9e6d-00ece6410e34", "D93BE6AB790D89FE980920B2BE03BC0CD41ADDA9AAE104121854C0CC86D5A063"),
            new("f13b1f1f-fd4a-441a-bdac-b3c5abb3675f", "8CC4C29DA5899F547CAEC065569151DEF545CB190B9A8488A23D9B0FAD03FCF2"),
            new("f8f9f456-aaf5-4b57-9551-9fae48c2eb8b", "84638B1F76F3DEEEA0EAA2554559A3E5C925474035C79861BFC0F1B842A6C64F")),
        new("15.0.3.7",
            new("8e662101-af12-485e-90bf-ddc19df3f357", "CFEE2C278B0AF6619503546B78F57B62539399F9BC60B089006F0408A595A275"),
            new("768c4b04-4ac2-4dae-a5ef-263d2fffbdaa", "B4DC56FF088217013871CECF4630AE596407A083D5F78DCC30FE4AE95E3A4BD1"),
            new("7d2ee25e-a51e-41b3-8097-2ad986272d0b", "B158F0A863B4AABB0855EDE5CA824C1FA7B9694DBADB3C65D489246F861A6028")),
    ];

    public bool IsCompatible => Failure is null;
    public string Diagnostic => $"NativeRuntime={DalamudVersion}; NativeProfile={Profile ?? "none"}; " +
        $"NativeStatus={Failure?.Key ?? T.NativeCompatible}; MismatchedModules={MismatchedModules}";

    public static NativeRuntimeCompatibility Inspect()
    {
        var game = "unknown";
        var dalamud = typeof(IPlayerState).Assembly;
        var version = dalamud.GetName().Version?.ToString() ?? "unknown";
        NativeRuntimeCompatibility Reject(UiMessage reason, string modules = "none") =>
            new(game, version, null, modules, reason);
        try
        {
            var processPath = Environment.ProcessPath;
            if (processPath is null || !Path.GetFileName(processPath).Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
                return Reject(new(T.NativeWrongProcess));
            game = File.ReadAllText(Path.Combine(Path.GetDirectoryName(processPath)!, "ffxivgame.ver")).Trim();
            if (game != SupportedGameVersion)
                return Reject(new(T.NativeGameMismatch));

            var profile = Profiles.SingleOrDefault(candidate => candidate.Version == version);
            if (profile is null)
                return Reject(new(T.NativeRuntimeUnsupported, version, string.Join(", ", Profiles.Select(candidate => candidate.Version))));
            var mismatches = new List<string>();
            if (!profile.Dalamud.Matches(dalamud)) mismatches.Add("Dalamud");
            if (!profile.ClientStructs.Matches(typeof(NativePlayerState).Assembly)) mismatches.Add("FFXIVClientStructs");
            if (!profile.Interop.Matches(typeof(InteropGenerator.Runtime.BitArray).Assembly)) mismatches.Add("InteropGenerator.Runtime");
            if (mismatches.Count > 0)
                return Reject(new(T.NativeDllMismatch), string.Join(',', mismatches));
            return new(game, version, profile.Version, "none", null);
        }
        catch (Exception ex)
        {
            // Only managed metadata and file reads run here. No native memory is touched.
            return Reject(new(T.NativeCheckFailed, ex.GetType().Name));
        }
    }

    private sealed record RuntimeProfile(string Version, ModuleIdentity Dalamud, ModuleIdentity ClientStructs, ModuleIdentity Interop);

    private sealed record ModuleIdentity(string Mvid, string Sha256)
    {
        public bool Matches(Assembly assembly)
        {
            if (assembly.ManifestModule.ModuleVersionId != Guid.Parse(Mvid) || string.IsNullOrEmpty(assembly.Location))
                return false;
            using var stream = File.OpenRead(assembly.Location);
            return Convert.ToHexString(SHA256.HashData(stream)) == Sha256;
        }
    }
}
