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
        TxtStatus.Text = message;
        PbProgress.Value = Math.Clamp(progress, 0, 100);
        TxtPercentage.Text = $"{(int)PbProgress.Value}%";
    }

    public void SetNodeCount(int count)
    {
        TxtNodesBadge.Text = $"🧩 {count} Nodos DAG";
    }

    public async Task CloseWithFadeAsync()
    {
        for (double opacity = 1.0; opacity > 0.05; opacity -= 0.15)
        {
            Opacity = opacity;
            await Task.Delay(16);
        }
        Close();
    }
}
