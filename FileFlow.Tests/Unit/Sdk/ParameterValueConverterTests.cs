using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

/// <summary>
/// Fija el contrato de <see cref="ParameterValueConverter"/> y, sobre todo, la <b>equivalencia</b> entre
/// las dos formas de leer un parámetro: el ayudante tipado <c>FlowNodeBase.GetParameter&lt;T&gt;</c> y los
/// estáticos de <see cref="ParameterHelper"/>.
///
/// Por qué existe: antes de unificarlos, <c>GetParameter&lt;T&gt;</c> usaba <c>Convert.ChangeType</c> y
/// devolvía el valor por defecto ante cualquier <c>JsonElement</c>, así que era imposible migrar las
/// lecturas de los nodos sin cambiar su comportamiento. Estos tests son la red que hace segura esa
/// migración: las expectativas son valores literales (no una comparación entre las dos rutas, que sería
/// tautológica porque ambas delegan), de modo que cualquier reimplementación divergente de una sola ruta
/// rompe aquí, con el caso concreto y el valor esperado.
/// </summary>
public class ParameterValueConverterTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Números embebidos en texto, incluidos porcentajes
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("50%", 50)]
    [InlineData("-25%", -25)]
    [InlineData("+8%", 8)]
    [InlineData("100 %", 100)]
    [InlineData("12,5%", 12)]
    [InlineData("12abc", 12)]
    [InlineData("abc12", 12)]
    [InlineData("v2", 2)]
    public void EmbeddedNumber_ShouldBecomeAnInteger_WithoutScalingPercentages(string text, int expected)
    {
        ParameterValueConverter.ConvertTo(text, 0).Should().Be(expected);
        ParameterHelper.GetInt32(text, 0).Should().Be(expected);
    }

    [Theory]
    [InlineData("50%", 50.0)]
    [InlineData("12,5%", 12.5)]
    [InlineData("0,7", 0.7)]
    [InlineData("abc2.5xyz", 2.5)]
    public void EmbeddedNumber_ShouldBecomeADouble(string text, double expected)
    {
        ParameterValueConverter.ConvertTo(text, 0.0).Should().Be(expected);
        ParameterHelper.GetDouble(text, 0.0).Should().Be(expected);
    }

    [Fact]
    public void Percentage_ShouldNotBeDividedByHundred()
    {
        // El signo de porcentaje es una unidad decorativa: los nodos esperan el número tal cual lo
        // escribió el usuario (un 80 que significa 80 %, no 0.8).
        ParameterHelper.GetInt32("80%", 0).Should().Be(80);
        ParameterHelper.GetDouble("80%", 0.0).Should().Be(80.0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Unidades de tiempo: sólo para enteros (asimetría heredada, fijada a propósito)
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("250ms", 250)]
    [InlineData("3s", 3)]
    [InlineData("2m", 120)]
    [InlineData("1h", 3600)]
    [InlineData("1,5s", 1)]
    [InlineData("500MS", 500)]
    public void TimeUnits_ShouldResolveForIntegers(string text, int expected) =>
        ParameterHelper.GetInt32(text, -1).Should().Be(expected);

    [Theory]
    [InlineData("2m", 2.0)]
    [InlineData("1h", 1.0)]
    [InlineData("250ms", 250.0)]
    public void TimeUnits_ShouldBeIgnoredForDoubles(string text, double expected) =>
        ParameterHelper.GetDouble(text, -1).Should().Be(expected);

    // ─────────────────────────────────────────────────────────────────────────────
    // JsonElement: la forma en que llegan los parámetros de un perfil guardado
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("\"42\"", 42)]
    [InlineData("42", 42)]
    [InlineData("-1", -1)]
    [InlineData("0", 0)]
    [InlineData("true", -7)]
    [InlineData("false", -7)]
    [InlineData("null", -7)]
    [InlineData("[1,2]", -7)]
    [InlineData("{\"a\":1}", -7)]
    [InlineData("42.9", -7)]
    [InlineData("3000000000", -7)]
    [InlineData("3000000000.0", -7)]
    public void JsonElement_ToInt(string json, int expected)
    {
        JsonElement element = JsonSerializer.Deserialize<JsonElement>(json);

        ParameterValueConverter.ConvertTo(element, -7).Should().Be(expected, $"el JSON era {json}");
        ParameterHelper.GetInt32(element, -7).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"42\"", 42.0)]
    [InlineData("42", 42.0)]
    [InlineData("42.9", 42.9)]
    [InlineData("3000000000", 3000000000.0)]
    [InlineData("\"50%\"", 50.0)]
    [InlineData("true", -7.0)]
    [InlineData("null", -7.0)]
    [InlineData("[1,2]", -7.0)]
    [InlineData("{\"a\":1}", -7.0)]
    public void JsonElement_ToDouble(string json, double expected)
    {
        JsonElement element = JsonSerializer.Deserialize<JsonElement>(json);

        ParameterValueConverter.ConvertTo(element, -7.0).Should().Be(expected, $"el JSON era {json}");
        ParameterHelper.GetDouble(element, -7.0).Should().Be(expected);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"xyz\"", true)]   // texto no interpretable → valor por defecto
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("5", true)]
    [InlineData("42.9", false)]     // número JSON no integral: ni true ni el defecto, siempre false
    [InlineData("null", true)]
    [InlineData("[1,2]", true)]
    [InlineData("{\"a\":1}", true)]
    public void JsonElement_ToBoolean(string json, bool expected)
    {
        JsonElement element = JsonSerializer.Deserialize<JsonElement>(json);

        ParameterValueConverter.ConvertTo(element, true).Should().Be(expected, $"el JSON era {json}");
        ParameterHelper.GetBoolean(element, true).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"hola\"", "hola")]
    [InlineData("42", "42")]
    [InlineData("42.9", "42.9")]
    [InlineData("true", "true")]
    [InlineData("null", "<def>")]
    [InlineData("[1,2]", "[1,2]")]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    public void JsonElement_ToString(string json, string expected)
    {
        JsonElement element = JsonSerializer.Deserialize<JsonElement>(json);

        ParameterValueConverter.ConvertTo(element, "<def>").Should().Be(expected, $"el JSON era {json}");
        ParameterHelper.GetString(element, "<def>").Should().Be(expected);
    }

    [Fact]
    public void JsonElement_Undefined_ShouldBehaveLikeNull()
    {
        JsonElement undefined = default;

        ParameterHelper.GetString(undefined, "<def>").Should().Be("<def>");
        ParameterHelper.GetInt32(undefined, -7).Should().Be(-7);
        ParameterHelper.GetDouble(undefined, -7.0).Should().Be(-7.0);
        ParameterHelper.GetBoolean(undefined, false).Should().Be(false);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // long y demás tipos enteros
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5L, 5)]
    [InlineData(0L, 0)]
    [InlineData(-1L, -1)]
    [InlineData(int.MaxValue + 0L, int.MaxValue)]
    [InlineData(int.MinValue + 0L, int.MinValue)]
    public void Long_ShouldConvertToInt_WhenItFits(long value, int expected) =>
        ParameterValueConverter.ConvertTo(value, -7).Should().Be(expected);

    [Fact]
    public void OutOfRange_ShouldReturnTheDefault_InsteadOfOverflowing()
    {
        // Normalización deliberada frente al comportamiento anterior: `(int)3_000_000_000L` se envolvía
        // a -1294967296 y `(int)1e12` saturaba a int.MaxValue. Ahora, fuera de rango, valor por defecto.
        ParameterValueConverter.ConvertTo(3_000_000_000L, -7).Should().Be(-7);
        ParameterValueConverter.ConvertTo(1e12, -7).Should().Be(-7);
        ParameterValueConverter.ConvertTo(double.NaN, -7).Should().Be(-7);
        ParameterValueConverter.ConvertTo(double.PositiveInfinity, -7).Should().Be(-7);
        ParameterValueConverter.ConvertTo("3000000000", -7).Should().Be(-7);
    }

    [Theory]
    [InlineData(5, 5L)]
    [InlineData(5L, 5L)]
    [InlineData("7", 7L)]
    [InlineData(7.9, 7L)]
    public void LongTarget_ShouldAcceptNumbersAndText(object value, long expected) =>
        ParameterValueConverter.ConvertTo(value, -1L).Should().Be(expected);

    [Theory]
    [InlineData(255, 255)]
    [InlineData("44", 44)]
    [InlineData(300, 255)]      // fuera del rango de byte → valor por defecto
    [InlineData(100000, 255)]
    [InlineData(-1, 255)]
    public void SmallerIntegralTargets_ShouldRespectTheirRange(object value, int expected) =>
        ParameterValueConverter.ConvertTo(value, (byte)255).Should().Be((byte)expected);

    [Theory]
    [InlineData(7L, 7)]
    [InlineData(7.9, 7)]
    [InlineData("9", 9)]
    [InlineData("9.9", 9)]
    [InlineData(true, 1)]     // bool sigue siendo convertible por la vía estándar, como antes
    [InlineData(false, 0)]
    public void SmallerIntegralTargets_FromSeveralSources(object value, int expected) =>
        ParameterValueConverter.ConvertTo(value, (short)-1).Should().Be((short)expected);

    [Theory]
    [InlineData(3, 3.0)]
    [InlineData(3L, 3.0)]
    [InlineData("3.5", 3.5)]
    [InlineData(true, 1.0)]
    public void FloatingTargets_ShouldAcceptIntegersAndText(object value, double expected) =>
        ParameterValueConverter.ConvertTo(value, (float)-1).Should().Be((float)expected);

    [Fact]
    public void DecimalTarget_ShouldAcceptTextAndRejectNaN()
    {
        ParameterValueConverter.ConvertTo("1.25", 0m).Should().Be(1.25m);
        ParameterValueConverter.ConvertTo("50%", 0m).Should().Be(50m);
        ParameterValueConverter.ConvertTo(double.NaN, 0m).Should().Be(0m);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Enums, nulables y valores ausentes
    // ─────────────────────────────────────────────────────────────────────────────

    public enum ProbeKind { First, Second, Third }

    [Theory]
    [InlineData("Second", ProbeKind.Second)]
    [InlineData("second", ProbeKind.Second)]
    [InlineData("1", ProbeKind.Second)]
    [InlineData("99", ProbeKind.First)]
    [InlineData("  ", ProbeKind.First)]
    public void EnumTarget_ShouldAcceptNamesAndNumbers(object value, ProbeKind expected) =>
        ParameterValueConverter.ConvertTo(value, ProbeKind.First).Should().Be(expected);

    [Fact]
    public void NullableTargets_ShouldConvertAndFallBackToNull()
    {
        ParameterValueConverter.ConvertTo("5", (int?)null).Should().Be(5);
        ParameterValueConverter.ConvertTo("abc", (int?)null).Should().BeNull();
        ParameterValueConverter.ConvertTo("true", (bool?)null).Should().BeTrue();
        ParameterValueConverter.ConvertTo(null, (double?)null).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrBlank_ShouldReturnTheDefault_ForNonTextTargets(object? value)
    {
        ParameterValueConverter.ConvertTo(value, 12).Should().Be(12);
        ParameterValueConverter.ConvertTo(value, 1.5).Should().Be(1.5);
        ParameterValueConverter.ConvertTo(value, false).Should().BeFalse();
    }

    [Fact]
    public void EmptyText_ShouldBeReturnedAsIs_AndOnlyNullFallsBackToTheDefault()
    {
        // Regla heredada que no se toca: un texto vacío es un valor legítimo (una plantilla en blanco),
        // no un valor ausente. Sólo null y el null de JSON caen al valor por defecto.
        ParameterValueConverter.ConvertTo("", "base").Should().BeEmpty();
        ParameterValueConverter.ConvertTo("   ", "base").Should().Be("   ");
        ParameterValueConverter.ConvertTo(null, "base").Should().Be("base");
        ParameterValueConverter.ConvertTo(JsonSerializer.Deserialize<JsonElement>("null"), "base").Should().Be("base");
        ParameterValueConverter.ConvertTo(JsonSerializer.Deserialize<JsonElement>("\"\""), "base").Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Equivalencia entre las dos rutas de lectura
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetParameter_And_ParameterHelper_ShouldAgreeOnEveryRawValue()
    {
        FlowNodeBase node = NodeParameterProbe.New();

        foreach ((string description, object? raw) in RawValues())
        {
            node.Parameters["P"] = raw;

            NodeParameterProbe.Read(node, "P", -7).Should().Be(ParameterHelper.GetInt32(raw, -7), $"leyendo {description} como int");
            NodeParameterProbe.Read(node, "P", -7.0).Should().Be(ParameterHelper.GetDouble(raw, -7.0), $"leyendo {description} como double");
            NodeParameterProbe.Read(node, "P", true).Should().Be(ParameterHelper.GetBoolean(raw, true), $"leyendo {description} como bool");
            NodeParameterProbe.Read(node, "P", false).Should().Be(ParameterHelper.GetBoolean(raw, false), $"leyendo {description} como bool");
            NodeParameterProbe.Read(node, "P", "<def>").Should().Be(ParameterHelper.GetString(raw, "<def>"), $"leyendo {description} como string");
        }
    }

    [Fact]
    public void GetParameter_ShouldUseTheDefault_WhenTheKeyIsMissing()
    {
        FlowNodeBase node = NodeParameterProbe.New();

        NodeParameterProbe.Read(node, "Ausente", 42).Should().Be(42);
        NodeParameterProbe.Read(node, "Ausente", 1.5).Should().Be(1.5);
        NodeParameterProbe.Read(node, "Ausente", true).Should().BeTrue();
        NodeParameterProbe.Read(node, "Ausente", "base").Should().Be("base");
        NodeParameterProbe.Read<int?>(node, "Ausente", null).Should().BeNull();
    }

    [Fact]
    public void GetParameter_ShouldUseTheDefault_WhenTheValueIsNull()
    {
        FlowNodeBase node = NodeParameterProbe.New();
        node.Parameters["P"] = null;

        NodeParameterProbe.Read(node, "P", 42).Should().Be(42);
        NodeParameterProbe.Read(node, "P", "base").Should().Be("base");
    }

    [Fact]
    public void GetParameter_ShouldReadAJsonProfile_ExactlyLikeTheLegacyHelper()
    {
        // El caso que motivó todo: un perfil guardado produce JsonElement, y el ayudante tipado devolvía
        // siempre el valor por defecto. Ahora ambos leen lo mismo.
        FlowNodeBase node = NodeParameterProbe.New();

        node.Parameters["Text"] = JsonSerializer.Deserialize<JsonElement>("\"hola\"");
        node.Parameters["Number"] = JsonSerializer.Deserialize<JsonElement>("42");
        node.Parameters["Percent"] = JsonSerializer.Deserialize<JsonElement>("\"80%\"");
        node.Parameters["Flag"] = JsonSerializer.Deserialize<JsonElement>("true");

        NodeParameterProbe.Read(node, "Text", "").Should().Be("hola");
        NodeParameterProbe.Read(node, "Number", 0).Should().Be(42);
        NodeParameterProbe.Read(node, "Percent", 0).Should().Be(80);
        NodeParameterProbe.Read(node, "Flag", false).Should().BeTrue();
    }

    private static IEnumerable<(string Description, object? Value)> RawValues()
    {
        yield return ("null", null);
        yield return ("int", 42);
        yield return ("long", 42L);
        yield return ("long grande", 3_000_000_000L);
        yield return ("double", 0.7);
        yield return ("double integral", 3.0);
        yield return ("bool", true);
        yield return ("texto", "hola");
        yield return ("texto numérico", "42");
        yield return ("porcentaje", "50%");
        yield return ("unidad de tiempo", "250ms");
        yield return ("decimal con coma", "1,5");
        yield return ("texto vacío", "");
        yield return ("texto basura", "abc");
        yield return ("json texto", JsonSerializer.Deserialize<JsonElement>("\"42\""));
        yield return ("json número", JsonSerializer.Deserialize<JsonElement>("42"));
        yield return ("json número decimal", JsonSerializer.Deserialize<JsonElement>("42.9"));
        yield return ("json booleano", JsonSerializer.Deserialize<JsonElement>("true"));
        yield return ("json nulo", JsonSerializer.Deserialize<JsonElement>("null"));
        yield return ("json array", JsonSerializer.Deserialize<JsonElement>("[1,2]"));
    }

}

/// <summary>
/// Acceso a <c>FlowNodeBase.GetParameter&lt;T&gt;</c> (es <c>protected</c>) sobre un nodo real, para poder
/// compararlo con <see cref="ParameterHelper"/> desde los tests.
///
/// Por qué reflexión y no un nodo de prueba derivado: el cargador descubre CUALQUIER tipo concreto que
/// implemente <c>IFlowNode</c>, sin comprobar visibilidad, y barre todos los ensamblados cargados
/// —incluido el de los tests—, así que un doble de nodo aparecería en el catálogo del producto (la
/// guardia <c>ToolboxOrganizationTests</c> exige exactamente 78 nodos oficiales). Invocando el método real
/// se prueba el código que se envía, no una copia. La clase es estática a propósito: el descubrimiento
/// descarta las clases abstractas, y una clase estática lo es.
/// </summary>
internal static class NodeParameterProbe
{
    /// <summary>Nodo real, sin dependencias, usado sólo como titular de un diccionario de parámetros.</summary>
    public static FlowNodeBase New() => new ThrottleDelayNode();

    /// <summary>Lee un parámetro con el mismo método que usan los nodos en producción.</summary>
    public static T Read<T>(FlowNodeBase node, string key, T defaultValue)
    {
        MethodInfo method = typeof(FlowNodeBase)
            .GetMethod("GetParameter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(T));

        return (T)method.Invoke(node, [key, defaultValue])!;
    }
}

// Muta CultureInfo.CurrentCulture (con restauración): se serializa con la colección "VisualSnapshots",
// que confina todo el estado de cultura e idioma del proceso.
[Collection("VisualSnapshots")]
public class ParameterValueConverterCultureTests
{
    [Fact]
    public void Results_ShouldNotDependOnTheSystemCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        FlowNodeBase node = NodeParameterProbe.New();
        node.Parameters["Decimal"] = "3.5";
        node.Parameters["Entero"] = "1,5";

        try
        {
            object[] resultsEs = Measure(new CultureInfo("es-ES"), node);
            object[] resultsInvariant = Measure(CultureInfo.InvariantCulture, node);

            resultsInvariant.Should().Equal(
                resultsEs,
                "el valor de un parámetro no puede cambiar según el idioma del sistema");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static object[] Measure(CultureInfo culture, FlowNodeBase node)
    {
        CultureInfo.CurrentCulture = culture;

        return
        [
            ParameterHelper.GetDouble("3.5", -1.0),
            ParameterHelper.GetDouble("3,5", -1.0),
            ParameterHelper.GetInt32("1,5", -1),
            ParameterHelper.GetInt32("1.000", -1),
            ParameterHelper.GetInt32("50%", -1),
            NodeParameterProbe.Read(node, "Decimal", 0.0),
            NodeParameterProbe.Read(node, "Entero", 0)
        ];
    }
}
