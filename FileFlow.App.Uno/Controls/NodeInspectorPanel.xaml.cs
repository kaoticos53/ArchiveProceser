using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El inspector de nodos del host Uno, montado sobre el <see cref="NodeInspectorViewModel"/> del
/// núcleo portable (el mismo que el escritorio): la ficha del nodo seleccionado con su descripción,
/// los parámetros con los MISMOS editores que el escritorio decide por los flags del
/// <see cref="NodeParameterViewModel"/> (toggle, slider, número, desplegable, ruta con explorar,
/// multilínea, texto) y el bloque de telemetría del nodo con su reinicio.
///
/// <para><b>Por qué por código y no XAML</b>: el escritorio decide los editores con un Selector de
/// estilos de Avalonia sobre los flags del VM; WinUI no tiene un equivalente directo (los DataTemplate
/// de WinUI no seleccionan por propiedad del ítem), así que la tabla de editores vive aquí como el
/// mismo orden de flags que el escritorio — la guardia de la rebanada compara esa tabla contra el VM.
/// El botón «Probar» del escritorio (prueba aislada con fichero) está ACTIVADO desde el hito 240:
/// ejecuta TestNodeWithCustomFileAsync del núcleo, que consume la variante asíncrona del
/// IFileDialogService — el picker se abre desde el click de UI sin bloquear el hilo de UI.</para>
/// </summary>
public sealed class NodeInspectorPanel : UserControl
{
    // ── Construcción una sola vez; el contenido se rellena por nodo inspeccionado ──
    private readonly TextBlock _titleText;
    private readonly TextBlock _emptyText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _paramsHeader;
    private readonly TextBlock _telemetryHeader;
    private readonly StackPanel _paramsHost = new() { Spacing = 4 };
    private readonly StackPanel _snapshotsHost = new() { Spacing = 6 };
    private readonly StackPanel _diffHost = new() { Spacing = 2 };
    private readonly Pivot _tabs = new();
    private PivotItem? _paramsTabItem;
    private PivotItem? _snapshotsTabItem;
    private PivotItem? _diffTabItem;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _inputsSub;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _outputsSub;
    private readonly StackPanel _telemetryRows = new() { Spacing = 2 };
    private readonly Button _resetMetricsButton;
    private readonly Button _testButton;
    private readonly FrameworkElement _body;
    private readonly Grid _root = new();

    private NodeInspectorViewModel? _vm;
    private NodeViewModel? _inspected;
    private NotifyCollectionChangedEventHandler? _paramsSub;
    private PropertyChangedEventHandler? _nodePropsSub;
    private bool _buildingTelemetry;

    public NodeInspectorPanel()
    {
        var loc = LocalizationManager.Instance;

        // Cabecera: título + cerrar (el comando del VM de la rebanada del escritorio).
        var closeButton = new Button
        {
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
            Content = loc.GetString("Uno_InspectorClose", "Cerrar")
        };
        closeButton.Click += (_, _) => _vm?.ClosePanelCommand.Execute(null);

        // El «Probar» del escritorio (hito 240): el comando canónico del núcleo abre el picker con
        // la variante asíncrona del IFileDialogService — desde el click de UI sin interbloqueo —
        // y ejecuta el nodo con el fichero elegido (estados Running/Completed/PausedOnError,
        // snapshot de entrada, diff de metadatos y diálogos de resultado viven en el núcleo).
        _testButton = new Button
        {
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
            Content = loc.GetString("Uno_InspectorTest", "Probar")
        };
        AutomationProperties.SetAutomationId(_testButton, "InspectorTestButton");
        _testButton.Click += (_, _) => _vm?.TestNodeWithCustomFileCommand.Execute(null);

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleText = new TextBlock
        {
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("CanvasTextBrush")
        };
        Grid.SetColumn(_titleText, 0);
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(_titleText);
        header.Children.Add(_testButton);
        header.Children.Add(closeButton);

        _emptyText = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasSecondaryBrush")
        };

