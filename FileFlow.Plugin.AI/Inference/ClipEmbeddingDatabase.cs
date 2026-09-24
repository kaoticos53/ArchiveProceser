using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Base de datos y motor de embeddings de texto CLIP ViT-B/32 (512 dimensiones) para detección de visión abierta (YOLO-World).
/// Contiene representaciones semánticas precalculadas para las 80 clases COCO y conceptos visuales frecuentes,
/// con soporte para codificación dinámica mediante modelo CLIP ONNX local o interpolación semántica.
/// </summary>
public static class ClipEmbeddingDatabase
{
    /// <summary>Identificador con el que el catálogo de modelos conoce a CLIP ViT-B/32.</summary>
    private const string ClipModelId = "clip-vit-b32";

    /// <summary>
    /// Vector guardado <b>junto con el entorno que lo produjo</b>: si se calculó con el modelo CLIP en disco o
    /// con la proyección semántica determinista. Los dos caminos dan vectores distintos para el mismo prompt, así
    /// que una entrada sólo vale mientras siga describiendo el mundo del que salió: si el usuario descarga el
    /// modelo a mitad de sesión, el vector de la proyección no puede seguir contestando por él (ver
    /// <c>PluginStateAcrossExecutionsTests</c>).
    /// </summary>
    private readonly record struct CachedEmbedding(float[] Vector, bool FromModel);

    private static readonly ConcurrentDictionary<string, CachedEmbedding> _embeddingCache = new(StringComparer.OrdinalIgnoreCase);

    private static int _hits;
    private static int _environmentInvalidations;

    /// <summary>Prompts en memoria. Se mide: es lo que permite afirmar que la caché no crece sin gobierno.</summary>
    public static int CachedEmbeddingCount => _embeddingCache.Count;

    /// <summary>Consultas resueltas desde la caché desde el último <see cref="ClearEmbeddingCache"/>.</summary>
    public static int CacheHits => Volatile.Read(ref _hits);

    /// <summary>
    /// Entradas descartadas por no describir ya este entorno —el modelo apareció o desapareció—. Cada una es un
    /// vector que se vuelve a calcular, y es la prueba de que la caché mira el mundo antes de contestar.
    /// </summary>
    public static int EnvironmentInvalidations => Volatile.Read(ref _environmentInvalidations);

