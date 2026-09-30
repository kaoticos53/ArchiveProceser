using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de los nombres de estado del XAML del host Uno: los <c>x:Name</c> de <c>VisualStateGroup</c> y
/// <c>VisualState</c> son únicos <b>por archivo</b>, porque comparten espacio de nombres.
///
/// <para><b>El defecto que evita</b>: repetir un nombre de estado no da un error, da un <c>XamlCompiler</c>
/// con <b>código 1 y sin mensaje</b> (<c>MSB3073: el comando … salió con el código 1</c>, sin fichero ni
/// línea). Es la tarde más cara posible: el compilador se niega a decir qué le molesta. Le pasó al catálogo
/// de nodos en el hito 290 —su encabezado quería un segundo <c>CommonStates</c> y el build se cayó sin
/// explicar por qué— y se resolvió por bisección, no por un mensaje.</para>
///
/// <para><b>Qué se afirma</b>: (1) el árbol XAML real del host no repite ni un nombre de estado;
/// (2) cada plantilla con estados vive en su propio archivo —el chip y el encabezado del catálogo en dos
/// archivos distintos—, que es la cura que devuelve a cada plantilla su espacio de nombres; (3) cada bloque
/// de estados cuelga DENTRO del raíz de su plantilla (la forma que Uno aplica; como hermano del raíz compila
/// pero queda mudo); (4) el escáner detecta cada forma medida —grupo repetido, estado repetido, la repetición
/// que cruza los recursos con el contenido y el bloque hermano del raíz— y no se deja engañar por las que no
/// son defecto (nombres distintos, nombres citados en un comentario, nombres repetidos de elementos que no
/// son estados).</para>
///
/// <para><b>Sin mutación</b>: el defecto no compila, y un <c>NO-COMPILA</c> es un veredicto de fallo del
/// andamiaje, no un éxito — aquí la guardia es toda la red, y la medida de la bisección (cinco builds) es su
/// testigo.</para>
/// </summary>
public class UnoVisualStateNameGuardTests
{
    private static string RepoRoot() => TestRepositoryLocator.RepositoryRoot();

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del host Uno
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryVisualStateNameInTheUnoHost_ShouldBeUniquePerFile()
    {
        var violations = new List<string>();
        var declaredInTheHost = 0;

        foreach (string file in UnoGeometryBindingScanner.UnoXamlFiles(RepoRoot()))
        {
            string relative = Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');
            string source = File.ReadAllText(file);

            declaredInTheHost += UnoVisualStateNameScanner.DeclaredStates(source).Count;

            foreach (var collision in UnoVisualStateNameScanner.FindCollisions(source))
            {
                violations.Add(
                    $"{relative}: {collision.Describe()} — los nombres de VisualStateGroup/VisualState " +
                    "comparten espacio de nombres en TODO el archivo, así que el XamlCompiler sale con " +
                    "código 1 y sin mensaje (MSB3073 sin fichero ni línea). Renombra el estado: los " +
                    "convencionales (CommonStates, Normal, Checked…) sólo caben una vez por archivo.");
            }
        }

        violations.Should().BeEmpty(
            "repetir un nombre de estado rompe la compilación de XAML en silencio: la guardia es la única " +
            "que dice qué archivo y qué línea, porque el compilador no lo dice");

        // El control de que la guardia MIRÓ: sin esto, un patrón roto que dejara de encontrar estados
        // también daría verde, y el verde mentiría igual que el compilador mudo.
        declaredInTheHost.Should().BeGreaterThan(0,
            "el host declara estados (la máquina de estados del chip, como mínimo): una guardia que no " +
            "encuentra ni un estado no está vigilando nada");
    }

