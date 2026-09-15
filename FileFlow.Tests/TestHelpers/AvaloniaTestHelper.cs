using Avalonia;
using Avalonia.Headless;

namespace FileFlow.Tests.TestHelpers;

public static class AvaloniaTestHelper
{
    private static readonly Lock _lock = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                AppBuilder.Configure<FileFlow.App.App>()
                    .UseSkia()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                    .SetupWithoutStarting();
            }
            catch
            {
            }
            _initialized = true;
        }
    }
}
