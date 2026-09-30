using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno;

/// <summary>
/// El CINTURÓN de medida compartido del sondeo del host Uno: recorrer el árbol visual —la única vía de
/// WinUI es <c>VisualTreeHelper</c>— y nombrar el marco donde nació un fallo. Cuatro modos medían con las
/// mismas funciones y vivían dentro del fichero del despachador; aquí están en su propio archivo, para que
/// quien mida el árbol no vuelva a escribir el recorrido ni a inventarse una quinta variante.
///
/// <para><b>Qué expone</b>: <c>Find</c> (el primero de un tipo), <c>FindAll</c> (todos, en orden de árbol),
/// <c>FindAscendant</c> (el ascendiente: llegar al contenedor que generó el <c>ItemsControl</c>),
/// <c>FindDescendantNamed</c> (por <c>Name</c> de framework, independiente del reflejo sobre campos
/// generados) y <c>Children</c> (los hijos visuales); <c>DescribeFrame</c> es para el informe: sin él, una
/// excepción del hilo de UI sólo deja su mensaje y hay que adivinar qué lectura la lanzó.</para>
///
/// <para><b>Quién afirma</b>: nadie — este archivo MIDE y devuelve. El <c>[OK]</c>/<c>[FALLO]</c> lo escribe
/// el sondeo que lo llama, y la forma del reparto la fija <c>UnoSelfCheckLayoutGuardTests</c>.</para>
/// </summary>
internal static class SelfCheckTree
{
    /// <summary>El descendiente por Name de framework (independiente del reflejo sobre campos generados).</summary>
    internal static T? FindDescendantNamed<T>(object? root, string name) where T : FrameworkElement
    {
        if (root is FrameworkElement element && element.Name == name && element is T match)
        {
            return match;
        }

        foreach (var child in Children(root))
        {
            var found = FindDescendantNamed<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal static T? Find<T>(object? root) where T : class
    {
        if (root is T match)
        {
            return match;
        }

        foreach (var child in Children(root))
        {
            var found = Find<T>(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal static System.Collections.Generic.IEnumerable<T> FindAll<T>(object? root) where T : class
    {
        if (root is T match)
        {
            yield return match;
        }

        foreach (var child in Children(root))
        {
            foreach (var found in FindAll<T>(child))
            {
                yield return found;
            }
        }
    }

    /// <summary>El ascendiente de un tipo dado (el ContentPresenter del contenedor, p. ej.).</summary>
    internal static T? FindAscendant<T>(DependencyObject start) where T : DependencyObject
    {
        var current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        return default;
    }

    /// <summary>Los hijos visuales por VisualTreeHelper (la única vía de WinUI).</summary>
    internal static System.Collections.Generic.IEnumerable<object?> Children(object? node)
    {
        if (node is not DependencyObject dep)
        {
            yield break;
        }

        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(dep);
        for (int i = 0; i < count; i++)
        {
            yield return Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(dep, i);
        }
    }

    /// <summary>
    /// El marco donde nació una excepción del sondeo, para poder nombrarlo en el informe. Sin esto, una
    /// excepción del hilo de UI sólo deja su mensaje y hay que adivinar qué lectura la lanzó.
    /// </summary>
    internal static string DescribeFrame(Exception ex)
    {
        string? frame = ex.StackTrace?
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Contains("FileFlow", StringComparison.Ordinal));
        if (string.IsNullOrEmpty(frame))
        {
            return "(sin traza de FileFlow)";
        }

        // «en <ruta>:línea N» se cae (la ruta de esta máquina no dice nada al informe): queda el método.
        foreach (string cut in new[] { " en ", " in " })
        {
            int at = frame.IndexOf(cut, StringComparison.Ordinal);
            if (at > 0)
            {
                frame = frame[..at];
                break;
            }
        }

        return frame;
    }
}
