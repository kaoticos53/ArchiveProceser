using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de atajos de la fase 3.2: <b>las claves de teclado son del producto, no de la vista</b>.
///
/// <para>El §2.1 del plan Uno las declaraba «código de vista hoy» — y así se duplicaban: el switch del
/// <c>EditorView_KeyDown</c> del Avalonia y el del lienzo Uno eran dos copias de la misma tabla que ya
/// se habían desincronizado en el 3.1 (el Uno solo tenía pan, el escritorio ya tenía spotlight con
/// posición del cursor). La cura es la <see cref="EditorKeyboardShortcuts"/>: una tabla canónica en el
/// núcleo y un ejecutor sobre el <c>EditorViewModel</c>. Esta guardia impide que vuelva a duplicarse:</para>
///
/// <list type="number">
/// <item>la tabla no puede quedar vacía ni duplicar combinaciones (misma tecla + modificadores);</item>
/// <item>el host Avalonia sólo mapea teclas que existen en la tabla, y sus handlers de teclado citan el
/// servicio (nada de switches paralelos con literales de comando);</item>
/// <item>el host Uno cita el servicio en su handler de teclado y el XAML del lienzo lo cablea;</item>
/// <item>las ventanas propias (spotlight, renombrado) conservan su teclado local, que no es del lienzo.</item>
/// </list>
/// </summary>
public class UnoShortcutParityGuardTests
{
    private const string UnoCanvas = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";
    private const string UnoCanvasXaml = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml";

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    [Fact]
    public void TheSharedTable_ShouldBeComplete_AndWithoutDuplicateCombinations()
    {
        EditorKeyboardShortcuts.Table.Should().NotBeEmpty("sin tabla no hay producto que compare");

        var combinations = EditorKeyboardShortcuts.Table
            .Select(b => (b.Key, b.Modifiers))
            .ToList();
        combinations.Should().OnlyHaveUniqueItems(
            "dos comandos sobre la misma combinación harían la tabla ambigua: el usuario no puede saber qué ejecutará");

        // Las operaciones del lienzo que el usuario aprende, todas presentes:
        var commands = EditorKeyboardShortcuts.Table.Select(b => b.Command).ToHashSet();
        commands.Should().Contain(new[]
        {
            EditorKeyboardShortcuts.ShortcutKey.Undo,
            EditorKeyboardShortcuts.ShortcutKey.Redo,
            EditorKeyboardShortcuts.ShortcutKey.Copy,
            EditorKeyboardShortcuts.ShortcutKey.Paste,
            EditorKeyboardShortcuts.ShortcutKey.Cut,
            EditorKeyboardShortcuts.ShortcutKey.Duplicate,
            EditorKeyboardShortcuts.ShortcutKey.Delete,
            EditorKeyboardShortcuts.ShortcutKey.Rename,
            EditorKeyboardShortcuts.ShortcutKey.Spotlight,
            EditorKeyboardShortcuts.ShortcutKey.Escape
        }, "la tabla es el catálogo canónico de atajos del lienzo");
    }

    [Fact]
    public void UnoHost_ShouldResolveItsKeysThroughTheSharedTable_AndHaveItWired()
    {
        string code = Code(UnoCanvas);
        code.Should().Contain("EditorKeyboardShortcuts.Resolve(",
            "el lienzo Uno clasifica con la tabla canónica");
        code.Should().Contain("EditorKeyboardShortcuts.Execute(",
            "el lienzo Uno ejecuta el comando canónico del núcleo");

        Code(UnoCanvasXaml).Should().Contain("KeyDown=\"OnKeyDown\"",
            "un handler que nadie cablea no intercepta ninguna tecla");
    }

    [Fact]
    public void TheRenameBox_ShouldKeepItsLocalKeyboard_InUnoHost()
    {
        // El renombrado (Enter/Escape dentro de la caja) y la navegación del spotlight son teclado de la
        // caja, no del lienzo: los handlers del lienzo no secuestran las teclas de un TextBox.
        Code(UnoCanvas).Should().Contain("IsTextInput(source as DependencyObject)",
            "la caja de renombrado consume sus propias teclas: Enter confirma y Escape cancela, no borra nodos");
    }

    [Fact]
    public void UnoHost_ShouldNotHardcodeAKeyTheTableDoesNotDeclare()
    {
        // Las teclas físicas que el host mapea deben existir en la tabla: si alguien añade un mapping
        // nuevo sin su entrada canónica, la tabla y el host se desincronizan en silencio.
        var tableKeys = EditorKeyboardShortcuts.Table.Select(b => b.Key).ToHashSet();

        string uno = Code(UnoCanvas);

        foreach (EditorKeyboardShortcuts.PhysicalKey key in Enum.GetValues<EditorKeyboardShortcuts.PhysicalKey>())
        {
            bool unoMapsIt = uno.Contains($"EditorKeyboardShortcuts.PhysicalKey.{key}", StringComparison.Ordinal);

            if (unoMapsIt)
            {
                tableKeys.Should().Contain(key,
                    $"'{key}' se mapea en el host pero no tiene binding canónico en la tabla compartida");
            }
        }
    }
}
