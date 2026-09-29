using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de los <b>DIÁLOGOS DE ARCHIVO</b> del host Uno: cada picker nace ATADO a la ventana del host
/// antes de abrirse.
///
/// <para><b>Qué protege</b>: el host Uno se distribuye SIN empaquetar, y en una app sin empaquetar un
/// <c>FileSavePicker</c>/<c>FileOpenPicker</c>/<c>FolderPicker</c> de WinRT no tiene dueño propio: al abrirse
/// revienta con <b>«Invalid window handle (0x80070578)»</b> —el propio mensaje del runtime nombra la vía:
/// <c>WindowNative</c> + <c>InitializeWithWindow</c>—. Medido con el ratón antes de esta guardia: pulsar
/// «Guardar Flujo…» del cajón cerraba el cajón, no abría picker alguno y dejaba en la barra de estado
/// «flujo guardado: EXCEPCION COMException: Invalid window handle»; lo mismo esperaba a «Cargar Flujo…» y a
/// cualquier ruta de «…» que el usuario fuese a explorar. La guardia exige que <b>cada</b> picker del
/// servicio se ate antes de su <c>Pick…Async</c>, y que el atado sea el interop del runtime y no una copia
/// local.</para>
/// </summary>
public class UnoPickerOwnershipGuardTests
{
    private const string FileDialogService = "FileFlow.App.Uno/Platform/UnoFileDialogService.cs";

    /// <summary>Los constructores de picker del servicio: cada uno es un diálogo que el usuario puede abrir.</summary>
    private static readonly string[] PickerConstructors =
    [
        "new FileOpenPicker",
        "new FileSavePicker",
        "new FolderPicker"
    ];

    /// <summary>El método que abre de verdad cada picker (el punto donde un dueño ausente revienta).</summary>
    private static readonly string[] PickerLaunches =
    [
        "PickSingleFileAsync",
        "PickSaveFileAsync",
        "PickSingleFolderAsync"
    ];

    private static string Code() => SourceText.CodeWithoutComments(FileDialogService);

    [Fact]
    public void Cada_picker_del_host_se_ata_a_la_ventana_antes_de_abrirse()
    {
        string code = Code();

        var pickers = PickerConstructors
            .Select(constructor => (Constructor: constructor, Count: Regex.Matches(code, Regex.Escape(constructor)).Count))
            .ToList();

        pickers.Sum(p => p.Count).Should().BeGreaterThan(0,
            "el servicio de diálogos del host debe construir sus pickers en algún sitio");

        // El método que ata; su PROPIA definición («void OwnPicker(...)») no cuenta como uso.
        var ownershipUses = Regex.Matches(code, @"OwnPicker\s*\(")
            .Cast<Match>()
            .Count(m => !code[..m.Index].EndsWith("void ", StringComparison.Ordinal));
        ownershipUses.Should().Be(pickers.Sum(p => p.Count),
            "cada picker construido (" + string.Join(", ", pickers.Select(p => $"{p.Constructor}={p.Count}"))
            + ") tiene que pasar por el atado a la ventana; sin él, el picker lanza"
            + " «Invalid window handle (0x80070578)» al abrirse (app sin empaquetar)");

        // Y cada APERTURA tiene su atado por delante, en el mismo cuerpo: no vale atar uno y abrir otro.
        foreach (string launch in PickerLaunches)
        {
            foreach (Match match in Regex.Matches(code, Regex.Escape(launch)))
            {
                string before = code[..match.Index];
                int owned = before.LastIndexOf("OwnPicker(", StringComparison.Ordinal);
                int constructed = PickerConstructors
                    .Select(c => before.LastIndexOf(c, StringComparison.Ordinal))
                    .DefaultIfEmpty(-1)
                    .Max();
                owned.Should().BeGreaterThan(constructed,
                    $"«{launch}» se abre sobre un picker ya atado: el atado tiene que ir después de construir el"
                    + " picker y antes de abrirlo");
            }
        }
    }

    [Fact]
    public void El_atado_es_el_interop_del_runtime_y_no_una_copia_local()
    {
        string code = Code();

        code.Should().Contain("WinRT.Interop.InitializeWithWindow.Initialize",
            "el dueño se pone con el interop del runtime, que es lo que el mensaje del error pide");
        code.Should().Contain("WinRT.Interop.WindowNative.GetWindowHandle",
            "el HWND sale de la ventana del host, no de una constante ni de un handle inventado");
        code.Should().Contain("private static void OwnPicker(",
            "el atado vive en UN método: seis pickers en seis sitios es como se pierde uno");
    }

    [Fact]
    public void Ninguna_apertura_de_picker_queda_sin_dueno_en_el_servicio()
    {
        string code = Code();

        var launches = PickerLaunches
            .SelectMany(launch => Regex.Matches(code, Regex.Escape(launch)).Cast<Match>())
            .OrderBy(m => m.Index)
            .ToList();

        launches.Should().NotBeEmpty("el servicio construye pickers para abrirlos");

        var unowned = new List<int>();
        foreach (Match launch in launches)
        {
            string before = code[..launch.Index];
            int owned = before.LastIndexOf("OwnPicker(", StringComparison.Ordinal);
            int constructed = PickerConstructors
                .Select(c => before.LastIndexOf(c, StringComparison.Ordinal))
                .DefaultIfEmpty(-1)
                .Max();
            if (owned < constructed)
            {
                unowned.Add(launch.Index);
            }
        }

        unowned.Should().BeEmpty(
            "un picker abierto sin dueño no abre: deja al usuario con un cajón cerrado y una excepción en la"
            + " barra de estado (el defecto que el ratón midió antes de esta guardia)");
    }
}
