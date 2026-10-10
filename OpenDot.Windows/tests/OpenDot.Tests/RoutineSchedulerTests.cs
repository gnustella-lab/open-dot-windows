using Moq;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Scheduling;

namespace OpenDot.Tests;

public class RoutineSchedulerTests
{
    [Theory]
    [InlineData("0 9 * * 1-5", true)]
    [InlineData("*/15 * * * *", true)]
    [InlineData("invalid cron syntax", false)]
    [InlineData("0 60 * * *", false)]
    public void IsValidCron_ValidatesExpressionsAccurately(string cron, bool expected)
    {
        var mockRepo = new Mock<IDotRepository>();
        var mockRuntime = new Mock<IAgentRuntime>();

        var scheduler = new RoutineScheduler(mockRepo.Object, () => mockRuntime.Object);
        var result = scheduler.IsValidCron(cron);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNextOccurrence_ReturnsFutureDateForValidCron()
    {
        var mockRepo = new Mock<IDotRepository>();
        var mockRuntime = new Mock<IAgentRuntime>();

        var scheduler = new RoutineScheduler(mockRepo.Object, () => mockRuntime.Object);
        var next = scheduler.GetNextOccurrence("0 8 * * *");

        Assert.NotNull(next);
        Assert.True(next.Value > DateTimeOffset.UtcNow);
    }
}
