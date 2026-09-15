using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FileFlow.Sdk;

namespace FileFlow.App.Views;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
        TxtVersion.Text = $"v{AppVersionInfo.DisplayVersion}";
    }

    public void UpdateStatus(string message, double progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TxtStatus.Text = message;
            PbProgress.Value = Math.Clamp(progress, 0, 100);
            TxtPercentage.Text = $"{(int)PbProgress.Value}%";
        });
    }

    public void SetNodeCount(int count)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TxtNodesBadge.Text = $"🧩 {count} Nodos DAG";
        });
    }

    public async Task CloseWithFadeAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(150);
            Close();
        });
    }
}
