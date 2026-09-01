using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileFlow.App.Views.Components;

public partial class ColorPickerButton : UserControl
{
    private bool _isInternalChange;

    public static readonly DependencyProperty SelectedColorHexProperty =
        DependencyProperty.Register(
            nameof(SelectedColorHex),
            typeof(string),
            typeof(ColorPickerButton),
            new FrameworkPropertyMetadata("#6366F1", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorHexChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(ColorPickerButton),
            new PropertyMetadata("Color", (d, e) => ((ColorPickerButton)d).TxtLabel.Text = e.NewValue?.ToString() ?? "Color"));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description),
            typeof(string),
            typeof(ColorPickerButton),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var ctrl = (ColorPickerButton)d;
                string text = e.NewValue?.ToString() ?? string.Empty;
                ctrl.TxtDesc.Text = text;
                ctrl.TxtDesc.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
            }));

    public string SelectedColorHex
    {
        get => (string)GetValue(SelectedColorHexProperty);
        set => SetValue(SelectedColorHexProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<string>? ColorChanged;

    public ColorPickerButton()
    {
        InitializeComponent();
        UpdateVisuals(SelectedColorHex);
    }

    private static void OnSelectedColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerButton picker)
        {
            string oldVal = e.OldValue?.ToString() ?? string.Empty;
            string newVal = e.NewValue?.ToString() ?? "#FFFFFF";
            picker.UpdateVisuals(newVal);
            picker.ColorChanged?.Invoke(picker, new RoutedPropertyChangedEventArgs<string>(oldVal, newVal));
        }
    }

    private void UpdateVisuals(string hex)
    {
        if (_isInternalChange) return;

        try
        {
            _isInternalChange = true;
            TxtHex.Text = hex;
            var color = (Color)ColorConverter.ConvertFromString(hex);
            SwatchBorder.Background = new SolidColorBrush(color);
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

    private void TxtHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInternalChange) return;

        string text = TxtHex.Text.Trim();
        if (!text.StartsWith('#') && (text.Length == 6 || text.Length == 8))
        {
            text = "#" + text;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(text);
            _isInternalChange = true;
            SwatchBorder.Background = new SolidColorBrush(color);
            SelectedColorHex = text;
            _isInternalChange = false;
        }
        catch
        {
            // Invalid hex while typing, ignore
        }
    }

    private void BtnSwatch_Click(object sender, RoutedEventArgs e)
    {
        PalettePopup.IsOpen = !PalettePopup.IsOpen;
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            SelectedColorHex = hex;
            PalettePopup.IsOpen = false;
        }
    }
}
