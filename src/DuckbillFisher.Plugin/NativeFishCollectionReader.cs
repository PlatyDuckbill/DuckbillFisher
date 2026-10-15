using System.Reflection;
using System.Security.Cryptography;
using Dalamud.Plugin.Services;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace DuckbillFisher.Plugin;

internal sealed record FishBitSample(uint FishParameterId, bool? Value, string Reason);

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
    private readonly string? unsupportedReason;

    public NativeFishCollectionReader(IFramework framework)
    {
        this.framework = framework;
        // File/assembly checks happen once during construction, never in Draw or a native read.
        unsupportedReason = CheckCompatibility();
    }

    public string GameVersion { get; private set; } = "확인 불가";
    public string Compatibility => unsupportedReason ?? "조사한 KR 게임 빌드와 DLL 일치";

    public unsafe FishBitSample Read(FishGuideRow row, ulong expectedCharacter, bool fishGuideVisible)
    {
        FishBitSample Unknown(string reason) => new(row.FishParameterId, null, reason);
        if (unsupportedReason is not null)
            return Unknown(unsupportedReason);
        if (!framework.IsInFrameworkUpdateThread)
            return Unknown("게임 프레임워크 스레드가 아닙니다.");
        if (!fishGuideVisible)
            return Unknown("게임의 어류도감을 열고 다시 선택해 주세요.");
        if (!row.IsInLog || row.ItemType != "Item" || row.FishParameterId is 0 or > MaxValidatedFishParameterId)
            return Unknown("검증한 일반 낚시 어종의 수첩 ID 범위가 아닙니다.");
        if (expectedCharacter == 0)
            return Unknown("캐릭터 준비를 확인하지 못했습니다.");

        // Installed metadata confirms this is the generated getter for the resolved static address.
        // A null address is rejected before dereference; no pointer survives this call.
        var state = NativePlayerState.StaticAddressPointers.pInstance;
        if (state == null)
            return Unknown("PlayerState 주소가 준비되지 않았습니다.");
        if (!state->IsLoaded || state->ContentId != expectedCharacter)
            return Unknown("캐릭터가 준비되지 않았거나 조회 도중 변경됐습니다.");

        // This read is deliberately not wrapped in try/catch: invalid pointers are not recoverable
        // managed errors. Compatibility, scope, lifetime, and row eligibility are checked first.
        var value = state->IsFishCaught(row.FishParameterId);
        return new(row.FishParameterId, value, "원시 수집 비트 표본입니다. 실제 수첩과 대조 전이며 자동 필터에는 사용하지 않습니다.");
    }

    private string? CheckCompatibility()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (processPath is null || !Path.GetFileName(processPath).Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
                return "Unknown: 검증한 게임 프로세스가 아닙니다.";
            var versionPath = Path.Combine(Path.GetDirectoryName(processPath)!, "ffxivgame.ver");
            GameVersion = File.ReadAllText(versionPath).Trim();
            if (GameVersion != SupportedGameVersion)
                return "Unknown: 조사한 KR 게임 빌드와 다릅니다. 수집 비트 읽기를 비활성화했습니다.";

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
                return "Unknown: 조사한 KR DLL과 다릅니다. 수집 비트 읽기를 비활성화했습니다.";
            return null;
        }
        catch (Exception ex)
        {
            // Only managed file/metadata checks run here. No native memory is read.
            return $"Unknown: 호환성 확인 실패 ({ex.GetType().Name}). 수집 비트 읽기를 비활성화했습니다.";
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
