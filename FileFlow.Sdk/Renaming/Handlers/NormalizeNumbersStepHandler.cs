using System.Text.RegularExpressions;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la normalización de dígitos numéricos (01, 02...) con soporte para formato serie/episodio y expresiones regulares personalizadas.
/// </summary>
internal sealed class NormalizeNumbersStepHandler : IRenameStepHandler
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public RenameMethodType SupportedType => RenameMethodType.NormalizeNumbers;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
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
                    return Regex.Replace(match.Value, @"\d+", m => m.Value.PadLeft(padding, '0'), RegexOptions.None, RegexTimeout);
                });
            }

            default:
                return Regex.Replace(targetText, @"\d+", m => m.Value.PadLeft(padding, '0'), RegexOptions.None, RegexTimeout);
        }
    }
}
