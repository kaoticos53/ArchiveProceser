using Avalonia.Controls;

namespace FileFlow.App.Views.Components;

/// <summary>
/// Gestor de modelos de IA (lista, estado de instalación y descargas).
///
/// Es una pieza compartida: la pestaña «Modelos de IA» de los ajustes la hospeda en línea y el asistente de
/// descarga la envuelve en su propia ventana. No tiene lógica propia — todo el estado vive en
/// <see cref="ViewModels.AiModelManagerViewModel"/>, que se recibe por <c>DataContext</c>.
/// </summary>
public partial class AiModelManagerView : UserControl
{
    public AiModelManagerView()
    {
        InitializeComponent();
    }
}
