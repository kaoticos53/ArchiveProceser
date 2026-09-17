using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class AsyncTestWaiterTests
{
    [Fact]
    public async Task WaitForAsync_WhenConditionBecomesTrue_ShouldReturn()
    {
        int observations = 0;

        await AsyncTestWaiter.WaitForAsync(
            () => ++observations >= 3,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(1),
            description: "the synthetic condition");

        observations.Should().Be(3);
    }

    [Fact]
    public async Task WaitForAsync_WhenConditionNeverBecomesTrue_ShouldIncludeDescriptionInTimeout()
    {
        Func<Task> act = () => AsyncTestWaiter.WaitForAsync(
            () => false,
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(1),
            description: "the file watcher");

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*the file watcher*");
    }

    [Fact]
    public async Task WaitForAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> act = () => AsyncTestWaiter.WaitForAsync(
            () => false,
            TimeSpan.FromSeconds(1),
            cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
