using System;
using System.IO;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Localiza la raíz del repositorio subiendo desde el directorio de ejecución de los tests.
/// Necesario para las guardias que auditan ficheros AXAML del proyecto en disco
/// (completitud de tokens de tema y lint de estilos inline).
/// </summary>
public static class TestRepositoryLocator
{
    private const string RootMarker = "FileFlow.slnx";

    private static readonly Lazy<string> _root = new(FindRoot);

    public static string RepositoryRoot() => _root.Value;

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RootMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No se pudo localizar la raíz del repositorio (marcador '{RootMarker}') desde '{AppContext.BaseDirectory}'.");
    }
}
