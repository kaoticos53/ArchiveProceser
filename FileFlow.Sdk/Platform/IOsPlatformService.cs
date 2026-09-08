using System.Runtime.InteropServices;

namespace FileFlow.Sdk.Platform;

/// <summary>
/// Contrato universal de servicios dependientes del sistema operativo.
/// Desacopla las llamadas nativas (Win32, Linux FreeDesktop, macOS Cocoa) y comandos de sistema.
/// </summary>
public interface IOsPlatformService
{
    /// <summary>
    /// Identificador de la plataforma del sistema operativo actual.
    /// </summary>
    OSPlatform Platform { get; }

    /// <summary>
    /// Indica si el sistema operativo anfitrión es Windows.
    /// </summary>
    bool IsWindows => Platform == OSPlatform.Windows;

    /// <summary>
    /// Indica si el sistema operativo anfitrión es Linux.
    /// </summary>
    bool IsLinux => Platform == OSPlatform.Linux;

    /// <summary>
    /// Indica si el sistema operativo anfitrión es macOS (OSX).
    /// </summary>
    bool IsMac => Platform == OSPlatform.OSX;

    /// <summary>
    /// Envía un archivo o directorio a la papelera del sistema operativo de forma no destructiva.
    /// </summary>
    bool MoveToTrash(string path);

    /// <summary>
    /// Recorta y optimiza el Working Set del proceso para devolver memoria no utilizada al sistema operativo.
    /// </summary>
    bool TrimWorkingSet();

    /// <summary>
    /// Obtiene la cantidad de memoria física disponible (en bytes) en el sistema.
    /// </summary>
    long GetAvailablePhysicalMemoryBytes();

    /// <summary>
    /// Devuelve el ejecutable de shell por defecto para el sistema operativo actual (ej: cmd.exe, /bin/sh).
    /// </summary>
    string GetDefaultShellExecutable();

    /// <summary>
    /// Construye la cadena de argumentos necesaria para ejecutar un comando en el shell por defecto.
    /// </summary>
    string GetDefaultShellArguments(string command);

    /// <summary>
    /// Abre una carpeta en el explorador o gestor de archivos nativo del sistema.
    /// </summary>
    bool OpenFolderInFileManager(string folderPath);

    /// <summary>
    /// Abre el gestor de archivos nativo resaltando o seleccionando el archivo especificado.
    /// </summary>
    bool OpenFileInFileManager(string filePath);

    /// <summary>
    /// Abre un enlace web o URL en el navegador predeterminado del sistema.
    /// </summary>
    bool OpenUrl(string url);
}
