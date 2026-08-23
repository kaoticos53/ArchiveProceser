using FluentAssertions;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class SqliteLogQueryBuilderTests
{
    [Fact]
    public void BuildFilterClause_NullFilter_ShouldReturnEmptyWhereClause()
    {
        // Act
        var (whereClause, parameters) = SqliteLogQueryBuilder.BuildFilterClause(null);

        // Assert
        whereClause.Should().BeEmpty();
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void BuildFilterClause_ExactLevel_ShouldGenerateExactCondition()
    {
        // Arrange
        var filter = new LogFilterCriteria(ExactLevel: LogLevel.Warning);

        // Act
        var (whereClause, parameters) = SqliteLogQueryBuilder.BuildFilterClause(filter);

        // Assert
        whereClause.Should().Be("WHERE Level = @exactLevel");
        parameters.Should().ContainKey("@exactLevel");
        parameters["@exactLevel"].Should().Be((int)LogLevel.Warning);
    }

    [Fact]
    public void BuildFilterClause_SearchTextAndNodeId_ShouldCombineWithAnd()
    {
        // Arrange
        var filter = new LogFilterCriteria(
            SearchText: "Zip Slip",
            NodeId: "SmartUnpackNode"
        );

        // Act
        var (whereClause, parameters) = SqliteLogQueryBuilder.BuildFilterClause(filter);

        // Assert
        whereClause.Should().StartWith("WHERE ");
        whereClause.Should().Contain("(NodeId = @nodeId OR NodeName = @nodeId)");
        whereClause.Should().Contain("Message LIKE @searchText");
        whereClause.Should().Contain(" AND ");

        parameters["@nodeId"].Should().Be("SmartUnpackNode");
        parameters["@searchText"].Should().Be("%Zip Slip%");
    }

    [Fact]
    public void BuildFilterClause_TimestampRange_ShouldGenerateCorrectBounds()
    {
        // Arrange
        var filter = new LogFilterCriteria(
            FromTimestamp: 1700000000000,
            ToTimestamp: 1700000050000
        );

        // Act
        var (whereClause, parameters) = SqliteLogQueryBuilder.BuildFilterClause(filter);

        // Assert
        whereClause.Should().Contain("Timestamp >= @fromTs");
        whereClause.Should().Contain("Timestamp <= @toTs");
        parameters["@fromTs"].Should().Be(1700000000000);
        parameters["@toTs"].Should().Be(1700000050000);
    }
}
