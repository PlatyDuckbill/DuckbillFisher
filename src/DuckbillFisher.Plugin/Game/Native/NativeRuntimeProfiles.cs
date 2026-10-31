namespace DuckbillFisher.Plugin;

// Curated evidence only. Adding a row requires game/layout/thread/lifetime validation.
// No runtime configuration or version-range fallback can enable native access.
internal static class NativeRuntimeProfiles
{
    private const string SupportedGameVersion = "2026.10.06.0000.0000";

    // Each row is one complete distribution, not independently interchangeable module allowlists.
    // Sources and layout/IL comparisons: docs/verification/native/NATIVE-RUNTIME-15.0.3.7.md
    // and NATIVE-RUNTIME-DAL4KR.md (including 15.0.3.9). A version can name multiple KR builds.
    public static IReadOnlyList<NativeRuntimeProfile> All { get; } = Array.AsReadOnly<NativeRuntimeProfile>(
    [
        new(SupportedGameVersion, "15.0.3.6",
            new("d2fe7e59-9fb6-480d-9e6d-00ece6410e34", "D93BE6AB790D89FE980920B2BE03BC0CD41ADDA9AAE104121854C0CC86D5A063"),
            new("f13b1f1f-fd4a-441a-bdac-b3c5abb3675f", "8CC4C29DA5899F547CAEC065569151DEF545CB190B9A8488A23D9B0FAD03FCF2"),
            new("f8f9f456-aaf5-4b57-9551-9fae48c2eb8b", "84638B1F76F3DEEEA0EAA2554559A3E5C925474035C79861BFC0F1B842A6C64F")),
        new(SupportedGameVersion, "15.0.3.7",
            new("8e662101-af12-485e-90bf-ddc19df3f357", "CFEE2C278B0AF6619503546B78F57B62539399F9BC60B089006F0408A595A275"),
            new("768c4b04-4ac2-4dae-a5ef-263d2fffbdaa", "B4DC56FF088217013871CECF4630AE596407A083D5F78DCC30FE4AE95E3A4BD1"),
            new("7d2ee25e-a51e-41b3-8097-2ad986272d0b", "B158F0A863B4AABB0855EDE5CA824C1FA7B9694DBADB3C65D489246F861A6028")),
        new(SupportedGameVersion, "15.0.3.7",
            new("b4e06917-bffd-4e12-89da-82bde6b4c73e", "B92AC6B5BB51BB874590EB2E5D4BCDD1F9328786AB3D8A5D493583EFFD447926"),
            new("3b9613ec-ea8d-4d0e-9c1e-6f91f3b46d50", "0E9F985538F800F75B4AABADE1C443F3537D93C695BE368852CA44D9A7D3371E"),
            new("d5357f8d-0f92-464f-8936-de6fe0816b4d", "A75408D83A54D2ED59B3BF178A0474772CE86F4046F2645E76C2B84AE232B178"), "dal4kr"),
        new(SupportedGameVersion, "15.0.3.9",
            new("1e8f635a-5365-40f6-b920-830d6a816d43", "7255103B62A1BB3687492B78E8FAF4B91BD969826B39D76602D0A861C92E158D"),
            new("93c1ca2b-80f3-43ef-9d58-ab0e99bb8909", "052AC3B331F6DFC2A72B58D61506A1A696A68EB13416B3C3D514FAFDF5EB4F40"),
            new("a296f580-78c4-4617-afef-e85be4bdfe55", "04C4C92CD05506EDFED442EEE9D331734AF7B9E151A572964365B80EF38B9422")),
        new(SupportedGameVersion, "15.0.3.9",
            new("396f9566-f391-4d29-8022-f414a19bbfc7", "0E5FBEF4B66B90D9C5C82B890338D07689F9BB7EB8AEAB6287A4FB2AA645EBA6"),
            new("0b9687ac-66d1-439f-8785-f7fff74f3d38", "271A16276655A828DAE7A8AC213B3890B2B328A12B1FAB2B936E070931A5D7E1"),
            new("fe8269c0-eb0a-43d0-9032-c90f44f3136c", "4FACB44948B84BDB7A8FE9E336A390D92E4EF58684609F04B0F54A349B161A7B"), "dal4kr"),
    ]);
}

internal sealed record NativeRuntimeProfile(string GameVersion, string Version, NativeModuleIdentity Dalamud,
    NativeModuleIdentity ClientStructs, NativeModuleIdentity Interop, string? Variant = null)
{
    public string Id => Variant is null ? Version : $"{Version}-{Variant}";
}

internal sealed record NativeModuleIdentity(string Mvid, string Sha256)
{
    public bool Matches(NativeModuleIdentity actual) =>
        Guid.Parse(Mvid) == Guid.Parse(actual.Mvid) && Sha256 == actual.Sha256;
}
