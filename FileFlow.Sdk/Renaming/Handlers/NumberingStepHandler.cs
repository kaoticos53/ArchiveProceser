using System.Globalization;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la numeración secuencial con padding, incrementos y resets condicionales.
/// </summary>
internal sealed class NumberingStepHandler : IRenameStepHandler
{
    public RenameMethodType SupportedType => RenameMethodType.Numbering;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        string currentDir = Path.GetDirectoryName(item.CurrentPath) ?? string.Empty;
        string metaGroupKey = string.Empty;
        if (!string.IsNullOrEmpty(step.ResetMetadataKey) && item.Metadata.TryGetValue(step.ResetMetadataKey, out var metaVal) && metaVal != null)
        {
            metaGroupKey = metaVal.ToString()!;
        }

        int seqNumber = batchContext.GetNextSequenceNumber(
            step.Id,
            step.StartNumber,
            step.Increment,
            step.ResetOn,
            currentDir,
            metaGroupKey
        );

        string paddingFormat = "D" + Math.Max(1, step.PaddingZeroes);
        string formattedNumber = seqNumber.ToString(paddingFormat, CultureInfo.InvariantCulture);

        int insertIndex = RenameIndexCalculator.CalculateInsertIndex(step.Position, step.PositionIndex, targetText.Length);
        return targetText.Insert(insertIndex, formattedNumber);
    }
}
