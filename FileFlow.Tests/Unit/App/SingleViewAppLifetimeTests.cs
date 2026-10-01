using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FileFlow.App;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views;
using FileFlow.Tests.TestHelpers;
using FileFlow.Tests.Unit.Views;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Pruebas del ciclo de vida de vista única (<see cref="ISingleViewApplicationLifetime"/>),
/// utilizado en navegadores Web (WebAssembly / Avalonia.Browser) y plataformas embebidas.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class SingleViewAppLifetimeTests
{
    [Fact]
    public void AppSource_ShouldContainSingleViewLifetimeHandling()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        string appPath = Path.Combine(root, "FileFlow.App", "App.axaml.cs");
        string code = File.ReadAllText(appPath);

        code.Should().Contain("ISingleViewApplicationLifetime singleView",
            "el arranque de la aplicación debe soportar hosts de vista única como WebAssembly");
        code.Should().Contain("singleView.MainView = mainView",
            "el host debe asignar MainView al lifetime");
    }

    [Fact]
    public void MainView_CanBeInstantiatedAndBoundToMainViewModel()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var services = new ServiceCollection();
            services.AddFileFlowServices();
            var provider = services.BuildServiceProvider();

            var mainVm = provider.GetRequiredService<MainViewModel>();
            var mainView = new MainView
            {
                DataContext = mainVm
            };

            mainView.Should().NotBeNull();
            mainView.DataContext.Should().BeSameAs(mainVm);
            mainView.Content.Should().NotBeNull("el contenido raíz de MainView debe estar instanciado");
        });
    }

    [Fact]
    public void BrowserProject_ShouldExistAndTargetBrowserWasm()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        string browserProjPath = Path.Combine(root, "FileFlow.App.Browser", "FileFlow.App.Browser.csproj");
        File.Exists(browserProjPath).Should().BeTrue("el proyecto FileFlow.App.Browser debe existir");

        string projContent = File.ReadAllText(browserProjPath);
        projContent.Should().Contain("browser-wasm", "debe compilar para el runtime WebAssembly");
        projContent.Should().Contain("Avalonia.Browser", "debe referenciar Avalonia.Browser");
        projContent.Should().Contain("FileFlow.App.csproj", "debe referenciar FileFlow.App");

        string indexHtml = Path.Combine(root, "FileFlow.App.Browser", "wwwroot", "index.html");
        File.Exists(indexHtml).Should().BeTrue("debe existir el index.html en wwwroot");

        string mainJs = Path.Combine(root, "FileFlow.App.Browser", "wwwroot", "main.js");
        File.Exists(mainJs).Should().BeTrue("debe existir el main.js en wwwroot");
    }
}
