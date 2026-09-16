using System;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// <see cref="AppVisualFixture"/> compartida por una clase de pruebas vía <c>IClassFixture</c>.
///
/// xUnit construye la clase fixture una sola vez (antes de la primera prueba) y la dispone después de la
/// última; aquí la construcción y la liberación se marshaling al hilo de la sesión headless, como exige
/// <c>AppVisualFixture.Create</c> (los view models registran <c>DispatcherTimer</c> y tienen afinidad de
/// hilo). Cada prueba llama <see cref="AppVisualFixture.EnsureFrozen"/> antes de construir su superficie
/// para partir del estado congelado aunque otra prueba hubiera tocado view models.
/// </summary>
public sealed class SharedAppVisualFixture : IDisposable
{
    /// <summary>La muestra compartida por la clase.</summary>
    public AppVisualFixture Fixture { get; }

    public SharedAppVisualFixture()
    {
        Fixture = AvaloniaTestHelper.RunOnUI(AppVisualFixture.Create);
    }

    /// <summary>Devuelve la fixture congelada para la captura (llamar dentro de la fábrica de captura).</summary>
    public AppVisualFixture Frozen() => Fixture;

    public void Dispose()
    {
        Fixture.Dispose();
    }
}
