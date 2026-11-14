namespace Rochambeau.Api.Common.Time;

/// <summary>Time values as PostgreSQL <c>timestamptz</c> stores them (microsecond precision, UTC).</summary>
public static class StorageTime
{
    /// <summary>
    /// The current UTC time truncated to whole microseconds, so a value written to the database and
    /// echoed from memory is the same value that is read back later.
    /// </summary>
    public static DateTimeOffset GetUtcNowForStorage(this TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.UtcTicks % TimeSpan.TicksPerMicrosecond));
    }
}
