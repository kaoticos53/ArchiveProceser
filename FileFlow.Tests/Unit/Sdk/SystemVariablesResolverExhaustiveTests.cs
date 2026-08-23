using System.Globalization;
using FluentAssertions;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class SystemVariablesResolverExhaustiveTests
{
    [Fact]
    public void SystemVariablesResolver_StandardPlaceholders_ShouldResolveCorrectly()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Files\documento_legal.pdf")
        {
            FileSizeBytes = 10 * 1024 * 1024 // 10 MB
        };

        // Act & Assert
        SystemVariablesResolver.GetVariableValue("filename", item, null).Should().Be("documento_legal.pdf");
        SystemVariablesResolver.GetVariableValue("filenamenoext", item, null).Should().Be("documento_legal");
        SystemVariablesResolver.GetVariableValue("ext", item, null).Should().Be("pdf");
        SystemVariablesResolver.GetVariableValue("filesize:mb", item, null).Should().Be("10.00");
    }

    [Fact]
    public void SystemVariablesResolver_CultureInvariance_SizeShouldAlwaysUseDotDecimal()
    {
        // Guardar cultura actual
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Forzar cultura con coma decimal (es-ES)
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");

            var item = new FileItemContext(@"C:\Temp\test.bin")
            {
                FileSizeBytes = (long)(14.50 * 1024 * 1024)
            };

            // Act
            string resolvedMb = SystemVariablesResolver.GetVariableValue("filesize:mb", item, null);

            // Assert: debe usar punto decimal invariant para evitar romper expresiones y formatos
            resolvedMb.Should().Contain(".");
            resolvedMb.Should().NotContain(",");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void SystemVariablesResolver_CustomMetadataAndCounters_ShouldResolve()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Temp\img.jpg");
        item.Metadata["Counter"] = 5;
        item.Metadata["Category"] = "Finance";

        // Act & Assert
        SystemVariablesResolver.GetVariableValue("counter:D4", item, null).Should().Be("0005");
        SystemVariablesResolver.GetVariableValue("Category", item, null).Should().Be("Finance");
    }

    [Theory]
    [InlineData("unknown_variable_name")]
    [InlineData("")]
    public void SystemVariablesResolver_UnknownOrEmptyVariable_ShouldReturnEmptyString(string varName)
    {
        // Arrange
        var item = new FileItemContext(@"C:\Temp\doc.txt");

        // Act
        string result = SystemVariablesResolver.GetVariableValue(varName, item, null);

        // Assert
        result.Should().BeEmpty();
    }
}
