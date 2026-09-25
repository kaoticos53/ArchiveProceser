using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Payload del diálogo de variables: lo que el ViewModel debe entregar para que el host construya la
/// ventana sin conocerla. Vive en el núcleo portable: lo publica el ViewModel y cada host lo consume
/// para construir su ventana concreta.
/// </summary>
public sealed record VariablePickerRequest(
    System.Collections.Generic.IEnumerable<VariableGroupItem>? Groups,
    NodeViewModel? TargetNode,
    FileItemContext? PreviewContext,
    ILocalizationService? Localization);
