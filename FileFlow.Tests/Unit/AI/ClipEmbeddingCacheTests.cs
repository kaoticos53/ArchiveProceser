using System;
using System.IO;
using FileFlow.Plugin.AI;
using FileFlow.Plugin.AI.Inference;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

/// <summary>
/// <b>La caché de embeddings de CLIP: dos preguntas y un mundo que cambia entre ellas.</b>
///
/// <para>Los prompts de la detección de vocabulario abierto (YOLO-World) se traducen a vectores con el modelo
/// CLIP si está descargado, y con una proyección semántica determinista si no lo está. Los dos caminos dan
/// vectores distintos para el mismo prompt —uno mira la imagen real, el otro la proyecta—, y el resultado se
/// guarda en un estático que vive todo el proceso. Guardado sin más, la entrada creada «sin modelo» seguía
/// contestando después de descargar el modelo de 65 MB: el flujo se ejecutaba con los vectores sintéticos hasta
/// reiniciar la aplicación, con el modelo ya en disco.</para>
///
/// <para>La caché recuerda ahora <b>de qué entorno salió cada vector</b>, así que el cambio de mundo la invalida
/// sola. Es la misma lección de <c>PluginStateAcrossExecutionsTests</c>, aplicada a los modelos cargados: lo que
/// sobrevive a una ejecución tiene que saber cuándo dejó de describir la realidad.</para>
/// </summary>
[Collection(OnnxInferenceCollection.Name)]
public class ClipEmbeddingCacheTests : IDisposable
{
    private readonly string _modelsDirectory;

    public ClipEmbeddingCacheTests()
    {
        _modelsDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_ClipModels_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_modelsDirectory);

        AiModelCatalog.ModelsDirectoryOverride = _modelsDirectory;
        ClipEmbeddingDatabase.ClearEmbeddingCache();
    }

    public void Dispose()
    {
        AiModelCatalog.ModelsDirectoryOverride = null;
        ClipEmbeddingDatabase.ClearEmbeddingCache();

        if (Directory.Exists(_modelsDirectory))
        {
            try { Directory.Delete(_modelsDirectory, true); } catch { }
        }
    }

    [Fact]
    public void AVectorFromAWorldWithoutModel_ShouldNotAnswerOnceTheModelArrives()
    {
        // 1) Sin modelo: el vector sale de la proyección determinista y se guarda para no repetir el cálculo.
        float[] withoutModel = ClipEmbeddingDatabase.GetClipTextEmbedding("cat");
        withoutModel.Should().NotBeEmpty();
        ClipEmbeddingDatabase.CachedEmbeddingCount.Should().Be(1);

        ClipEmbeddingDatabase.GetClipTextEmbedding("cat").Should().BeSameAs(withoutModel,
            "el segundo prompt no cambia nada en el mundo: la caché contesta con lo que ya calculó");
        ClipEmbeddingDatabase.CacheHits.Should().Be(1);
        ClipEmbeddingDatabase.EnvironmentInvalidations.Should().Be(0);

        // 2) El usuario descarga el modelo a mitad de sesión: aparece en disco con el nombre del catálogo.
        string modelFileName = AiModelManager.Catalog["clip-vit-b32"].FileName;
        string modelPath = Path.Combine(_modelsDirectory, modelFileName);
        File.WriteAllBytes(modelPath, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]);
        File.Exists(modelPath).Should().BeTrue("la prueba escribe el modelo del catálogo en el directorio que el plugin consulta");

        // 3) El mismo prompt: el vector de antes se calculó en un mundo sin modelo y ya no describe este.
        float[] withModel = ClipEmbeddingDatabase.GetClipTextEmbedding("cat");

        ClipEmbeddingDatabase.EnvironmentInvalidations.Should().Be(1,
            "el entorno cambió bajo la entrada —el modelo está en disco— y la caché tiene que darse cuenta: si " +
            "no, el modelo descargado no se usaría hasta reiniciar la aplicación");
        withModel.Should().NotBeSameAs(withoutModel, "el vector se vuelve a calcular para el mundo que hay ahora");

        // 4) Y vuelve a haber caché, ahora de la entrada que sí describe este mundo.
        ClipEmbeddingDatabase.GetClipTextEmbedding("cat").Should().BeSameAs(withModel);
        ClipEmbeddingDatabase.CacheHits.Should().Be(2);
    }

    [Fact]
    public void TheSamePrompt_ShouldOnlyBeComputedOnce()
    {
        // El otro lado de la caché: mientras el mundo no cambie, el prompt se calcula una vez y se reutiliza. Es
        // el control de los dos casos anteriores —con la caché vaciada y el mismo directorio de modelos— y el
        // recordatorio de que invalidar de más tampoco vale: un vector recalculado por prompt tira la caché.
        float[] first = ClipEmbeddingDatabase.GetClipTextEmbedding("bicycle");
        float[] second = ClipEmbeddingDatabase.GetClipTextEmbedding("bicycle");
        float[] third = ClipEmbeddingDatabase.GetClipTextEmbedding("BICYCLE");

        second.Should().BeSameAs(first);
        third.Should().BeSameAs(first, "el prompt se normaliza: mayúsculas y espacios son la misma clave");
        ClipEmbeddingDatabase.CachedEmbeddingCount.Should().Be(1);
        ClipEmbeddingDatabase.CacheHits.Should().Be(2);
        ClipEmbeddingDatabase.EnvironmentInvalidations.Should().Be(0, "el mundo no cambió entre las tres preguntas");
    }

    [Fact]
    public void AFileNamedWithTheModelId_ShouldNotCountAsTheDownloadedModel()
    {
        // El catálogo identifica el modelo por su id ('clip-vit-b32') y lo descarga con su nombre de fichero
        // ('clip-vit-base-patch32.onnx'). Resolverlo con el id daba una ruta que no existe nunca, así que la rama
        // del modelo real estaba muerta con el modelo ya descargado. Este caso distingue los dos nombres: un
        // fichero con el id <b>no</b> es el modelo, y el fichero del catálogo sí se anuncia como tal.
        string modelFileName = AiModelManager.Catalog["clip-vit-b32"].FileName;
        modelFileName.Should().NotBe("clip-vit-b32",
            "si el id y el nombre de fichero coincidieran, esta prueba dejaría de distinguir por cuál se resuelve");

        File.WriteAllBytes(Path.Combine(_modelsDirectory, "clip-vit-b32"), [0x00, 0x01, 0x02, 0x03]);

        ClipEmbeddingDatabase.GetClipTextEmbedding("bicycle");
        ClipEmbeddingDatabase.GetClipTextEmbedding("bicycle");
        ClipEmbeddingDatabase.CacheHits.Should().Be(1, "el vector se guardó en el entorno sin modelo, que es el que hay");

        File.WriteAllBytes(Path.Combine(_modelsDirectory, modelFileName), [0x00, 0x01, 0x02, 0x03]);
        ClipEmbeddingDatabase.GetClipTextEmbedding("bicycle");

        ClipEmbeddingDatabase.EnvironmentInvalidations.Should().Be(1,
            "acaba de aparecer el fichero del catálogo: el vector se había guardado sin modelo, y si el plugin " +
            "hubiera tomado por modelo el fichero del id, no habría nada que invalidar");
    }
}
