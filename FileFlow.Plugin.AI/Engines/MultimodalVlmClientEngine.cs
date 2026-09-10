using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Presets de tareas comunes para modelos multimodales de visión y lenguaje (VLM).
/// </summary>
public enum VlmTaskPreset
{
    ExtractInvoiceReceiptJson,
    DocumentOcrAndSummary,
    TranslateDocument,
    ClassifyAndTag,
    QualityInspection,
    CustomPrompt
}

/// <summary>
/// Resultado de inferencia multimodal obtenido del servidor VLM.
/// </summary>
public record VlmInferenceResult(
    string RawText,
    string? ExtractedJson,
    string? DetectedCategory,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long DurationMs,
    string ModelUsed);

/// <summary>
/// Motor cliente HTTP resiliente para interactuar con servidores locales (LM Studio, Ollama)
/// o remotos compatibles con la API OpenAI Chat Completions para modelos de Visión-Lenguaje (Qwen2.5-VL, Llama-3.2-Vision, etc.).
/// </summary>
public static partial class MultimodalVlmClientEngine
{
    private static readonly HttpClient DefaultHttpClient = CreateDefaultHttpClient();

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FileFlowStudio-VLM/1.0");
        return client;
    }

    /// <summary>
    /// Retorna los prompts de sistema y usuario sugeridos según el preset seleccionado.
    /// </summary>
    public static (string SystemPrompt, string UserPrompt) GetPresetPrompts(VlmTaskPreset preset, string targetLanguage = "Español")
    {
        return preset switch
        {
            VlmTaskPreset.ExtractInvoiceReceiptJson => (
                "Eres un asistente contable y fiscal experto. Analiza la imagen del documento (factura, recibo, ticket o albarán) y extrae todos sus datos clave en formato JSON estricto. " +
                "Incluye campos como: emisor_nombre, emisor_cif_nif, receptor_nombre, receptor_cif_nif, fecha_emision, numero_factura, base_imponible, porcentaje_iva, cuota_iva, importe_total, divisa y lineas_articulos (lista con descripcion, cantidad, precio_unitario, importe). " +
                "No agregues texto explicativo ni bloques markdown adicionales fuera del JSON.",
                "Por favor, lee y extrae todos los datos contables y fiscales de esta imagen de documento en formato JSON estructurado."
            ),
            VlmTaskPreset.DocumentOcrAndSummary => (
                "Eres un analista documental experto. Transcribe con fidelidad el texto visible en la imagen y a continuación elabora un resumen ejecutivo destacando los puntos y conclusiones principales en " + targetLanguage + ".",
                "Transcribe el texto visible de esta imagen o documento escaneado y genera un resumen ejecutivo claro y conciso."
            ),
            VlmTaskPreset.TranslateDocument => (
                $"Eres un traductor profesional multilingüe. Lee todo el contenido textual visible en la imagen y tradúcelo fielmente al {targetLanguage}. Preserva el formato de párrafos, listas y encabezados en Markdown.",
                $"Traduce todo el texto visible de esta imagen directamente al {targetLanguage} manteniendo el estilo y disposición original."
            ),
            VlmTaskPreset.ClassifyAndTag => (
                "Eres un clasificador de visión computacional y catalogación digital. Analiza la imagen y clasifícala en una de las siguientes categorías principales: " +
                "['Documento_Legal', 'Factura_Recibo', 'Documento_Identidad', 'Fotografia_Retrato', 'Fotografia_Paisaje', 'Captura_Pantalla_UI', 'Ilustracion_Dibujo', 'Otro']. " +
                "Responde con un objeto JSON que contenga: 'categoria', 'confianza_aproximada' (0.0 a 1.0), 'etiquetas_descriptivas' (lista de 5 a 10 tags) y 'motivo' (una frase explicativa).",
                "Clasifica esta imagen, asigna etiquetas descriptivas y explica brevemente el motivo."
            ),
            VlmTaskPreset.QualityInspection => (
                "Eres un auditor de calidad documental y fotográfica. Inspecciona minuciosamente la imagen y evalúa: " +
                "1) Legibilidad del texto (Excelente, Aceptable, Deficiente, Ilegible), " +
                "2) Presencia de firmas manuscritas o sellos oficiales (Sí/No y ubicación), " +
                "3) Defectos de imagen (desenfoque, sobreexposición, sombras excesivas, recortes o rotaciones indeseadas). " +
                "Devuelve un informe estructurado en formato JSON con los campos: 'es_valido_para_tramite' (booleano), 'legibilidad', 'tiene_firma', 'tiene_sello', 'defectos_detectados' y 'recomendacion'.",
                "Realiza una inspección exhaustiva de calidad y validez formal sobre esta imagen o documento escaneado."
            ),
            VlmTaskPreset.CustomPrompt => (
                "Eres un asistente de inteligencia artificial visual multimodal preciso, conciso y objetivo.",
                "Describe detalladamente qué contiene esta imagen y extrae la información relevante."
            ),
            _ => (
                "Eres un asistente visual multimodal.",
                "Analiza la imagen adjunta."
            )
        };
    }

    /// <summary>
    /// Codifica una imagen ImageSharp en una URI de datos Base64 JPEG optimizada para envío HTTP a modelos VLM,
    /// aplicando un reescalado bicúbico proporcional si sobrepasa la dimensión máxima configurada.
    /// </summary>
    public static string PrepareImageAsBase64Jpeg(Image<Rgb24> image, int maxDimension = 1536)
    {
        ArgumentNullException.ThrowIfNull(image);

        int origW = image.Width;
        int origH = image.Height;
        int maxSide = Math.Max(origW, origH);

        Image<Rgb24> processImage = image;
        bool isCloned = false;

        if (maxSide > maxDimension && maxDimension >= 256)
        {
            float scale = (float)maxDimension / maxSide;
            int newW = Math.Max(1, (int)Math.Round(origW * scale));
            int newH = Math.Max(1, (int)Math.Round(origH * scale));

            processImage = image.Clone(ctx => ctx.Resize(newW, newH, KnownResamplers.Bicubic));
            isCloned = true;
        }

        try
        {
            using var ms = new MemoryStream();
            var encoder = new JpegEncoder
            {
                Quality = 85
            };
            processImage.SaveAsJpeg(ms, encoder);
            string base64 = Convert.ToBase64String(ms.ToArray());
            return "data:image/jpeg;base64," + base64;
        }
        finally
        {
            if (isCloned)
            {
                processImage.Dispose();
            }
        }
    }

    /// <summary>
    /// Ejecuta una inferencia multimodal contra un servidor compatible con la API de OpenAI (LM Studio, Ollama, etc.).
    /// </summary>
    public static async Task<VlmInferenceResult> ExecuteChatCompletionAsync(
        string endpointUrl,
        string modelName,
        string? apiKey,
        string systemPrompt,
        string userPrompt,
        string base64ImageDataUrl,
        double temperature = 0.1,
        int maxTokens = 2048,
        bool forceJsonOutput = false,
        TimeSpan? timeout = null,
        HttpClient? customHttpClient = null,
        CancellationToken cancellationToken = default)
    {
        var client = customHttpClient ?? DefaultHttpClient;
        var sw = Stopwatch.StartNew();

        // Normalizar endpoint URL: asegurar formato http://host:port/v1/chat/completions
        string cleanEndpoint = (endpointUrl ?? "http://localhost:1234/v1").Trim().TrimEnd('/');
        if (!cleanEndpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            cleanEndpoint += "/chat/completions";
        }

        // Construir payload JSON compatible con OpenAI Chat Completions Multimodal
        var userContentList = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = userPrompt
            },
            new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject
                {
                    ["url"] = base64ImageDataUrl
                }
            }
        };

        var messagesArray = new JsonArray();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messagesArray.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = systemPrompt
            });
        }

        messagesArray.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = userContentList
        });

        var requestBody = new JsonObject
        {
            ["model"] = !string.IsNullOrWhiteSpace(modelName) ? modelName : "qwen2.5-vl-7b-instruct",
            ["messages"] = messagesArray,
            ["temperature"] = Math.Clamp(temperature, 0.0, 1.0),
            ["max_tokens"] = Math.Max(64, maxTokens),
            ["stream"] = false
        };

        if (forceJsonOutput)
        {
            requestBody["response_format"] = new JsonObject
            {
                ["type"] = "json_object"
            };
        }

        string requestJson = requestBody.ToJsonString();
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, cleanEndpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            cts.CancelAfter(timeout.Value);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(requestMessage, cts.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"No se pudo conectar con el servidor VLM en '{cleanEndpoint}'. Asegúrate de que LM Studio o el servidor local esté en ejecución: {ex.Message}", ex);
        }

        string responseContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"El servidor VLM respondió con código {(int)response.StatusCode} ({response.StatusCode}): {responseContent}");
        }

        // Parsear respuesta
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        string assistantText = string.Empty;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentElem))
            {
                assistantText = contentElem.GetString() ?? string.Empty;
            }
        }

        int promptTokens = 0;
        int completionTokens = 0;
        int totalTokens = 0;

        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt)) promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct)) completionTokens = ct.GetInt32();
            if (usage.TryGetProperty("total_tokens", out var tt)) totalTokens = tt.GetInt32();
        }

        sw.Stop();

        // Extraer y sanitizar JSON si existe
        string? extractedJson = TryExtractValidJson(assistantText);
        string? detectedCategory = TryExtractCategory(extractedJson ?? assistantText);

        return new VlmInferenceResult(
            RawText: assistantText,
            ExtractedJson: extractedJson,
            DetectedCategory: detectedCategory,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            TotalTokens: totalTokens > 0 ? totalTokens : promptTokens + completionTokens,
            DurationMs: sw.ElapsedMilliseconds,
            ModelUsed: modelName);
    }

    /// <summary>
    /// Intenta extraer un bloque JSON válido de la respuesta, limpiando etiquetas markdown si estuvieran presentes.
    /// </summary>
    public static string? TryExtractValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string trimmed = text.Trim();

        // 1. Si el texto completo es JSON directo
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) || (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            if (IsValidJson(trimmed)) return trimmed;
        }

        // 2. Extraer bloques de código ```json ... ```
        var match = JsonBlockRegex().Match(trimmed);
        if (match.Success)
        {
            string candidate = match.Groups[1].Value.Trim();
            if (IsValidJson(candidate)) return candidate;
        }

        // 3. Buscar el primer '{' y el último '}'
        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            string candidate = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            if (IsValidJson(candidate)) return candidate;
        }

        return null;
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryExtractCategory(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("categoria", out var c1)) return c1.GetString();
                if (root.TryGetProperty("category", out var c2)) return c2.GetString();
                if (root.TryGetProperty("type", out var c3)) return c3.GetString();
            }
        }
        catch
        {
            // No es JSON estructurado
        }

        return null;
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonBlockRegex();
}
