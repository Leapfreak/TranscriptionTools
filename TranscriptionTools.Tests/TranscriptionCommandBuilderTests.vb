Imports Xunit
Imports TranscriptionTools.Models
Imports TranscriptionTools.Pipeline

Public Class TranscriptionCommandBuilderTests

    <Fact>
    Public Sub Build_DefaultConfig_ContainsModelAndFile()
        Dim config As New AppConfig()
        Dim result = TranscriptionCommandBuilder.Build(config, ".\test\audio.wav")

        Assert.Contains("-m "".\ggml-large-v3.bin""", result)
        Assert.Contains("-f "".\test\audio.wav""", result)
    End Sub

    <Fact>
    Public Sub Build_DefaultConfig_ContainsLanguageAuto()
        Dim config As New AppConfig()
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-l auto", result)
    End Sub

    <Fact>
    Public Sub Build_DefaultConfig_ContainsNumericParams()
        Dim config As New AppConfig()
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-t 4", result)
        Assert.Contains("-p 1", result)
        Assert.Contains("-bs 5", result)
        Assert.Contains("-bo 5", result)
        Assert.Contains("-tp 0.0", result)
        Assert.Contains("-tpi 0.2", result)
        Assert.Contains("-mc 0", result)
    End Sub

    <Fact>
    Public Sub Build_DefaultConfig_IncludesSrtFlag()
        Dim config As New AppConfig()
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-osrt", result)
    End Sub

    <Fact>
    Public Sub Build_DefaultConfig_IncludesSplitOnWord()
        Dim config As New AppConfig()
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-sow", result)
    End Sub

    <Fact>
    Public Sub Build_NoOutputFormats_OmitsFormatFlags()
        Dim config As New AppConfig() With {
            .OutputSrt = False,
            .OutputVtt = False,
            .OutputTxt = False,
            .OutputJson = False,
            .OutputCsv = False,
            .OutputLrc = False
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.DoesNotContain("-osrt", result)
        Assert.DoesNotContain("-ovtt", result)
        Assert.DoesNotContain("-otxt", result)
        Assert.DoesNotContain("-ojf", result)
        Assert.DoesNotContain("-ocsv", result)
        Assert.DoesNotContain("-olrc", result)
    End Sub

    <Fact>
    Public Sub Build_AllFormatsEnabled_IncludesAllFormatFlags()
        Dim config As New AppConfig() With {
            .OutputSrt = True,
            .OutputVtt = True,
            .OutputTxt = True,
            .OutputJson = True,
            .OutputCsv = True,
            .OutputLrc = True
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-osrt", result)
        Assert.Contains("-ovtt", result)
        Assert.Contains("-otxt", result)
        Assert.Contains("-ojf", result)
        Assert.Contains("-ocsv", result)
        Assert.Contains("-olrc", result)
    End Sub

    <Fact>
    Public Sub Build_BooleanFlagsOff_OmitsBooleanFlags()
        Dim config As New AppConfig() With {
            .SplitOnWord = False,
            .NoGpu = False,
            .FlashAttn = False,
            .Diarize = False,
            .OutputLanguage = "auto"
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.DoesNotContain("-sow", result)
        Assert.DoesNotContain("-ng", result)
        Assert.DoesNotContain("-fa", result)
        Assert.DoesNotContain("-di", result)
        Assert.DoesNotContain("-tr", result)
    End Sub

    <Fact>
    Public Sub Build_BooleanFlagsOn_IncludesBooleanFlags()
        Dim config As New AppConfig() With {
            .NoGpu = True,
            .FlashAttn = True,
            .Diarize = True,
            .OutputLanguage = "en",
            .Language = "auto"
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-ng", result)
        Assert.Contains("-fa", result)
        Assert.Contains("-di", result)
        Assert.Contains("-tr", result)
    End Sub

    <Fact>
    Public Sub Build_WithInitialPrompt_IncludesPrompt()
        Dim config As New AppConfig() With {
            .InitialPrompt = "This is a test prompt"
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("--prompt ""This is a test prompt""", result)
    End Sub

    <Fact>
    Public Sub Build_EmptyPrompt_OmitsPromptFlag()
        Dim config As New AppConfig() With {
            .InitialPrompt = ""
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.DoesNotContain("--prompt", result)
    End Sub

    <Fact>
    Public Sub Build_PathWithSpaces_QuotedCorrectly()
        Dim config As New AppConfig() With {
            .PathModel = "C:\My Models\large model.bin"
        }
        Dim result = TranscriptionCommandBuilder.Build(config, "C:\My Audio\test file.wav")

        Assert.Contains("-m ""C:\My Models\large model.bin""", result)
        Assert.Contains("-f ""C:\My Audio\test file.wav""", result)
    End Sub

    <Fact>
    Public Sub Build_Deterministic_SameConfigSameOutput()
        Dim config As New AppConfig()
        Dim result1 = TranscriptionCommandBuilder.Build(config, "audio.wav")
        Dim result2 = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Equal(result1, result2)
    End Sub

    <Fact>
    Public Sub Build_CustomLanguage_UsesSpecifiedLanguage()
        Dim config As New AppConfig() With {.Language = "es"}
        Dim result = TranscriptionCommandBuilder.Build(config, "audio.wav")

        Assert.Contains("-l es", result)
    End Sub
End Class
