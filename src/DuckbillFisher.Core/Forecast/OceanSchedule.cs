namespace DuckbillFisher.Core;

/// <summary>Registration slots in UTC. Route order is supplied by the client's IKDRouteTable.</summary>
public static class OceanSchedule
{
    public static readonly TimeSpan RegistrationDuration = TimeSpan.FromMinutes(15);
    private const long SlotSeconds = 2 * 60 * 60;
    private const int CycleLength = 144;
    // The client table and the author's complete Indigo/Ruby cycles agree at this Unix phase.
    private const int UnixTablePhase = 40;

    public static DateTimeOffset[] RegistrationStarts(DateTimeOffset utcNow, int count = 3)
    {
        if (count is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(count));
        var seconds = utcNow.ToUnixTimeSeconds();
        var slot = (long)Math.Floor(seconds / (double)SlotSeconds);
        var start = DateTimeOffset.FromUnixTimeSeconds(slot * SlotSeconds);
        if (utcNow >= start + RegistrationDuration) start += TimeSpan.FromSeconds(SlotSeconds);
        return Enumerable.Range(0, count).Select(i => start + TimeSpan.FromSeconds(i * SlotSeconds)).ToArray();
    }

    public static int TableIndex(DateTimeOffset registrationStart)
    {
        var slot = (long)Math.Floor(registrationStart.ToUnixTimeSeconds() / (double)SlotSeconds);
        return (int)(((slot + UnixTablePhase) % CycleLength + CycleLength) % CycleLength);
    }
}
