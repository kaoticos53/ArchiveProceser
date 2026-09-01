using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Motor principal de transformación de nombres mediante pipeline acumulativo de métodos secuenciales.
/// </summary>
public sealed class RenameTransformEngine : IRenameTransformEngine
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

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
        return step.MethodType switch
        {
            RenameMethodType.NewName => ExecuteNewName(step, item),
            RenameMethodType.SearchReplace => ExecuteSearchReplace(step, targetText, item),
            RenameMethodType.Insert => ExecuteInsert(step, targetText, item),
            RenameMethodType.Remove => ExecuteRemove(step, targetText, item),
            RenameMethodType.CaseConversion => ExecuteCaseConversion(step, targetText),
            RenameMethodType.Numbering => ExecuteNumbering(step, targetText, item, batchContext),
            RenameMethodType.ReplaceList => ExecuteReplaceList(step, targetText, item),
            RenameMethodType.TrimClean => ExecuteTrimClean(step, targetText),
            RenameMethodType.NormalizeNumbers => ExecuteNormalizeNumbers(step, targetText, item),
            _ => targetText
        };
    }

    private static string ExecuteNewName(RenameMethodStep step, FileItemContext item)
    {
        return VariableTemplateResolver.Resolve(step.Pattern, item);
    }

    private static string ExecuteSearchReplace(RenameMethodStep step, string targetText, FileItemContext item)
    {
        if (string.IsNullOrEmpty(step.SearchText))
        {
            return targetText;
        }

        string searchPattern = VariableTemplateResolver.Resolve(step.SearchText, item);
        if (string.IsNullOrEmpty(searchPattern))
        {
            return targetText;
        }

        if (step.UseRegex)
        {
            var options = step.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(searchPattern, options, RegexTimeout);
            return VariableTemplateResolver.ApplyRegexReplacement(regex, targetText, step.ReplaceText ?? string.Empty, item, step.ReplaceAll);
        }

        string replacePattern = VariableTemplateResolver.Resolve(step.ReplaceText ?? string.Empty, item);
        var comparison = step.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (step.ReplaceAll)
        {
            return targetText.Replace(searchPattern, replacePattern, comparison);
        }

        int idx = targetText.IndexOf(searchPattern, comparison);
        if (idx >= 0)
        {
            return string.Concat(targetText.AsSpan(0, idx), replacePattern, targetText.AsSpan(idx + searchPattern.Length));
        }

        return targetText;
    }

    private static string ExecuteInsert(RenameMethodStep step, string targetText, FileItemContext item)
    {
        string textToInsert = VariableTemplateResolver.Resolve(step.Pattern, item);
        if (string.IsNullOrEmpty(textToInsert))
        {
            return targetText;
        }

        int index = CalculateInsertIndex(step.Position, step.PositionIndex, targetText.Length);
        return targetText.Insert(index, textToInsert);
    }

    private static string ExecuteRemove(RenameMethodStep step, string targetText, FileItemContext item)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        if (!string.IsNullOrEmpty(step.SearchText) || !string.IsNullOrEmpty(step.Pattern))
        {
            string search = !string.IsNullOrEmpty(step.SearchText) ? step.SearchText : step.Pattern;
            string searchPattern = VariableTemplateResolver.Resolve(search, item);
            if (string.IsNullOrEmpty(searchPattern))
            {
                return targetText;
            }

            if (step.UseRegex)
            {
                var options = step.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(searchPattern, options, RegexTimeout);
                return regex.Replace(targetText, string.Empty);
            }
            return targetText.Replace(searchPattern, string.Empty, step.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        int count = Math.Max(1, step.CharacterCount);
        int startIndex = CalculateRemoveStartIndex(step.Position, step.PositionIndex, targetText.Length, count);

        if (startIndex < 0 || startIndex >= targetText.Length)
        {
            return targetText;
        }

        int actualCount = Math.Min(count, targetText.Length - startIndex);
        return targetText.Remove(startIndex, actualCount);
    }

    private static string ExecuteCaseConversion(RenameMethodStep step, string targetText)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        return step.CaseType switch
        {
            CaseTransformType.Lowercase => targetText.ToLowerInvariant(),
            CaseTransformType.Uppercase => targetText.ToUpperInvariant(),
            CaseTransformType.TitleCase => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(targetText.ToLowerInvariant()),
            CaseTransformType.CapitalizeFirst => char.ToUpperInvariant(targetText[0]) + targetText.Substring(1),
            CaseTransformType.SentenceCase => ToSentenceCase(targetText),
            _ => targetText
        };
    }

    private static string ToSentenceCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsLetter(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
                if (c == '.' || c == '!' || c == '?')
                {
                    capitalizeNext = true;
                }
            }
        }

        return sb.ToString();
    }

    private static string ExecuteNumbering(
        RenameMethodStep step,
        string targetText,
        FileItemContext item,
        RenameBatchContext batchContext)
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

        int insertIndex = CalculateInsertIndex(step.Position, step.PositionIndex, targetText.Length);
        return targetText.Insert(insertIndex, formattedNumber);
    }

    private static string ExecuteReplaceList(RenameMethodStep step, string targetText, FileItemContext item)
    {
        if (step.ReplaceList == null || step.ReplaceList.Count == 0)
        {
            return targetText;
        }

        string result = targetText;
        foreach (var entry in step.ReplaceList)
        {
            if (string.IsNullOrEmpty(entry.Find)) continue;

            string findText = VariableTemplateResolver.Resolve(entry.Find, item);
            if (string.IsNullOrEmpty(findText)) continue;

            if (entry.UseRegex)
            {
                var options = entry.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(findText, options, RegexTimeout);
                result = VariableTemplateResolver.ApplyRegexReplacement(regex, result, entry.ReplaceWith ?? string.Empty, item, replaceAll: true);
            }
            else
            {
                string replaceWith = VariableTemplateResolver.Resolve(entry.ReplaceWith ?? string.Empty, item);
                result = result.Replace(findText, replaceWith, entry.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }

    private static string ExecuteTrimClean(RenameMethodStep step, string targetText)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        string result = targetText;

        if (step.TrimWhitespace)
        {
            result = result.Trim();
        }

        if (step.CollapseSpaces)
        {
            result = Regex.Replace(result, @"\s+", " ", RegexOptions.None, RegexTimeout);
        }

        if (step.SanitizeInvalidChars)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            if (result.IndexOfAny(invalid) >= 0)
            {
                var sb = new StringBuilder(result.Length);
                foreach (char c in result)
                {
                    sb.Append(invalid.Contains(c) ? step.InvalidCharReplacement : c);
                }
                result = sb.ToString();
            }
        }

        if (step.NormalizationMode != UnicodeNormalizationMode.None)
        {
            result = step.NormalizationMode switch
            {
                UnicodeNormalizationMode.FormC => result.Normalize(NormalizationForm.FormC),
                UnicodeNormalizationMode.FormD => result.Normalize(NormalizationForm.FormD),
                UnicodeNormalizationMode.FormKC => result.Normalize(NormalizationForm.FormKC),
                UnicodeNormalizationMode.FormKD => result.Normalize(NormalizationForm.FormKD),
                _ => result
            };
        }

        return result;
    }

    private static int CalculateInsertIndex(CharacterPosition pos, int index, int length)
    {
        return pos switch
        {
            CharacterPosition.FromStart => Math.Clamp(index, 0, length),
            CharacterPosition.FromEnd => Math.Clamp(length - index, 0, length),
            CharacterPosition.AbsoluteIndex => Math.Clamp(index, 0, length),
            _ => length
        };
    }

    private static int CalculateRemoveStartIndex(CharacterPosition pos, int index, int length, int count)
    {
        return pos switch
        {
            CharacterPosition.FromStart => Math.Clamp(index, 0, length),
            CharacterPosition.FromEnd => Math.Clamp(length - index - count, 0, length),
            CharacterPosition.AbsoluteIndex => Math.Clamp(index, 0, length),
            _ => 0
        };
    }

    private static string ExecuteNormalizeNumbers(RenameMethodStep step, string targetText, FileItemContext item)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        int padding = Math.Max(1, step.NumberPaddingDigits);

        switch (step.NumberTarget)
        {
            case NumberPaddingTarget.AllNumbers:
                return Regex.Replace(targetText, @"\d+", m => m.Value.PadLeft(padding, '0'), RegexOptions.None, RegexTimeout);

            case NumberPaddingTarget.FirstNumber:
            {
                var match = Regex.Match(targetText, @"\d+", RegexOptions.None, RegexTimeout);
                if (match.Success)
                {
                    string padded = match.Value.PadLeft(padding, '0');
                    return string.Concat(targetText.AsSpan(0, match.Index), padded, targetText.AsSpan(match.Index + match.Length));
                }
                return targetText;
            }

            case NumberPaddingTarget.LastNumber:
            {
                var matches = Regex.Matches(targetText, @"\d+", RegexOptions.None, RegexTimeout);
                if (matches.Count > 0)
                {
                    var match = matches[^1];
                    string padded = match.Value.PadLeft(padding, '0');
                    return string.Concat(targetText.AsSpan(0, match.Index), padded, targetText.AsSpan(match.Index + match.Length));
                }
                return targetText;
            }

            case NumberPaddingTarget.EpisodeFormat:
            {
                // 1. Patrón 'NxN' (ej. 1x2 -> 1x02 o 01x02)
                var nxnRegex = new Regex(@"(?<season>\d+)(?<sep>[xX])(?<episode>\d+)", RegexOptions.None, RegexTimeout);
                if (nxnRegex.IsMatch(targetText))
                {
                    return nxnRegex.Replace(targetText, m =>
                    {
                        string season = m.Groups["season"].Value;
                        string sep = m.Groups["sep"].Value;
                        string episode = m.Groups["episode"].Value;

                        string formattedSeason = step.PadSeasonAndEpisode ? season.PadLeft(padding, '0') : season;
                        string formattedEpisode = episode.PadLeft(padding, '0');
                        return $"{formattedSeason}{sep}{formattedEpisode}";
                    });
                }

                // 2. Patrón 'S01E02'
                var sxeRegex = new Regex(@"(?<sPrefix>[sS])(?<season>\d+)(?<ePrefix>[eE])(?<episode>\d+)", RegexOptions.None, RegexTimeout);
                if (sxeRegex.IsMatch(targetText))
                {
                    return sxeRegex.Replace(targetText, m =>
                    {
                        string sPrefix = m.Groups["sPrefix"].Value;
                        string season = m.Groups["season"].Value;
                        string ePrefix = m.Groups["ePrefix"].Value;
                        string episode = m.Groups["episode"].Value;

                        string formattedSeason = season.PadLeft(padding, '0');
                        string formattedEpisode = episode.PadLeft(padding, '0');
                        return $"{sPrefix}{formattedSeason}{ePrefix}{formattedEpisode}";
                    });
                }

                // 3. Patrón 'Capítulo 1', 'Episodio 2', 'Track 3', etc.
                var namedSeqRegex = new Regex(@"(?i)(?<prefix>\b(?:Cap[íi]tulo|Cap\.?|Episodio|Ep\.?|Temporada|Temp\.?|Part\.?|Parte|Track|Pista|Vol\.?|Volumen)\s*)(?<num>\d+)", RegexOptions.None, RegexTimeout);
                if (namedSeqRegex.IsMatch(targetText))
                {
                    return namedSeqRegex.Replace(targetText, m =>
                    {
                        string prefix = m.Groups["prefix"].Value;
                        string num = m.Groups["num"].Value;
                        return $"{prefix}{num.PadLeft(padding, '0')}";
                    });
                }

                // Fallback: normalizar el último número encontrado
                var fallbackMatches = Regex.Matches(targetText, @"\d+", RegexOptions.None, RegexTimeout);
                if (fallbackMatches.Count > 0)
                {
                    var match = fallbackMatches[^1];
                    string padded = match.Value.PadLeft(padding, '0');
                    return string.Concat(targetText.AsSpan(0, match.Index), padded, targetText.AsSpan(match.Index + match.Length));
                }

                return targetText;
            }

            case NumberPaddingTarget.CustomRegex:
            {
                if (string.IsNullOrWhiteSpace(step.NumberRegexPattern))
                {
                    return Regex.Replace(targetText, @"\d+", m => m.Value.PadLeft(padding, '0'), RegexOptions.None, RegexTimeout);
                }

                string pattern = VariableTemplateResolver.Resolve(step.NumberRegexPattern, item);
                var options = step.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                var customRegex = new Regex(pattern, options, RegexTimeout);

                return customRegex.Replace(targetText, match =>
                {
                    // Si el regex tiene grupos de captura con dígitos, rellenar los dígitos
                    return Regex.Replace(match.Value, @"\d+", m => m.Value.PadLeft(padding, '0'), RegexOptions.None, RegexTimeout);
                });
            }

            default:
                return Regex.Replace(targetText, @"\d+", m => m.Value.PadLeft(padding, '0'), RegexOptions.None, RegexTimeout);
        }
    }
}
