using System;
using System.Collections.Generic;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Capturas de las ventanas modales de la aplicación y de los plugins — la parte de la interfaz que hoy no
/// tenía líneas base visuales ni cobertura de estilo.
///
/// Igual que <see cref="AppShellVisualRegressionTests"/> para el shell: las aserciones sobre el XAML (clases,
/// tokens, bindings) comprueban el contrato de cada pieza, pero no cómo queda el conjunto. Aquí se congelan
/// las modales píxel a píxel para que un cambio de espaciado, un color fuera de token o un panel recortado
/// se vean en el fallo, no en producción.
///
/// Las ventanas se montan con dobles de sus puertos (<see cref="ModalVisualFixture"/>): nada lee ni escribe
/// el perfil real ni el disco de modelos, y cada captura purga sus bindings antes de terminar (ver
/// <see cref="VisualSnapshot.CaptureWindow"/>), como exige el suite paralelo.
///
/// Si un cambio visual es intencionado, se regeneran las líneas base con <c>FILEFLOW_UPDATE_VISUALS=1</c> y
/// se revisan las imágenes resultantes antes de darlas por buenas.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class ModalVisualRegressionTests
{
    private const string DarkTheme = "dark_fluent";
    private const string LightTheme = "light_studio";

    /// <summary>Capturas clave: cada modal con su nombre de línea base y el tema con el que se congeló.</summary>
    private static readonly (ModalSurface Surface, string Baseline, string Theme)[] KeySurfaces =
    [
        (ModalSurface.About, "modal-about-dark", DarkTheme),
        (ModalSurface.VariablePicker, "modal-variable-picker-dark", DarkTheme),
        (ModalSurface.AiModelManager, "modal-ai-model-manager-dark", DarkTheme),
        (ModalSurface.AiModelUrls, "modal-ai-model-urls-dark", DarkTheme),
        (ModalSurface.WorkflowSettings, "modal-workflow-settings-dark", DarkTheme),
        // Una captura por cuerpo del TabControl: las secciones nuevas (apariencia, rendimiento, herramientas
        // externas y modelos de IA) eran UI sin línea base visual.
        (ModalSurface.WorkflowSettingsAppearance, "modal-settings-appearance-dark", DarkTheme),
        (ModalSurface.WorkflowSettingsPerformance, "modal-settings-performance-dark", DarkTheme),
        (ModalSurface.WorkflowSettingsExternalTools, "modal-settings-external-tools-dark", DarkTheme),
        (ModalSurface.WorkflowSettingsAiModels, "modal-settings-ai-models-dark", DarkTheme),
        (ModalSurface.MultimodalVlm, "modal-multimodal-vlm-dark", DarkTheme),
        (ModalSurface.PasswordManager, "modal-password-manager-dark", DarkTheme),
        (ModalSurface.RegexHelper, "modal-regex-helper-dark", DarkTheme),
        (ModalSurface.SyntheticDataSetDesigner, "modal-synthetic-data-designer-dark", DarkTheme),
        (ModalSurface.MediaPresetManager, "modal-media-preset-manager-dark", DarkTheme),
        (ModalSurface.StartupError, "modal-startup-error-dark", DarkTheme),
        // Una segunda modal en claro: cubre el camino del tema alternativo sin duplicar las nueve.
        (ModalSurface.About, "modal-about-light", LightTheme)
    ];

    [Fact]
    public void EveryKeyModal_ShouldMatchItsBaseline()
    {
        var failures = new List<string>();

        foreach (var (surface, baseline, theme) in KeySurfaces)
        {
            try
            {
                byte[] capture = ModalVisualFixture.Capture(surface, theme);
                VisualSnapshot.AssertMatchesBaseline(baseline, capture);
            }
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
            {
                // Seguir con el resto para que un fallo entregue todas las modales rotas de una vez.
                failures.Add($"{surface} ({baseline}): {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            "cada modal debe coincidir con su línea base; revisiona las imágenes en {0}",
            VisualSnapshot.BaselineDirectoryPath);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sondas: la captura no puede quedarse vacía ni perder el tema por el camino
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryModal_ShouldRenderContent_WithItsOwnThemedBackground()
    {
        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            byte[] capture = ModalVisualFixture.Capture(surface, DarkTheme);
            int colors = DistinctColors(capture);

            colors.Should().BeGreaterThan(
                4,
                $"'{surface}' debe pintar contenido real con las clases del tema, no una ventana plana");
        }
    }

    /// <summary>
    /// Cuenta colores distintos muestreando cada 3 píxeles (misma heurística que el shell): una ventana sin
    /// construir, sin tema aplicado o con su contenido fallido sale como una imagen casi plana.
    /// </summary>
    private static int DistinctColors(byte[] png)
    {
        var seen = new HashSet<uint>();

        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(png);

        for (int y = 0; y < image.Height; y += 3)
        {
            for (int x = 0; x < image.Width; x += 3)
            {
                var pixel = image[x, y];
                seen.Add(((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B);
            }
        }

        return seen.Count;
    }
}
