using FileFlow.Core.Utils;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class MemoryReclamationTests
{
    [Fact]
    public void MemoryReclamationHelper_ReclaimMemory_ShouldExecuteSafelyWithoutExceptions()
    {
        // Arrange & Act
        var act = () => MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void MemoryReclamationHelper_RegisterCleanupCallback_ShouldInvokeRegisteredAction()
    {
        // Arrange
        bool callbackInvoked = false;
        MemoryReclamationHelper.RegisterCleanupCallback(() => callbackInvoked = true);

        // Act
        MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: false);

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void MemoryReclamationHelper_AfterLargeAllocation_ReclaimsMemory()
    {
        // Arrange: allocate inside a separate method/scope so references are dead
        static void AllocateGarbage()
        {
            var list = new List<byte[]>();
            for (int i = 0; i < 20; i++)
            {
                list.Add(new byte[1024 * 1024 * 2]); // 40 MB total
            }
        }

        AllocateGarbage();
        long memoryWithGarbage = GC.GetTotalMemory(false);

        // Act
        MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: true);
        long memoryAfter = GC.GetTotalMemory(true);

        // Assert
        memoryAfter.Should().BeLessThan(memoryWithGarbage);
    }
}
