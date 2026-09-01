using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class RenameTransformEngineTests
{
    private readonly RenameTransformEngine _engine = new();

    private static FileItemContext CreateContext(string path, Dictionary<string, object?>? metadata = null)
    {
        var item = new FileItemContext(path)
        {
            FileSizeBytes = 2048
        };
        if (metadata != null)
        {
            foreach (var (k, v) in metadata)
            {
                item.Metadata[k] = v;
            }
        }
        return item;
    }

    [Fact]
    public void Transform_NewNameMethod_ShouldEvaluateClassicTagsAndProperties()
    {
        // Arrange
        var item = CreateContext(@"C:\Photos\DSC_0042.jpg", new Dictionary<string, object?>
        {
            ["Exif:CameraModel"] = "SonyA7",
            ["DateTaken"] = new DateTime(2026, 8, 15)
        });
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.NewName,
                ApplyTo = ApplyToTarget.NameOnly,
                Pattern = "<Date Taken:yyyyMMdd>_<Exif:CameraModel>_<FileNameNoExt>",
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("DSC_0042.jpg", item, steps, batch);

        // Assert
        result.HasChanges.Should().BeTrue();
        result.ResultFileName.Should().Be("20260815_SonyA7_DSC_0042.jpg");
        result.Traces.Should().HaveCount(1);
        result.Traces[0].WasModified.Should().BeTrue();
    }

    [Fact]
    public void Transform_SearchReplace_RegexAndGroups_ShouldReplaceCorrectly()
    {
        // Arrange
        var item = CreateContext(@"C:\Files\Track_01_Rock.mp3");
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.SearchReplace,
                ApplyTo = ApplyToTarget.NameOnly,
                UseRegex = true,
                SearchText = @"Track_(\d+)_(.+)",
                ReplaceText = "$2 - Track $1",
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("Track_01_Rock.mp3", item, steps, batch);

        // Assert
        result.ResultFileName.Should().Be("Rock - Track 01.mp3");
    }

    [Fact]
    public void Transform_InsertAndRemove_ShouldModifyPositionsAccurately()
    {
        // Arrange
        var item = CreateContext(@"C:\Files\Document_Final_v1.pdf");
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.Insert,
                ApplyTo = ApplyToTarget.NameOnly,
                Position = CharacterPosition.FromStart,
                PositionIndex = 0,
                Pattern = "CONFIDENTIAL_",
                IsEnabled = true
            },
            new()
            {
                MethodType = RenameMethodType.Remove,
                ApplyTo = ApplyToTarget.NameOnly,
                Position = CharacterPosition.FromEnd,
                PositionIndex = 0,
                CharacterCount = 3, // Elimina "_v1" desde el final del nombre
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("Document_Final_v1.pdf", item, steps, batch);

        // Assert
        result.ResultFileName.Should().Be("CONFIDENTIAL_Document_Final.pdf");
    }

    [Theory]
    [InlineData("hello world.txt", CaseTransformType.Uppercase, "HELLO WORLD.txt")]
    [InlineData("HELLO WORLD.txt", CaseTransformType.Lowercase, "hello world.txt")]
    [InlineData("hello world.txt", CaseTransformType.TitleCase, "Hello World.txt")]
    [InlineData("hello world.txt", CaseTransformType.CapitalizeFirst, "Hello world.txt")]
    [InlineData("hello world. this is test.txt", CaseTransformType.SentenceCase, "Hello world. This is test.txt")]
    public void Transform_CaseConversion_ShouldConvertAccurately(string input, CaseTransformType caseType, string expected)
    {
        // Arrange
        var item = CreateContext(@"C:\Files\" + input);
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.CaseConversion,
                ApplyTo = ApplyToTarget.NameOnly,
                CaseType = caseType,
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform(input, item, steps, batch);

        // Assert
        result.ResultFileName.Should().Be(expected);
    }

    [Fact]
    public void Transform_Numbering_WithPaddingAndReset_ShouldSequenceCorrectly()
    {
        // Arrange
        var batch = new RenameBatchContext();
        var step = new RenameMethodStep
        {
            Id = "step-num",
            MethodType = RenameMethodType.Numbering,
            ApplyTo = ApplyToTarget.NameOnly,
            Position = CharacterPosition.FromStart,
            PositionIndex = 0,
            StartNumber = 1,
            Increment = 1,
            PaddingZeroes = 3,
            ResetOn = NumberingResetOn.DirectoryChange,
            IsEnabled = true
        };
        var steps = new List<RenameMethodStep> { step };

        var itemFolderA1 = CreateContext(@"C:\FolderA\file1.txt");
        var itemFolderA2 = CreateContext(@"C:\FolderA\file2.txt");
        var itemFolderB1 = CreateContext(@"C:\FolderB\file1.txt");

        // Act
        var res1 = _engine.Transform("file1.txt", itemFolderA1, steps, batch);
        var res2 = _engine.Transform("file2.txt", itemFolderA2, steps, batch);
        var res3 = _engine.Transform("file1.txt", itemFolderB1, steps, batch);

        // Assert
        res1.ResultFileName.Should().Be("001file1.txt");
        res2.ResultFileName.Should().Be("002file2.txt");
        res3.ResultFileName.Should().Be("001file1.txt"); // Reiniciado por cambio de carpeta
    }

    [Fact]
    public void Transform_ReplaceList_ShouldApplyTableSubstitutions()
    {
        // Arrange
        var item = CreateContext(@"C:\Files\bad_word_sample_draft_v2.txt");
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.ReplaceList,
                ApplyTo = ApplyToTarget.NameOnly,
                ReplaceList =
                [
                    new ReplaceListEntry { Find = "bad_word", ReplaceWith = "good_term" },
                    new ReplaceListEntry { Find = "_draft", ReplaceWith = string.Empty }, // Eliminación
                    new ReplaceListEntry { Find = "v2", ReplaceWith = "FINAL" }
                ],
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("bad_word_sample_draft_v2.txt", item, steps, batch);

        // Assert
        result.ResultFileName.Should().Be("good_term_sample_FINAL.txt");
    }

    [Fact]
    public void Transform_TrimClean_ShouldSanitizeAndNormalize()
    {
        // Arrange
        var item = CreateContext(@"C:\Files\  invalid:name*test?  .txt");
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.TrimClean,
                ApplyTo = ApplyToTarget.FullName,
                TrimWhitespace = true,
                CollapseSpaces = true,
                SanitizeInvalidChars = true,
                InvalidCharReplacement = '_',
                NormalizationMode = UnicodeNormalizationMode.FormC,
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("  invalid:name*test?  .txt", item, steps, batch);

        // Assert
        result.ResultFileName.Should().Be("invalid_name_test_ .txt");
        result.ResultFileName.Should().NotContain(":").And.NotContain("*").And.NotContain("?");
    }

    [Fact]
    public void Transform_CumulativePipeline_AllSevenMethods_ShouldExecuteInSequence()
    {
        // Arrange
        var item = CreateContext(@"C:\Music\01 - my_favorite_song [raw].flac", new Dictionary<string, object?>
        {
            ["Audio:Artist"] = "DaftPunk",
            ["Year"] = "2026"
        });
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            // 1. Plantilla inicial
            new()
            {
                MethodType = RenameMethodType.NewName,
                ApplyTo = ApplyToTarget.NameOnly,
                Pattern = "<Audio:Artist> - <FileNameNoExt>",
                IsEnabled = true
            },
            // 2. Reemplazo de caracteres
            new()
            {
                MethodType = RenameMethodType.SearchReplace,
                ApplyTo = ApplyToTarget.NameOnly,
                SearchText = "[raw]",
                ReplaceText = "Remastered",
                IsEnabled = true
            },
            // 3. Inserción
            new()
            {
                MethodType = RenameMethodType.Insert,
                ApplyTo = ApplyToTarget.NameOnly,
                Position = CharacterPosition.FromStart,
                PositionIndex = 0,
                Pattern = "<Year>_",
                IsEnabled = true
            },
            // 4. Case Conversion
            new()
            {
                MethodType = RenameMethodType.CaseConversion,
                ApplyTo = ApplyToTarget.ExtensionOnly,
                CaseType = CaseTransformType.Lowercase,
                IsEnabled = true
            },
            // 5. Tabla de sustitución
            new()
            {
                MethodType = RenameMethodType.ReplaceList,
                ApplyTo = ApplyToTarget.NameOnly,
                ReplaceList =
                [
                    new ReplaceListEntry { Find = "my_favorite_song", ReplaceWith = "AroundTheWorld" }
                ],
                IsEnabled = true
            },
            // 6. Trim / Clean
            new()
            {
                MethodType = RenameMethodType.TrimClean,
                ApplyTo = ApplyToTarget.FullName,
                CollapseSpaces = true,
                SanitizeInvalidChars = true,
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("01 - my_favorite_song [raw].FLAC", item, steps, batch);

        // Assert
        result.HasChanges.Should().BeTrue();
        result.ResultFileName.Should().Be("2026_DaftPunk - 01 - AroundTheWorld Remastered.flac");
        result.Traces.Should().HaveCount(6);
    }

    [Fact]
    public void Transform_ExpressionFunctionsAndUpstreamVariables_ShouldEvaluateInAllMethods()
    {
        // Arrange
        var item = CreateContext(@"C:\Projects\document_draft_v1.docx", new Dictionary<string, object?>
        {
            ["Department"] = "Finance",
            ["ProjectCode"] = "PRJ-900",
            ["Author"] = "alice",
            ["Hash:SHA256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
        });
        var batch = new RenameBatchContext();
        var steps = new List<RenameMethodStep>
        {
            // 1. NewName con Upper() y Coalesce()
            new()
            {
                MethodType = RenameMethodType.NewName,
                ApplyTo = ApplyToTarget.NameOnly,
                Pattern = "{Upper(Department)}_{Coalesce(ProjectCode, \"UNKNOWN\")}_{FileNameNoExt}",
                IsEnabled = true
            },
            // 2. SearchReplace evaluando variables en Search y Replace
            new()
            {
                MethodType = RenameMethodType.SearchReplace,
                ApplyTo = ApplyToTarget.NameOnly,
                SearchText = "draft",
                ReplaceText = "{Upper(Author)}",
                IsEnabled = true
            },
            // 3. Inserción con Substring() de Hash
            new()
            {
                MethodType = RenameMethodType.Insert,
                ApplyTo = ApplyToTarget.NameOnly,
                Position = CharacterPosition.FromEnd,
                PositionIndex = 0,
                Pattern = "_{Substring(Hash:SHA256, 0, 8)}",
                IsEnabled = true
            },
            // 4. ReplaceList con variables en ReplaceWith
            new()
            {
                MethodType = RenameMethodType.ReplaceList,
                ApplyTo = ApplyToTarget.NameOnly,
                ReplaceList =
                [
                    new ReplaceListEntry { Find = "v1", ReplaceWith = "{ProjectCode}" }
                ],
                IsEnabled = true
            }
        };

        // Act
        var result = _engine.Transform("document_draft_v1.docx", item, steps, batch);

        // Assert
        result.HasChanges.Should().BeTrue();
        // 1: FINANCE_PRJ-900_document_draft_v1
        // 2: FINANCE_PRJ-900_document_ALICE_v1
        // 3: FINANCE_PRJ-900_document_ALICE_v1_e3b0c442
        // 4: FINANCE_PRJ-900_document_ALICE_PRJ-900_e3b0c442
        result.ResultFileName.Should().Be("FINANCE_PRJ-900_document_ALICE_PRJ-900_e3b0c442.docx");
    }

    [Theory]
    [InlineData("1 - pepe.jpg", "01 - pepe.jpg", 2)]
    [InlineData("2 - jaco.jpg", "02 - jaco.jpg", 2)]
    [InlineData("10 - kilo.jpg", "10 - kilo.jpg", 2)]
    [InlineData("5 - doc.pdf", "005 - doc.pdf", 3)]
    public void Transform_NormalizeNumbers_FirstAndAllNumbers_ShouldPadSequences(string input, string expected, int padding)
    {
        // Arrange
        var item = CreateContext(Path.Combine(@"C:\Temp", input));
        var batch = new RenameBatchContext();
        var step = new RenameMethodStep
        {
            MethodType = RenameMethodType.NormalizeNumbers,
            ApplyTo = ApplyToTarget.NameOnly,
            NumberTarget = NumberPaddingTarget.FirstNumber,
            NumberPaddingDigits = padding,
            IsEnabled = true
        };

        // Act
        var result = _engine.Transform(input, item, [step], batch);

        // Assert
        result.ResultFileName.Should().Be(expected);
    }

    [Theory]
    [InlineData("serie guapa 1x1.mov", "serie guapa 1x01.mov", false)]
    [InlineData("serie guapa papo 1x2.mov", "serie guapa papo 1x02.mov", false)]
    [InlineData("serie guapa jose 1x10.mov", "serie guapa jose 1x10.mov", false)]
    [InlineData("serie guapa 1x1.mov", "serie guapa 01x01.mov", true)]
    [InlineData("Breaking Bad S1E2 Pilot.mkv", "Breaking Bad S01E02 Pilot.mkv", false)]
    [InlineData("Capitulo 3.mp4", "Capitulo 03.mp4", false)]
    [InlineData("Track 7.flac", "Track 07.flac", false)]
    public void Transform_NormalizeNumbers_EpisodeFormat_ShouldPadAccurately(string input, string expected, bool padSeasonAndEpisode)
    {
        // Arrange
        var item = CreateContext(Path.Combine(@"C:\Temp", input));
        var batch = new RenameBatchContext();
        var step = new RenameMethodStep
        {
            MethodType = RenameMethodType.NormalizeNumbers,
            ApplyTo = ApplyToTarget.NameOnly,
            NumberTarget = NumberPaddingTarget.EpisodeFormat,
            NumberPaddingDigits = 2,
            PadSeasonAndEpisode = padSeasonAndEpisode,
            IsEnabled = true
        };

        // Act
        var result = _engine.Transform(input, item, [step], batch);

        // Assert
        result.ResultFileName.Should().Be(expected);
    }

    [Fact]
    public void Transform_SearchReplace_RegexWithTemplateVariablesAndFunctions_ShouldEvaluateAccurately()
    {
        // Arrange
        var item = CreateContext(@"C:\Temp\serie guapa 1x2.mov");
        var batch = new RenameBatchContext();
        var step = new RenameMethodStep
        {
            MethodType = RenameMethodType.SearchReplace,
            ApplyTo = ApplyToTarget.NameOnly,
            SearchText = @"(\d+)[xX](\d+)",
            ReplaceText = "Temporada {PadLeft($1, 2, 0)} Episodio {PadLeft($2, 2, 0)} ({Year})",
            UseRegex = true,
            ReplaceAll = true,
            IsEnabled = true
        };

        // Act
        var result = _engine.Transform("serie guapa 1x2.mov", item, [step], batch);

        // Assert
        string currentYear = DateTime.Now.Year.ToString();
        result.ResultFileName.Should().Be($"serie guapa Temporada 01 Episodio 02 ({currentYear}).mov");
    }

    [Fact]
    public void Transform_SearchReplace_RegexWithInjectedVariablesAndNamedGroups_ShouldEvaluateAccurately()
    {
        // Arrange
        var item = CreateContext(@"C:\Temp\4 - my video.mp4", new Dictionary<string, object?>
        {
            ["ShowPrefix"] = "HBO",
            ["Author"] = "papo"
        });
        var batch = new RenameBatchContext();
        var step = new RenameMethodStep
        {
            MethodType = RenameMethodType.SearchReplace,
            ApplyTo = ApplyToTarget.NameOnly,
            SearchText = @"(?<ep>\d+)\s*-\s*(?<name>.*)",
            ReplaceText = "{ShowPrefix}_Ep_{PadLeft(${ep}, 3, 0)}_{Upper(${name})}_by_{Author}",
            UseRegex = true,
            ReplaceAll = true,
            IsEnabled = true
        };

        // Act
        var result = _engine.Transform("4 - my video.mp4", item, [step], batch);

        // Assert
        result.ResultFileName.Should().Be("HBO_Ep_004_MY VIDEO_by_papo.mp4");
    }

    [Fact]
    public void Transform_SearchReplace_RegexWithSearchPatternVariables_ShouldResolveSearchPatternVariables()
    {
        // Arrange
        var item = CreateContext(@"C:\Temp\IMG_1234.jpg", new Dictionary<string, object?>
        {
            ["Prefix"] = "IMG"
        });
        var batch = new RenameBatchContext();
        var step = new RenameMethodStep
        {
            MethodType = RenameMethodType.SearchReplace,
            ApplyTo = ApplyToTarget.NameOnly,
            SearchText = @"{Prefix}_(\d+)",
            ReplaceText = "PHOTO_$1",
            UseRegex = true,
            ReplaceAll = true,
            IsEnabled = true
        };

        // Act
        var result = _engine.Transform("IMG_1234.jpg", item, [step], batch);

        // Assert
        result.ResultFileName.Should().Be("PHOTO_1234.jpg");
    }
}
