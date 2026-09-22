using System.Diagnostics;

namespace FileFlow.Sdk.Diagnostics;

/// <summary>
/// Punto único para diagnósticos "best effort" que hoy se escriben con
/// <see cref="Debug.WriteLine(string)"/> repartidos por todo el código (config loaders,
/// gestores de plugins, limpieza de recursos, etc.), casi siempre en sitios sin acceso a un
/// <see cref="IFlowExecutionContext"/> (no forman parte de la ejecución de un flujo).
///
/// <para><b>Contrato</b>: mismo comportamiento que el <c>Debug.WriteLine</c> que sustituye —
/// solo visible en compilaciones Debug, nunca lanza, nunca cambia el flujo de control del
/// llamador. Este helper solo unifica el formato (<c>[Categoria] mensaje: detalle</c>) y evita
/// que cada sitio reinvente el mismo patrón.</para>
/// </summary>
public static class DiagnosticLog
{
    /// <summary>Registra una condición anómala no fatal (fallback, degradación, reintento).</summary>
    /// <param name="category">Nombre corto de la clase u origen (ej. <c>nameof(MyClass)</c>).</param>
    /// <param name="message">Mensaje descriptivo, sin la excepción incluida.</param>
    public static void Warn(string category, string message)
    {
        Debug.WriteLine($"[{category}] {message}");
    }

    /// <summary>Registra un error atrapado localmente que no se propaga al llamador.</summary>
    /// <param name="category">Nombre corto de la clase u origen (ej. <c>nameof(MyClass)</c>).</param>
    /// <param name="message">Mensaje descriptivo, sin la excepción incluida.</param>
    /// <param name="ex">Excepción capturada; puede ser <see langword="null"/> si no aplica.</param>
    public static void Error(string category, string message, Exception? ex = null)
    {
        Debug.WriteLine(ex is null
            ? $"[{category}] {message}"
            : $"[{category}] {message}: {ex.Message}");
    }
}
