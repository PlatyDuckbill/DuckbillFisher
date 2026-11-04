namespace DuckbillFisher.Plugin;

/// <summary>
/// Sheet identities with the same meaning in KR and global clients (docs/verification/native/GLOBAL-COMPAT.md).
/// Dependency-free on purpose: FishingDataImport links this file so `--data-references` checks the same values
/// at patch time. Translated names are never used to identify these rows.
/// </summary>
internal static class FishingGameIds
{
    public const uint FisherJob = 18;
    public const uint PatienceStatus = 850;
    public const uint IntuitionStatus = 568;
    public const uint GiganticLureAction = 37594;
    public const uint SmallLureAction = 37595;
    public const uint GiganticLureStatus = 3972;
    public const uint SmallLureStatus = 3973;
    public const uint FishEyesAction = 4105;
    // PlaceName shown for an undiscovered fishing hole.
    public const uint UnknownSpotPlaceName = 950;
    // ItemUICategory of fishing bait.
    public const uint BaitCategory = 33;

    // Housing TerritoryType.ExVersion is 0 even for later residential districts (KR 2026.10.06, global 2026.10.16).
    // The correction applies only while the sheet still reports 0. Sources: DATA-SOURCES, collection geography audit.
    public static readonly (uint Territory, uint Expansion)[] HousingExpansionCorrections = [(641, 2), (979, 4)];

    public static readonly uint[] Statuses = [PatienceStatus, IntuitionStatus, GiganticLureStatus, SmallLureStatus];
    public static readonly uint[] Actions = [GiganticLureAction, SmallLureAction, FishEyesAction];
}
