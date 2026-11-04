using System.Reflection;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace DuckbillFisher.Plugin;

/// <summary>
/// Report-only description of the running client and the Dalamud/ClientStructs build. Nothing here enables or
/// disables a feature: loading is Dalamud's API-level decision, and each native feature checks its own contract
/// (resolved addresses, value ranges, UI shape, cross-checks). See docs/development/RUNTIME-CONTRACT-REDESIGN.md.
/// </summary>
internal sealed record NativeEnvironment(string GameVersion, ClientLanguage Language, string Modules)
{
    public string Diagnostic => $"NativeEnvironment: Game={GameVersion}; ClientLanguage={Language}; {Modules}; NativeGate=None";

    public static NativeEnvironment Inspect(ClientLanguage language)
    {
        var game = "unknown";
        try
        {
            // Managed file read once at startup; never in Draw or a native read.
            if (Environment.ProcessPath is { } path && Path.GetDirectoryName(path) is { } directory)
                game = File.ReadAllText(Path.Combine(directory, "ffxivgame.ver")).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { game = $"unknown({ex.GetType().Name})"; }

        static string Describe(string name, Assembly assembly) =>
            $"{name}={assembly.GetName().Version?.ToString() ?? "unknown"}/{assembly.ManifestModule.ModuleVersionId}";
        var modules = string.Join("; ",
            Describe("Dalamud", typeof(IPlayerState).Assembly),
            Describe("FFXIVClientStructs", typeof(NativePlayerState).Assembly),
            Describe("InteropGenerator.Runtime", typeof(InteropGenerator.Runtime.BitArray).Assembly));
        return new(game, language, modules);
    }
}