        _descriptionText = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasSecondaryBrush")
        };

        _paramsHeader = new TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 2),
            Foreground = Brush("CanvasTextBrush")
        };

        _telemetryHeader = new TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 4),
            Foreground = Brush("CanvasTextBrush")
        };

        _resetMetricsButton = new Button
        {
            FontSize = 11,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _resetMetricsButton.Click += (_, _) =>
        {
            if (_inspected is not null)
            {
                // La misma orden que el botón del escritorio: vaciar las métricas del nodo.
                _inspected.UpdateTelemetryStats(FileFlow.Sdk.Telemetry.NodeTelemetryStats.Empty(_inspected.Id));
            }
        };

        // Las tres pestañas del escritorio (hito 242): Parámetros, Snapshots (los snapshots del
        // nodo con su vista) y Diff (el diff de metadatos que el VM del núcleo computa al
        // seleccionar un snapshot). Los AIDs dan anclas a la observación UIA externa.
        var paramsGrid = new Grid { RowSpacing = 0 };
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_descriptionText, 0);
        Grid.SetRow(_paramsHeader, 1);
        var paramsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _paramsHost };
        Grid.SetRow(paramsScroll, 2);
        paramsGrid.Children.Add(_descriptionText);
        paramsGrid.Children.Add(_paramsHeader);
        paramsGrid.Children.Add(paramsScroll);

        var paramsTab = new PivotItem
        {
            Header = loc.GetString("Uno_InspectorTabParams", "Parámetros"),
            Content = paramsGrid
        };
        AutomationProperties.SetAutomationId(paramsTab, "InspectorTabParams");

        var snapshotsTab = new PivotItem
        {
            Header = loc.GetString("Uno_InspectorTabSnapshots", "Snapshots"),
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _snapshotsHost }
        };
        AutomationProperties.SetAutomationId(snapshotsTab, "InspectorTabSnapshots");

        var diffTab = new PivotItem
        {
            Header = loc.GetString("Uno_InspectorTabDiff", "Diff"),
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _diffHost }
        };
        AutomationProperties.SetAutomationId(diffTab, "InspectorTabDiff");

        _paramsTabItem = paramsTab;
        _snapshotsTabItem = snapshotsTab;
        _diffTabItem = diffTab;
        _tabs.Items.Add(paramsTab);
        _tabs.Items.Add(snapshotsTab);
        _tabs.Items.Add(diffTab);
        _tabs.SelectionChanged += (_, _) => RebuildDiff();

        _body = new Border
        {
            Child = _tabs
        };

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(_emptyText, 1);
        Grid.SetRow(_body, 2);
        _root.Children.Add(header);
        _root.Children.Add(_emptyText);
        _root.Children.Add(_body);

        Content = new Border
        {
            Background = Brush("CanvasCardBrush"),
            BorderBrush = Brush("CanvasBorderBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _root
        };
        Padding = new Thickness(12, 10, 10, 10);

        ApplyLocalization();
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>El VM del núcleo que la vista consume (selección, apertura, cierre).</summary>
    public NodeInspectorViewModel? Vm
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
            {
                return;
            }

            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
            }

            _vm = value;
            if (_vm is not null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                // El diff vive en el VM (la lógica es del núcleo): la pestaña lo sigue en vivo.
                _vm.MetadataDiffs.CollectionChanged += (_, _) => RebuildDiff();
            }

            RefreshNode();
        }
    }

    /// <summary>Claves de localización propias del inspector (el título admite override del host).</summary>
    public void ApplyLocalization(string? titleKey = null)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = LocalizationManager.Instance;
        _paramsHeader.Text = loc.GetString("Uno_InspectorParams", "Parámetros");
        _telemetryHeader.Text = loc.GetString("Uno_InspectorTelemetry", "Telemetría");
        _resetMetricsButton.Content = loc.GetString("Uno_InspectorResetMetrics", "Vaciar métricas");
        _testButton.Content = loc.GetString("Uno_InspectorTest", "Probar");
        if (_paramsTabItem is not null)
        {
            _paramsTabItem.Header = loc.GetString("Uno_InspectorTabParams", "Parámetros");
        }

        if (_snapshotsTabItem is not null)
        {
            _snapshotsTabItem.Header = loc.GetString("Uno_InspectorTabSnapshots", "Snapshots");
        }

        if (_diffTabItem is not null)
        {
            _diffTabItem.Header = loc.GetString("Uno_InspectorTabDiff", "Diff");
        }

        RefreshHeaderTexts();
    }

    private void OnLanguageChanged(object? sender, CultureInfo e) => ApplyLocalization();

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeInspectorViewModel.IsOpen) or nameof(NodeInspectorViewModel.SelectedSnapshot))
        {
            UpdateVisibility();
        }
        else if (e.PropertyName is nameof(NodeInspectorViewModel.InspectedNode))
        {
            RefreshNode();
        }
    }

    private void RefreshNode()
    {
        // El nodo anterior deja de notificar: el panel sólo vive del nodo inspeccionado.
        if (_inspected is not null)
        {
            if (_paramsSub is not null)
            {
                _inspected.Parameters.CollectionChanged -= _paramsSub;
            }

            _inspected.PropertyChanged -= _nodePropsSub;
        }

        _inspected = _vm?.InspectedNode;

        if (_inspected is null)
        {
            _paramsHost.Children.Clear();
            _telemetryRows.Children.Clear();
            _snapshotsHost.Children.Clear();
            _diffHost.Children.Clear();
            RefreshHeaderTexts();
            UpdateVisibility();
            return;
        }

        _paramsSub = (_, _) => RebuildParameters();
        _inspected.Parameters.CollectionChanged += _paramsSub;

        // Las colecciones de snapshots del nodo (hito 242): las dos pestañas nuevas viven de ellas.
        if (_inputsSub is not null)
        {
            _inspected.InputSnapshots.CollectionChanged -= _inputsSub;
        }

        if (_outputsSub is not null)
        {
            _inspected.OutputSnapshots.CollectionChanged -= _outputsSub;
        }

        _inputsSub = (_, _) => RebuildSnapshots();
        _outputsSub = (_, _) => RebuildSnapshots();
        _inspected.InputSnapshots.CollectionChanged += _inputsSub;
        _inspected.OutputSnapshots.CollectionChanged += _outputsSub;

        _nodePropsSub = (_, e) =>
        {
            if (e.PropertyName is nameof(NodeViewModel.CurrentStats) or nameof(NodeViewModel.ExecutionStatus))
            {
                RebuildTelemetry();
            }
        };
        _inspected.PropertyChanged += _nodePropsSub;

        RebuildParameters();
        RebuildSnapshots();
        RebuildTelemetry();
        RefreshHeaderTexts();
        UpdateVisibility();
    }

    /// <summary>
    /// La pestaña de snapshots (hito 242): los snapshots del NODO (entradas y salidas), con la
    /// cabecera del escritorio (puerto, timestamp, ruta actual), el contenido desplegable (ruta
    /// original, tamaño, metadatos, tags, error) y el botón «Ver» por el comando canónico del VM
    /// (<c>PreviewSpecificSnapshotCommand</c> — la misma vista previa del escritorio).
    /// </summary>
    private void RebuildSnapshots()
    {
        _snapshotsHost.Children.Clear();
        if (_inspected is null)
        {
            return;
        }

        var loc = LocalizationManager.Instance;
        foreach (var snapshot in _inspected.InputSnapshots.Concat(_inspected.OutputSnapshots))
        {
            _snapshotsHost.Children.Add(BuildSnapshotCard(snapshot, loc));
        }
    }

    private FrameworkElement BuildSnapshotCard(NodeDataSnapshot snapshot, LocalizationManager loc)
    {
        var root = new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 2) };

        var header = new Grid { ColumnSpacing = 6 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerLines = new StackPanel { Spacing = 1 };
        var line1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        line1.Children.Add(new TextBlock
        {
            Text = (snapshot.IsInput ? "▼ In: " : "▲ Out: ") + snapshot.PortName,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(snapshot.IsInput ? "CanvasAccentGlowBrush" : "CanvasWireBrush")
        });
        line1.Children.Add(new TextBlock
        {
            Text = " • " + snapshot.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture),
            FontSize = 10,
            Opacity = 0.7,
            Foreground = Brush("CanvasSecondaryBrush")
        });
        headerLines.Children.Add(line1);
        headerLines.Children.Add(new TextBlock
        {
            Text = snapshot.ItemSnapshot.CurrentPath,
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("CanvasTextBrush")
        });
        Grid.SetColumn(headerLines, 0);
        header.Children.Add(headerLines);

        // El «Ver» del escritorio: el comando canónico del VM (la vista previa vive en el núcleo).
        var viewButton = new Button
        {
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 10,
            Content = loc.GetString("Preview_InspectFileBtn", "Ver")
        };
        AutomationProperties.SetAutomationId(viewButton, "SnapshotViewButton_" + snapshot.SnapshotId);
        viewButton.Click += (_, _) => _vm?.PreviewSpecificSnapshotCommand.Execute(snapshot);
        Grid.SetColumn(viewButton, 1);
        header.Children.Add(viewButton);

        root.Children.Add(header);

        // El contenido desplegable: la misma información que el Expander del escritorio.
        var details = new StackPanel { Spacing = 3, Margin = new Thickness(12, 2, 0, 0) };
        details.Children.Add(new TextBlock
        {
            Text = loc.GetString("Inspector_OriginalPath", "Original Path:") + " " + snapshot.ItemSnapshot.OriginalPath,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasSecondaryBrush")
        });
        details.Children.Add(new TextBlock
        {
            Text = loc.GetString("Inspector_SizeLabel", "Size:") + " " + snapshot.ItemSnapshot.FileSizeBytes + " "
                + loc.GetString("Inspector_BytesLabel", "bytes"),
            FontSize = 10,
            Foreground = Brush("CanvasSecondaryBrush")
        });

        foreach (var kv in snapshot.ItemSnapshot.Metadata)
        {
            details.Children.Add(new TextBlock
            {
                Text = kv.Key + " = " + kv.Value,
                FontSize = 10,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
                Foreground = Brush("CanvasTextBrush")
            });
        }

        if (snapshot.ItemSnapshot.Tags.Count > 0)
        {
            details.Children.Add(new TextBlock
            {
                Text = loc.GetString("Inspector_Tags", "Tags:") + " " + string.Join(", ", snapshot.ItemSnapshot.Tags),
                FontSize = 10,
                Foreground = Brush("CanvasSecondaryBrush")
            });
        }

        if (snapshot.HasError)
        {
            details.Children.Add(new TextBlock
            {
                Text = "⚠ " + snapshot.ErrorMessage,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11))
            });
        }

        var expander = new Expander
        {
            Content = details,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        expander.Header = header;
        root.Children.Add(expander);

        return root;
    }

    /// <summary>
    /// La pestaña de diff (hito 242): las filas de <c>MetadataDiffs</c> que el VM del núcleo
    /// computa (al inspeccionar y al seleccionar un snapshot) — Added/Removed/Modified con los
    /// colores del escritorio.
    /// </summary>
    private void RebuildDiff()
    {
        _diffHost.Children.Clear();
        if (_vm is null)
        {
            return;
        }

        foreach (var diff in _vm.MetadataDiffs)
        {
            _diffHost.Children.Add(BuildDiffRow(diff));
        }
    }

    private static FrameworkElement BuildDiffRow(MetadataDiffItem diff)
    {
        var changeColor = diff.ChangeType switch
        {
            "Added" => Windows.UI.Color.FromArgb(255, 16, 185, 129),
            "Removed" => Windows.UI.Color.FromArgb(255, 239, 68, 68),
            _ => Windows.UI.Color.FromArgb(255, 245, 158, 11)
        };

        var key = new TextBlock
        {
            Text = diff.Key,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(changeColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        var values = new TextBlock
        {
            Text = diff.ChangeType == "Added" ? "→ " + diff.NewValue
                : diff.ChangeType == "Removed" ? diff.OldValue + " →"
                : diff.OldValue + " → " + diff.NewValue,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasTextBrush")
        };

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(key, 0);
        Grid.SetColumn(values, 1);
        grid.Children.Add(key);
        grid.Children.Add(values);
        return new StackPanel { Children = { grid } };
    }

    private void RefreshHeaderTexts()
    {
        var loc = LocalizationManager.Instance;
        _titleText.Text = _inspected?.Title ?? loc.GetString("Uno_InspectorTitle", "Inspector");
        _emptyText.Text = loc.GetString("Uno_InspectorEmpty", "Selecciona un nodo para inspeccionarlo.");
        _descriptionText.Text = _inspected?.Description ?? string.Empty;
        _descriptionText.Visibility = string.IsNullOrEmpty(_descriptionText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void UpdateVisibility()
    {
        bool isOpen = _vm?.IsOpen == true;
        Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        _emptyText.Visibility = isOpen && _inspected is null ? Visibility.Visible : Visibility.Collapsed;
        _body.Visibility = isOpen && _inspected is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── La tabla de editores: los mismos flags del VM que el escritorio usa en su Selector ──

    private void RebuildParameters()
    {
        _paramsHost.Children.Clear();
        if (_inspected is null)
        {
            return;
        }

        foreach (var parameter in _inspected.Parameters)
        {
            var row = BuildParameterRow(parameter);
            if (row is not null)
            {
                _paramsHost.Children.Add(row);
            }
        }

        // El sondeo (y cualquier lector) cuenta editores con la cuenta de parámetros del nodo: la
        // fila SIEMPRE se construye (encabezado + editor), sin excepciones ocultas.
    }

    private UIElement? BuildParameterRow(NodeParameterViewModel p)
    {
        var root = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 2) };

        var name = new TextBlock
        {
            Text = p.DisplayName,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasTextBrush")
        };
        root.Children.Add(name);

        FrameworkElement editor;
        if (p.IsToggle)
        {
            var toggle = new ToggleSwitch
            {
                OnContent = null,
                OffContent = null,
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            toggle.SetBinding(ToggleSwitch.IsOnProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.ValueAsBool)),
                Source = p,
                Mode = BindingMode.TwoWay
            });
            editor = toggle;
        }
        else if (p.IsSlider)
        {
            var slider = new Slider { Minimum = p.SliderMin, Maximum = p.SliderMax, StepFrequency = Math.Max(p.SliderStep, 0.01) };
            slider.SetBinding(Slider.ValueProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.SliderValue)),
                Source = p,
                Mode = BindingMode.TwoWay
            });
            var display = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush("CanvasSecondaryBrush")
            };
            display.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.SliderDisplayValue)),
                Source = p,
                Mode = BindingMode.OneWay
            });
            var stack = new StackPanel { Spacing = 0 };
            stack.Children.Add(slider);
            stack.Children.Add(display);
            editor = stack;
        }
        else if (p.IsDropdown)
        {
            var combo = new ComboBox
            {
                ItemsSource = p.Options,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 12
            };
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.Value)),
                Source = p,
                Mode = BindingMode.TwoWay
            });
            editor = combo;
        }
        else if (p.HasBrowseButton)
        {
            var box = new TextBox { FontSize = 12 };
            box.TextChanged += (_, _) =>
            {
                if (box.Text != (p.Value?.ToString() ?? string.Empty))
                {
                    p.Value = box.Text;
                }
            };
            var browse = new Button
            {
                Content = "…",
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 12
            };
            browse.Click += (_, _) => p.BrowsePathCommand.Execute(null);
            var grid = new Grid { ColumnSpacing = 4 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(box, 0);
            Grid.SetColumn(browse, 1);
            grid.Children.Add(box);
            grid.Children.Add(browse);
            editor = grid;
        }
        else if (p.IsMultiLine)
        {
            var box = new TextBox
            {
                AcceptsReturn = true,
                Height = 72,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            box.TextChanged += (_, _) =>
            {
                if (box.Text != (p.Value?.ToString() ?? string.Empty))
                {
                    p.Value = box.Text;
                }
            };
            editor = box;
        }
        else
        {
            // Texto estándar y número: una caja (el número deja la validación al nodo, igual que la ficha).
            var box = new TextBox { FontSize = 12 };
            box.TextChanged += (_, _) =>
            {
                if (box.Text != (p.Value?.ToString() ?? string.Empty))
                {
                    p.Value = box.Text;
                }
            };
            editor = box;
        }

        root.Children.Add(editor);

        // El valor evaluado con su copia (la fila que el escritorio pinta bajo el campo).
        if (p.HasExpression)
        {
            var evaluated = new TextBlock
            {
                FontSize = 10,
                Opacity = 0.8,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brush("CanvasSuccessBrush")
            };
            evaluated.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.EvaluatedValue)),
                Source = p,
                Mode = BindingMode.OneWay
            });

            var copyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var copyButton = new Button
            {
                FontSize = 10,
                Padding = new Thickness(6, 0, 6, 0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = "⧉"
            };
            copyButton.Click += (_, _) => p.CopyEvaluatedValueCommand.Execute(null);
            copyRow.Children.Add(copyButton);
            copyRow.Children.Add(evaluated);
            root.Children.Add(copyRow);
        }

        return root;
    }

    // ── Telemetría: el bloque que el escritorio muestra en su pestaña de telemetría ──

    private void RebuildTelemetry()
    {
        if (_buildingTelemetry)
        {
            return;
        }

        _buildingTelemetry = true;
        try
        {
            _telemetryRows.Children.Clear();
            if (_inspected is null)
            {
                return;
            }

            var loc = LocalizationManager.Instance;
            var stats = _inspected.CurrentStats;
            var culture = CultureInfo.CurrentCulture;

            _telemetryRows.Children.Add(TelemetryRow(
                loc.GetString("Uno_InspectorStatus", "Estado"),
                _inspected.ExecutionStatusText));
            _telemetryRows.Children.Add(TelemetryRow(
                loc.GetString("Uno_InspectorProcessed", "Procesados"),
                stats.ProcessedCount.ToString(culture)));
            _telemetryRows.Children.Add(TelemetryRow(
                loc.GetString("Uno_InspectorAvgLatency", "Latencia media"),
                string.Format(culture, "{0:F1} ms", stats.AverageTimeMs)));
            _telemetryRows.Children.Add(TelemetryRow(
                loc.GetString("Uno_InspectorTotalTime", "Tiempo total"),
                string.Format(culture, "{0:F1} ms", stats.TotalTimeMs)));
            _telemetryRows.Children.Add(TelemetryRow(
                loc.GetString("Uno_InspectorPeakRam", "Pico de memoria"),
                FormatBytes(stats.PeakAllocatedBytes, culture)));
        }
        finally
        {
            _buildingTelemetry = false;
        }
    }

    private static StackPanel TelemetryRow(string label, string value)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Opacity = 0.7,
            Foreground = Brush("CanvasSecondaryBrush")
        };
        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = Brush("CanvasTextBrush")
        };
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(valueBlock, 1);
        valueBlock.HorizontalAlignment = HorizontalAlignment.Right;
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        return new StackPanel { Children = { grid } };
    }

    private static string FormatBytes(long bytes, CultureInfo culture)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        const long kb = 1024;
        const long mb = kb * 1024;
        return bytes >= mb
            ? string.Format(culture, "{0:F1} MB", bytes / (double)mb)
            : bytes >= kb
                ? string.Format(culture, "{0:F1} KB", bytes / (double)kb)
                : string.Format(culture, "{0} B", bytes);
    }

    /// <summary>El pincel de un token Canvas* resuelto de los recursos de la app (el patrón del lienzo).</summary>
    private static Brush Brush(string key)
    {
        return (Brush)Application.Current.Resources[key];
    }

    // ── Superficie interna para el sondeo en runtime (--selfcheck) ──

    /// <summary>La pila donde viven las filas de telemetría (cada una con su etiqueta y valor).</summary>
    internal int TelemetryRowCount => _telemetryRows.Children.Count;

    /// <summary>Los editores de parámetros materializados (uno por parámetro con editor).</summary>
    internal int ParameterEditorCount => _paramsHost.Children.Count;

    /// <summary>Abre el panel sobre un nodo, como haría la selección del lienzo (mismo método del VM).</summary>
    internal void InspectForProbe(NodeViewModel node)
    {
        _vm?.InspectNode(node, autoOpen: true);
    }

    /// <summary>
    /// La sonda del «Probar» (hito 240): el botón existe en la cabecera, canta su AutomationId para
    /// la observación UIA externa y está atado al comando canónico del núcleo (la variante async
    /// del diálogo vive en el VM; el host no abre pickers por su cuenta).
    /// </summary>
    internal bool HasWiredTestButton()
    {
        return _testButton is not null
            && AutomationProperties.GetAutomationId(_testButton) == "InspectorTestButton"
            && _vm?.TestNodeWithCustomFileCommand is not null;
    }

    /// <summary>
    /// La sonda de las pestañas nuevas (hito 242): tarjetas de snapshots materializadas desde las
    /// colecciones del nodo, filas de diff pintadas desde el VM (el VM computa al seleccionar un
    /// snapshot), y la conmutación del Pivot dejando las tarjetas en el árbol.
    /// </summary>
    internal (int SnapshotCards, int DiffRows, bool TabSwitch) ProbeSnapshotTabs()
    {
        if (_vm?.InspectedNode is null || _snapshotsTabItem is null)
        {
            return (0, 0, false);
        }

        int cards = _snapshotsHost.Children.Count;
        int diffRows = _diffHost.Children.Count;

        int previousIndex = _tabs.SelectedIndex;
        _tabs.SelectedIndex = _tabs.Items.IndexOf(_snapshotsTabItem);
        bool switchOk = _tabs.SelectedIndex == _tabs.Items.IndexOf(_snapshotsTabItem)
            && _snapshotsHost.Children.Count == cards;
        _tabs.SelectedIndex = previousIndex;

        return (cards, diffRows, switchOk);
    }

    /// <summary>Cierra el panel por el comando del VM (el botón de la cabecera).</summary>
    internal bool CloseViaCommand()
    {
        if (_vm is null)
        {
            return false;
        }

        _vm.ClosePanelCommand.Execute(null);
        return !_vm.IsOpen;
    }
}
