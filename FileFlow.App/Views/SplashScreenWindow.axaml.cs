using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FileFlow.Sdk;

namespace FileFlow.App.Views;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
        var txtVersion = this.FindControl<TextBlock>("TxtVersion");
        if (txtVersion != null)
        {
            txtVersion.Text = $"v{AppVersionInfo.DisplayVersion}";
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void UpdateStatus(string message, double progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var txtStatus = this.FindControl<TextBlock>("TxtStatus");
            var pbProgress = this.FindControl<ProgressBar>("PbProgress");
            var txtPercentage = this.FindControl<TextBlock>("TxtPercentage");

            if (txtStatus != null) txtStatus.Text = message;
            if (pbProgress != null) pbProgress.Value = Math.Clamp(progress, 0, 100);
            if (txtPercentage != null && pbProgress != null) txtPercentage.Text = $"{(int)pbProgress.Value}%";
        });
    }

    public void SetNodeCount(int count)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var txtNodesBadge = this.FindControl<TextBlock>("TxtNodesBadge");
            if (txtNodesBadge != null) txtNodesBadge.Text = $"🧩 {count} Nodos DAG";
        });
    }

    public async Task CloseWithFadeAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(50);
            Close();
        });
    }
}
