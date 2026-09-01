using System.Collections.Frozen;
using FileFlow.Sdk.Renaming.Handlers;

namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Motor principal de transformación de nombres mediante pipeline acumulativo de métodos secuenciales (Strategy Pattern).
/// </summary>
public sealed class RenameTransformEngine : IRenameTransformEngine
{
    private static readonly FrozenDictionary<RenameMethodType, IRenameStepHandler> Handlers =
        new Dictionary<RenameMethodType, IRenameStepHandler>
        {
            [RenameMethodType.NewName] = new NewNameStepHandler(),
            [RenameMethodType.SearchReplace] = new SearchReplaceStepHandler(),
            [RenameMethodType.Insert] = new InsertStepHandler(),
            [RenameMethodType.Remove] = new RemoveStepHandler(),
            [RenameMethodType.CaseConversion] = new CaseStepHandler(),
            [RenameMethodType.Numbering] = new NumberingStepHandler(),
            [RenameMethodType.ReplaceList] = new ReplaceListStepHandler(),
            [RenameMethodType.TrimClean] = new CleanupStepHandler(),
            [RenameMethodType.NormalizeNumbers] = new NormalizeNumbersStepHandler()
        }.ToFrozenDictionary();

    public RenameResult Transform(
        string currentFileName,
        FileItemContext item,
        IReadOnlyList<RenameMethodStep> steps,
        RenameBatchContext batchContext)
    {
        if (string.IsNullOrEmpty(currentFileName))
        {
            return new RenameResult(currentFileName, currentFileName, [], false);
        }

        string originalFileName = currentFileName;
        string workingFileName = currentFileName;
        var traces = new List<RenameStepTrace>(steps.Count);

        try
        {
            foreach (var step in steps)
            {
                if (!step.IsEnabled)
                {
                    continue;
                }

                string inputBeforeStep = workingFileName;
                string outputAfterStep = ApplyStep(step, inputBeforeStep, item, batchContext);

                bool modified = !string.Equals(inputBeforeStep, outputAfterStep, StringComparison.Ordinal);
                traces.Add(new RenameStepTrace(
                    step.Id,
                    step.MethodType,
                    inputBeforeStep,
                    outputAfterStep,
                    modified,
                    step.Name
                ));

                workingFileName = outputAfterStep;
            }

            bool hasChanges = !string.Equals(originalFileName, workingFileName, StringComparison.Ordinal);
            return new RenameResult(originalFileName, workingFileName, traces, hasChanges);
        }
        catch (Exception ex)
        {
            return new RenameResult(originalFileName, workingFileName, traces, false, ex.Message);
        }
    }

    private static string ApplyStep(
        RenameMethodStep step,
        string fileName,
        FileItemContext item,
        RenameBatchContext batchContext)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName).TrimStart('.');

        switch (step.ApplyTo)
        {
            case ApplyToTarget.NameOnly:
                string transformedName = ExecuteMethod(step, nameWithoutExt, item, batchContext);
                return string.IsNullOrEmpty(extension) ? transformedName : $"{transformedName}.{extension}";

            case ApplyToTarget.ExtensionOnly:
                string transformedExt = ExecuteMethod(step, extension, item, batchContext);
                transformedExt = transformedExt.TrimStart('.');
                return string.IsNullOrEmpty(transformedExt) ? nameWithoutExt : $"{nameWithoutExt}.{transformedExt}";

            case ApplyToTarget.FullName:
            default:
                return ExecuteMethod(step, fileName, item, batchContext);
        }
    }

    private static string ExecuteMethod(
        RenameMethodStep step,
        string targetText,
        FileItemContext item,
        RenameBatchContext batchContext)
    {
        if (Handlers.TryGetValue(step.MethodType, out var handler))
        {
            return handler.Execute(step, targetText, item, batchContext);
        }

        return targetText;
    }
}
