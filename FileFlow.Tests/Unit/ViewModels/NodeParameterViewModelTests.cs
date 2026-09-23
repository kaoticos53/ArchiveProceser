using FileFlow.Tests.TestHelpers;
using System.Resources;
using System.Threading.Tasks;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.ViewModels;

[Collection("VisualSnapshots")]
public class NodeParameterViewModelTests : IDisposable
{
    public void Dispose()
    {
        AvaloniaTestHelper.SetCultureOnUI("es-ES");
    }

    [Fact]
    public void DisplayName_ShouldReturnFormattedFallback_WhenNoResourceManagerRegistered()
    {
        // Arrange
        using var param = new NodeParameterViewModel("CustomUnregisteredSetting", "WebP");

        // Act & Assert
        param.DisplayName.Should().Be("Custom Unregistered Setting");
    }

    [Fact]
    public void DisplayName_ShouldUpdateReactively_WhenCultureChanges()
    {
        // Arrange
        var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(FileFlow.App.App).Assembly);
        LocalizationManager.Instance.RegisterResourceManager(resourceManager);

        AvaloniaTestHelper.SetCultureOnUI("es-ES");
        using var param = new NodeParameterViewModel("Width", 1920);

        // Act - En español
        string nameEs = param.DisplayName;

        // Cambiar a inglés
        AvaloniaTestHelper.SetCultureOnUI("en-US");
        string nameEn = param.DisplayName;

        // Assert
        nameEs.Should().Be("Ancho");
        nameEn.Should().Be("Width");

