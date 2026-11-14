namespace Rochambeau.UnitTests.Common;

public class HarnessTests
{
    [Fact]
    public void Unit_test_harness_runs()
    {
        var time = TimeProvider.System;

        Assert.NotEqual(default, time.GetUtcNow());
    }
}
