using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using FileFlow.App.Services;
using FileFlow.App.Views.Components;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardia del <b>muestrario</b> del selector de color: el color elegido debe sobrevivir a que el tema se
/// vuelva a publicar.
///
/// <para>Antes, el control escribía el color en el <c>Background</c> del borde del muestrario, que su XAML
/// enlazaba a <c>AccentPrimaryBrush</c>: al aplicar un tema (arranque, Theme Studio, cambio de tema)
/// <c>ThemeManager.ApplyResourceDictionary</c> reemplaza el recurso, Avalonia vuelve a evaluar el
/// <c>DynamicResource</c> y el muestrario volvía al acento del tema, perdiendo el color elegido en silencio.
/// Es el mismo patrón que tumbó el barrido de la splash (hito 169); la regla estática del analizador lo
/// detecta, y esta prueba comprueba el efecto sobre el control real.</para>
///
/// <para><b>Colección exclusiva</b>: la prueba publica temas en el <c>ThemeManager</c> (proceso entero), así
/// que no puede correr en paralelo con las capturas headless, que están renderizando el tema activo.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class ColorPickerSwatchTests
{
    private const string PickedColor = "#10B981";

    [Fact]
    public void ThePickedColor_ShouldSurviveAThemeRepublication()
    {
        string originalThemeId = ThemeManager.Instance.CurrentThemeId;

        AvaloniaTestHelper.RunOnUI(() =>
        {
            var picker = new ColorPickerButton();
            var window = new Window { Content = picker, Width = 220, Height = 140 };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                picker.SelectedColorHex = PickedColor;
                Dispatcher.UIThread.RunJobs();

                SwatchColor(picker).Should().Be(
                    Color.Parse(PickedColor),
                    "el muestrario muestra el color elegido por el usuario");

                // Lo que hace cualquier aplicación de tema, en el arranque o desde el Theme Studio.
                ThemeManager.Instance.SetThemeById("light_studio");
                Dispatcher.UIThread.RunJobs();

                SwatchColor(picker).Should().Be(
                    Color.Parse(PickedColor),
                    "el color es del control, no del tema: republicar el tema no puede revertirlo al acento");
            }
            finally
            {
                ThemeManager.Instance.SetThemeById(originalThemeId);
                Dispatcher.UIThread.RunJobs();

                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Color visible del muestrario: el borde más interno del botón de la paleta. Se busca por lo que se ve
    /// —un borde que pinta un color sólido— y no por su nombre, para que la prueba siga midiendo el efecto
    /// aunque el árbol del control se reorganice.
    /// </summary>
    private static Color? SwatchColor(ColorPickerButton picker)
    {
        var swatchButton = picker.GetLogicalDescendants().OfType<Button>()
            .FirstOrDefault(button => button.Name == "BtnSwatch");

        swatchButton.Should().NotBeNull("el selector de color expone su botón de muestrario");

        var painted = swatchButton!.GetLogicalDescendants().OfType<Border>()
            .LastOrDefault(border => border.Background is ISolidColorBrush);

        return painted?.Background is ISolidColorBrush brush ? brush.Color : null;
    }
}
