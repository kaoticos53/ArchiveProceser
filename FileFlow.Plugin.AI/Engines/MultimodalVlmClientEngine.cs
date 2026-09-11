using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk.Serialization;
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

    /// <summary>
    /// Semáforos de concurrencia por host/endpoint para evitar saturar la memoria VRAM y los slots
    /// de inferencia de servidores locales de VLM (LM Studio, Ollama) durante ejecuciones paralelas en pipeline.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_endpointThrottles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registro en memoria de endpoints/modelos que han rechazado el parámetro 'response_format' (ej. Error 400).
    /// Evita reenviar 'response_format' en subsecuentes imágenes del mismo lote.
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> s_unsupportedResponseFormatCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registro en memoria de endpoints/modelos que han rechazado 'json_schema' en 'response_format' pero admiten 'json_object'.
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> s_unsupportedJsonSchemaCache = new(StringComparer.OrdinalIgnoreCase);

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
    /// Retorna el esquema JSON canónico para el preset indicado (si aplica).
    /// </summary>
    public static string? GetPresetJsonSchema(VlmTaskPreset preset)
    {
        return preset switch
        {
            VlmTaskPreset.ExtractInvoiceReceiptJson => """
            {
              "type": "object",
              "properties": {
                "tipo_documento": { "type": "string" },
                "numero_factura": { "type": "string" },
                "fecha_emision": { "type": "string" },
                "emisor_nombre": { "type": "string" },
                "emisor_cif_nif": { "type": "string" },
                "receptor_nombre": { "type": "string" },
                "receptor_cif_nif": { "type": "string" },
                "base_imponible": { "type": "number" },
                "porcentaje_iva": { "type": "number" },
                "cuota_iva": { "type": "number" },
                "importe_total": { "type": "number" },
                "divisa": { "type": "string" },
                "lineas_articulos": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "descripcion": { "type": "string" },
                      "cantidad": { "type": "number" },
                      "precio_unitario": { "type": "number" },
                      "importe": { "type": "number" }
                    },
                    "required": ["descripcion", "importe"]
                  }
                }
              },
              "required": ["tipo_documento", "importe_total"]
            }
            """,
            VlmTaskPreset.ClassifyAndTag => """
            {
              "type": "object",
              "properties": {
                "categoria": { "type": "string" },
                "confianza_aproximada": { "type": "number" },
                "etiquetas_descriptivas": { "type": "array", "items": { "type": "string" } },
                "motivo": { "type": "string" },
                "archivo": { "type": "string" }
              },
              "required": ["categoria", "confianza_aproximada", "etiquetas_descriptivas", "motivo"]
            }
            """,
            VlmTaskPreset.QualityInspection => """
            {
              "type": "object",
              "properties": {
                "es_valido_para_tramite": { "type": "boolean" },
                "legibilidad": { "type": "string" },
                "tiene_firma": { "type": "boolean" },
                "tiene_sello": { "type": "boolean" },
                "defectos_detectados": { "type": "array", "items": { "type": "string" } },
                "recomendacion": { "type": "string" }
              },
              "required": ["es_valido_para_tramite", "legibilidad", "tiene_firma", "tiene_sello", "defectos_detectados", "recomendacion"]
            }
            """,
            _ => null
        };
    }

    /// <summary>
    /// Retorna los prompts de sistema y usuario sugeridos según el preset seleccionado.
    /// </summary>
    public static (string SystemPrompt, string UserPrompt) GetPresetPrompts(VlmTaskPreset preset, string targetLanguage = "Español")
    {
        return preset switch
        {
            VlmTaskPreset.ExtractInvoiceReceiptJson => (
                "Eres un asistente contable y fiscal experto. Analiza la imagen del documento (factura, recibo, ticket o albarán) y extrae sus datos fiscales y económicos.\n" +
                "REGLA CRÍTICA DE ESTRUCTURA: Debes responder EXCLUSIVAMENTE con un único objeto JSON válido que respete de forma exacta e inmutable la siguiente estructura de campos (no inventes, no traduzcas ni renombres las claves; usa null o 0.00 si no se encuentra el dato):\n" +
                "{\n" +
                "  \"tipo_documento\": \"Factura | Recibo | Ticket | Albaran\",\n" +
                "  \"numero_factura\": \"string\",\n" +
                "  \"fecha_emision\": \"YYYY-MM-DD\",\n" +
                "  \"emisor_nombre\": \"string\",\n" +
                "  \"emisor_cif_nif\": \"string\",\n" +
                "  \"receptor_nombre\": \"string\",\n" +
                "  \"receptor_cif_nif\": \"string\",\n" +
                "  \"base_imponible\": 0.00,\n" +
                "  \"porcentaje_iva\": 21.00,\n" +
                "  \"cuota_iva\": 0.00,\n" +
                "  \"importe_total\": 0.00,\n" +
                "  \"divisa\": \"EUR | USD | GBP\",\n" +
                "  \"lineas_articulos\": [\n" +
                "    {\n" +
                "      \"descripcion\": \"string\",\n" +
                "      \"cantidad\": 1.0,\n" +
                "      \"precio_unitario\": 0.00,\n" +
                "      \"importe\": 0.00\n" +
                "    }\n" +
                "  ]\n" +
                "}\n" +
                "No agregues texto explicativo, comentarios ni bloques markdown fuera del JSON.",
                "Por favor, analiza este documento y extrae todos sus datos contables y fiscales completando estrictamente el esquema JSON requerido."
            ),
            VlmTaskPreset.DocumentOcrAndSummary => (
                "Eres un analista documental experto. Transcribe con fidelidad el texto visible en la imagen y a continuación elabora un resumen ejecutivo destacando los puntos y conclusiones principales en " + targetLanguage + ".\n" +
                "Si la salida requerida es JSON, utiliza obligatoriamente las siguientes claves inmutables: {\"texto_transcrito\": \"string\", \"resumen_ejecutivo\": \"string\", \"puntos_clave\": [\"string\"], \"idioma_detectado\": \"string\"}.",
                "Transcribe el texto visible de esta imagen o documento escaneado y genera un resumen ejecutivo claro y conciso."
            ),
            VlmTaskPreset.TranslateDocument => (
                $"Eres un traductor profesional multilingüe. Lee todo el contenido textual visible en la imagen y tradúcelo fielmente al {targetLanguage}. Preserva el formato de párrafos, listas y encabezados en Markdown.",
                $"Traduce todo el texto visible de esta imagen directamente al {targetLanguage} manteniendo el estilo y disposición original."
            ),
            VlmTaskPreset.ClassifyAndTag => (
                "Eres un clasificador de visión computacional y catalogación digital. Analiza la imagen y clasifícala en una de las siguientes categorías principales: " +
                "['Documento_Legal', 'Factura_Recibo', 'Documento_Identidad', 'Fotografia_Retrato', 'Fotografia_Paisaje', 'Captura_Pantalla_UI', 'Ilustracion_Dibujo', 'Otro'].\n" +
                "REGLA CRÍTICA DE ESTRUCTURA: Debes responder EXCLUSIVAMENTE con un único objeto JSON válido con estas claves inmutables en minúsculas:\n" +
                "{\n" +
                "  \"categoria\": \"string\",\n" +
                "  \"confianza_aproximada\": 0.95,\n" +
                "  \"etiquetas_descriptivas\": [\"tag1\", \"tag2\"],\n" +
                "  \"motivo\": \"string\",\n" +
                "  \"archivo\": \"string\"\n" +
                "}\n" +
                "No agregues explicaciones fuera del JSON.",
                "Clasifica esta imagen, asigna etiquetas descriptivas y explica brevemente el motivo."
            ),
            VlmTaskPreset.QualityInspection => (
                "Eres un auditor de calidad documental y fotográfica. Inspecciona minuciosamente la imagen y evalúa formalmente su validez.\n" +
                "REGLA CRÍTICA DE ESTRUCTURA: Debes responder EXCLUSIVAMENTE con un único objeto JSON válido con estas claves inmutables en minúsculas:\n" +
                "{\n" +
                "  \"es_valido_para_tramite\": true,\n" +
                "  \"legibilidad\": \"Excelente | Aceptable | Deficiente | Ilegible\",\n" +
                "  \"tiene_firma\": false,\n" +
                "  \"tiene_sello\": false,\n" +
                "  \"defectos_detectados\": [\"string\"],\n" +
                "  \"recomendacion\": \"string\"\n" +
                "}\n" +
                "No agregues texto explicativo fuera del JSON.",
                "Realiza una inspección exhaustiva de calidad y validez formal sobre esta imagen o documento escaneado respetando el esquema JSON requerido."
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
    public static string PrepareImageAsBase64Jpeg(Image<Rgb24> image, int maxDimension = 1024)
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
        string? jsonSchema = null,
        TimeSpan? timeout = null,
        HttpClient? customHttpClient = null,
        int concurrencyLimit = 0,
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

        string effectiveModel = !string.IsNullOrWhiteSpace(modelName) ? modelName : "qwen2.5-vl-7b-instruct";
        string unsupportedCacheKey = $"{cleanEndpoint}::{effectiveModel}";

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
            ["model"] = effectiveModel,
            ["messages"] = messagesArray,
            ["temperature"] = Math.Clamp(temperature, 0.0, 1.0),
            ["max_tokens"] = Math.Max(64, maxTokens),
            ["stream"] = false
        };

        // Configuración de response_format determinista (Structured Outputs con json_schema o json_object)
        bool shouldSendResponseFormat = forceJsonOutput && !s_unsupportedResponseFormatCache.ContainsKey(unsupportedCacheKey);
        if (shouldSendResponseFormat)
        {
            bool tryJsonSchema = !string.IsNullOrWhiteSpace(jsonSchema) && !s_unsupportedJsonSchemaCache.ContainsKey(unsupportedCacheKey);
            if (tryJsonSchema)
            {
                try
                {
                    var schemaNode = JsonNode.Parse(jsonSchema!);
                    if (schemaNode != null)
                    {
                        requestBody["response_format"] = new JsonObject
                        {
                            ["type"] = "json_schema",
                            ["json_schema"] = new JsonObject
                            {
                                ["name"] = "vlm_output_schema",
                                ["strict"] = true,
                                ["schema"] = schemaNode
                            }
                        };
                    }
                    else
                    {
                        requestBody["response_format"] = new JsonObject { ["type"] = "json_object" };
                    }
                }
                catch
                {
                    requestBody["response_format"] = new JsonObject { ["type"] = "json_object" };
                }
            }
            else
            {
                requestBody["response_format"] = new JsonObject
                {
                    ["type"] = "json_object"
                };
            }
        }

        // Obtener el semáforo de concurrencia adecuado según el host y la concurrencia configurada
        var throttle = GetThrottleForEndpoint(cleanEndpoint, concurrencyLimit);
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            cts.CancelAfter(timeout.Value);
        }

        HttpResponseMessage response;
        string responseContent;

        try
        {
            const int maxAttempts = 3;
            int currentAttempt = 0;

            while (true)
            {
                currentAttempt++;
                string requestJson = requestBody.ToJsonString();

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, cleanEndpoint)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                }

                try
                {
                    response = await client.SendAsync(requestMessage, cts.Token).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    if (currentAttempt < maxAttempts && !cts.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1.5 * currentAttempt), cts.Token).ConfigureAwait(false);
                        continue;
                    }
                    throw new InvalidOperationException($"No se pudo conectar con el servidor VLM en '{cleanEndpoint}'. Asegúrate de que LM Studio o el servidor local esté en ejecución: {ex.Message}", ex);
                }

                responseContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

                // Manejo de Error 400 por rechazo de 'response_format' o 'json_schema':
                // Si falla con json_schema, degradamos a json_object. Si falla con json_object, omitimos response_format.
                if (response.StatusCode == HttpStatusCode.BadRequest && requestBody.ContainsKey("response_format"))
                {
                    var currentRf = requestBody["response_format"] as JsonObject;
                    string currentType = currentRf?["type"]?.ToString() ?? string.Empty;

                    if (string.Equals(currentType, "json_schema", StringComparison.OrdinalIgnoreCase))
                    {
                        s_unsupportedJsonSchemaCache[unsupportedCacheKey] = true;
                        requestBody["response_format"] = new JsonObject { ["type"] = "json_object" };
                        response.Dispose();
                        continue;
                    }

                    if (responseContent.Contains("response_format", StringComparison.OrdinalIgnoreCase) ||
                        responseContent.Contains("json_schema", StringComparison.OrdinalIgnoreCase) ||
                        responseContent.Contains("json_object", StringComparison.OrdinalIgnoreCase) ||
                        responseContent.Contains("schema", StringComparison.OrdinalIgnoreCase))
                    {
                        s_unsupportedResponseFormatCache[unsupportedCacheKey] = true;
                        requestBody.Remove("response_format");
                        response.Dispose();
                        continue;
                    }
                }

                // Manejo de errores transitorios 5xx (500 Channel Error, 502, 503, 504) o 400 por colapso de slot / canal en LM Studio
                // Ocurren típicamente en LM Studio cuando un slot de inferencia se reinicia o se recupera de sobrecarga.
                bool isTransient = ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 504) ||
                                   (response.StatusCode == HttpStatusCode.BadRequest &&
                                    (responseContent.Contains("channel", StringComparison.OrdinalIgnoreCase) ||
                                     responseContent.Contains("overload", StringComparison.OrdinalIgnoreCase) ||
                                     responseContent.Contains("busy", StringComparison.OrdinalIgnoreCase) ||
                                     responseContent.Contains("terminated", StringComparison.OrdinalIgnoreCase) ||
                                     responseContent.Contains("aborted", StringComparison.OrdinalIgnoreCase) ||
                                     responseContent.Contains("slot", StringComparison.OrdinalIgnoreCase)));

                if (isTransient && currentAttempt < maxAttempts && !cts.IsCancellationRequested)
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(2.0 * currentAttempt), cts.Token).ConfigureAwait(false);
                    continue;
                }

                break;
            }
        }
        finally
        {
            // Breve enfriamiento (cooldown) en endpoints locales para permitir que llama-server / LM Studio
            // libere completamente la memoria KV Cache del slot antes de admitir la siguiente inferencia en pipeline.
            if (IsLocalEndpoint(cleanEndpoint))
            {
                try
                {
                    await Task.Delay(250, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Ignorar cancelación en cooldown
                }
            }
            throttle.Release();
        }

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
                assistantText = JsonDefaults.UnescapeUnicode(contentElem.GetString() ?? string.Empty);
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
            if (IsValidJson(trimmed)) return JsonDefaults.FormatDetailsForDisplay(trimmed);
        }

        // 2. Extraer bloques de código ```json ... ```
        var match = JsonBlockRegex().Match(trimmed);
        if (match.Success)
        {
            string candidate = match.Groups[1].Value.Trim();
            if (IsValidJson(candidate)) return JsonDefaults.FormatDetailsForDisplay(candidate);
        }

        // 3. Buscar el primer '{' y el último '}'
        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            string candidate = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            if (IsValidJson(candidate)) return JsonDefaults.FormatDetailsForDisplay(candidate);
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

    /// <summary>
    /// Determina si una URL corresponde a un servidor local (localhost, 127.0.0.1, ::1 o puertos locales 1234/11434).
    /// </summary>
    public static bool IsLocalEndpoint(string endpointUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl)) return false;

        try
        {
            var uri = new Uri(endpointUrl);
            return uri.IsLoopback ||
                   string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return endpointUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                   endpointUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   endpointUrl.Contains("1234", StringComparison.OrdinalIgnoreCase) ||
                   endpointUrl.Contains("11434", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Retorna un semáforo de limitación de concurrencia adecuado para el endpoint dado.
    /// Si el usuario configuró una concurrencia específica, se respeta dicha capacidad.
    /// Por defecto, para servidores locales se usa 1 (salvo configuración explícita) y para remotos 4.
    /// </summary>
    private static SemaphoreSlim GetThrottleForEndpoint(string endpointUrl, int requestedConcurrency = 0)
    {
        string hostKey;
        bool isLocal = IsLocalEndpoint(endpointUrl);

        try
        {
            var uri = new Uri(endpointUrl);
            hostKey = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            hostKey = endpointUrl;
        }

        int count = requestedConcurrency > 0 ? requestedConcurrency : (isLocal ? 1 : 4);
        string throttleKey = $"{hostKey}::{count}";
        return s_endpointThrottles.GetOrAdd(throttleKey, _ => new SemaphoreSlim(count, count));
    }
}
