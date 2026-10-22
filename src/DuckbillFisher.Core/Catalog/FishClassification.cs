namespace DuckbillFisher.Core;

public enum FishGrade { Unknown, Regular, Big, Legendary }
public enum FishTimeRestriction { Unknown, Unrestricted, Restricted }
public enum FishGradeVisibility { All, BigIncludingLegendary, LegendaryOnly }

public static class FishClassificationFilter
{
    public static bool Matches(FishGrade grade, FishTimeRestriction timing, bool restrictedOnly, FishGradeVisibility visibility) =>
        (!restrictedOnly || timing == FishTimeRestriction.Restricted) && (visibility switch
        {
            FishGradeVisibility.All => true,
            FishGradeVisibility.BigIncludingLegendary => grade is FishGrade.Big or FishGrade.Legendary,
            FishGradeVisibility.LegendaryOnly => grade == FishGrade.Legendary,
            _ => false,
        });
}
