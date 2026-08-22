namespace FileFlow.Sdk.TemplateEngine.Functions;

public interface ITemplateFunctionEvaluator
{
    bool CanEvaluate(string functionName);
    string Evaluate(string functionName, IReadOnlyList<string> args, FileItemContext item, string? sourceRootPath);
}
