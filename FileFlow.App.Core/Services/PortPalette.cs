using System;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Paleta semantica de puertos: tipo de dato, color RGB. Los mismos bytes que pinta el
/// escritorio en Styles/Ports.axaml (archivos=exito, texto=cian, booleano=ambar,
/// numerico=primario, binario=purpura, coleccion=error, any=glow).
///
/// <para>Decision de producto (que color habla cada tipo y como se comporta durante el
/// arrastre), no del framework: los hosts la consumen para construir sus pinceles nativos.
/// La forma del socket la resuelve PortViewModel.SocketShape; aqui vive el color y el
/// comportamiento de arrastre.</para>
/// </summary>
public static class PortPalette
{
    /// <summary>RGB por tipo de dato, los mismos bytes que el escritorio.</summary>
    public static (byte R, byte G, byte B) TypeColor(PortTypeKind kind) => kind switch
    {
        PortTypeKind.Files => (0x10, 0xB9, 0x81),
        PortTypeKind.Text => (0x06, 0xB6, 0xD4),
        PortTypeKind.Boolean => (0xF5, 0x9E, 0x0B),
        PortTypeKind.Number => (0x63, 0x66, 0xF1),
        PortTypeKind.Binary => (0xA8, 0x55, 0xF7),
        PortTypeKind.Collection => (0xEF, 0x44, 0x44),
        _ => (0x81, 0x8C, 0xF8)
    };

    /// <summary>El fondo del socket libre: BgDark del tema.</summary>
    public static (byte R, byte G, byte B) FreeFill => (0x0D, 0x11, 0x17);

    /// <summary>El acento primario: el origen del arrastre se rellena con el.</summary>
    public static (byte R, byte G, byte B) DragSourceFill => (0x63, 0x66, 0xF1);

    /// <summary>El blanco del TextOnAccent: el origen del arrastre lo usa de borde.</summary>
    public static (byte R, byte G, byte B) DragSourceBorder => (0xFF, 0xFF, 0xFF);
}
