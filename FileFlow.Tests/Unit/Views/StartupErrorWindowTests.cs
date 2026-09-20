using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using FileFlow.App.Services;
using FileFlow.App.Views;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Contrato de la <b>ventana de error de arranque</b>: la que hace visible un fallo que antes terminaba con el
/// proceso desapareciendo sin interfaz.
///
/// Se comprueban las dos propiedades que la hacen útil y que son fáciles de romper sin darse cuenta:
/// <list type="number">
///   <item><b>Dice qué pasó</b>: etapa fallida, excepción y ruta del registro de incidentes, tanto en la
///   superficie visible como en el detalle copiable.</item>
///   <item><b>Se pinta sin depender del tema</b>: la ventana no usa XAML ni <c>DynamicResource</c>, porque el
///   motivo por el que aparece puede ser justamente un fallo del tema o de los recursos. La captura de un
///   fotograma real es la prueba de que el caso «sin tema» sigue siendo legible.</item>
/// </list>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class StartupErrorWindowTests
{
    private const string LogPath = "/home/user/.local/share/FileFlow/logs/crash.log";

    internal static StartupFailureReport BuildReport() => new(
        StartupPhase.Services,
        StartupPhaseDescriptions.Describe(StartupPhase.Services),
        new InvalidOperationException("A circular dependency was detected for the service of type 'EditorViewModel'."),
        LogPath,
        new DateTime(2026, 9, 20, 19, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void TheErrorWindow_ShouldShowTheFailingStageTheExceptionAndTheLogPath()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            // Arrange
            var report = BuildReport();
            var window = StartupErrorWindow.Create(report);

            try
            {
                // Assert: lo que el usuario ve
                window.Title.Should().Be(report.Headline);
                window.HeadlineText.Text.Should().Be(report.Headline);
                window.PhaseText.Text.Should().Contain(report.PhaseLabel);
                window.ExceptionText.Text.Should().Contain("InvalidOperationException");
                window.ExceptionText.Text.Should().Contain("circular dependency");
                window.LogPathText.Text.Should().Contain(LogPath, "el usuario debe saber dónde quedó el registro");

                // Assert: el detalle técnico copiable incluye todo lo necesario para informar del problema
                window.DetailsBox.Text.Should().Contain(report.PhaseLabel);
                window.DetailsBox.Text.Should().Contain(LogPath);
                window.DetailsBox.Text.Should().Contain("EditorViewModel");
                window.DetailsBox.IsReadOnly.Should().BeTrue();

                // Assert: las tres salidas posibles están rotuladas
                foreach (var button in new[] { window.CopyButton, window.OpenLogButton, window.ExitButton })
                {
                    (button.Content as TextBlock)?.Text.Should().NotBeNullOrWhiteSpace();
                }
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void TheErrorWindow_ShouldRenderItself_WithoutTheApplicationTheme()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            // La ventana se construye en código y con colores explícitos: es la única vista que debe funcionar
            // cuando lo que falló son los diccionarios del tema. Un fotograma real lo demuestra.
            var window = StartupErrorWindow.Create(BuildReport());

            try
            {
                window.Show();

                var frame = window.CaptureRenderedFrame();

                frame.Should().NotBeNull("la ventana de error debe pintarse aunque el tema no esté disponible");
                frame!.PixelSize.Width.Should().BeGreaterThan(200);
                frame.PixelSize.Height.Should().BeGreaterThan(200);
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void ShowFailure_ShouldCreateAndShowTheWindow_SoTheFailureStopsBeingSilent()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            // Act
            StartupErrorWindow.ShowFailure(BuildReport());

            // Assert
            StartupErrorWindow.Current.Should().NotBeNull("un fallo de arranque debe terminar con una ventana visible");
            StartupErrorWindow.Current!.IsVisible.Should().BeTrue();

            try
            {
                StartupErrorWindow.Current.Content.Should().NotBeNull();
            }
            finally
            {
                var current = StartupErrorWindow.Current!;
                VisualSnapshot.DetachTree(current);
                current.Close();
            }
        });
    }

    [Fact]
    public void TheErrorWindow_ShouldSurviveBeingBuiltOffTheUiThreadDispatcher()
    {
        // 'ShowFailure' se llama también desde el manejador de excepciones no controladas, que puede correr en
        // cualquier hilo: no debe lanzar ni quedarse a medias.
        Action show = () => StartupErrorWindow.ShowFailure(BuildReport());

        show.Should().NotThrow();

        // El despacho al hilo de UI deja la ventana creada (la sesión headless la mantiene viva).
        AvaloniaTestHelper.RunOnUI(() =>
        {
            StartupErrorWindow.Current.Should().NotBeNull();

            var current = StartupErrorWindow.Current!;
            VisualSnapshot.DetachTree(current);
            current.Close();
        });
    }
}
