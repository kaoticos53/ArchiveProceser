using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno;

/// <summary>
/// El rastro del FOCO del lienzo (hito 252): quién tiene el foco cuando el usuario clica y qué teclas
/// llegan después.
///
/// <para>Existe porque hay una clase de defecto que este entorno no puede provocar solo: el puntero no se
/// puede inyectar (medido en los hitos 231 y 250), así que el foco que entrega un clic real —y lo que el
/// árbol hace con él después— sólo se ve con dedos de verdad. Con el rastro encendido, una sesión manual
/// deja la secuencia escrita en lugar de depender de la memoria del operador.</para>
///
/// <para><b>Apagado por defecto</b>: sólo escribe si el proceso arranca con <c>FILEFLOW_CANVAS_TRACE=1</c>;
/// sin la variable no toca el disco ni una vez (la ruta se calcula perezosamente). El fichero va junto al
/// ejecutable, la misma convención que el espejo de la línea de estado del hito 243.</para>
/// </summary>
public static class CanvasFocusTrace
{
    /// <summary>El fichero del rastro, o <c>null</c> si el rastro está apagado (no se toca el disco).</summary>
    public static string? FilePath => IsEnabled
        ? Path.Combine(AppContext.BaseDirectory, "canvas-focus-trace.txt")
        : null;

    /// <summary>¿El proceso pidió el rastro? (la variable se lee una vez, al arrancar el tipo).</summary>
    public static bool IsEnabled { get; } =
        Environment.GetEnvironmentVariable("FILEFLOW_CANVAS_TRACE") == "1";

    /// <summary>
    /// Describe un elemento por su TIPO y su nombre, y sube por sus ancestros: con la cadena se sabe qué
    /// panel se quedó con el foco cuando el que lo recibe no tiene nombre propio (un <c>ScrollViewer</c>).
    /// </summary>
    public static string Describe(object? element, int ancestors = 3)
    {
        if (element is not DependencyObject current)
        {
            return "nadie";
        }

        var parts = new List<string>();
        DependencyObject? node = current;
        for (int i = 0; node is not null && i <= ancestors; i++)
        {
            parts.Add(node is FrameworkElement named ? $"{named.GetType().Name}#{named.Name}" : node.GetType().Name);
            node = node is UIElement ui ? VisualTreeHelper.GetParent(ui) : null;
        }

        return string.Join("<-", parts);
    }

    /// <summary>Añade una línea con el reloj monótono delante, para poder ordenar los gestos.</summary>
    public static void Write(string line)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            File.AppendAllText(FilePath!, $"{Environment.TickCount64} {line}{Environment.NewLine}");
        }
        catch
        {
            // El rastro es diagnóstico: su fallo no puede romper el gesto que está midiendo.
        }
    }
}
