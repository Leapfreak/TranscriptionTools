Imports TranscriptionTools.Models

Namespace Pipeline
    Public Class TranscriptionCommandBuilder

        Public Shared Function Build(config As AppConfig, audioFile As String) As String
            Dim args As New List(Of String)

            ' Model
            args.Add($"-m ""{AppConfig.ResolvePath(config.PathModel)}""")

            ' Input file
            args.Add($"-f ""{audioFile}""")

            ' Language
            args.Add($"-l {config.Language}")

            ' Numeric parameters
            args.Add($"-t {config.Threads}")
            args.Add($"-p {config.Processors}")
            args.Add($"-bs {config.BeamSize}")
            args.Add($"-bo {config.BestOf}")
            args.Add($"-tp {config.Temperature:F1}")
            args.Add($"-tpi {config.TemperatureInc:F1}")
            args.Add($"-mc {config.MaxContext}")
            args.Add($"-wt {config.WordThreshold:F2}")
            args.Add($"-et {config.EntropyThreshold:F1}")
            args.Add($"-lpt {config.LogProbThreshold:F1}")
            args.Add($"-nth {config.NoSpeechThreshold:F1}")
            args.Add($"-ml {config.MaxSegmentLength}")
            args.Add($"-ac {config.AudioContext}")

            ' VAD (only if threshold differs from default, use new-style flags)
            args.Add($"-vt {config.VadThreshold:F1}")

            ' Output format flags
            If config.OutputSrt Then args.Add("-osrt")
            If config.OutputVtt Then args.Add("-ovtt")
            If config.OutputTxt Then args.Add("-otxt")
            If config.OutputJson Then args.Add("-ojf")
            If config.OutputCsv Then args.Add("-ocsv")
            If config.OutputLrc Then args.Add("-olrc")

            ' Boolean flags
            If config.SplitOnWord Then args.Add("-sow")
            If config.NoGpu Then args.Add("-ng")
            If config.FlashAttn Then args.Add("-fa")
            If config.PrintProgress Then args.Add("-pp")
            If config.PrintColours Then args.Add("-pc")
            If config.Diarize Then args.Add("-di")
            If config.Tinydiarize Then args.Add("-tdrz")
            If config.NoTimestamps Then args.Add("-nt")
            If config.OutputLanguage = "en" AndAlso config.Language <> "en" Then args.Add("-tr")

            ' String parameters (only if non-empty)
            If Not String.IsNullOrWhiteSpace(config.InitialPrompt) Then
                args.Add($"--prompt ""{config.InitialPrompt}""")
            End If

            Return String.Join(" ", args)
        End Function
    End Class
End Namespace
