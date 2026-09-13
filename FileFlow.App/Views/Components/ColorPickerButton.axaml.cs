using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace FileFlow.App.Views.Components;

public partial class ColorPickerButton : UserControl
{
    private bool _isInternalChange;

    public static readonly StyledProperty<string> SelectedColorHexProperty =
        AvaloniaProperty.Register<ColorPickerButton, string>(nameof(SelectedColorHex), "#6366F1");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ColorPickerButton, string>(nameof(Label), "Color");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ColorPickerButton, string>(nameof(Description), string.Empty);

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

    public ColorPickerButton()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedColorHexProperty)
        {
            string hex = change.GetNewValue<string>();
            UpdateVisuals(hex);
            ColorChanged?.Invoke(this, hex);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateVisuals(string hex)
    {
        if (_isInternalChange || string.IsNullOrWhiteSpace(hex)) return;

        try
        {
            _isInternalChange = true;
            var txtHex = this.FindControl<TextBox>("TxtHex");
            var swatchBorder = this.FindControl<Border>("SwatchBorder");
            if (txtHex != null) txtHex.Text = hex;
            if (swatchBorder != null && Color.TryParse(hex, out var color))
            {
                swatchBorder.Background = new SolidColorBrush(color);
            }
        }
        catch
        {
            // Invalid hex string while typing, keep current swatch
        }
        finally
        {
            _isInternalChange = false;
        }
    }

    private void TxtHex_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isInternalChange) return;

        var txtHex = this.FindControl<TextBox>("TxtHex");
        string text = txtHex?.Text?.Trim() ?? string.Empty;
        if (!text.StartsWith('#') && (text.Length == 6 || text.Length == 8))
        {
            text = "#" + text;
        }

        if (Color.TryParse(text, out var color))
        {
            _isInternalChange = true;
            var swatchBorder = this.FindControl<Border>("SwatchBorder");
            if (swatchBorder != null) swatchBorder.Background = new SolidColorBrush(color);
            SelectedColorHex = text;
            _isInternalChange = false;
        }
    }

    private void BtnSwatch_Click(object? sender, RoutedEventArgs e)
    {
        var popup = this.FindControl<Avalonia.Controls.Primitives.Popup>("PalettePopup");
        if (popup != null)
        {
            popup.IsOpen = !popup.IsOpen;
        }
    }

    private void ColorSwatch_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            SelectedColorHex = hex;
        }
    }
}