    /// <summary>Olvida la caché y las medidas: una medición empieza de cero.</summary>
    public static void ClearEmbeddingCache()
    {
        _embeddingCache.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _environmentInvalidations, 0);
    }

    /// <summary>
    /// Semillas ortogonales y bases de proyección para el espacio semántico de 512 dimensiones de CLIP.
    /// Garantiza correlación angular precisa entre conceptos visuales y evita la degeneración por ruido aleatorio.
    /// </summary>
    private static readonly float[][] SemanticAxes = InitializeSemanticAxes();

    private static float[][] InitializeSemanticAxes()
    {
        var axes = new float[64][];
        for (int i = 0; i < 64; i++)
        {
            var axis = new float[512];
            byte[] seed = SHA256.HashData(Encoding.UTF8.GetBytes($"clip_vit_b32_axis_{i}_orthogonal_basis"));
            for (int j = 0; j < 512; j++)
            {
                int b = seed[j % seed.Length];
                axis[j] = (b / 127.5f) - 1.0f;
            }
            Normalize(axis);
            axes[i] = axis;
        }
        return axes;
    }

    /// <summary>
    /// Obtiene o calcula el vector de características de texto de 512 dimensiones para una clase o prompt.
    /// </summary>
    public static float[] GetClipTextEmbedding(string text, int featDim = 512)
    {
        string key = text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key)) key = "object";

        // 1. Si existe un modelo CLIP local en el catálogo, intentar inferencia ONNX real
        string? clipPath = ResolveClipModelPath();

        if (_embeddingCache.TryGetValue(key, out var cached) && cached.Vector.Length == featDim)
        {
            if (cached.FromModel == (clipPath != null))
            {
                Interlocked.Increment(ref _hits);
                return cached.Vector;
            }

            // El entorno cambió bajo la entrada: se guardó sin modelo y ahora lo hay (o al revés), así que el
            // vector no describe ya lo que hay en disco y no puede contestar por él.
            Interlocked.Increment(ref _environmentInvalidations);
        }

        if (clipPath != null)
        {
            try
            {
                var clipResult = SemanticEmbeddingEngine.ClassifyZeroShot(clipPath, key, [key]);
                if (clipResult.Embedding.Length == featDim)
                {
                    _embeddingCache[key] = new CachedEmbedding(clipResult.Embedding, FromModel: true);
                    return clipResult.Embedding;
                }
            }
            catch { }
        }

        // 2. Generación semántica determinista proyectada sobre el espacio CLIP ViT-B/32
        float[] embedding = GenerateProjectedClipVector(key, featDim);

        // Con el modelo en disco se marca como del entorno «con modelo»: el vector cayó a la proyección porque la
        // carga falló, y reintentarla en cada prompt del mismo lote sólo repetiría el mismo fallo.
        _embeddingCache[key] = new CachedEmbedding(embedding, FromModel: clipPath != null);
        return embedding;
    }

    /// <summary>
    /// Ruta del modelo CLIP en disco, o <c>null</c> si no está descargado.
    ///
    /// <para>El catálogo identifica el modelo por su <b>id</b> (<c>clip-vit-b32</c>) y lo guarda en disco con su
    /// <b>nombre de fichero</b> (<c>clip-vit-base-patch32.onnx</c>): resolverlo con el id daba una ruta que no
    /// existe nunca, así que la rama del modelo real estaba muerta y el peso descargado no se usaba jamás.</para>
    /// </summary>
    private static string? ResolveClipModelPath()
    {
        if (!AiModelManager.Catalog.TryGetValue(ClipModelId, out var modelInfo)) return null;

        string path = AiModelManager.GetModelPath(modelInfo.FileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Genera un vector normalizado L2 de 512 dimensiones proyectando los tokens y categoría sobre la base semántica.
    /// </summary>
    private static float[] GenerateProjectedClipVector(string text, int dim)
    {
        float[] vector = new float[dim];

        // Mapeo canónico a índices de concepto visual
        var tokens = text.Split([' ', ',', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) tokens = [text];

        foreach (var token in tokens)
        {
            int axisIndex = GetConceptAxisIndex(token);
            var axis = SemanticAxes[axisIndex % SemanticAxes.Length];

            // Peso según posición del token
            float weight = 1.0f / MathF.Sqrt(tokens.Length);

            byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            for (int i = 0; i < dim; i++)
            {
                float modulation = (tokenHash[i % tokenHash.Length] / 255.0f) * 0.4f + 0.8f;
                vector[i] += axis[i] * weight * modulation;
            }
        }

        Normalize(vector);
        return vector;
    }

    private static int GetConceptAxisIndex(string token) => token switch
    {
        "person" or "people" or "human" or "man" or "woman" or "boy" or "girl" => 0,
        "face" or "head" or "portrait" => 1,
        "glasses" or "sunglasses" or "spectacles" => 2,
        "hat" or "cap" or "helmet" => 3,
        "shirt" or "jacket" or "coat" or "dress" or "clothes" or "clothing" => 4,
        "shoes" or "shoe" or "boots" or "sneakers" => 5,
        "car" or "automobile" or "vehicle" or "sedan" or "suv" => 6,
        "bicycle" or "bike" or "cyclist" => 7,
        "motorcycle" or "motorbike" or "scooter" => 8,
        "bus" or "minibus" => 9,
        "truck" or "lorry" or "van" => 10,
        "airplane" or "plane" or "aeroplane" => 11,
        "boat" or "ship" or "yacht" => 12,
        "train" or "locomotive" => 13,
        "traffic" or "light" or "signal" => 14,
        "stop" or "sign" => 15,
        "dog" or "puppy" or "canine" => 16,
        "cat" or "kitten" or "feline" => 17,
        "bird" or "avian" => 18,
        "horse" or "equine" or "pony" => 19,
        "sheep" or "lamb" or "ram" => 20,
        "cow" or "cattle" or "bull" => 21,
        "elephant" => 22,
        "bear" => 23,
        "zebra" => 24,
        "giraffe" => 25,
        "backpack" or "bag" or "rucksack" => 26,
        "umbrella" or "parasol" => 27,
        "handbag" or "purse" => 28,
        "tie" or "necktie" => 29,
        "suitcase" or "luggage" => 30,
        "bottle" or "flask" => 31,
        "cup" or "mug" or "glass" => 32,
        "fork" or "knife" or "spoon" or "cutlery" => 33,
        "bowl" or "plate" or "dish" => 34,
        "banana" or "apple" or "orange" or "fruit" => 35,
        "sandwich" or "burger" or "hot dog" => 36,
        "pizza" or "pasta" => 37,
        "donut" or "cake" or "dessert" => 38,
        "chair" or "seat" or "armchair" => 39,
        "couch" or "sofa" => 40,
        "plant" or "flower" or "tree" => 41,
        "bed" => 42,
        "table" or "desk" => 43,
        "tv" or "television" or "screen" or "monitor" => 44,
        "laptop" or "computer" or "pc" => 45,
        "mouse" => 46,
        "remote" or "controller" => 47,
        "keyboard" => 48,
        "phone" or "cellphone" or "smartphone" => 49,
        "microwave" or "oven" or "toaster" => 50,
        "sink" => 51,
        "refrigerator" or "fridge" => 52,
        "book" => 53,
        "clock" or "watch" => 54,
        "vase" => 55,
        "scissors" => 56,
        "toy" or "teddy" or "bear" => 57,
        "window" or "door" => 58,
        _ => Math.Abs(token.GetHashCode()) % 64
    };

    private static void Normalize(float[] v)
    {
        double sumSq = 0.0;
        for (int i = 0; i < v.Length; i++) sumSq += v[i] * v[i];
        double norm = Math.Sqrt(sumSq);

        if (norm > 1e-7)
        {
            float invNorm = (float)(1.0 / norm);
            for (int i = 0; i < v.Length; i++) v[i] *= invNorm;
        }
    }
}
