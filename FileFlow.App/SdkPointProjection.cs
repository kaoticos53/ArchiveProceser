using System;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaSize = Avalonia.Size;

namespace FileFlow.App;

/// <summary>
/// Proyección entre el <see cref="Point"/> neutro de FileFlow.Sdk y el <see cref="Avalonia.Point"/>
/// del framework. El corazón del editor (ViewModels del grafo) habla en unidades Sdk; las vistas,
/// los controles de Nodify y los eventos de puntero hablan en unidades Avalonia. Estas extensiones
/// son la única traducción permitida, y viven en el host — nunca en el ensamblado compartido.
/// </summary>
public static class SdkPointProjection
{
    public static AvaloniaPoint ToAvalonia(this Sdk.Point p) => new(p.X, p.Y);

    public static Sdk.Point ToSdk(this AvaloniaPoint p) => new(p.X, p.Y);

    public static AvaloniaSize ToAvalonia(this Sdk.Size s) => new(s.Width, s.Height);

    public static Sdk.Size ToSdk(this AvaloniaSize s) => new(s.Width, s.Height);
}
