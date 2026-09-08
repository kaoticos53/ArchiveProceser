namespace FileFlow.Sdk.Services;

/// <summary>
/// Implementación por defecto y fallback seguro (headless/pruebas) para <see cref="IDialogService"/>.
/// </summary>
public class NullDialogService : IDialogService
{
    private static readonly Lazy<NullDialogService> _instance = new(() => new NullDialogService());
    public static NullDialogService Instance => _instance.Value;

    public void ShowInformation(string message, string title = "FileFlow Studio") { }
    public void ShowWarning(string message, string title = "FileFlow Studio") { }
    public void ShowError(string message, string title = "Error") { }
    public bool ShowConfirmation(string message, string title = "FileFlow Studio") => true;
    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio") => DialogResult.Ok;
}
