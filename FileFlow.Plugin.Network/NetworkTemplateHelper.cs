using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Network;

/// <summary>
/// Helper interno para resolver variables de contexto en rutas remotas y nombres de archivo de red.
/// Reutiliza el motor transversal <see cref="VariableTemplateResolver"/> del SDK.
/// </summary>
public static class NetworkTemplateHelper
{
    public static string ResolveRemotePath(string template, FileItemContext item)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return item.FileName;
        }

        string resolved = VariableTemplateResolver.Resolve(template, item);
        return resolved.Replace('\\', '/');
    }
}
