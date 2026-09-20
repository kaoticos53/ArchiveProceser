using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Plugin.AI.Management;
using FileFlow.Plugin.AI.ViewModels;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

/// <summary>
/// Guardias del sondeo VLM contra un servidor local apagado, que fue la fuente medida del flood de
/// excepciones no observadas (≈350 por sesión contra <c>localhost:1234</c>).
///
/// <para>Se cubren tres contratos: (1) una conexión rechazada no se reintenta ni se repite en ráfaga,
/// (2) el sondeo de la ventana de configuración no vuelve a golpear un endpoint recién caído, y
/// (3) ninguna de esas rutas deja una tarea fallida sin observar que acabe en el log de incidentes.</para>
///
/// <para><b>Endpoint aislado</b>: las pruebas usan un host propio (<c>fileflow-guard.invalid</c>) para que el
/// cortocircuito —estado estático del motor— no interfiera con otras clases que sí usan <c>localhost:1234</c>
/// con handlers simulados.</para>
/// </summary>
public class VlmEndpointFloodGuardTests : IDisposable
{
    /// <summary>Marca del fallo simulado: permite distinguir nuestras tareas de las de otras clases.</summary>
    private const string RefusedMarker = "FileFlow-Guard-Refused";

    private const string GuardEndpoint = "http://fileflow-guard.invalid:1234/v1";

    private readonly string _tempDir;
    private readonly int _previousBackoffScale;
    private readonly TimeSpan _previousEngineCooldown;
    private readonly TimeSpan _previousProbeCooldown;

    public VlmEndpointFloodGuardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_VlmFloodGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _previousBackoffScale = MultimodalVlmClientEngine.RetryBackoffScalePercent;
        _previousEngineCooldown = MultimodalVlmClientEngine.UnreachableEndpointCooldown;
        _previousProbeCooldown = MultimodalVlmConfigViewModel.ProbeFailureCooldown;

