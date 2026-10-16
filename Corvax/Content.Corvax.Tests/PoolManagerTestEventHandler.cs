namespace Content.Corvax.Tests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    private static TimeSpan MaximumTotalTestingTimeLimit => TimeSpan.FromMinutes(30);
    private static TimeSpan HardStopTimeLimit => MaximumTotalTestingTimeLimit.Add(TimeSpan.FromMinutes(1));

    [OneTimeSetUp]
    public void Setup()
    {
        PoolManager.Startup();
        _ = Task.Delay(MaximumTotalTestingTimeLimit).ContinueWith(_ =>
        {
            TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests are taking too long. Shutting down.\n\n");
            PoolManager.Shutdown();
        });

        _ = Task.Delay(HardStopTimeLimit).ContinueWith(_ =>
        {
            var deathReport = PoolManager.DeathReport();
            Environment.FailFast($"Tests took way too long;\n Death Report:\n{deathReport}");
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        PoolManager.Shutdown();
    }
}
