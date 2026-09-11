using System.Collections.Generic;
using FileFlow.Plugin.AI.Utilities;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class JsonMetadataFlattenerTests
{
    [Fact]
    public void Flatten_SimpleFlatJson_ShouldExtractAllProperties()
    {
        // Arrange
        string json = """
        {
            "numero_factura": "FAC-2026-001",
            "fecha_emision": "2026-09-11",
            "importe_total": 1250.50,
            "es_valido": true
        }
        """;

        // Act
        var result = JsonMetadataFlattener.Flatten(json);

        // Assert
        result.Should().ContainKey("numero_factura").WhoseValue.Should().Be("FAC-2026-001");
        result.Should().ContainKey("fecha_emision").WhoseValue.Should().Be("2026-09-11");
        result.Should().ContainKey("importe_total").WhoseValue.Should().Be("1250.50");
        result.Should().ContainKey("es_valido").WhoseValue.Should().Be("True");
    }

    [Fact]
    public void Flatten_NestedJsonObject_ShouldFlattenWithUnderscoresAndDots()
    {
        // Arrange
        string json = """
        {
            "factura": "FAC-99",
            "emisor": {
                "nombre": "Acme Corp",
                "cif": "A12345678"
            }
        }
        """;

        // Act
        var result = JsonMetadataFlattener.Flatten(json);

        // Assert
        result.Should().ContainKey("factura").WhoseValue.Should().Be("FAC-99");
        result.Should().ContainKey("emisor_nombre").WhoseValue.Should().Be("Acme Corp");
        result.Should().ContainKey("emisor.nombre").WhoseValue.Should().Be("Acme Corp");
        result.Should().ContainKey("emisor_cif").WhoseValue.Should().Be("A12345678");
    }

    [Fact]
    public void Flatten_StringContainingEscapedNestedJson_ShouldUnwrapAndFlatten()
    {
        // Arrange (caso común donde el modelo emite un JSON con otra cadena JSON escapada dentro)
        string json = """
        {
            "status": "success",
            "raw_payload": "{\"cliente\": \"Carlos Gómez\", \"saldo\": 500}"
        }
        """;

        // Act
        var result = JsonMetadataFlattener.Flatten(json);

        // Assert
        result.Should().ContainKey("status").WhoseValue.Should().Be("success");
        result.Should().ContainKey("raw_payload_cliente").WhoseValue.Should().Be("Carlos Gómez");
        result.Should().ContainKey("raw_payload_saldo").WhoseValue.Should().Be("500");
    }

    [Fact]
    public void Flatten_MarkdownWrappedJson_ShouldUnwrapCleanly()
    {
        // Arrange
        string json = """
        ```json
        {
            "categoria": "Factura_Recibo",
            "etiquetas": ["fiscal", "contabilidad", "2026"]
        }
        ```
        """;

        // Act
        var result = JsonMetadataFlattener.Flatten(json);

        // Assert
        result.Should().ContainKey("categoria").WhoseValue.Should().Be("Factura_Recibo");
        result.Should().ContainKey("etiquetas").WhoseValue.Should().Be("fiscal, contabilidad, 2026");
    }

    [Fact]
    public void FlattenAndInject_ShouldInjectIntoMetadataWithAndWithoutPrefix()
    {
        // Arrange
        string json = """
        {
            "categoria": "Documento_Legal",
            "etiquetas_descriptivas": ["contrato", "notaria"],
            "motivo": "Contiene firmas y sellos oficiales"
        }
        """;
        var metadata = new Dictionary<string, object?>();

        // Act
        var result = JsonMetadataFlattener.FlattenAndInject(json, metadata, prefix: "AI:Vlm:");

        // Assert
        metadata.Should().ContainKey("categoria").WhoseValue.Should().Be("Documento_Legal");
        metadata.Should().ContainKey("AI:Vlm:categoria").WhoseValue.Should().Be("Documento_Legal");
        metadata.Should().ContainKey("AI:VlmTags").WhoseValue.Should().Be("contrato, notaria");
        metadata.Should().ContainKey("AI:VlmReason").WhoseValue.Should().Be("Contiene firmas y sellos oficiales");
    }

    [Fact]
    public void Flatten_NonJsonText_ShouldReturnEmptyDictionary()
    {
        // Arrange
        string text = "Esta es una simple respuesta de texto sin formato JSON.";

        // Act
        var result = JsonMetadataFlattener.Flatten(text);

        // Assert
        result.Should().BeEmpty();
    }
}