    [Fact]
    public void TheChipStateMachine_ShouldLiveInItsOwnDictionary_AndThePanelRecoverItsNamespace()
    {
        // La cura del hito 291: extraer el chip a su propio diccionario devuelve el espacio de nombres de
        // estados al archivo que lo consume, y por eso el encabezado del catálogo puede volver a declarar
        // su máquina de estados (el realce del puntero) en vez de pintarlo por code-behind.
        var chipStates = DeclaredStatesOf("FileFlow.App.Uno/Themes/ControlStyles.xaml");
        chipStates.Should().Contain(s => s.Kind == "VisualStateGroup" && s.Name == "CommonStates",
            "el chip vive ahora en su propio diccionario: su máquina de estados ya no comparte archivo con " +
            "la página que lo consume");
        chipStates.Should().Contain(s => s.Kind == "VisualState" && s.Name == "Checked",
            "el estado seleccionado del chip es el que su plantilla pinta con el acento");

        var panelStates = DeclaredStatesOf("FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml");
        panelStates.Should().Contain(s => s.Kind == "VisualStateGroup" && s.Name == "CommonStates",
            "con el chip fuera, el encabezado del panel recupera el espacio de nombres y declara su propia " +
            "máquina de estados (antes iba por code-behind por el choque de nombres)");
        panelStates.Should().Contain(s => s.Kind == "VisualState" && s.Name == "PointerOver",
            "el realce del encabezado bajo el puntero es ahora declarativo");
        panelStates.Should().OnlyContain(s => s.Line > 0, "la guardia cita líneas: cada estado tiene la suya");
    }

