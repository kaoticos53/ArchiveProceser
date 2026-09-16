using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Colección de las pruebas que usan la sesión headless de Avalonia, marcada como <b>no paralelizable</b>.
///
/// Una colección de xUnit sin esta marca se ejecuta en paralelo con el resto de colecciones. Eso era un
/// riesgo real para todo el suite, porque estas pruebas comparten estado global de la aplicación:
/// <list type="bullet">
///   <item>El tema activo (<c>ThemeManager.Instance</c>) y el diccionario de recursos de la aplicación: una
///   captura cambia el tema para el resto de la ejecución.</item>
///   <item>El idioma y los diccionarios de <c>LocalizationManager</c>, que el helper fija al arrancar.</item>
///   <item>El hilo de UI de la sesión y los registros de modelos de <c>ModelSessionRegistry</c>.</item>
/// </list>
/// Aislarlas no cuesta nada —son una minoría del suite— y evita la clase de fallo más difícil de depurar:
/// una captura que falla sólo cuando otra prueba toca el tema en ese instante.
///
/// Cualquier clase nueva que arranque o use la sesión headless (capturas, ventanas, controles) debe
/// declararse en esta colección.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class VisualSnapshotsCollection
{
    public const string Name = "VisualSnapshots";
}
