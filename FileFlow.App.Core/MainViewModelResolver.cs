namespace FileFlow.App.Core;

/// <summary>
/// Ancla del ViewModel raíz de la aplicación (el <c>MainViewModel</c> del host). Lo instalaba cada
/// host de forma implícita como <c>DataContext</c> de su ventana principal; con el núcleo portable
/// el host lo fija aquí (<c>MainViewModelResolver.Current = mainVm</c>) y los ViewModels portables
/// lo consultan cuando necesitan el editor activo sin conocer ventanas ni frameworks. Null cuando
/// nadie lo fijó (pruebas, hosts sin ventana): los consultantes ya tratan ese caso.
/// </summary>
public static class MainViewModelResolver
{
    public static FileFlow.App.ViewModels.MainViewModel? Current { get; set; }
}