        // Reset
        AvaloniaTestHelper.SetCultureOnUI("es-ES");
    }

    [Fact]
    public void EvaluatedValue_ShouldDetectExpression_WhenBracesOrTagsPresent()
    {
        // Arrange & Act
        using var paramPlain = new NodeParameterViewModel("Destination", @"C:\Output\StaticFolder");
        using var paramExpr = new NodeParameterViewModel("Destination", @"{RelativeDir}\Output");
        using var paramTag = new NodeParameterViewModel("Destination", @"<FileName>_backup");

        // Assert
        paramPlain.HasExpression.Should().BeFalse();
        paramPlain.EvaluatedValue.Should().Be(@"C:\Output\StaticFolder");

        paramExpr.HasExpression.Should().BeTrue();
        paramTag.HasExpression.Should().BeTrue();
    }

    [Fact]
    public void EvaluatedValue_ShouldResolveWithFileContext_WhenContextUpdated()
    {
        // Arrange
        using var param = new NodeParameterViewModel("Destination", @"{SourceDir}\Output_{Year}\{FileName}");
        var item = new FileItemContext(@"C:\Photos\Album\image.png");

        // Act
        param.UpdateEvaluationContext(item);

        // Assert
        param.HasExpression.Should().BeTrue();
        param.EvaluatedValue.Should().Be($@"C:\Photos\Album\Output_{DateTime.Now.Year}\image.png");
    }

    [Fact]
    public void EvaluatedValue_ShouldRecalculate_WhenValueChanged()
    {
        // Arrange
        using var param = new NodeParameterViewModel("Prefix", "PlainPrefix");
        var item = new FileItemContext(@"C:\Data\file.txt");
        item.Metadata["CustomTag"] = "XYZ";
        param.UpdateEvaluationContext(item);

        param.HasExpression.Should().BeFalse();
        param.EvaluatedValue.Should().Be("PlainPrefix");

        // Act - Cambiar valor a plantilla con metadato
        param.Value = "Prefix_{CustomTag}_{FileName}";

        // Assert
        param.HasExpression.Should().BeTrue();
        param.EvaluatedValue.Should().Be("Prefix_XYZ_file.txt");
    }

    [Fact]
    public void IsStandardRow_ShouldBeTrueForSingleLine_AndFalseForMultiLine()
    {
        // Arrange
        using var singleLineParam = new NodeParameterViewModel("Width", 1920);
        using var multiLineParam = new NodeParameterViewModel(
            new NodeParameterDescriptor("AdditionalPrompt", ParameterEditorType.MultiLineText, DefaultValue: ""),
            ""
        );

        // Act & Assert
        singleLineParam.IsStandardRow.Should().BeTrue();
        singleLineParam.IsMultiLine.Should().BeFalse();

        multiLineParam.IsStandardRow.Should().BeFalse();
        multiLineParam.IsMultiLine.Should().BeTrue();
    }

    [Fact]
    public void UpdateOptions_ShouldRefreshOptions_AndKeepMatchedValue()
    {
        // Arrange
        using var param = new NodeParameterViewModel("Format", "PNG", ["JPG", "PNG", "WEBP"]);

        // Act
        param.UpdateOptions(["GIF", "PNG", "AVIF"]);

        // Assert
        param.Options.Should().ContainInOrder("GIF", "PNG", "AVIF");
        param.Value.Should().Be("PNG");
    }

    [Fact]
    public void DropdownParameter_ShouldRecognizeDropdownAndMatchOption()
    {
        // Arrange
        var desc = new NodeParameterDescriptor("ArchiveFormat", ParameterEditorType.Dropdown, DefaultValue: "ZIP", Options: ["ZIP", "TAR", "GZ", "7Z"]);
        using var param = new NodeParameterViewModel(desc, "tar");

        // Act & Assert
        param.IsDropdown.Should().BeTrue();
        param.IsEditableDropdown.Should().BeFalse();
        param.Options.Should().ContainInOrder("ZIP", "TAR", "GZ", "7Z");
        param.Value.Should().Be("TAR"); // matched case-insensitively to exact option
    }

    [Fact]
    public void EditableDropdownParameter_ShouldBeMarkedAsEditable()
    {
        // Arrange
        var desc = new NodeParameterDescriptor("CustomPreset", ParameterEditorType.EditableDropdown, DefaultValue: "Default", Options: ["Default", "High", "Low"]);
        using var param = new NodeParameterViewModel(desc, "CustomValue");

        // Act & Assert
        param.IsDropdown.Should().BeTrue();
        param.IsEditableDropdown.Should().BeTrue();
        param.Options.Should().Contain("CustomValue");
        param.Value.Should().Be("CustomValue");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Aviso de copiado: la duración es semántica, así que el reloj se inyecta
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plazo máximo para observar un vencimiento que el reloj manual ya disparó, deliberadamente <b>menor</b> que
    /// la duración del aviso: si alguien volviera a esperar de verdad, la prueba falla en lugar de tardar 1500 ms
    /// y aprobar por paciencia. Con el reloj inyectado, la espera se cubre en microsegundos.
    /// </summary>
    private static readonly TimeSpan BoundedWait =
        NodeParameterViewModel.CopyFeedbackDuration - TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task CopyEvaluatedValue_ShouldKeepTheConfirmationUntilTheClockReachesItsDuration()
    {
        // Sin reloj inyectado, comprobar que el aviso se apaga costaría esperar los 1500 ms reales (y seguiría
        // sin probar lo que dice la duración). Con el reloj manual, el tiempo que pasa es el de la prueba.
        var clock = new ManualTimeProvider();
        EnsureClipboardHost();
        using var param = new NodeParameterViewModel("Destination", "valor", timeProvider: clock);
        param.EvaluatedValue = "valor";

        Task copy = param.CopyEvaluatedValueAsync();

        param.IsCopied.Should().BeTrue("el gesto se acaba de confirmar");
        clock.PendingTimerCount.Should().Be(1, "el vencimiento quedó programado en el reloj inyectado, no en el del sistema");

        clock.AdvanceBy(NodeParameterViewModel.CopyFeedbackDuration - TimeSpan.FromMilliseconds(1));
        param.IsCopied.Should().BeTrue("el aviso dura exactamente lo que declara CopyFeedbackDuration");

        clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
        await copy.WaitAsync(BoundedWait);

        param.IsCopied.Should().BeFalse("al vencer su duración el aviso se apaga solo");
    }

    [Fact]
    public async Task CopyEvaluatedValue_ShouldRestartTheConfirmationWindow_OnASecondClick()
    {
        // Dos clics seguidos: el vencimiento del primero llega con el aviso del segundo abierto. Si no se
        // descartara, el aviso del segundo se apagaría a mitad de camino, justo cuando el usuario está mirando.
        var clock = new ManualTimeProvider();
        EnsureClipboardHost();
        using var param = new NodeParameterViewModel("Destination", "valor", timeProvider: clock);

        Task first = param.CopyEvaluatedValueAsync();
        clock.AdvanceBy(TimeSpan.FromMilliseconds(1200));
        Task second = param.CopyEvaluatedValueAsync();

        clock.AdvanceBy(TimeSpan.FromMilliseconds(300)); // vence la ventana del primer clic, ya obsoleta
        await first.WaitAsync(BoundedWait);

        param.IsCopied.Should().BeTrue("el segundo clic reabrió la ventana del aviso");

        clock.AdvanceBy(NodeParameterViewModel.CopyFeedbackDuration);
        await second.WaitAsync(BoundedWait);

        param.IsCopied.Should().BeFalse("el último clic sí cierra su propio aviso");
    }

    [Fact]
    public async Task CopyEvaluatedValue_ShouldNotConfirm_WhenThereIsNothingToCopy()
    {
        var clock = new ManualTimeProvider();
        using var param = new NodeParameterViewModel("Destination", string.Empty, timeProvider: clock);
        param.EvaluatedValue = string.Empty;

        await param.CopyEvaluatedValueAsync();

        param.IsCopied.Should().BeFalse("sin valor no hay copia ni confirmación que mostrar");
        clock.PendingTimerCount.Should().Be(0, "sin confirmación no hay nada que venza después");
    }

    /// <summary>
    /// Arranca la sesión headless de forma explícita antes de copiar.
    ///
    /// El portapapeles se publica a través del <c>Dispatcher</c> de la aplicación, así que un test que copie
    /// necesita la aplicación en marcha: sin ella, el propio despacho lanza y el aviso nunca se enciende por un
    /// motivo que no tiene nada que ver con lo que se está midiendo. Depender de que otra clase haya arrancado la
    /// sesión antes haría que esta prueba pasara o fallara según el orden de ejecución.
    /// </summary>
    private static void EnsureClipboardHost() => AvaloniaTestHelper.RunOnUI(static () => { });
}
