namespace FileFlow.Sdk;

public enum PortDirection
{
    Input,
    Output
}

public record NodePort(
    string Name,
    Type DataType,
    PortDirection Direction,
    string DisplayName,
    string Description = ""
);
