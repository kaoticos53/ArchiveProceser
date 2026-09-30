using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Los <b>diccionarios de cadenas</b> del árbol de fuentes (los <c>.resx</c> del host Uno, de la versión anterior y
/// de los plugins), leídos como clave -> valor.
///
/// <para><b>Por qué es un ayudante</b>: la paridad de TEXTOS es la misma pregunta en las tres guardias que
/// la hacen —los textos del menú, los de los paneles de nodo y los de la ficha—, y la lectura del
/// <c>.resx</c> (su <c>&lt;data name="…"&gt;</c> con su <c>&lt;value&gt;</c>) es una sola. Estaba escrita en
/// cada guardia; ahora se escribe una vez.</para>
/// </summary>
internal static class HostDictionaries
{
    /// <summary>De la ruta del fichero relativa a la raíz del repositorio al diccionario clave -> texto.</summary>
    public static Dictionary<string, string> Of(string relativePath)
    {
        XDocument document = XDocument.Parse(File.ReadAllText(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath)));
        return document.Root!.Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }
}
