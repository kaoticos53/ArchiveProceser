using System;
using System.IO;
using System.Threading;

namespace FileFlow.App.Uno;

/// <summary>
/// El buffer de la línea de ejecución de la barra de estado (hito 243): la UI la escribe en un
/// renglón de longitud fija con padding a espacios (borra el rastro del renglón anterior) y un
/// observador externo la lee con una lectura volátil — un solo string de referencia, sin
/// tearing. La longitud fija no es un hueco: es el formato del canal.
/// </summary>
public static class StatusLineWriter
{
    private const int LineWidth = 220;

    private static string _line = string.Empty.PadRight(LineWidth);

    /// <summary>El último renglón escrito (lectura volátil, sin tearing entre hilos).</summary>
    public static string Current => Volatile.Read(ref _line);

    /// <summary>
    /// El fichero espejo del renglón (junto al ejecutable): la segunda vía de lectura para un
    /// observador externo, la que no depende del fragmentado del TextBlock. Se escribe en cada
    /// renglón; un fallo de disco no rompe el canal en memoria.
    /// </summary>
    public static string CurrentExecutionStatusFile => Path.Combine(AppContext.BaseDirectory, "execution-status.txt");

    /// <summary>El renglón con padding a espacios: la UI lo asigna tal cual al TextBlock.</summary>
    public static string Padded(string content)
    {
        var line = (content ?? string.Empty).PadRight(LineWidth);
        if (line.Length > LineWidth)
        {
            line = line[..LineWidth];
        }

        Volatile.Write(ref _line, line);
        try
        {
            File.WriteAllText(CurrentExecutionStatusFile, line);
        }
        catch
        {
            // El espejo es una conveniencia; su fallo no rompe el canal en memoria.
        }

        return line;
    }
}
