using FileFlow.Core.Engine;

namespace FileFlow.App.Services;

public sealed record WorkflowTemplate(string Title, string Category, string Description, WorkflowGraph Graph);

public static class PresetWorkflowsService
{
    public static IReadOnlyList<WorkflowTemplate> GetTemplates()
    {
        return
        [
            CreatePhotoOrganizerTemplate(),
            CreateSmartArchiveCleanerTemplate(),
            CreateBulkCliProcessingTemplate()
        ];
    }

    private static WorkflowTemplate CreatePhotoOrganizerTemplate()
    {
        var graph = new WorkflowGraph
        {
            Name = "Photo Ingestion & Normalizer"
        };

        var sourceNode = new WorkflowNode
        {
            Id = "node_source",
            NodeTypeName = "FileFlow.Plugin.FileSystem.FolderSourceNode",
            X = 50,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["SourceFolder"] = @"C:\Photos\Input",
                ["SearchPattern"] = "*.jpg;*.png;*.raw;*.cr3"
            }
        };

        var hashNode = new WorkflowNode
        {
            Id = "node_hash",
            NodeTypeName = "FileFlow.Plugin.Hashing.HashCalculatorNode",
            X = 350,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["Algorithm"] = "SHA256"
            }
        };

        var renameNode = new WorkflowNode
        {
            Id = "node_rename",
            NodeTypeName = "FileFlow.Plugin.FileSystem.AdvancedRenamerNode",
            X = 650,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["Pattern"] = "{CreationDate:yyyyMMdd}_{Hash:SHA256:8}_{FileNameNoExt}.{Ext}",
                ["CollisionStrategy"] = "AutoIncrement"
            }
        };

        var relocatorNode = new WorkflowNode
        {
            Id = "node_relocate",
            NodeTypeName = "FileFlow.Plugin.FileSystem.FileRelocatorNode",
            X = 950,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["Operation"] = "Move",
                ["DestinationDirectory"] = @"{SourceDir}\{Year}\{Month}"
            }
        };

        graph.Nodes.AddRange([sourceNode, hashNode, renameNode, relocatorNode]);

        graph.Edges.Add(new WorkflowEdge { SourceNodeId = sourceNode.Id, SourcePortName = "FileOut", TargetNodeId = hashNode.Id, TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = hashNode.Id, SourcePortName = "Out", TargetNodeId = renameNode.Id, TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = renameNode.Id, SourcePortName = "Out", TargetNodeId = relocatorNode.Id, TargetPortName = "In" });

        return new WorkflowTemplate(
            "Photo Ingestion & Normalizer",
            "Media",
            "Escanea fotos, calcula hash SHA-256, renombra con fecha y mueve a carpetas Año/Mes.",
            graph
        );
    }

    private static WorkflowTemplate CreateSmartArchiveCleanerTemplate()
    {
        var graph = new WorkflowGraph
        {
            Name = "Smart Deduplication & Safe Recycle"
        };

        var sourceNode = new WorkflowNode
        {
            Id = "node_source",
            NodeTypeName = "FileFlow.Plugin.FileSystem.FolderSourceNode",
            X = 50,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["SourceFolder"] = @"C:\Downloads",
                ["SearchPattern"] = "*.*"
            }
        };

        var dedupNode = new WorkflowNode
        {
            Id = "node_dedup",
            NodeTypeName = "FileFlow.Plugin.Hashing.DeduplicationFilterNode",
            X = 350,
            Y = 100,
            Parameters = new Dictionary<string, object?>()
        };

        var safeDeleteNode = new WorkflowNode
        {
            Id = "node_recycle",
            NodeTypeName = "FileFlow.Plugin.FileSystem.SafeRecycleDeleteNode",
            X = 650,
            Y = 220,
            Parameters = new Dictionary<string, object?>()
        };

        graph.Nodes.AddRange([sourceNode, dedupNode, safeDeleteNode]);

        graph.Edges.Add(new WorkflowEdge { SourceNodeId = sourceNode.Id, SourcePortName = "FileOut", TargetNodeId = dedupNode.Id, TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = dedupNode.Id, SourcePortName = "Duplicate", TargetNodeId = safeDeleteNode.Id, TargetPortName = "In" });

        return new WorkflowTemplate(
            "Smart Deduplication & Safe Recycle",
            "Deduplication",
            "Detecta archivos duplicados por hash SHA-256 y envía automáticamente las copias a la Papelera de reciclaje de Windows.",
            graph
        );
    }

    private static WorkflowTemplate CreateBulkCliProcessingTemplate()
    {
        var graph = new WorkflowGraph
        {
            Name = "Bulk CLI & Webhook Notification"
        };

        var sourceNode = new WorkflowNode
        {
            Id = "node_source",
            NodeTypeName = "FileFlow.Plugin.FileSystem.FolderSourceNode",
            X = 50,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["SourceFolder"] = @"C:\Files",
                ["SearchPattern"] = "*.mp4;*.mkv"
            }
        };

        var cliNode = new WorkflowNode
        {
            Id = "node_cli",
            NodeTypeName = "FileFlow.Plugin.Integrations.CliExecutionNode",
            X = 350,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["ExecutablePath"] = "cmd.exe",
                ["ArgumentsTemplate"] = "/c echo Transcoded {FileName}"
            }
        };

        var webhookNode = new WorkflowNode
        {
            Id = "node_webhook",
            NodeTypeName = "FileFlow.Plugin.Integrations.WebhookNotificationNode",
            X = 650,
            Y = 100,
            Parameters = new Dictionary<string, object?>
            {
                ["Url"] = "https://httpbin.org/post",
                ["PayloadTemplate"] = "{\"file\": \"{FileName}\", \"status\": \"done\"}"
            }
        };

        graph.Nodes.AddRange([sourceNode, cliNode, webhookNode]);

        graph.Edges.Add(new WorkflowEdge { SourceNodeId = sourceNode.Id, SourcePortName = "FileOut", TargetNodeId = cliNode.Id, TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = cliNode.Id, SourcePortName = "Success", TargetNodeId = webhookNode.Id, TargetPortName = "In" });

        return new WorkflowTemplate(
            "Bulk CLI & Webhook Notification",
            "Integrations",
            "Ejecuta procesos de línea de comandos externos por archivo y dispara notificaciones HTTP POST Webhook.",
            graph
        );
    }
}
