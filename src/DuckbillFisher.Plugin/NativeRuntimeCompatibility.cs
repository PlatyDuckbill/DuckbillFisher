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
            var gameProfiles = NativeRuntimeProfiles.All.Where(candidate => candidate.GameVersion == game).ToArray();
            if (gameProfiles.Length == 0)
                return Reject(new(T.NativeGameMismatch));

            var candidates = gameProfiles.Where(candidate => candidate.Version == version).ToArray();
            if (candidates.Length == 0)
                return Reject(new(T.NativeRuntimeUnsupported, version,
                    string.Join(", ", gameProfiles.Select(candidate => candidate.Version).Distinct())));
            // Capture each loaded module once, even when several distributions share a version.
            var dalamudIdentity = ReadIdentity(dalamud);
            var structsIdentity = ReadIdentity(typeof(NativePlayerState).Assembly);
            var interopIdentity = ReadIdentity(typeof(InteropGenerator.Runtime.BitArray).Assembly);
            List<string>? closestMismatches = null;
            foreach (var profile in candidates)
            {
                var mismatches = new List<string>();
                if (!profile.Dalamud.Matches(dalamudIdentity)) mismatches.Add("Dalamud");
                if (!profile.ClientStructs.Matches(structsIdentity)) mismatches.Add("FFXIVClientStructs");
                if (!profile.Interop.Matches(interopIdentity)) mismatches.Add("InteropGenerator.Runtime");
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

    private static NativeModuleIdentity ReadIdentity(Assembly assembly)
    {
        var mvid = assembly.ManifestModule.ModuleVersionId.ToString();
        if (string.IsNullOrEmpty(assembly.Location))
            return new(mvid, ""); // Unavailable file identity must never match an inspected module.
        using var stream = File.OpenRead(assembly.Location);
        return new(mvid, Convert.ToHexString(SHA256.HashData(stream)));
    }
}
