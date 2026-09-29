using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Las <b>tablas que declara el control de la barra</b> del host Uno, leídas de su fuente: el censo de
/// entradas pendientes, las que sirve el catálogo de diálogos, las órdenes que el host cumple por su cuenta y
/// sus atajos sin ruta.
///
/// <para><b>Por qué es un ayudante y no parte de una guardia</b>: dos guardias distintas leen las MISMAS
/// tablas —la del censo y la paridad de entradas, y la de las entradas con ventana—, y la lectura (el nombre
/// de la tabla en el código y el corte hasta su <c>];</c>) es la misma para las dos. Tenerla escrita dos veces
/// era tener dos formas de leer la misma verdad.</para>
///
/// <para><b>Qué lee</b>: cada fila de la tabla, la primera columna cuando es una orden del núcleo
/// (<c>("ToggleMenuCommand", "…")</c>). Una tabla que no existe, o que existe y no se puede leer, es un
/// fallo de la guardia que la pide —no un conjunto vacío que haga pasar una aserción vacía—: por eso lanza
/// en vez de devolver nada.</para>
///
/// <para><b>Qué admite</b>: que la tabla esté VACÍA (<paramref name="allowEmpty"/>). Desde el hito 261 no
/// queda ninguna orden del escritorio sin servir, y una tabla vacía es la verdad —no una tabla que no se
/// lee—. Que la tabla EXISTA (con su guardia y su razón) lo comprueba el caso del censo en su propia
/// guardia.</para>
/// </summary>
internal static class UnoControlBarTables
{
    private const string ControlBarCode = "FileFlow.App.Uno/Controls/ControlBar.xaml.cs";

    /// <summary>Las órdenes de la tabla del control que se llame así, leídas del propio código fuente.</summary>
    public static HashSet<string> Of(string tableName, bool allowEmpty = false)
    {
        string code = SourceText.CodeWithoutComments(ControlBarCode);
        int at = code.IndexOf(tableName + " =", StringComparison.Ordinal);
        if (at < 0)
        {
            throw new InvalidOperationException($"el control tiene que declarar la tabla {tableName}");
        }

        int end = code.IndexOf("];", at, StringComparison.Ordinal);
        if (end < at)
        {
            throw new InvalidOperationException($"la tabla {tableName} tiene que cerrarse con '];'");
        }

        var declared = Regex.Matches(code[at..end], @"""([A-Za-z]+Command)"", ""[^""]+""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        if (!allowEmpty && declared.Count == 0)
        {
            throw new InvalidOperationException(
                $"la tabla {tableName} tiene que leerse desde el código, con su razón");
        }

        return declared;
    }
}
