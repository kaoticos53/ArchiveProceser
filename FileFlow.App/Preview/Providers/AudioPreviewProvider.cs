using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    public Task<Control> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var rootGrid = new Grid
        {
            Background = new SolidColorBrush(Color.Parse("#111318")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(32)
        };

        var playerCard = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1D24")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A2D35")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24)
        };

        var mainStack = new StackPanel();

        // Icono y título
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        var iconText = new TextBlock { Text = "🎙️", FontSize = 32, Margin = new Thickness(0, 0, 12, 0) };
        var fileDetails = new StackPanel();
        var fileNameBlock = new TextBlock { Text = context.FileName, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.Bold };
        var fileSizeBlock = new TextBlock { Text = $"{context.FileSizeBytes / 1024.0:F1} KB • Audio", Foreground = new SolidColorBrush(Color.Parse("#8F95A3")), FontSize = 12 };
        fileDetails.Children.Add(fileNameBlock);
        fileDetails.Children.Add(fileSizeBlock);
        titleStack.Children.Add(iconText);
        titleStack.Children.Add(fileDetails);
        mainStack.Children.Add(titleStack);

        // Si hay transcripción en los metadatos (Whisper / IA), mostrarla
        if (context.Metadata.TryGetValue("Transcript", out var transcriptObj) && transcriptObj is string transcript && !string.IsNullOrWhiteSpace(transcript))
        {
            var transcriptBox = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#14161D")),
                BorderBrush = new SolidColorBrush(Color.Parse("#00E5FF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 16, 0, 0),
                Padding = new Thickness(12)
            };

            var tStack = new StackPanel();
            tStack.Children.Add(new TextBlock { Text = "🤖 Transcripción Whisper IA:", Foreground = new SolidColorBrush(Color.Parse("#00E5FF")), FontWeight = FontWeight.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
            tStack.Children.Add(new TextBlock { Text = transcript, Foreground = new SolidColorBrush(Color.Parse("#E1E4EA")), FontSize = 13, TextWrapping = TextWrapping.Wrap });
            transcriptBox.Child = tStack;
            mainStack.Children.Add(transcriptBox);
        }

        playerCard.Child = mainStack;
        rootGrid.Children.Add(playerCard);

        return Task.FromResult<Control>(rootGrid);
    }
}
