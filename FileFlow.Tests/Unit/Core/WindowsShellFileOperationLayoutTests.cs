using System;
using System.Reflection;
using System.Runtime.InteropServices;
using FileFlow.Core.Platform;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// <b>La forma con la que se declara <c>SHFILEOPSTRUCT</c> es la que tiene en Windows, y de eso depende que la
/// papelera no se lleve el proceso por delante.</b>
///
/// <para>El hito 204 ejecutó de punta a punta el ejemplo 10 del catálogo —«eliminación segura enviando a la
/// papelera de reciclaje»— y el host de pruebas murió con una violación de acceso
/// (<c>0xC0000005</c>) dentro de <c>SHFileOperation</c>. La causa no era la llamada, era la declaración: el
/// struct estaba con <c>Pack = 1</c>, así que los punteros quedaban sin alinear a palabra —<c>pFrom</c> en el
/// desplazamiento 12 en vez del 16— y la parte nativa leía ahí un puntero inventado. Un fallo de la pila nativa
/// no es una excepción que el código pueda atrapar: se lleva el proceso.</para>
///
/// <para>La prueba no necesita llamar a la papelera —ni Windows, ni una sesión de escritorio—: compara el
/// <b>diseño declarado</b> con el que declara el SDK, campo por campo y en tamaño. Es la forma del struct lo que
/// se juzga, y es exactamente el defecto que se encontró. Se lee por reflexión para no abrir la visibilidad de
/// un tipo nativo privado.</para>
/// </summary>
public class WindowsShellFileOperationLayoutTests
{
    /// <summary>
    /// <c>SHFILEOPSTRUCTW</c> según el SDK de Windows (<c>shellapi.h</c>), con la alineación natural: es lo que
    /// la parte nativa espera encontrar al recibir el puntero. Los mismos tipos y el mismo orden que la
    /// declaración del producto, y ninguno de los dos cae en un <c>Pack</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeShFileOperation
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    private static readonly string[] Fields =
    [
        "hwnd",
        "wFunc",
        "pFrom",
        "pTo",
        "fFlags",
        "fAnyOperationsAborted",
        "hNameMappings",
        "lpszProgressTitle"
    ];

    private static Type DeclaredStruct() =>
        typeof(WindowsPlatformService).GetNestedType("SHFILEOPSTRUCT", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "WindowsPlatformService ya no declara el struct de SHFileOperation: actualiza esta guardia antes de dar la papelera por cubierta.");

    [Fact]
    public void TheDeclaredStruct_ShouldPlaceEveryFieldWhereWindowsExpectsIt()
    {
        Type declared = DeclaredStruct();

        foreach (string field in Fields)
        {
            Marshal.OffsetOf(declared, field).ToInt64().Should().Be(
                Marshal.OffsetOf<NativeShFileOperation>(field).ToInt64(),
                $"'{field}' tiene que caer en el mismo desplazamiento que en SHFILEOPSTRUCTW: la parte nativa no " +
                "recibe nombres de campo, recibe bytes");
        }

        Marshal.SizeOf(declared).Should().Be(Marshal.SizeOf<NativeShFileOperation>(),
            "un tamaño distinto significa relleno de más o de menos, y el struct que se pasa por puntero se lee " +
            "campo a campo");
    }

    [Fact]
    public void ThePointerFields_ShouldBeAlignedToWord()
    {
        // Es la propiedad que el `Pack = 1` rompía, escrita aparte del struct de referencia: si alguien vuelve a
        // apilar los campos, el desplazamiento de un puntero deja de ser múltiplo de su tamaño y la llamada nativa
        // lee una dirección inventada.
        Type declared = DeclaredStruct();

        foreach (string field in new[] { "pFrom", "pTo" })
        {
            long offset = Marshal.OffsetOf(declared, field).ToInt64();

            offset.Should().BeGreaterThan(0);
            (offset % IntPtr.Size).Should().Be(0,
                $"'{field}' es un puntero y tiene que ir alineado a palabra: sin relleno delante, la parte nativa " +
                "lee media dirección y la primera mitad del campo siguiente");
        }
    }
}
