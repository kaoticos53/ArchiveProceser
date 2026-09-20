using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views;

/// <summary>
/// Ventana de error de arranque: la que convierte «el proceso desaparece sin abrir nada» en un motivo legible
/// con una acción concreta.
///
/// <para><b>Construida en código, a propósito.</b> Es la única vista de la aplicación sin XAML, sin
/// <c>DynamicResource</c>, sin contenedor de servicios y sin recursos del tema. El motivo es directo: esta
/// ventana aparece precisamente cuando lo que ha fallado puede ser el tema, los recursos de texto o la carga
/// del XAML. Si dependiera de cualquiera de ellos, fallaría exactamente en el caso que debe reportar.</para>
///
/// <para>Muestra la etapa fallida, la excepción, la ruta del registro de incidentes y el detalle técnico
/// completo (seleccionable y copiable), con tres acciones: copiar el informe, abrir la carpeta del registro y
/// cerrar la aplicación.</para>
/// </summary>
public sealed class StartupErrorWindow : Window
{
    private const int WindowWidth = 720;
    private const int WindowHeight = 430;

    // Paleta propia y explícita: no se resuelve ningún recurso del tema (puede ser justo lo que falló).
    private static readonly IBrush BackdropBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x1A));
    private static readonly IBrush CardBrush = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x2A));
    private static readonly IBrush OutlineBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44));
    private static readonly IBrush PrimaryTextBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF6));
    private static readonly IBrush SecondaryTextBrush = new SolidColorBrush(Color.FromRgb(0xB4, 0xB4, 0xC0));
    private static readonly IBrush AlertBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xB0, 0x2E));
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly IBrush ButtonTextBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF6));

    /// <summary>Última ventana de error mostrada (la usa el arranque para gobernar el cierre).</summary>
    public static StartupErrorWindow? Current { get; private set; }

    /// <summary>Informe que muestra la ventana.</summary>
    public StartupFailureReport Report { get; }

    /// <summary>Título visible. Expuesto para las pruebas de contrato visual.</summary>
    public TextBlock HeadlineText { get; }

    /// <summary>Línea con la etapa fallida.</summary>
    public TextBlock PhaseText { get; }

    /// <summary>Resumen de la excepción (tipo + mensaje).</summary>
    public TextBlock ExceptionText { get; }

    /// <summary>Ruta del registro de incidentes.</summary>
    public TextBlock LogPathText { get; }

    /// <summary>Detalle técnico completo, seleccionable.</summary>
    public TextBox DetailsBox { get; }

    /// <summary>Botón de copiar el informe.</summary>
    public Button CopyButton { get; }

    /// <summary>Botón de abrir la carpeta del registro.</summary>
    public Button OpenLogButton { get; }

    /// <summary>Botón de cerrar la aplicación.</summary>
    public Button ExitButton { get; }

    /// <summary>Mensaje de estado de la copia (vacío hasta que se pulsa copiar).</summary>
    public TextBlock StatusText { get; }

    private StartupErrorWindow(StartupFailureReport report)
    {
        Report = report;

        Title = report.Headline;
        Width = WindowWidth;
        Height = WindowHeight;
        MinWidth = 420;
        MinHeight = 300;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BackdropBrush;

        HeadlineText = new TextBlock
        {
            Text = report.Headline,
            FontSize = 19,
            FontWeight = FontWeight.Bold,
            Foreground = PrimaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        };

        PhaseText = new TextBlock
        {
            Text = report.PhaseLine,
            FontSize = 13,
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };

        ExceptionText = new TextBlock
        {
            Text = report.ExceptionSummary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = AlertBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };

        LogPathText = new TextBlock
        {
            Text = report.LogFilePath ?? Localize("Startup_DetailsLogUnavailable", "(no disponible)"),
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };

        DetailsBox = new TextBox
        {
            Text = report.BuildDetails(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas, Menlo, DejaVu Sans Mono, monospace"),
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            Background = BackdropBrush,
            BorderBrush = OutlineBrush,
            MinHeight = 120,
            Margin = new Thickness(0, 10, 0, 0)
        };

        StatusText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        CopyButton = CreateButton(Localize("Startup_FailureCopy", "Copiar detalles"), isPrimary: true);
        CopyButton.Click += (_, _) => CopyDetails();

        OpenLogButton = CreateButton(Localize("Startup_FailureOpenLog", "Abrir carpeta del registro"), isPrimary: false);
        OpenLogButton.Click += (_, _) => OpenLogFolder();

        ExitButton = CreateButton(Localize("Startup_FailureExit", "Cerrar la aplicación"), isPrimary: false);
        ExitButton.Click += (_, _) => Close();

        Content = BuildLayout();
    }

    /// <summary>Construye la ventana a partir de un informe. Debe llamarse en el hilo de UI.</summary>
    public static StartupErrorWindow Create(StartupFailureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Avalonia.Threading.Dispatcher.UIThread.VerifyAccess();
        return new StartupErrorWindow(report);
    }

    /// <summary>
    /// Muestra la ventana de error, desde cualquier hilo. No puede lanzar: se ejecuta en el último recurso del
    /// arranque y un fallo aquí no debe sustituir el error original por otro.
    /// </summary>
    public static void ShowFailure(StartupFailureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ShowCore(report);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ShowCore(report));
            }
        }
        catch
        {
            // Sin bucle de UI disponible no hay nada que mostrar: el registro ya tiene el informe.
        }
    }

    private static void ShowCore(StartupFailureReport report)
    {
        try
        {
            var window = Create(report);
            Current = window;
            window.Show();
        }
        catch
        {
            // Ver ShowFailure.
        }
    }

    private void CopyDetails()
    {
        try
        {
            var clipboard = Clipboard;
            if (clipboard is null)
            {
                StatusText.Text = Localize("Startup_FailureCopyFailed", "No se pudo copiar al portapapeles.");
                return;
            }

            // Sin esperar: el manejador de un botón no puede bloquear el hilo de UI esperando al portapapeles.
            _ = CopyAndReportAsync(clipboard);
        }
        catch
        {
            StatusText.Text = Localize("Startup_FailureCopyFailed", "No se pudo copiar al portapapeles.");
        }
    }

    private async Task CopyAndReportAsync(IClipboard clipboard)
    {
        try
        {
            await clipboard.SetTextAsync(Report.BuildDetails());
            StatusText.Text = Localize("Startup_FailureCopied", "Detalles copiados al portapapeles.");
        }
        catch
        {
            StatusText.Text = Localize("Startup_FailureCopyFailed", "No se pudo copiar al portapapeles.");
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(Report.LogFilePath);
            if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
            {
                StatusText.Text = Localize("Startup_FailureLogNotFound", "No se encontró la carpeta del registro.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch
        {
            // Abrir el explorador es una comodidad: si el sistema no lo permite, el informe sigue visible.
        }
    }

    private Control BuildLayout()
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        // Se cualifica el tipo porque 'Path' es ambiguo con System.IO.Path (usings implícitos).
        header.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M12,2 L23,21 L1,21 Z M11,9 L13,9 L13,15 L11,15 Z M11,17 L13,17 L13,19 L11,19 Z"),
            Fill = AlertBrush,
            Width = 26,
            Height = 26,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top
        });

        var headerText = new StackPanel();
        headerText.Children.Add(HeadlineText);
        headerText.Children.Add(PhaseText);
        header.Children.Add(headerText);

        var hint = new TextBlock
        {
            Text = Localize(
                "Startup_FailureHint",
                "El detalle completo queda en el registro de incidentes. Puedes copiarlo para informar del problema."),
            FontSize = 12,
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };

        actions.Children.Add(CopyButton);
        actions.Children.Add(OpenLogButton);
        actions.Children.Add(ExitButton);
        actions.Children.Add(StatusText);

        var card = new StackPanel
        {
            Margin = new Thickness(18)
        };

        card.Children.Add(header);
        card.Children.Add(ExceptionText);
        card.Children.Add(LogPathText);
        card.Children.Add(hint);
        card.Children.Add(DetailsBox);
        card.Children.Add(actions);

        return new Border
        {
            Background = CardBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(10),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = card
            }
        };
    }

    private static Button CreateButton(string text, bool isPrimary) => new()
    {
        Content = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = ButtonTextBrush
        },
        Background = isPrimary ? AccentBrush : CardBrush,
        BorderBrush = OutlineBrush,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(14, 8),
        CornerRadius = new CornerRadius(6)
    };

    private static string Localize(string key, string fallback)
    {
        try
        {
            string localized = LocalizationManager.Instance.GetString(key, fallback);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }
        catch
        {
            return fallback;
        }
    }
}
