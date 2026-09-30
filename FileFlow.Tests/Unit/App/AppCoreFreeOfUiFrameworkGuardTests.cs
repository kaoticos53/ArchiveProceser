using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de la migración: el núcleo portable <c>FileFlow.App.Core</c> debe vivir sin Avalonia.
/// Mira paquetes en el csproj, usings y referencias de tipo en el C#. Los comentarios que citan
/// "Avalonia" para explicar de dónde viene algo son legítimos: la guardia no lee prosa, sólo código.
/// Es la hermana de <see cref="UnoHostFreeOfUiFrameworkGuardTests"/>: entre las dos atienden que el
/// producto tenga dos hosts y un único núcleo, sin fugas del framework de uno al otro.
/// </summary>
public class AppCoreFreeOfUiFrameworkGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FileFlow.slnx")))
        {
            dir = dir.Parent!;
        }
        return dir!.FullName;
    }

    private static IEnumerable<string> SourceFiles(string extension, string subdirectory)
    {
        var root = Path.Combine(RepoRoot(), "FileFlow.App.Core", subdirectory);
        return Directory.EnumerateFiles(root, "*" + extension, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void AppCore_HasNoUiFrameworkPackages()
    {
        foreach (var csproj in SourceFiles(".csproj", "."))
        {
            string text = File.ReadAllText(csproj);
            Assert.DoesNotContain("Avalonia", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Uno", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Microsoft.UI", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AppCore_HasNoUiFrameworkReferencesInCsharp()
    {
        foreach (var cs in SourceFiles(".cs", "."))
        {
            string text = File.ReadAllText(cs);
            Assert.DoesNotContain("using Avalonia", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Avalonia.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("using Microsoft.UI", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.UI.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Windows.UI", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AppCore_ShouldBeReferencedByUnoHost()
    {
        string repoRoot = RepoRoot();
        string hostCsproj = Path.Combine(repoRoot, "FileFlow.App.Uno", "FileFlow.App.Uno.csproj");
        Assert.True(File.Exists(hostCsproj), $"No se encontró el csproj del host: {hostCsproj}");
        Assert.Contains("FileFlow.App.Core.csproj", File.ReadAllText(hostCsproj), StringComparison.Ordinal);
    }
}
