using System;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de infraestructura multiplataforma para <see cref="IDialogService"/>.
/// </summary>
public class AvaloniaDialogService : IDialogService
{
    private static readonly Lazy<AvaloniaDialogService> _instance = new(() => new AvaloniaDialogService());
    public static AvaloniaDialogService Instance => _instance.Value;

    public void ShowInformation(string message, string title = "FileFlow Studio")
    {
        Console.WriteLine($"[INFO] {title}: {message}");
    }

    public void ShowWarning(string message, string title = "FileFlow Studio")
    {
        Console.WriteLine($"[WARN] {title}: {message}");
    }

    public void ShowError(string message, string title = "Error")
    {
        Console.Error.WriteLine($"[ERROR] {title}: {message}");
    }

    public bool ShowConfirmation(string message, string title = "FileFlow Studio")
    {
        return true;
    }

    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio")
    {
        return DialogResult.Yes;
    }
}