    private static IReadOnlyList<UnoVisualStateNameScanner.VisualStateName> DeclaredStatesOf(string relative)
        => UnoVisualStateNameScanner.DeclaredStates(
            File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar))));

    [Fact]
    public void EveryVisualStateGroupInTheUnoHost_ShouldLiveInsideItsTemplateRoot()
    {
        // La segunda regla muda (hito 291): los grupos de estados tienen que colgar DENTRO del elemento
        // raíz de la plantilla, no ser hermanos suyos dentro del ControlTemplate. Las dos formas compilan,
        // pero sólo la primera se aplica: unos estados hermanos del raíz quedan declarados y mudos —el chip
        // del catálogo nació así, sin pintar nunca su estado seleccionado, y nada avisaba.
        var violations = new List<string>();
        int insideRoot = 0;

        foreach (string file in UnoGeometryBindingScanner.UnoXamlFiles(RepoRoot()))
        {
            string relative = Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');
            var placement = UnoVisualStateNameScanner.InspectTemplateStateGroupPlacement(File.ReadAllText(file));
            insideRoot += placement.InsideRoot;
            foreach (int line in placement.SiblingOfRoot)
            {
                violations.Add(
                    $"{relative}:{line} — el VisualStateManager.VisualStateGroups cuelga del ControlTemplate, " +
                    "no de su raíz");
            }
        }

        violations.Should().BeEmpty(
            "en Uno los grupos de estados sólo se APLICAN si cuelgan dentro del raíz de la plantilla; como " +
            "hermano suyo compilan y quedan mudos (el chip del catálogo nació sin pintar su estado " +
            "seleccionado por esto, y el compilador no lo dice)");

        insideRoot.Should().BeGreaterThan(0,
            "el host tiene plantillas con estados dentro de su raíz: una guardia que no encuentra ninguna no " +
            "está vigilando nada");
    }

    [Fact]
    public void Scanner_ShouldScanTheWholeHostSurface()
    {
        var files = UnoGeometryBindingScanner.UnoXamlFiles(RepoRoot()).ToList();

        files.Should().NotBeEmpty("el host Uno tiene XAML que barrer");

        var relatives = files
            .Select(f => Path.GetRelativePath(RepoRoot(), f).Replace('\\', '/'))
            .ToList();

        relatives.Should().Contain("FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml",
            "el catálogo de nodos es el archivo donde el defecto se midió (hito 290): su encabezado y su " +
            "chip no pueden declarar los dos un CommonStates en el mismo archivo");
        relatives.Should().Contain("FileFlow.App.Uno/Themes/ControlStyles.xaml",
            "el diccionario compartido de estilos es parte de la superficie vigilada: es donde vive la " +
            "plantilla del chip con sus estados, y un nombre repetido ahí rompería el build en silencio");
        relatives.Should().Contain("FileFlow.App.Uno/MainWindow.xaml",
            "la ventana principal es parte de la superficie vigilada");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del escáner con snippets sintéticos (probar el detector no debe exigir
    // plantar ficheros infractores en el árbol del host)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Scanner_ShouldCatchADuplicatedStateGroupName()
    {
        // La forma exacta que rompió el build del hito 290: dos plantillas del mismo diccionario con su
        // grupo CommonStates cada una.
        const string snippet =
            """
            <UserControl.Resources>
              <Style x:Key="ChipStyle" TargetType="ToggleButton">
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                      <Border x:Name="ChipRoot" />
                      <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                          <VisualState x:Name="Normal" />
                        </VisualStateGroup>
                      </VisualStateManager.VisualStateGroups>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
              <Style x:Key="HeaderStyle" TargetType="ToggleButton">
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                      <Border x:Name="HeaderRoot" />
                      <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                          <VisualState x:Name="Normal" />
                        </VisualStateGroup>
                      </VisualStateManager.VisualStateGroups>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
            </UserControl.Resources>
            """;

        var collisions = UnoVisualStateNameScanner.FindCollisions(snippet);

        collisions.Should().HaveCount(2,
            "se repiten el grupo CommonStates y el estado Normal: los dos son nombres de estado");

        collisions.Should().Contain(c => c.Repeated.Kind == "VisualStateGroup" && c.Repeated.Name == "CommonStates",
            "el nombre del grupo es el que el hito 290 midió primero");

        collisions.Should().Contain(c => c.Repeated.Kind == "VisualState" && c.Repeated.Name == "Normal",
            "los nombres de estado colisionan igual que los de grupo (medido: grupo renombrado y estado no → falla)");
    }

    [Fact]
    public void Scanner_ShouldCatchADuplicatedStateNameAcrossDistinctGroups()
    {
        // Medido: con el grupo renombrado a GroupHeaderStates el build SEGUÍA cayendo mientras los estados
        // conservaran los nombres convencionales. Un grupo nuevo no basta; los nombres de estado son el
        // espacio de nombres compartido.
        const string snippet =
            """
            <VisualStateManager.VisualStateGroups>
              <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="PointerOver" />
              </VisualStateGroup>
              <VisualStateGroup x:Name="GroupHeaderStates">
                <VisualState x:Name="PointerOver" />
              </VisualStateGroup>
            </VisualStateManager.VisualStateGroups>
            """;

        var collisions = UnoVisualStateNameScanner.FindCollisions(snippet);

        collisions.Should().ContainSingle("sólo se repite PointerOver: los grupos se llaman distinto")
            .Which.Repeated.Name.Should().Be("PointerOver");
    }

    [Fact]
    public void Scanner_ShouldCatchACollisionBetweenTheResourcesAndTheContent()
    {
        // Medido: el espacio de nombres NO es el diccionario, es el ARCHIVO. Un bloque de estados en el
        // contenido que repita el CommonStates de una plantilla de los recursos también rompe el build.
        const string snippet =
            """
            <UserControl.Resources>
              <Style x:Key="ChipStyle" TargetType="ToggleButton">
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                      <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates" />
                      </VisualStateManager.VisualStateGroups>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
            </UserControl.Resources>
            <Grid>
              <VisualStateManager.VisualStateGroups>
                <VisualStateGroup x:Name="CommonStates" />
              </VisualStateManager.VisualStateGroups>
            </Grid>
            """;

        UnoVisualStateNameScanner.FindCollisions(snippet)
            .Should().ContainSingle("recursos y contenido comparten espacio de nombres: es un solo archivo")
            .Which.Repeated.Name.Should().Be("CommonStates");
    }

    [Fact]
    public void Scanner_ShouldAcceptDistinctStateNamesInOneFile()
    {
        // El control de las tres anteriores: dos máquinas de estados en el mismo archivo NO son el defecto.
        // Lo que la guardia prohíbe es el nombre repetido, no el segundo juego de estados.
        const string snippet =
            """
            <VisualStateManager.VisualStateGroups>
              <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="Normal" />
                <VisualState x:Name="Checked" />
              </VisualStateGroup>
              <VisualStateGroup x:Name="GroupHeaderStates">
                <VisualState x:Name="HdrNormal" />
                <VisualState x:Name="HdrChecked" />
              </VisualStateGroup>
            </VisualStateManager.VisualStateGroups>
            """;

        UnoVisualStateNameScanner.FindCollisions(snippet).Should().BeEmpty(
            "un segundo grupo con nombres propios compila: la guardia no puede prohibir lo que el " +
            "compilador acepta (medido: grupo y estados renombrados → compila)");
    }

    [Fact]
    public void Scanner_ShouldCatchStateGroupsThatAreSiblingsOfTheTemplateRoot()
    {
        // La forma que compila pero queda MUDA (medido en el hito 291 sobre la app viva): el bloque de
        // estados como hermano del raíz de la plantilla. El chip del catálogo nació así y nunca pintó su
        // acento; se corrigió metiendo el bloque dentro del Border raíz.
        const string snippet =
            """
            <ControlTemplate xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ToggleButton">
              <Border x:Name="Root" />
              <VisualStateManager.VisualStateGroups>
                <VisualStateGroup x:Name="CommonStates">
                  <VisualState x:Name="Normal" />
                </VisualStateGroup>
              </VisualStateManager.VisualStateGroups>
            </ControlTemplate>
            """;

        var placement = UnoVisualStateNameScanner.InspectTemplateStateGroupPlacement(snippet);

        placement.SiblingOfRoot.Should().ContainSingle(
            "el bloque cuelga directamente del ControlTemplate: es la forma que Uno no aplica");
        placement.InsideRoot.Should().Be(0, "no hay ningún bloque dentro del raíz de la plantilla");
    }

    [Fact]
    public void Scanner_ShouldAcceptStateGroupsInsideTheTemplateRoot()
    {
        // La forma que Uno SÍ aplica (la del chip y el encabezado desde el hito 291): el bloque dentro del
        // elemento raíz de la plantilla.
        const string snippet =
            """
            <ControlTemplate xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ToggleButton">
              <Border x:Name="Root">
                <VisualStateManager.VisualStateGroups>
                  <VisualStateGroup x:Name="CommonStates">
                    <VisualState x:Name="Normal" />
                  </VisualStateGroup>
                </VisualStateManager.VisualStateGroups>
              </Border>
            </ControlTemplate>
            """;

        var placement = UnoVisualStateNameScanner.InspectTemplateStateGroupPlacement(snippet);

        placement.SiblingOfRoot.Should().BeEmpty("el bloque vive dentro del raíz: Uno lo aplica");
        placement.InsideRoot.Should().Be(1, "la guardia cuenta los bloques bien colocados (su control)");
    }

    [Fact]
    public void Scanner_ShouldIgnoreStateNamesQuotedInComments()
    {
        // La lección del 165: un lint que lee el texto se conforma con lo comentado. Un CommonStates citado
        // en un comentario no declara nada y no puede contar como colisión.
        const string snippet =
            """
            <!-- Dos bloques con x:Name="CommonStates" colisionarían aquí -->
            <VisualStateManager.VisualStateGroups>
              <VisualStateGroup x:Name="CommonStates" />
            </VisualStateManager.VisualStateGroups>
            """;

        UnoVisualStateNameScanner.FindCollisions(snippet).Should().BeEmpty(
            "los comentarios XML del XAML no declaran estados");
    }

    [Fact]
    public void Scanner_ShouldIgnoreRepeatedNamesThatAreNotStates()
    {
        // Medido: repetir el x:Name de un Border entre dos plantillas del mismo diccionario compila. Si la
        // guardia contara todos los nombres, prohibiría algo legítimo y el falsos positivos la harían
        // inservible.
        const string snippet =
            """
            <UserControl.Resources>
              <DataTemplate x:Key="One">
                <Border x:Name="Root" />
              </DataTemplate>
              <DataTemplate x:Key="Two">
                <Border x:Name="Root" />
              </DataTemplate>
            </UserControl.Resources>
            """;

        UnoVisualStateNameScanner.FindCollisions(snippet).Should().BeEmpty(
            "la colisión es de los NOMBRES DE ESTADO: repetir el nombre de un elemento normal compila");
    }

    [Fact]
    public void Scanner_ShouldNotMistakeTheStateGroupsHostForAState()
    {
        // <VisualStateManager.VisualStateGroups> es el contenedor; el \b del patrón tiene que dejarlo fuera
        // para no censar el mismo nombre dos veces por el envoltorio.
        const string snippet =
            """
            <VisualStateManager.VisualStateGroups>
              <VisualStateGroup x:Name="CommonStates" />
            </VisualStateManager.VisualStateGroups>
            """;

        UnoVisualStateNameScanner.FindCollisions(snippet).Should().BeEmpty(
            "el contenedor no es un estado: censarlo haría que cualquier bloque con un solo grupo se " +
            "reportara como colisión consigo mismo");
    }

    [Fact]
    public void Scanner_ShouldReportBothLinesOfTheCollision()
    {
        const string snippet =
            """
            <VisualStateGroup x:Name="CommonStates" />
            <VisualStateGroup x:Name="Other" />
            <VisualStateGroup x:Name="CommonStates" />
            """;

        UnoVisualStateNameScanner.FindCollisions(snippet)
            .Should().ContainSingle()
            .Which.Should().Match<UnoVisualStateNameScanner.VisualStateNameCollision>(c =>
                c.First.Line == 1 && c.Repeated.Line == 3);
    }
}
