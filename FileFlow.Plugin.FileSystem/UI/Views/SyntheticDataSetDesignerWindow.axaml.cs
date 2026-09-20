using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.FileSystem.UI.ViewModels;

namespace FileFlow.Plugin.FileSystem.UI.Views;

/// <summary>
/// Diseñador visual de conjuntos de datos sintéticos (árbol jerárquico, DSL, JSON e inspector).
///
/// <para><b>Contrato del view model</b>: la ventana lo <b>garantiza</b>. El XAML depende por completo de sus
/// enlaces —la lista de datasets, el árbol editable, los comandos de guardar, duplicar, añadir archivo…—, y una
/// ventana sin <c>DataContext</c> no falla: simplemente se abre vacía, sin datos y sin un solo botón que haga
/// nada. Eso es exactamente lo que ocurría cuando la aplicación la construía con <c>new
/// SyntheticDataSetDesignerWindow()</c>, así que la red de seguridad vive aquí y no en cada punto de apertura.</para>
///
/// <para>Se resuelve al <b>abrir</b> (no en el constructor) para que quien sí inyecta su view model —las pruebas,
/// las capturas, cualquier apertura que conozca su estado— no pague uno de descarte.</para>
/// </summary>
public partial class SyntheticDataSetDesignerWindow : Window
{
    public SyntheticDataSetDesignerWindow()
    {
        InitializeComponent();
    }

    public SyntheticDataSetDesignerWindow(SyntheticDataSetDesignerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>View model en uso (el inyectado o el que crea la propia ventana al abrirse).</summary>
    public SyntheticDataSetDesignerViewModel? ViewModel => DataContext as SyntheticDataSetDesignerViewModel;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is null)
        {
            DataContext = new SyntheticDataSetDesignerViewModel();
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
