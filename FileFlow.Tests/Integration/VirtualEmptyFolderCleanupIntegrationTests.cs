using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Integration;

/// <summary>
/// El limpiador de carpetas vacías, ejecutado por el motor <b>en una ejecución virtual</b>: un origen sintético
/// deja un árbol de carpetas con archivos dentro, un nodo mueve los archivos a su destino y el limpiador retira
/// las carpetas que quedaron vacías del almacén virtual.
///
/// <para>Existe porque este nodo <b>no funcionaba</b> en una ejecución virtual y lo decía en voz baja: el borrado
/// pasaba por el contrato del almacenamiento —así que borraba en el almacén virtual— pero el recorrido miraba el
/// disco del anfitrión (<c>Directory.Enumerate*</c>), donde esa carpeta no existe. El resultado era el peor
/// posible: la carpeta a limpiar «no existía», el nodo salía por <c>Out</c> sin borrar nada y el flujo terminaba
/// en verde. Ninguna prueba lo veía porque las que había ejecutan el nodo contra el disco; esta usa el motor real
/// con el sistema de archivos virtual activado y afirma el estado del almacén, no una descripción.</para>
/// </summary>
public class VirtualEmptyFolderCleanupIntegrationTests
{
    private const string Root = @"C:\Muestras\Personalizada";

    [Fact]
    public async Task AFlujoThatEmptiesItsFolders_ShouldCleanThemInTheVirtualStore()
    {
        // Arrange: árbol de dos capítulos dentro de su carpeta, con dos niveles por encima
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(SyntheticDataSourceNode).Assembly);

        var graph = new WorkflowGraph { Name = "Mover a destino y limpiar carpetas vacías" };

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "origen",
            NodeTypeName = "SyntheticDataSourceNode",
            Parameters = new Dictionary<string, object?>
            {
                ["Category"] = "Personalizada",
                ["EmissionMode"] = "Virtual",
                ["MaxItems"] = 0,
                ["EmissionDelayMs"] = 0,
                ["EmitDirectories"] = "False",
                ["CustomItems"] = """
                Series/Anime/DeathNote/
                    Capitulo_01.mkv | size=1MB
                    Capitulo_02.mkv | size=1MB
                """
            }
        });

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "mover",
            NodeTypeName = "FileRelocatorNode",
            Parameters = new Dictionary<string, object?>
            {
                ["Operation"] = "Move",
                ["DestinationDirectory"] = @"C:\Salida",
                ["CreateDirectories"] = "True",
                ["VerifyIntegrity"] = "False",
                ["CleanupSource"] = "False"
            }
        });

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "limpiar",
            NodeTypeName = "EmptyDirectoryCleanerNode",
            Parameters = new Dictionary<string, object?>
            {
                ["TargetDirectory"] = Root,
                ["Recursive"] = "True",
                ["IgnoreHiddenSystemFiles"] = "True"
            }
        });

        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "mover", TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "mover", SourcePortName = "Out", TargetNodeId = "limpiar", TargetPortName = "TriggerIn" });

        var executor = new WorkflowExecutor();
        var completedNodes = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        executor.NodeStatusChanged += (nodeId, status) =>
        {
            if (status == NodeExecutionStatus.Completed) completedNodes[nodeId] = true;
        };

        // Act
        await executor.ExecuteAsync(graph, loader, cancellationToken: CancellationToken.None);

        var store = executor.VirtualFileSystem;

        // Assert: la ejecución se activó en modo virtual sola (el origen es sintético) y llegó hasta el final
        executor.IsVirtualFileSystemEnabled.Should().BeTrue(
            "la ejecución virtual es la condición del caso: sin ella lo que se prueba es el disco");
        completedNodes.Keys.Should().BeEquivalentTo(["origen", "mover", "limpiar"],
            "el limpiador tiene que llegar a ejecutarse aguas abajo del movimiento");

        // Los archivos se movieron de verdad (en el almacén virtual): el flujo hizo su trabajo antes de limpiar
        store.FileExists(@"C:\Salida\Capitulo_01.mkv").Should().BeTrue();
        store.FileExists(@"C:\Salida\Capitulo_02.mkv").Should().BeTrue();
        store.DirectoryExists(@"C:\Salida").Should().BeTrue(
            "la carpeta de destino tiene contenido: el limpiador no puede tocar nada fuera de su objetivo");

        // Las carpetas que quedaron sin archivos ya no están en el almacén virtual
        foreach (string folder in new[] { Root + @"\Series\Anime\DeathNote", Root + @"\Series\Anime", Root + @"\Series", Root })
        {
            store.DirectoryExists(folder).Should().BeFalse($"'{folder}' se quedó sin archivos y el nodo la retira");
        }

        // Y lo que está por encima del objetivo sigue ahí: limpiar no es arrasar
        store.DirectoryExists(@"C:\Muestras").Should().BeTrue(
            "el nodo limpia la carpeta que se le indica, no sus padres");

        // El rastro del propio nodo: un asiento de borrado permanente por carpeta, con su ruta
        executor.JournalService.Entries
            .Where(e => e.OperationType == JournalOperationType.DeletedPermanently)
            .Select(e => e.SourcePath)
            .Should().BeEquivalentTo(
                [Root + @"\Series\Anime\DeathNote", Root + @"\Series\Anime", Root + @"\Series", Root],
                "el diario de ejecución dice qué se borró: cuatro carpetas, de dentro hacia fuera");
    }
}
