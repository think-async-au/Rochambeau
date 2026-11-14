using Microsoft.Extensions.Time.Testing;
using Rochambeau.Api.Common.Time;

namespace Rochambeau.UnitTests.Common;

public class StorageTimeTests
{
    [Fact]
    public void Now_for_storage_is_truncated_to_whole_microseconds()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_567));

        var now = time.GetUtcNowForStorage();

        Assert.Equal(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_560), now);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }
}
