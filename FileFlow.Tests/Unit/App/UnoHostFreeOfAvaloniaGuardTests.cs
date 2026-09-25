using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de la migración: el host <c>FileFlow.App.Uno</c> debe vivir sin Avalonia.
/// Mira paquetes en el csproj, usings y referencias de tipo en el C#, y namespaces en el XAML.
/// Los comentarios que citan "Avalonia" para explicar la migración son legítimos: la guardia no
/// lee prosa, sólo código.
/// </summary>
public class UnoHostFreeOfAvaloniaGuardTests
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

    private static IEnumerable<string> SourceFiles(string extension)
    {
        var host = Path.Combine(RepoRoot(), "FileFlow.App.Uno");
        return Directory.EnumerateFiles(host, "*" + extension, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static IEnumerable<string> Csproj()
    {
        var host = Path.Combine(RepoRoot(), "FileFlow.App.Uno");
        return Directory.EnumerateFiles(host, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin"));
    }

    [Fact]
    public void UnoHost_HasNoAvaloniaPackages()
    {
        foreach (var csproj in Csproj())
        {
            Assert.DoesNotContain("Avalonia", File.ReadAllText(csproj), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UnoHost_HasNoAvaloniaInCsharp()
    {
        foreach (var cs in SourceFiles(".cs"))
        {
            var text = File.ReadAllText(cs);
            Assert.DoesNotContain("using Avalonia", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Avalonia.", text, StringComparison.Ordinal);
        }
    }
}
