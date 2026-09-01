using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Par clave-valor para la tabla de sustituciones masivas.
/// </summary>
public sealed class ReplaceListEntry
{
    public string Find { get; set; } = string.Empty;
    public string ReplaceWith { get; set; } = string.Empty;
    public bool MatchCase { get; set; }
    public bool UseRegex { get; set; }

    public ReplaceListEntry Clone() => new()
    {
        Find = Find,
        ReplaceWith = ReplaceWith,
        MatchCase = MatchCase,
        UseRegex = UseRegex
    };
}

/// <summary>
/// Paso configurable dentro del pipeline de métodos acumulativos de renombrado.
/// </summary>
public sealed class RenameMethodStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public RenameMethodType MethodType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public ApplyToTarget ApplyTo { get; set; } = ApplyToTarget.NameOnly;

    // Propiedades de configuración comunes / específicas
    public string Pattern { get; set; } = string.Empty;           // Para NewName o Insert
    public string SearchText { get; set; } = string.Empty;        // Para SearchReplace
    public string ReplaceText { get; set; } = string.Empty;       // Para SearchReplace
    public bool UseRegex { get; set; }
    public bool MatchCase { get; set; }
    public bool ReplaceAll { get; set; } = true;

    public CharacterPosition Position { get; set; } = CharacterPosition.FromStart;
    public int PositionIndex { get; set; } = 0;                  // 0-based o 1-based según convención
    public int CharacterCount { get; set; } = 1;                  // Para Remove

    public CaseTransformType CaseType { get; set; } = CaseTransformType.Lowercase;

    public int StartNumber { get; set; } = 1;                     // Para Numbering
    public int Increment { get; set; } = 1;                       // Para Numbering
    public int PaddingZeroes { get; set; } = 3;                   // Para Numbering (ej. 3 -> 001)
    public NumberingResetOn ResetOn { get; set; } = NumberingResetOn.Never;
    public string ResetMetadataKey { get; set; } = string.Empty;

    public List<ReplaceListEntry> ReplaceList { get; set; } = []; // Para ReplaceList

    public bool TrimWhitespace { get; set; } = true;              // Para TrimClean
    public bool CollapseSpaces { get; set; } = true;              // Para TrimClean
    public bool SanitizeInvalidChars { get; set; } = true;        // Para TrimClean
    public char InvalidCharReplacement { get; set; } = '_';       // Para TrimClean
    public UnicodeNormalizationMode NormalizationMode { get; set; } = UnicodeNormalizationMode.None;

    // Para NormalizeNumbers (Relleno y Normalización de Números / Secuencias / Episodios)
    public NumberPaddingTarget NumberTarget { get; set; } = NumberPaddingTarget.AllNumbers;
    public int NumberPaddingDigits { get; set; } = 2;
    public string NumberRegexPattern { get; set; } = string.Empty;
    public bool PadSeasonAndEpisode { get; set; } = false;

    public RenameMethodStep Clone()
    {
        return new RenameMethodStep
        {
            Id = Guid.NewGuid().ToString("N"),
            MethodType = this.MethodType,
            IsEnabled = this.IsEnabled,
            Name = this.Name,
            ApplyTo = this.ApplyTo,
            Pattern = this.Pattern,
            SearchText = this.SearchText,
            ReplaceText = this.ReplaceText,
            UseRegex = this.UseRegex,
            MatchCase = this.MatchCase,
            ReplaceAll = this.ReplaceAll,
            Position = this.Position,
            PositionIndex = this.PositionIndex,
            CharacterCount = this.CharacterCount,
            CaseType = this.CaseType,
            StartNumber = this.StartNumber,
            Increment = this.Increment,
            PaddingZeroes = this.PaddingZeroes,
            ResetOn = this.ResetOn,
            ResetMetadataKey = this.ResetMetadataKey,
            ReplaceList = this.ReplaceList.Select(e => e.Clone()).ToList(),
            TrimWhitespace = this.TrimWhitespace,
            CollapseSpaces = this.CollapseSpaces,
            SanitizeInvalidChars = this.SanitizeInvalidChars,
            InvalidCharReplacement = this.InvalidCharReplacement,
            NormalizationMode = this.NormalizationMode,
            NumberTarget = this.NumberTarget,
            NumberPaddingDigits = this.NumberPaddingDigits,
            NumberRegexPattern = this.NumberRegexPattern,
            PadSeasonAndEpisode = this.PadSeasonAndEpisode
        };
    }
}

/// <summary>
/// Contexto compartido de lote durante la ejecución de un flujo para contadores e índices.
/// </summary>
public sealed class RenameBatchContext
{
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _lastGroupKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public int TotalItemCount { get; set; } = 1;

    public int GetNextSequenceNumber(string stepId, int startNumber, int increment, NumberingResetOn resetOn, string currentDirectory, string currentGroupKey)
    {
        lock (_lock)
        {
            string stateKey = stepId;
            string currentScopeKey = resetOn switch
            {
                NumberingResetOn.DirectoryChange => currentDirectory,
                NumberingResetOn.MetadataChange => currentGroupKey,
                _ => string.Empty
            };

            if (resetOn != NumberingResetOn.Never)
            {
                string lastScope = _lastGroupKeys.GetValueOrDefault(stateKey, string.Empty);
                if (!string.Equals(lastScope, currentScopeKey, StringComparison.OrdinalIgnoreCase))
                {
                    _counters[stateKey] = startNumber;
                    _lastGroupKeys[stateKey] = currentScopeKey;
                    int initialSeq = startNumber;
                    _counters[stateKey] = startNumber + increment;
                    return initialSeq;
                }
            }

            int currentVal = _counters.GetOrAdd(stateKey, startNumber);
            _counters[stateKey] = currentVal + increment;
            return currentVal;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _counters.Clear();
            _lastGroupKeys.Clear();
        }
    }
}

/// <summary>
/// Traza diagnóstica de un paso individual ejecutado en el pipeline.
/// </summary>
public sealed record RenameStepTrace(
    string StepId,
    RenameMethodType MethodType,
    string InputName,
    string OutputName,
    bool WasModified,
    string? Description = null);

/// <summary>
/// Resultado integral tras ejecutar el pipeline de renombrado sobre un archivo.
/// </summary>
public sealed record RenameResult(
    string OriginalFileName,
    string ResultFileName,
    IReadOnlyList<RenameStepTrace> Traces,
    bool HasChanges,
    string? ErrorMessage = null);
