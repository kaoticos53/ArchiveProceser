using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

[Collection("AppPaths")]
public class GlobalOutputDirTests
{
    [Fact]
    public void ResolveOutputPath_WithRelativePath_AnchorsUnderGlobalOutputDir()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        string resolved = ParameterHelper.ResolveOutputPath("Converted/{FileNameNoExt}.zip", item);

        Assert.Equal(@"D:\FileFlowOutput\Converted\SampleFile.zip", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithAbsolutePath_KeepsAbsolutePathUnchanged()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        string resolved = ParameterHelper.ResolveOutputPath(@"E:\CustomLocation\Output.zip", item);

        Assert.Equal(@"E:\CustomLocation\Output.zip", resolved);
    }

    [Fact]
    public void VariableTemplateResolver_ResolvesGlobalOutputDirToken()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        string resolved = VariableTemplateResolver.Resolve("{GlobalOutputDir}/Exports/{FileName}", item);

        Assert.Equal(@"D:\FileFlowOutput/Exports/SampleFile.pdf", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithoutGlobalOutputDir_AnchorsUnderSourceDirectory()
    {
        // Arrange - File directly in d:\pepe\
        var item = new FileItemContext(@"d:\pepe\archivo.txt");

        // Act - Pattern with {RelativeDir}\Output
        string resolved = ParameterHelper.ResolveOutputPath(@"{RelativeDir}\Output", item);

        // Assert - RelativeDir is empty, so Output is anchored directly to d:\pepe\Output
        Assert.Equal(@"d:\pepe\Output", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithSubdirectoryAndSourceRootPath_AnchorsCorrectly()
    {
        // Arrange - File in subfolder d:\pepe\sub1\sub2\archivo.txt with SourceRootPath = d:\pepe
        var item = new FileItemContext(@"d:\pepe\sub1\sub2\archivo.txt");
        item.Metadata["SourceRootPath"] = @"d:\pepe";

        // Act
        string resolved = ParameterHelper.ResolveOutputPath(@"{RelativeDir}\Output", item);

        // Assert - RelativeDir is sub1\sub2, anchored under d:\pepe -> d:\pepe\sub1\sub2\Output
        Assert.Equal(@"d:\pepe\sub1\sub2\Output", resolved);
    }

    [Fact]
    public void VariableTemplateResolver_ResolvesAllGlobalOutputDirAliases()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{GlobalOutputDir}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{DefaultOutputDir}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{DefaultGlobalOutputDir}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{GlobalOutputPath}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{DefaultOutputPath}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{GlobalOutput}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{DefaultOutput}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("{OutputDir}", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("<GlobalOutputDir>", item));
        Assert.Equal(@"D:\FileFlowOutput", VariableTemplateResolver.Resolve("<DefaultOutputDir>", item));
    }

    [Fact]
    public void VariableTemplateResolver_WithoutExplicitMetadata_FallsBackToAppPathsDefault()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");

        string resolved = VariableTemplateResolver.Resolve("{GlobalOutputDir}", item);

        Assert.Equal(FileFlow.Sdk.Storage.AppPaths.DefaultGlobalOutputDir, resolved);
    }

    /// <summary>
    /// <b>La carpeta de salida del flujo es una carpeta, no una plantilla.</b> El catálogo de ejemplos entero la
    /// declara como <c>{RelativeDir}</c>, y ahí «la salida del flujo» significa la estructura del origen: sin
    /// expandirla y anclarla, <c>{GlobalOutputDir}</c> valía la plantilla literal, el anclaje la combinaba consigo
    /// mismo y la ruta resultante acababa absolutizada contra el directorio de trabajo del proceso. Medido antes de
    /// la cura: <c>bin/Debug/net10.0/{RelativeDir}/{RelativeDir}</c>.
    /// </summary>
    [Fact]
    public void ResolveOutputPath_WithGlobalOutputDirDeclaredAsATemplate_ExpandsAndAnchorsItUnderTheSourceRoot()
    {
        var item = new FileItemContext(@"D:\Fuente\sub\pepe.txt");
        item.Metadata["SourceRootPath"] = @"D:\Fuente";
        item.Metadata["GlobalOutputDir"] = "{RelativeDir}";

        Assert.Equal(@"D:\Fuente\sub", ParameterHelper.ResolveOutputPath("{GlobalOutputDir}", item));
        Assert.Equal(@"D:\Fuente\sub\Archivado",
            ParameterHelper.ResolveOutputPath("{GlobalOutputDir}/Archivado", item));
    }

    /// <summary>
    /// <b>La variable vale una carpeta en cualquier parámetro, no sólo en los que son rutas.</b> El destino por
    /// omisión del compresor es `{GlobalOutputDir}`, pero la variable se escribe también en mensajes de registro, en
    /// asuntos de notificación o en expresiones, y ahí no pasa por `ResolveOutputPath`: hasta el hito 210 devolvía el
    /// texto declarado —una plantilla— y quien lo leyera se encontraba `{RelativeDir}` dentro de su texto o de su
    /// ruta. La expansión y el anclaje viven ahora en la variable, así que vale lo mismo la lea quien la lea.
    /// </summary>
    [Fact]
    public void VariableTemplateResolver_WithATemplateOutputFolder_ResolvesAFolderInAnyParameter()
    {
        var item = new FileItemContext(@"D:\Fuente\sub\pepe.txt");
        item.Metadata["SourceRootPath"] = @"D:\Fuente";
        item.Metadata["GlobalOutputDir"] = "{RelativeDir}";

        Assert.Equal(@"D:\Fuente\sub", VariableTemplateResolver.Resolve("{GlobalOutputDir}", item));
        Assert.Equal(@"Guardado en D:\Fuente\sub",
            VariableTemplateResolver.Resolve("Guardado en {GlobalOutputDir}", item));
    }

    /// <summary>
    /// Un flujo puede declarar su salida en términos de sí misma (`{GlobalOutputDir}/sub`): la expansión tiene que
    /// terminar —con la salida por defecto, que es el último escalón— en vez de llamarse a sí misma sin fin.
    /// </summary>
    [Fact]
    public void VariableTemplateResolver_WithAnOutputFolderThatNamesItself_ShouldTerminate()
    {
        var item = new FileItemContext(@"D:\Fuente\sub\pepe.txt");
        item.Metadata["GlobalOutputDir"] = "{GlobalOutputDir}/sub";

        string resolved = VariableTemplateResolver.Resolve("{GlobalOutputDir}", item);

        Assert.True(Path.IsPathFullyQualified(resolved), $"la salida declarada tiene que terminar en una carpeta: '{resolved}'");
        Assert.DoesNotContain("{", resolved, StringComparison.Ordinal);
    }

    /// <summary>
    /// Y sin origen contra el que anclarla —el ítem no trae carpeta—, la salida global tampoco cae en el directorio
    /// de trabajo del proceso: se ancla en la salida por defecto de los ajustes, que siempre es una ruta completa.
    /// </summary>
    [Fact]
    public void ResolveOutputPath_WithATemplateGlobalOutputDirAndNoOrigin_AnchorsUnderTheSettingsOutputFolder()
    {
        var item = new FileItemContext();
        item.Metadata["GlobalOutputDir"] = "{RelativeDir}";

        string resolved = ParameterHelper.ResolveOutputPath("{GlobalOutputDir}", item);

        Assert.Equal(FileFlow.Sdk.Storage.AppPaths.DefaultGlobalOutputDir, resolved);
        Assert.DoesNotContain("{RelativeDir}", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveOutputPath_WithRelativeDirPattern_EvenWithGlobalOutputDir_AnchorsUnderSourceDirectory()
    {
        // Arrange - File from Downloads\Test images, GlobalOutputDir set to D:\Salida
        var item = new FileItemContext(@"D:\Users\ricardo\Downloads\---- Test imagenes\foto.jpg");
        item.Metadata["SourceRootPath"] = @"D:\Users\ricardo\Downloads\---- Test imagenes";
        item.Metadata["GlobalOutputDir"] = @"D:\Users\ricardo\Downloads\-- Salida";

        // Act - DestinationSink pattern with {RelativeDir}\Output
        string resolved = ParameterHelper.ResolveOutputPath(@"{RelativeDir}\Output", item);

        // Assert - MUST anchor under SourceRootPath (Downloads\Test imagenes\Output), NOT inside GlobalOutputDir
        Assert.Equal(@"D:\Users\ricardo\Downloads\---- Test imagenes\Output", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithGlobalOutputDirPattern_ResolvesToGlobalOutputDir()
    {
        // Arrange - File from Downloads\Test images, GlobalOutputDir set to D:\Salida
        var item = new FileItemContext(@"D:\Users\ricardo\Downloads\---- Test imagenes\foto.jpg");
        item.Metadata["SourceRootPath"] = @"D:\Users\ricardo\Downloads\---- Test imagenes";
        item.Metadata["GlobalOutputDir"] = @"D:\Users\ricardo\Downloads\-- Salida";

        // Act - BackgroundRemover pattern with {GlobalOutputDir}\procesado
        string resolved = ParameterHelper.ResolveOutputPath(@"{GlobalOutputDir}\procesado", item);

        // Assert - MUST resolve to D:\Users\ricardo\Downloads\-- Salida\procesado
        Assert.Equal(@"D:\Users\ricardo\Downloads\-- Salida\procesado", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithRelativeDir_AfterIntermediateNodeChangedCurrentPath_MaintainsSourceRootRelative()
    {
        // Arrange - Item whose CurrentPath changed after an AI node (e.g. BackgroundRemover placed it in GlobalOutputDir\procesado)
        var item = new FileItemContext(@"D:\Users\ricardo\Downloads\-- Salida\procesado\foto_nobg.png");
        item.OriginalPath = @"D:\Users\ricardo\Downloads\---- Test imagenes\SubFolder\foto.jpg";
        item.Metadata["SourceRootPath"] = @"D:\Users\ricardo\Downloads\---- Test imagenes";
        item.Metadata["GlobalOutputDir"] = @"D:\Users\ricardo\Downloads\-- Salida";

        // Act - DestinationSink pattern with {RelativeDir}\Output
        string resolved = ParameterHelper.ResolveOutputPath(@"{RelativeDir}\Output", item);

        // Assert - MUST resolve relative to original SourceRootPath + SubFolder -> D:\Users\ricardo\Downloads\---- Test imagenes\SubFolder\Output
        Assert.Equal(@"D:\Users\ricardo\Downloads\---- Test imagenes\SubFolder\Output", resolved);
    }
}
