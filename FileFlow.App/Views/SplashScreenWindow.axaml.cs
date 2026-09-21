using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views;

public partial class SplashScreenWindow : Window
{
    /// <summary>Duración de cada paso del barrido de acento de la barra (ms).</summary>
    private const int ShimmerStepMilliseconds = 40;

    /// <summary>Fracción de la barra que cubre el barrido (0..1).</summary>
    private const double ShimmerSpan = 0.35;

    private readonly DispatcherTimer? _shimmerTimer;
    private bool _shimmerEnabled;

    public SplashScreenWindow()
    {
        InitializeComponent();
        TxtVersion.Text = $"v{AppVersionInfo.DisplayVersion}";

        // Barra de acento: un barrido de gradiente recorre la barra mientras el progreso avanza. Va en
        // código y no en estilos porque la sesión headless purga las animaciones declaradas (sin animador
        // público para RenderTransform en Avalonia 12, y una captura con animaciones en vuelo no sería
        // determinista). El temporizador no arranca aquí: lo activa la aplicación real (StartShimmer),
        // de modo que las capturas de la splash son siempre el primer fotograma quieto.
        if (TryCreateShimmerBrush(out var shimmerBrush, out var highlightStop))
        {
            PbProgress.Foreground = shimmerBrush;
            _shimmerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ShimmerStepMilliseconds)
            };
            _shimmerTimer.Tick += (_, _) => AdvanceShimmer(highlightStop);
        }
    }

    public void UpdateStatus(string message, double progress)
    {
        TxtStatus.Text = message;
        PbProgress.Value = Math.Clamp(progress, 0, 100);
        TxtPercentage.Text = $"{(int)PbProgress.Value}%";
    }

    public void SetNodeCount(int count)
    {
        // La cifra va formateada en la plantilla localizada: el orden de las palabras cambia por idioma.
        TxtNodesBadge.Text = LocalizationManager.Instance.GetFormattedString(
            "Splash_NodesBadge", $"{count} DAG nodes", count);
    }

    /// <summary>
    /// Activa el barrido de la barra. Es un método explícito y no el constructor para que las pruebas y
    /// las capturas vean siempre el mismo estado inicial (barra en 0 %, gradiente quieto).
    /// </summary>
    public void StartShimmer()
    {
        if (_shimmerTimer is null)
        {
            return;
        }

        _shimmerEnabled = true;
        _shimmerTimer.Start();
    }

    public async Task CloseWithFadeAsync()
    {
        // El barrido sólo vive mientras la splash está en pantalla: al despedirla se detiene y la barra
        // queda en su estado final (progreso completo, sin gradiente en movimiento).
        _shimmerTimer?.Stop();
        _shimmerEnabled = false;

        for (double opacity = 1.0; opacity > 0.05; opacity -= 0.15)
        {
            Opacity = opacity;
            await Task.Delay(16);
        }
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _shimmerTimer?.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// Avanza una posición el barrido de acento. El gradiente se recalcula en la misma instancia de
    /// <see cref="LinearGradientBrush"/> (con los offset de sus tres paradas), de modo que el pincel no
    /// se recrea por paso y no hay(new) por fotograma.
    /// </summary>
    private void AdvanceShimmer(GradientStop highlightStop)
    {
        if (!_shimmerEnabled)
        {
            return;
        }

        // La cabeza del barrido da una vuelta completa (offset 0 → 1 → 0) de forma cíclica y continua.
        double head = (DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerMillisecond / 1000.0) % 1.0;

        highlightStop.Offset = head;
        var stops = ((LinearGradientBrush)PbProgress.Foreground!).GradientStops;
        stops[0].Offset = Math.Max(0.0, head - ShimmerSpan);
        stops[2].Offset = Math.Min(1.0, head + ShimmerSpan);
    }

    /// <summary>
    /// Construye el pincel del barrido: acento sólido con una ventana más clara que lo recorre. Usa los
    /// tokens del tema (AccentPrimary / AccentGlow); si el tema aún no está publicado (el caso más
    /// temprano del arranque) devuelve false y la barra queda con el pincel declarado en el XAML.
    /// </summary>
    private bool TryCreateShimmerBrush(out LinearGradientBrush brush, out GradientStop highlightStop)
    {
        brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
        };

        if (TryResolveThemeBrush("AccentPrimaryBrush", out var baseAccent) &&
            TryResolveThemeBrush("AccentGlowBrush", out var glow))
        {
            var left = new GradientStop(baseAccent.Color, 0.0);
            highlightStop = new GradientStop(glow.Color, 0.5);
            var right = new GradientStop(baseAccent.Color, 1.0);
            brush.GradientStops.Add(left);
            brush.GradientStops.Add(highlightStop);
            brush.GradientStops.Add(right);
            return true;
        }

        highlightStop = new GradientStop(Colors.Transparent, 0.5);
        return false;
    }

    private static bool TryResolveThemeBrush(string resourceKey, out ISolidColorBrush brush)
    {
        if (Application.Current is { } application &&
            application.TryFindResource(resourceKey, out var value) &&
            value is ISolidColorBrush solid)
        {
            brush = solid;
            return true;
        }

        brush = null!;
        return false;
    }
}
