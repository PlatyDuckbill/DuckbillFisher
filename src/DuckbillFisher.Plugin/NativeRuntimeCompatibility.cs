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
    // Sources and layout/IL comparisons: docs/validation/NATIVE-RUNTIME-15.0.3.7.md
    // and NATIVE-RUNTIME-DAL4KR.md. A version number can name more than one KR build.
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
        new("15.0.3.7",
            new("b4e06917-bffd-4e12-89da-82bde6b4c73e", "B92AC6B5BB51BB874590EB2E5D4BCDD1F9328786AB3D8A5D493583EFFD447926"),
            new("3b9613ec-ea8d-4d0e-9c1e-6f91f3b46d50", "0E9F985538F800F75B4AABADE1C443F3537D93C695BE368852CA44D9A7D3371E"),
            new("d5357f8d-0f92-464f-8936-de6fe0816b4d", "A75408D83A54D2ED59B3BF178A0474772CE86F4046F2645E76C2B84AE232B178"), "dal4kr"),
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

            var candidates = Profiles.Where(candidate => candidate.Version == version).ToArray();
            if (candidates.Length == 0)
                return Reject(new(T.NativeRuntimeUnsupported, version,
                    string.Join(", ", Profiles.Select(candidate => candidate.Version).Distinct())));
            List<string>? closestMismatches = null;
            foreach (var profile in candidates)
            {
                var mismatches = new List<string>();
                if (!profile.Dalamud.Matches(dalamud)) mismatches.Add("Dalamud");
                if (!profile.ClientStructs.Matches(typeof(NativePlayerState).Assembly)) mismatches.Add("FFXIVClientStructs");
                if (!profile.Interop.Matches(typeof(InteropGenerator.Runtime.BitArray).Assembly)) mismatches.Add("InteropGenerator.Runtime");
                if (mismatches.Count == 0)
                    return new(game, version, profile.Id, "none", null);
                if (closestMismatches is null || mismatches.Count < closestMismatches.Count)
                    closestMismatches = mismatches;
            }
            return Reject(new(T.NativeDllMismatch), string.Join(',', closestMismatches!));
        }
        catch (Exception ex)
        {
            // Only managed metadata and file reads run here. No native memory is touched.
            return Reject(new(T.NativeCheckFailed, ex.GetType().Name));
        }
    }

    private sealed record RuntimeProfile(string Version, ModuleIdentity Dalamud, ModuleIdentity ClientStructs,
        ModuleIdentity Interop, string? Variant = null)
    {
        public string Id => Variant is null ? Version : $"{Version}-{Variant}";
    }

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
