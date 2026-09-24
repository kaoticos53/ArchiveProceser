using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace FileFlow.App.Views.Components;

public partial class ColorPickerButton : UserControl
{
    private bool _isInternalChange;

    public static readonly StyledProperty<string> SelectedColorHexProperty =
        AvaloniaProperty.Register<ColorPickerButton, string>(
            nameof(SelectedColorHex),
            defaultValue: "#6366F1");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ColorPickerButton, string>(
            nameof(Label),
            defaultValue: "Color");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ColorPickerButton, string>(
            nameof(Description),
            defaultValue: string.Empty);

    public string SelectedColorHex
    {
        get => GetValue(SelectedColorHexProperty);
        set => SetValue(SelectedColorHexProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public event EventHandler<string>? ColorChanged;

    static ColorPickerButton()
    {
        SelectedColorHexProperty.Changed.AddClassHandler<ColorPickerButton>((x, e) =>
        {
            if (e.NewValue is string newVal)
            {
                x.UpdateVisuals(newVal);
                x.ColorChanged?.Invoke(x, newVal);
            }
        });

        LabelProperty.Changed.AddClassHandler<ColorPickerButton>((x, e) =>
        {
            x.TxtLabel.Text = e.NewValue as string ?? "Color";
        });

        DescriptionProperty.Changed.AddClassHandler<ColorPickerButton>((x, e) =>
        {
            string text = e.NewValue as string ?? string.Empty;
            x.TxtDesc.Text = text;
            x.TxtDesc.IsVisible = !string.IsNullOrWhiteSpace(text);
        });
    }

    public ColorPickerButton()
    {
        InitializeComponent();
        TxtHex.TextChanged += TxtHex_TextChanged;
        UpdateVisuals(SelectedColorHex);
    }

    private void UpdateVisuals(string hex)
    {
        if (_isInternalChange) return;

        try
        {
            _isInternalChange = true;
            TxtHex.Text = hex;
            if (Color.TryParse(hex, out var color))
            {
                // El color va al relleno interior, no al borde: el Background del borde sigue enlazado al
                // tema en su chrome y un cambio de tema revertiría el muestrario (ver ColorPickerButton.axaml).
                SwatchFill.Background = new SolidColorBrush(color);
            }
        }
        catch
        {
            // Invalid hex string
        }
        finally
        {
            _isInternalChange = false;
        }
    }

    private void TxtHex_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isInternalChange) return;

        string text = TxtHex.Text?.Trim() ?? string.Empty;
        if (!text.StartsWith('#') && (text.Length == 6 || text.Length == 8))
        {
            text = "#" + text;
        }

        if (Color.TryParse(text, out var color))
        {
            _isInternalChange = true;
            SwatchFill.Background = new SolidColorBrush(color);
            SelectedColorHex = text;
            _isInternalChange = false;
        }
    }

    private void ColorSwatch_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            SelectedColorHex = hex;
            UpdateVisuals(hex);
        }
    }
}
