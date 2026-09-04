using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de pipeline para cumplimiento normativo RGPD y sanitización de datos de identificación personal (PII).
/// Detecta DNI/NIE, cuentas IBAN, tarjetas de crédito, emails, teléfonos y nombres, ofuscándolos según el modo elegido.
/// </summary>
[NodeDefinition("PiiAnonymizerNode_Name", "Security", "PiiAnonymizerNode_Desc", PipelineRole.Transform,
    "gdpr", "rgpd", "dni", "nie", "iban", "tarjeta", "privacidad", "ofuscar", "anonimizar", "luhn", "email", "telefono")]
public class PiiAnonymizerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("PiiAnonymizerNode_Name", "Anonimizador de Datos RGPD (PII)");
    public string Category => "Security";
    public string Description => LocalizationManager.Instance.GetString("PiiAnonymizerNode_Desc", "Detecta y anonimiza datos personales sensibles (DNI, IBAN, tarjetas, emails, teléfonos) en documentos.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Clean", typeof(FileItemContext), PortDirection.Output, "Clean"),
        new NodePort("SensitiveFound", typeof(FileItemContext), PortDirection.Output, "SensitiveFound"),
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["AnonymizationMode"] = "TagReplacement",
        ["FilterDniNie"] = true,
        ["FilterIban"] = true,
        ["FilterCreditCards"] = true,
        ["FilterEmails"] = true,
        ["FilterPhones"] = true,
        ["FilterIpAddresses"] = true,
        ["FilterPersonNames"] = true,
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "pii-ner-multilingual", "RegexOnly"],
            HelpText: "Motor de análisis de entidades sensibles ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("AnonymizationMode", ParameterEditorType.Dropdown, DefaultValue: "TagReplacement",
            Options: ["TagReplacement", "Mask", "Hash", "Remove"],
            HelpText: "Modo de reemplazo (etiquetas [DNI], máscara con asteriscos, hash SHA-256 o eliminación).", DisplayOrder: 2),
        new("FilterDniNie", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar DNIs y NIEs españoles con validación de dígito de control.", DisplayOrder: 3),
        new("FilterIban", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar cuentas bancarias IBAN con validación MOD-97.", DisplayOrder: 4),
        new("FilterCreditCards", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar números de tarjetas de crédito con algoritmo de Luhn.", DisplayOrder: 5),
        new("FilterEmails", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar direcciones de correo electrónico.", DisplayOrder: 6),
        new("FilterPhones", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar números de teléfono nacionales e internacionales.", DisplayOrder: 7),
        new("FilterIpAddresses", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar direcciones IP públicas y privadas.", DisplayOrder: 8),
        new("FilterPersonNames", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Detectar y anonimizar nombres propios de personas por contexto honorífico.", DisplayOrder: 9),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}",
            HelpText: "Carpeta donde se guardará el archivo sanitizado resultante.", DisplayOrder: 10)
    ];

    private static readonly HashSet<string> _textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".log", ".yaml", ".yml", ".srt"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[PiiAnonymizer] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_textExtensions.Contains(ext))
        {
            context.Log($"[PiiAnonymizer] Formato binario o no analizable como texto ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:PiiDetected"] = false;
            await context.EmitAsync("Clean", item).ConfigureAwait(false);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string mode = Parameters.TryGetValue("AnonymizationMode", out var amVal) ? amVal?.ToString() ?? "TagReplacement" : "TagReplacement";
            bool filterDni = Parameters.TryGetValue("FilterDniNie", out var dniVal) && ParameterHelper.GetBoolean(dniVal, true);
            bool filterIban = Parameters.TryGetValue("FilterIban", out var ibanVal) && ParameterHelper.GetBoolean(ibanVal, true);
            bool filterCards = Parameters.TryGetValue("FilterCreditCards", out var cardVal) && ParameterHelper.GetBoolean(cardVal, true);
            bool filterEmails = Parameters.TryGetValue("FilterEmails", out var emailVal) && ParameterHelper.GetBoolean(emailVal, true);
            bool filterPhones = Parameters.TryGetValue("FilterPhones", out var phoneVal) && ParameterHelper.GetBoolean(phoneVal, true);
            bool filterIps = Parameters.TryGetValue("FilterIpAddresses", out var ipVal) && ParameterHelper.GetBoolean(ipVal, true);
            bool filterNames = Parameters.TryGetValue("FilterPersonNames", out var nameVal) && ParameterHelper.GetBoolean(nameVal, true);
            string outputDirRaw = Parameters.TryGetValue("OutputDirectory", out var odVal) ? odVal?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";

            var options = new PiiOptions(
                Mode: mode,
                FilterDniNie: filterDni,
                FilterIban: filterIban,
                FilterCreditCards: filterCards,
                FilterEmails: filterEmails,
                FilterPhones: filterPhones,
                FilterIpAddresses: filterIps,
                FilterPersonNames: filterNames);

            context.Log($"[PiiAnonymizer] 🛡️ Escaneando datos sensibles en '{item.FileName}'...", LogLevel.Information, item);

            string rawText = await File.ReadAllTextAsync(item.CurrentPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            var result = await Task.Run(() => PiiDetectionEngine.AnonymizeText(rawText, options), cancellationToken).ConfigureAwait(false);

            string targetDir = ParameterHelper.ResolveOutputPath(
                string.IsNullOrWhiteSpace(outputDirRaw) ? "{GlobalOutputDir}" : outputDirRaw,
                item);

            Directory.CreateDirectory(targetDir);

            string targetFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_anonymized{ext}";
            string targetPath = Path.Combine(targetDir, targetFileName);

            await File.WriteAllTextAsync(targetPath, result.SanitizedText, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            var newItem = item.DeepClone();
            newItem.CurrentPath = targetPath;
            newItem.PhysicalPath = targetPath;
            newItem.FileSizeBytes = new FileInfo(targetPath).Length;
            newItem.Metadata["AI:PiiDetected"] = result.PiiDetected;
            newItem.Metadata["AI:PiiTotalCount"] = result.TotalCount;
            newItem.Metadata["AI:PiiCategories"] = string.Join(", ", result.Categories);
            newItem.Metadata["AI:PiiReportJson"] = JsonSerializer.Serialize(result.CountsByCategory);

            if (result.PiiDetected)
            {
                context.Log($"[PiiAnonymizer] ⚠️ Detectadas {result.TotalCount} entidades sensibles ({string.Join(", ", result.Categories)}). Sanitizado generado: '{targetFileName}'.",
                    LogLevel.Warning, newItem);
                await context.EmitAsync("SensitiveFound", newItem).ConfigureAwait(false);
            }
            else
            {
                context.Log($"[PiiAnonymizer] ✅ Documento limpio de datos sensibles identificables.", LogLevel.Information, newItem);
                await context.EmitAsync("Clean", newItem).ConfigureAwait(false);
            }

            await context.EmitAsync("Out", newItem).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[PiiAnonymizer] ❌ Error anonimizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