        MultimodalVlmClientEngine.RetryBackoffScalePercent = 0;
        MultimodalVlmClientEngine.UnreachableEndpointCooldown = TimeSpan.FromSeconds(30);
        MultimodalVlmConfigViewModel.ProbeFailureCooldown = TimeSpan.FromSeconds(30);
        ResetStaticState();
    }

    public void Dispose()
    {
        MultimodalVlmClientEngine.RetryBackoffScalePercent = _previousBackoffScale;
        MultimodalVlmClientEngine.UnreachableEndpointCooldown = _previousEngineCooldown;
        MultimodalVlmConfigViewModel.ProbeFailureCooldown = _previousProbeCooldown;
        ResetStaticState();

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Limpieza best-effort del directorio temporal.
        }
    }

    private static void ResetStaticState()
    {
        MultimodalVlmClientEngine.ResetUnreachableEndpoints();
        MultimodalVlmConfigViewModel.ResetProbeFailures();
    }

    private static Task<VlmInferenceResult> RunInferenceAsync(HttpClient client) =>
        MultimodalVlmClientEngine.ExecuteChatCompletionAsync(
            GuardEndpoint,
            "qwen2.5-vl-7b-instruct",
            "api-key",
            "system prompt",
            "user prompt",
            "data:image/png;base64,iVBORw0KGgo=",
            customHttpClient: client);

    [Fact]
    public async Task ExecuteChatCompletionAsync_WhenServerRefusesConnection_ShouldNotRetryNorRepeatTheRequest()
    {
        // Arrange
        var handler = new RefusingHandler();
        using var client = new HttpClient(handler);

        // Act: primera petición contra un servidor apagado
        Func<Task> call = () => RunInferenceAsync(client);
        await call.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se pudo conectar con el servidor VLM*");

        handler.Attempts.Should().Be(1, "una conexión rechazada no es un error transitorio que merezca reintento");

        // Segunda y tercera petición dentro del enfriamiento: fallan sin abrir una sola conexión nueva
        await call.Should().ThrowAsync<InvalidOperationException>();
        await call.Should().ThrowAsync<InvalidOperationException>();

        handler.Attempts.Should().Be(1, "un servidor apagado no debe seguir acumulando conexiones rechazadas");
    }

    [Fact]
    public async Task ExecuteChatCompletionAsync_WhenCooldownHasExpired_ShouldContactTheEndpointAgain()
    {
        // Arrange: enfriamiento nulo = la marca expira de inmediato, como si hubiera pasado el tiempo
        MultimodalVlmClientEngine.UnreachableEndpointCooldown = TimeSpan.Zero;
        var handler = new RefusingHandler();
        using var client = new HttpClient(handler);
        Func<Task> call = () => RunInferenceAsync(client);

        await call.Should().ThrowAsync<InvalidOperationException>();
        int attemptsWhileDown = handler.Attempts;

        // Act: el usuario arranca el servidor después de que expire el cortocircuito
        await call.Should().ThrowAsync<InvalidOperationException>();

        // Assert: el cortocircuito no debe impedir volver a intentar cuando el servidor puede estar de nuevo en marcha
        handler.Attempts.Should().BeGreaterThan(attemptsWhileDown);
    }

    [Fact]
    public async Task ConfigViewModel_WithUnreachableProvider_ShouldProbeOnceAndNeverLeaveAnUnobservedTask()
    {
        // Arrange: capturamos sólo las tareas fallidas que provengan de esta guardia
        Exception? unobserved = null;
        void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.ToString().Contains(RefusedMarker, StringComparison.Ordinal))
            {
                unobserved = e.Exception;
            }

            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var handler = new RefusingHandler();
            using var client = new HttpClient(handler);
            var storage = new VlmConfigurationStorageService(_tempDir);
            storage.SaveProviders([BuildGuardProvider()]);
            string providerEndpoint = storage.LoadProviders().First().EndpointUrl.TrimEnd('/');

            // Act: 25 aperturas de la ventana de configuración contra el servidor apagado
            for (int i = 0; i < 25; i++)
            {
                _ = new MultimodalVlmConfigViewModel(storageService: storage, customHttpClient: client);
            }

            await AsyncTestWaiter.WaitForAsync(
                () => MultimodalVlmConfigViewModel.IsProbeFailureMemoizedForTests(providerEndpoint),
                TimeSpan.FromSeconds(15),
                description: "el sondeo marcara el endpoint como inalcanzable");

            // El finalizador es quien reporta las tareas fallidas: forzamos la recolección dos veces.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Assert
            handler.Attempts.Should().BeLessThanOrEqualTo(2,
                "un endpoint apagado no puede sondearse una vez por cada ventana abierta");
            unobserved.Should().BeNull("el sondeo nunca debe dejar una tarea fallida sin observar");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }
    }

    [Fact]
    public async Task ConfigViewModel_AfterCooldownExpires_ShouldDiscoverModelsAgain()
    {
        // Arrange: el primer sondeo falla (servidor apagado) y el siguiente responde el catálogo
        var handler = new RecoveringHandler();
        using var client = new HttpClient(handler);
        var storage = new VlmConfigurationStorageService(_tempDir);
        storage.SaveProviders([BuildGuardProvider()]);
        string providerEndpoint = storage.LoadProviders().First().EndpointUrl.TrimEnd('/');

        var firstWindow = new MultimodalVlmConfigViewModel(storageService: storage, customHttpClient: client);
        await AsyncTestWaiter.WaitForAsync(
            () => MultimodalVlmConfigViewModel.IsProbeFailureMemoizedForTests(providerEndpoint),
            TimeSpan.FromSeconds(15),
            description: "el primer sondeo fallara y quedara memorizado");

        // La memoria de fallo expira: equivalen a que pase el enfriamiento y se vuelva a sondear.
        MultimodalVlmConfigViewModel.ResetProbeFailures();

        // Act: segunda ventana, ya con la memoria de fallo expirada
        var secondWindow = new MultimodalVlmConfigViewModel(storageService: storage, customHttpClient: client);
        await AsyncTestWaiter.WaitForAsync(
            () => secondWindow.AvailableModels.Any(),
            TimeSpan.FromSeconds(15),
            description: "el segundo sondeo descubriera los modelos");

        // Assert
        secondWindow.AvailableModels.Should().Contain("qwen2.5-vl-7b-instruct");
        firstWindow.AvailableModels.Should().BeEmpty();
    }

    private static VlmProviderProfile BuildGuardProvider() => new()
    {
        ProviderId = "GuardLocal",
        DisplayName = "Guard Local Server",
        EndpointUrl = GuardEndpoint,
        ModelName = string.Empty,
        ApiKey = "none",
        Temperature = 0.1,
        MaxTokens = 2048,
        MaxImageDimension = 1024,
        TimeoutSeconds = 5,
        ConcurrencyLimit = 1,
        IsBuiltIn = false
    };

    /// <summary>Handler que simula un servidor apagado: conexión rechazada al instante.</summary>
    private sealed class RefusingHandler : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            throw new HttpRequestException(
                $"{RefusedMarker}: no se pudo conectar (localhost:1234)",
                new SocketException((int)SocketError.ConnectionRefused));
        }
    }

    /// <summary>Handler que rechaza el primer sondeo y responde el catálogo en los siguientes.</summary>
    private sealed class RecoveringHandler : HttpMessageHandler
    {
        private int _attempts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref _attempts);
            if (attempt == 1)
            {
                throw new HttpRequestException(
                    $"{RefusedMarker}: no se pudo conectar (localhost:1234)",
                    new SocketException((int)SocketError.ConnectionRefused));
            }

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"id":"qwen2.5-vl-7b-instruct"}]}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
