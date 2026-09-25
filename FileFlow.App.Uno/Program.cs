using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Microsoft.UI.Xaml.Application.Start(_ => new App());
        return 0;
    }
}
