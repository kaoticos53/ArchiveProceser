using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de pipeline para cumplimiento normativo RGPD y sanitización de datos de identificación personal (PII).
/// Detecta DNI/NIE, cuentas IBAN, tarjetas de crédito, emails, teléfonos y nombres, ofuscándolos según el modo elegido.
/// </summary>
[NodeDefinition("PiiAnonymizerNode_Name", "Security", "PiiAnonymizerNode_Desc", PipelineRole.Transform,
    "gdpr", "rgpd", "dni", "nie", "iban", "tarjeta", "privacidad", "ofuscar", "anonimizar", "luhn", "email", "telefono")]
public sealed class PiiAnonymizerNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("PiiAnonymizerNode_Name", "Anonimizador de Datos RGPD (PII)");
    public override string Category => "Security";
    public override string Description => LocalizationManager.Instance.GetString("PiiAnonymizerNode_Desc", "Detecta y anonimiza datos personales sensibles (DNI, IBAN, tarjetas, emails, teléfonos) en documentos.");
    public override AiTaskType TaskType => AiTaskType.PiiAnonymization;

    /// <summary>
    /// La detección es determinista (regex + validadores de dígito de control), no una red neuronal: el nodo
    /// no materializa ninguna sesión ONNX y por eso declara explícitamente su identidad y su ciclo de vida.
    /// </summary>
    public override string? ModelIdentifier => "RGPD Regex / NER";

    public PiiAnonymizerNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Clean", typeof(FileItemContext), PortDirection.Output, "Clean"),
            new NodePort("SensitiveFound", typeof(FileItemContext), PortDirection.Output, "SensitiveFound"),
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["AnonymizationMode"] = "TagReplacement";
        Parameters["FilterDniNie"] = true;
        Parameters["FilterIban"] = true;
        Parameters["FilterCreditCards"] = true;
        Parameters["FilterEmails"] = true;
        Parameters["FilterPhones"] = true;
        Parameters["FilterIpAddresses"] = true;
        Parameters["FilterPersonNames"] = true;
        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
        Parameters["SkipIfExists"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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
            HelpText: "Carpeta donde se guardará el archivo sanitizado resultante.", DisplayOrder: 10),
        new("SkipIfExists", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Si el archivo resultante ya existe en destino, omite el análisis y reutiliza el archivo.", DisplayOrder: 11)
    ];

    private static readonly HashSet<string> _textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".log", ".yaml", ".yml", ".srt"
    };

    /// <summary>
    /// Sin sesión que cargar, la precarga se limita a refrescar el estado del modelo en la UI: heredar la
    /// implementación base descargaría el modelo de NER que este nodo nunca llega a usar.
    /// </summary>
    public override Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        RaiseModelStatusChanged();
        return Task.CompletedTask;
    }

    /// <summary>Igual que la precarga: sin sesión en memoria, sólo se notifica el cambio de estado.</summary>
    public override void UnloadModel() => RaiseModelStatusChanged();

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"[PiiAnonymizer] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_textExtensions.Contains(ext))
        {
            Log(context, $"[PiiAnonymizer] Formato binario o no analizable como texto ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:PiiDetected"] = false;
            await EmitAsync(context, item, "Clean").ConfigureAwait(false);
            await EmitAsync(context, item).ConfigureAwait(false);
            return;
        }

        try
        {
            string mode = GetParameter("AnonymizationMode", "TagReplacement");
            bool filterDni = GetParameter("FilterDniNie", true);
            bool filterIban = GetParameter("FilterIban", true);
            bool filterCards = GetParameter("FilterCreditCards", true);
            bool filterEmails = GetParameter("FilterEmails", true);
            bool filterPhones = GetParameter("FilterPhones", true);
            bool filterIps = GetParameter("FilterIpAddresses", true);
            bool filterNames = GetParameter("FilterPersonNames", true);
            string outputDirRaw = GetParameter("OutputDirectory", "{GlobalOutputDir}");
            bool skipIfExists = GetParameter("SkipIfExists", false);

            // La carpeta la decide la regla compartida del plugin: ver NodeOutputDirectory.
            string targetDir = NodeOutputDirectory.For(outputDirRaw, item);

            await storage.CreateDirectoryAsync(targetDir, cancellationToken).ConfigureAwait(false);

            string targetFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_anonymized{ext}";
            string targetPath = Path.Combine(targetDir, targetFileName);

            if (skipIfExists && await storage.FileExistsAsync(targetPath, cancellationToken).ConfigureAwait(false))
            {
                Log(context, $"[PiiAnonymizer] ⏭️ El archivo de salida ya existe ('{targetFileName}'). Omitiendo análisis.", LogLevel.Information, item);
                var existingItem = item.DeepClone();
                existingItem.CurrentPath = targetPath;
                existingItem.PhysicalPath = targetPath;
                existingItem.FileSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
                await EmitAsync(context, existingItem, "Clean").ConfigureAwait(false);
                await EmitAsync(context, existingItem).ConfigureAwait(false);
                return;
            }

            var options = new PiiOptions(
                Mode: mode,
                FilterDniNie: filterDni,
                FilterIban: filterIban,
                FilterCreditCards: filterCards,
                FilterEmails: filterEmails,
                FilterPhones: filterPhones,
                FilterIpAddresses: filterIps,
                FilterPersonNames: filterNames);

            Log(context, $"[PiiAnonymizer] 🛡️ Escaneando datos sensibles en '{item.FileName}'...", LogLevel.Information, item);

            string rawText = await storage.ReadAllTextAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);

            var result = await Task.Run(() => PiiDetectionEngine.AnonymizeText(rawText, options), cancellationToken).ConfigureAwait(false);

            await storage.WriteAllTextAsync(targetPath, result.SanitizedText, ct: cancellationToken).ConfigureAwait(false);

            var newItem = item.DeepClone();
            newItem.CurrentPath = targetPath;
            newItem.PhysicalPath = targetPath;
            newItem.FileSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
            newItem.Metadata["AI:PiiDetected"] = result.PiiDetected;
            newItem.Metadata["AI:PiiTotalCount"] = result.TotalCount;
            newItem.Metadata["AI:PiiCategories"] = string.Join(", ", result.Categories);
            newItem.Metadata["AI:PiiReportJson"] = JsonSerializer.Serialize(result.CountsByCategory);

            if (result.PiiDetected)
            {
                Log(context, $"[PiiAnonymizer] ⚠️ Detectadas {result.TotalCount} entidades sensibles ({string.Join(", ", result.Categories)}). Sanitizado generado: '{targetFileName}'.",
                    LogLevel.Warning, newItem);
                await EmitAsync(context, newItem, "SensitiveFound").ConfigureAwait(false);
            }
            else
            {
                Log(context, "[PiiAnonymizer] ✅ Documento limpio de datos sensibles identificables.", LogLevel.Information, newItem);
                await EmitAsync(context, newItem, "Clean").ConfigureAwait(false);
            }

            await EmitAsync(context, newItem).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[PiiAnonymizer] ❌ Error anonimizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
