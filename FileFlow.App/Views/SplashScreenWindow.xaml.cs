using System.Windows;
using System.Windows.Media.Animation;
using FileFlow.Sdk;

namespace FileFlow.App.Views;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
        TxtVersion.Text = $"v{AppVersionInfo.DisplayVersion}";

        Loaded += (s, e) =>
        {
            if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
            {
                fadeIn.Begin(this);
            }
        };
    }

    public void UpdateStatus(string message, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = message;
            PbProgress.Value = Math.Clamp(progress, 0, 100);
            TxtPercentage.Text = $"{(int)PbProgress.Value}%";
        });
    }

    public void SetNodeCount(int count)
    {
        Dispatcher.Invoke(() =>
        {
            TxtNodesBadge.Text = $"🧩 {count} Nodos DAG";
        });
    }

    public async Task CloseWithFadeAsync()
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            if (Resources["FadeOutStoryboard"] is Storyboard fadeOut)
            {
                var tcs = new TaskCompletionSource();
                fadeOut.Completed += (s, e) => tcs.TrySetResult();
                fadeOut.Begin(this);
                await tcs.Task;
            }
            Close();
        });
    }
}
