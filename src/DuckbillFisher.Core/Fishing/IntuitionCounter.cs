namespace DuckbillFisher.Core;

/// <summary>Observed prey quantities. A missing observation never becomes a known zero.</summary>
public sealed class IntuitionCounter
{
    private readonly Dictionary<uint, int> counts = [];
    public bool? Active { get; private set; }
    public bool Exact { get; private set; }
    public int Revision { get; private set; }
    public IReadOnlyDictionary<uint, int> Counts => counts;

    public void Reset(bool exact = false)
    {
        counts.Clear(); Active = null; Exact = exact; Revision++;
    }

    public void ObserveBuff(bool? active, bool uniqueTarget)
    {
        if (active is null)
        {
            if (Active is not null || Exact || counts.Count > 0) Reset();
            return;
        }
        if (active == Active) return;
        if (active == true || Active == true)
        {
            counts.Clear();
            // Only a continuously observed end of an identified target establishes a new zero baseline.
            Exact = !active.Value && uniqueTarget;
        }
        Active = active;
        Revision++;
    }

    public void Catch(uint itemId, int quantity, bool? activeAtCatch, bool uniqueTarget)
    {
        ObserveBuff(activeAtCatch, uniqueTarget);
        if (Active != false || itemId == 0 || quantity is < 1 or > 999) return;
        counts[itemId] = Math.Min(1_000_000, counts.GetValueOrDefault(itemId) + quantity);
        Revision++;
    }
}
