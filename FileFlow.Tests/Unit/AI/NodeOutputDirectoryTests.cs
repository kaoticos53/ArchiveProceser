using System;
using System.IO;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

/// <summary>
/// <b>Dónde escriben los nodos de IA que declaran su carpeta de salida.</b> Cuatro nodos del plugin compartían la
/// misma regla copiada —síntesis de voz, detección de voz, anonimizador de datos y transcripción con subtítulos—, y
/// la copia tenía dos defectos: leía la carpeta del flujo <b>de la metadata tal cual</b> —así que una salida
/// declarada con plantilla (`{RelativeDir}`, que es lo que declara el catálogo de ejemplos) acababa dentro de la
/// ruta— y, cuando no había ninguna, caía en el <b>directorio de trabajo del proceso</b>: el archivo salía dentro de
/// la aplicación y el flujo parecía haber entregado cero (la forma del defecto del hito 204).
///
/// <para>La regla vive ahora en un solo sitio (<c>NodeOutputDirectory</c>) y estas pruebas la leen por la costura del
/// nodo —el método que el nodo llama de verdad—, no por una copia paralela en el suite.</para>
/// </summary>
public class NodeOutputDirectoryTests
{
    [Fact]
    public void WithAFlowFolderDeclaredAsATemplate_ShouldAnchorItInsideTheOrigin()
    {
        string root = NewDirectory();
        string subFolder = Path.Combine(root, "sub");
        Directory.CreateDirectory(subFolder);
        string input = Path.Combine(subFolder, "voz.wav");
        File.WriteAllText(input, "audio");

        try
        {
            var item = new FileItemContext(input);
            item.Metadata["SourceRootPath"] = root;
            item.Metadata["GlobalOutputDir"] = "{RelativeDir}";

            string resolved = TextToSpeechNode.ResolveTargetDirectory("{GlobalOutputDir}", item);

            resolved.Should().Be(subFolder,
                "la carpeta del flujo declarada con plantilla es la del archivo dentro del origen, no el texto de la plantilla");
        }
        finally
        {
            Clean(root);
        }
    }

    [Fact]
    public void WithNothingDeclared_ShouldUseTheItemFolder()
    {
        string root = NewDirectory();
        string input = Path.Combine(root, "voz.wav");
        File.WriteAllText(input, "audio");

        try
        {
            var item = new FileItemContext(input);

            string resolved = VoiceActivityDetectorNode.ResolveTargetDirectory("{GlobalOutputDir}", item);

            resolved.Should().Be(root,
                "sin salida declarada el archivo se queda junto al que entró, no donde corre el proceso");
            Path.GetFullPath(resolved).Should().NotBe(Path.GetFullPath(Directory.GetCurrentDirectory()),
                "el directorio de trabajo del proceso nunca es un destino: ahí el archivo aparece dentro de la aplicación");
        }
        finally
        {
            Clean(root);
        }
    }

    /// <summary>
    /// El último escalón, con un elemento que no trae carpeta (un nombre suelto): la carpeta temporal del producto.
    /// Cadena vacía no es nulo, así que la regla encadenaba aquí con <c>??</c> y el escalón no se disparaba nunca; el
    /// arnés de mutaciones lo destapó al medir la mutación como superviviente.
    /// </summary>
    [Fact]
    public void WithNoFolderAtAll_ShouldUseTheProductsTempFolder_NotTheProcessWorkingDirectory()
    {
        var item = new FileItemContext("voz.wav");

        string resolved = TextToSpeechNode.ResolveTargetDirectory("{GlobalOutputDir}", item);

        resolved.Should().Be(FileFlow.Sdk.Storage.AppPaths.DefaultTempDirectory,
            "sin carpeta en ninguna parte el archivo se va al temporal del producto, que es suyo y existe");
        resolved.Should().NotBe(Directory.GetCurrentDirectory(),
            "y nunca al directorio de trabajo del proceso, que es donde el archivo desaparece de la vista del usuario");
    }

    [Fact]
    public void WithADeclaredFolder_ShouldUseIt_AnchoringItInTheFlowFolderWhenRelative()
    {
        string root = NewDirectory();
        string output = NewDirectory();
        string input = Path.Combine(root, "voz.wav");
        File.WriteAllText(input, "audio");

        try
        {
            var item = new FileItemContext(input);
            item.Metadata["GlobalOutputDir"] = output;

            TextToSpeechNode.ResolveTargetDirectory(Path.Combine(root, "aqui"), item)
                .Should().Be(Path.Combine(root, "aqui"), "una carpeta declarada en redondo manda, y se usa tal cual");

            TextToSpeechNode.ResolveTargetDirectory("relativa", item)
                .Should().Be(Path.Combine(output, "relativa"),
                    "y una relativa cae dentro de la carpeta de salida del flujo, que es lo que el flujo declara");
        }
        finally
        {
            Clean(root, output);
        }
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "FF_NodeOutputDir_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Clean(params string[] directories)
    {
        foreach (string directory in directories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
