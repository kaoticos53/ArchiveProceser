using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.Subflows;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La frontera de un subflujo se descubre resolviendo su definición, y el inspector pide esa respuesta en
/// cada escritura de parámetro: sin memoria, el mismo archivo se leería y se analizaría decenas de veces
/// para contestar siempre lo mismo. El descubrimiento se memoriza por nodo y por <b>huella de su origen</b>
/// —el JSON de la definición y el archivo resuelto con su fecha y su tamaño—, así que comprobarla no exige
/// abrir el archivo.
///
/// Estas pruebas fijan las dos caras del trato: mientras la huella no cambie no se vuelve a leer (se
/// comprueba cambiando el contenido del archivo <i>sin</i> cambiar su fecha ni su tamaño: si la respuesta
/// nueva apareciera, es que se releyó), y cuando el origen cambia de verdad no se sirve una respuesta
/// vieja. El tamaño y la fecha se fijan explícitamente para que el experimento no dependa del reloj.
/// </summary>
public class SubflowDefinitionCacheTests
{
    private static readonly DateTime Stamp = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [Fact]
    public void Discover_ShouldNotReReadTheDefinition_WhileItsSourceLooksTheSame()
    {
        string path = SubflowFixtures.TempFile();
        string first = SubflowFixtures.DefinitionJson(inputPorts: "In;Alternate", outputPorts: "Out;Errores");
        SubflowFixtures.WriteFile(path, first, Stamp);

        var node = new SubflowNode { SubflowPath = path };

        SubflowPortResolver.Discover(node).Inputs.Should().Equal("In", "Alternate");

        // El archivo cambia de contenido, pero no de marca: misma fecha, mismo tamaño.
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Otro", "Out;Fallos").SameLengthAs(first), Stamp);

        SubflowPortResolver.Discover(node).Inputs.Should().Equal(
            new[] { "In", "Alternate" },
            "con la huella del origen intacta, el nodo responde con lo que ya había descubierto");

        // Un nodo nuevo sobre el mismo archivo sí lee: el contenido cambió de verdad y la respuesta
        // anterior era la caché, no una lectura.
        SubflowPortResolver.Discover(new SubflowNode { SubflowPath = path }).Inputs.Should().Equal("In", "Otro");
    }

    [Fact]
    public void Discover_ShouldFollowTheDefinition_WhenItsSourceReallyChanges()
    {
        string path = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), Stamp);

        var node = new SubflowNode { SubflowPath = path };
        SubflowPortResolver.Discover(node).Inputs.Should().Equal("In", "Alternate");

        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Otro", "Out;Fallos"), Stamp.AddSeconds(1));

        var (inputs, outputs) = SubflowPortResolver.Discover(node);

        inputs.Should().Equal("In", "Otro");
        outputs.Should().Equal("Out", "Fallos");
    }

    [Fact]
    public void Discover_ShouldNotRememberADefinitionItCouldNotRead()
    {
        string path = SubflowFixtures.TempFile();
        // Texto ilegible a propósito y holgado: el experimento necesita sitio para escribir después un
        // subgrafo válido sin que cambie el tamaño del archivo, y el relleno sólo puede alargar el
        // contenido de prueba, así que este relleno tiene que sobrar de largo.
        string unreadable = "{" + new string('x', 4000);
        SubflowFixtures.WriteFile(path, unreadable, Stamp);

        var node = new SubflowNode { SubflowPath = path };

        SubflowPortResolver.Discover(node).Inputs.Should().Equal(
            new[] { "In" },
            "una definición ilegible responde con los puertos genéricos");

        // El archivo se arregla sin que cambie su marca: si el fallo se hubiera memorizado, el nodo
        // seguiría dando puertos genéricos para siempre.
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores").SameLengthAs(unreadable), Stamp);

        SubflowPortResolver.Discover(node).Inputs.Should().Equal(
            new[] { "In", "Alternate" },
            "un fallo de lectura puede ser transitorio, así que no se memoriza");
    }

    [Fact]
    public void RepeatingAWriteInTheInspector_ShouldNotReReadTheDefinition()
    {
        string path = SubflowFixtures.TempFile();
        string first = SubflowFixtures.DefinitionJson(inputPorts: "In;Alternate", outputPorts: "Out;Errores");
        SubflowFixtures.WriteFile(path, first, Stamp);

        var editor = new EditorViewModel(new PluginLoader());
        var containerVm = EditorFixtures.AddNode(editor, new SubflowNode { SubflowPath = path });
        containerVm.SyncSubflowPorts();
        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");

        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Otro", "Out;Fallos").SameLengthAs(first), Stamp);

        // El inspector reescribe el mismo valor (repetir el texto, deshacer, recargar los parámetros del
        // nodo): es el camino que antes releía y analizaba la definición entera.
        containerVm.OnParameterValueChanged("SubflowPath", path);

        containerVm.InputPorts.Select(port => port.Name).Should().Equal(
            new[] { "In", "Alternate" },
            "la definición de este origen ya se resolvió y su huella no ha cambiado");

        // Un editor nuevo sobre el mismo archivo sí trae el contenido nuevo: la caché no está ciega.
        var freshEditor = new EditorViewModel(new PluginLoader());
        var freshVm = EditorFixtures.AddNode(freshEditor, new SubflowNode { SubflowPath = path });
        freshVm.SyncSubflowPorts();
        freshVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Otro");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────
}

/// <summary>
/// Rellena un JSON con espacios finales hasta medir lo mismo que otro. El experimento de la caché exige
/// cambiar el contenido del archivo sin cambiar su tamaño, y el espacio en blanco sobrante tras la raíz es
/// válido para el analizador.
/// </summary>
internal static class SameLengthJsonExtensions
{
    public static string SameLengthAs(this string json, string reference)
    {
        json.Length.Should().BeLessThanOrEqualTo(reference.Length,
            "el relleno sólo puede alargar: el contenido de prueba debe ser el corto");
        return json.PadRight(reference.Length);
    }
}
