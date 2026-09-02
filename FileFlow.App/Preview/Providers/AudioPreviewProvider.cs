using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FileFlow.App.Preview.Core;

namespace FileFlow.App.Preview.Providers;

public class AudioPreviewProvider : IFilePreviewProvider
{
    public string ProviderName => "Audio & Voice Previewer";
    public int Priority => 85;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".wma", ".aac"
    };

    public bool CanHandle(FilePreviewContext context)
    {
        return _supportedExtensions.Contains(context.Extension);
    }

    public Task<FrameworkElement> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var rootGrid = new Grid
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(32)
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var playerCard = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1D24")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2D35")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24)
        };

        var mainStack = new StackPanel();

        // Icono y título
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        var iconText = new TextBlock { Text = "🎙️", FontSize = 32, Margin = new Thickness(0, 0, 12, 0) };
        var fileDetails = new StackPanel();
        var fileNameBlock = new TextBlock { Text = context.FileName, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold };
        var fileSizeBlock = new TextBlock { Text = $"{context.FileSizeBytes / 1024.0:F1} KB • Audio", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8F95A3")), FontSize = 12 };
        fileDetails.Children.Add(fileNameBlock);
        fileDetails.Children.Add(fileSizeBlock);
        titleStack.Children.Add(iconText);
        titleStack.Children.Add(fileDetails);
        mainStack.Children.Add(titleStack);

        var player = new MediaPlayer();
        if (File.Exists(context.CurrentPath))
        {
            try
            {
                player.Open(new Uri(context.CurrentPath));
            }
            catch { }
        }

        // Barra de progreso y botones
        var controlsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
        var playBtn = new Button { Content = "▶ Reproducir", Padding = new Thickness(16, 8, 16, 8), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D2FF")), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, Cursor = System.Windows.Input.Cursors.Hand };
        var pauseBtn = new Button { Content = "⏸ Pausar", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
        var stopBtn = new Button { Content = "⏹ Detener", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };

        playBtn.Click += (_, _) => player.Play();
        pauseBtn.Click += (_, _) => player.Pause();
        stopBtn.Click += (_, _) => player.Stop();

        controlsStack.Children.Add(playBtn);
        controlsStack.Children.Add(pauseBtn);
        controlsStack.Children.Add(stopBtn);
        mainStack.Children.Add(controlsStack);

        // Si hay transcripción en los metadatos (Whisper / IA), mostrarla
        if (context.Metadata.TryGetValue("Transcript", out var transcriptObj) && transcriptObj is string transcript && !string.IsNullOrWhiteSpace(transcript))
        {
            var transcriptBox = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14161D")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 16, 0, 0),
                Padding = new Thickness(12)
            };

            var tStack = new StackPanel();
            tStack.Children.Add(new TextBlock { Text = "🤖 Transcripción Whisper IA:", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
            tStack.Children.Add(new TextBlock { Text = transcript, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E4EA")), FontSize = 13, TextWrapping = TextWrapping.Wrap });
            transcriptBox.Child = tStack;
            mainStack.Children.Add(transcriptBox);
        }

        playerCard.Child = mainStack;
        Grid.SetRow(playerCard, 0);
        rootGrid.Children.Add(playerCard);

        return Task.FromResult<FrameworkElement>(rootGrid);
    }
}
