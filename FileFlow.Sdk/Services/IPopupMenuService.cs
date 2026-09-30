using System.Windows.Input;

namespace FileFlow.Sdk.Services;

/// <summary>
/// Descriptor neutral de un menú emergente (el menú rápido de variables, hoy construido con tipos
/// del framework dentro del ViewModel). El ViewModel describe <b>qué</b> se muestra —títulos, negritas,
/// comandos y parámetros— y el host lo pinta con sus controles nativos.
/// Los comandos son <see cref="ICommand"/> portables: el host los ejecuta tal cual al pulsar cada entrada.
/// </summary>
public sealed record PopupMenuDescriptor
{
    /// <summary>Entradas del primer nivel del menú, en orden.</summary>
    public required IReadOnlyList<PopupMenuItem> Items { get; init; }
}

/// <summary>Una entrada de menú: texto, peso tipográfico, comando a ejecutar, tooltip y subentradas.</summary>
public sealed record PopupMenuItem
{
    public required string Header { get; init; }

    /// <summary>La entrada se destaca (primera opción del catálogo, grupos con variables propias del flujo).</summary>
    public bool IsBold { get; init; }

    /// <summary>Comando portable que el host ejecuta al activar la entrada; null = entrada informativa.</summary>
    public ICommand? Command { get; init; }

    /// <summary>Parámetro que el host pasa al comando.</summary>
    public object? CommandParameter { get; init; }

    /// <summary>Aclaración flotante de la entrada (p. ej. el valor de ejemplo de una variable).</summary>
    public string? ToolTip { get; init; }

    /// <summary>Subentradas; si trae, la entrada abre un submenú y no ejecuta comando.</summary>
    public IReadOnlyList<PopupMenuItem>? Items { get; init; }
}

/// <summary>
/// Contrato para mostrar menús emergentes desde los ViewModels sin conocer el framework de UI.
/// El ancla es <c>object?</c> a propósito: el host la interpreta (el control desde el que abrir el menú);
/// cuando no hay ancla el host no muestra nada.
/// </summary>
public interface IPopupMenuService
{
    /// <summary>Muestra el menú descrito, anclado al elemento indicado (si el host lo puede resolver).</summary>
    void ShowMenu(PopupMenuDescriptor descriptor, object? anchor = null);
}
