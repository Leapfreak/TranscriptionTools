Imports System.Text.Json.Serialization

Namespace Models
    Public Class AppConfig

        ' --- Paths & Tools ---

        Public Property PathWhisper As String = ".\whisper\whisper-cli.exe"

        Public Property PathYtdlp As String = ".\yt-dlp.exe"

        Public Property PathFfmpeg As String = ".\ffmpeg.exe"

        Public Property PathFfprobe As String = ".\ffprobe.exe"

        Public Property PathModel As String = ".\ggml-large-v3.bin"

        Public Property PathModelAudio As String = ".\ggml-large-v3.bin"

        Public Property PathSubtitleEdit As String = ".\SubtitleEdit\SubtitleEdit.exe"

        Public Property PathOutputRoot As String = "."

        Public Property YtdlpFormat As String = "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]"

        ' --- Settings tab ---

        Public Property UiLanguage As String = "en"

        Public Property ParallelJobs As Integer = 4

        Public Property ChunkSizeSec As Integer = 300

        Public Property PollIntervalMs As Integer = 2000

        Public Property ChunkTimeoutMin As Integer = 60

        Public Property KeepChunkFiles As Boolean = False

        Public Property KeepPreview As Boolean = True

        Public Property SkipDownloadIfExists As Boolean = False

        Public Property Theme As String = "System"

        Public Property LastLiveDeviceId As String = ""

        ' --- Output formats (Main tab) ---

        Public Property OutputSrt As Boolean = True

        Public Property OutputVtt As Boolean = False

        Public Property OutputTxt As Boolean = False

        Public Property OutputJson As Boolean = False

        Public Property OutputCsv As Boolean = False

        Public Property OutputLrc As Boolean = False

        ' --- Whisper Parameters ---

        Public Property Language As String = "auto"

        Public Property OutputLanguage As String = "en"

        Public Property Threads As Integer = 4

        Public Property Processors As Integer = 1

        Public Property BeamSize As Integer = 5

        Public Property BestOf As Integer = 5

        Public Property Temperature As Single = 0.0F

        Public Property TemperatureInc As Single = 0.2F

        Public Property MaxContext As Integer = 0

        Public Property WordThreshold As Single = 0.01F

        Public Property EntropyThreshold As Single = 2.4F

        Public Property LogProbThreshold As Single = -1.0F

        Public Property NoSpeechThreshold As Single = 0.6F

        Public Property SplitOnWord As Boolean = True

        Public Property NoGpu As Boolean = False

        Public Property FlashAttn As Boolean = False

        Public Property PrintProgress As Boolean = False

        Public Property PrintColours As Boolean = False

        Public Property Diarize As Boolean = False

        Public Property Tinydiarize As Boolean = False

        Public Property PrintRealtime As Boolean = False

        Public Property NoTimestamps As Boolean = False

        Public Property MaxSegmentLength As Integer = 0

        Public Property MaxTokens As Integer = 0

        Public Property AudioContext As Integer = 0

        Public Property InitialPrompt As String = ""

        Public Property Hotwords As String = ""

        Public Property TranslateToEnglish As Boolean = False

        Public Property VadThreshold As Single = 0.6F

        Public Property FreqThreshold As Integer = 100

        ' --- Subtitle Server ---

        Public Property SubtitleServerPort As Integer = 5080

        Public Property SubtitleBgColor As String = "#000000"

        Public Property SubtitleFgColor As String = "#FFFFFF"

        Public Property SubtitleFontFamily As String = "Segoe UI"
        Public Property SubtitleFontSize As Single = 14
        Public Property SubtitleFontBold As Boolean = True

        ' --- Live Server (faster-whisper + VAD) ---

        Public Property LiveServerPort As Integer = 5091
        Public Property PathFasterWhisperModel As String = ".\faster-whisper-large-v3"
        Public Property LiveComputeType As String = "int8_float16"
        Public Property LiveVadSilenceMs As Integer = 600
        Public Property LiveMaxSegmentSec As Integer = 30
        Public Property LiveInterimIntervalMs As Integer = 1000

        ' --- Translation (NLLB-200) ---

        Public Property TranslationEnabled As Boolean = True
        Public Property TranslationPort As Integer = 5090
        Public Property TranslationModelPath As String = ".\nllb-model"
        Public Property TranslationDevice As String = "cuda"
        Public Property TranslationUnloadMinutes As Integer = 10
        Public Property TranslationGlossaryPath As String = ".\nllb-server\glossary.json"

        Public Property FirstRunComplete As Boolean = False
        Public Property StartWithWindows As Boolean = False
        Public Property AllowFirewall As Boolean = False

        Public Shared Function CreateDefault() As AppConfig
            Return New AppConfig()
        End Function

        Public Shared Function ResolvePath(configPath As String) As String
            If String.IsNullOrWhiteSpace(configPath) Then Return ""
            If IO.Path.IsPathRooted(configPath) Then Return configPath
            Return IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath)
        End Function
    End Class
End Namespace
