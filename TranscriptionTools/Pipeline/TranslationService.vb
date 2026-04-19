Imports System.Diagnostics
Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading

Namespace Pipeline
    Public Class TranslationService

        Public Event StatusChanged As EventHandler(Of String)

        Private Shared ReadOnly _httpClient As New HttpClient() With {
            .Timeout = TimeSpan.FromSeconds(3)
        }

        Private _process As Process
        Private _isRunning As Boolean = False
        Private _port As Integer = 5090
        Private _modelPath As String = ".\nllb-model"
        Private _device As String = "cuda"
        Private _glossaryPath As String = ""
        Private _modelLoaded As Boolean = False
        Private _restartCount As Integer = 0
        Private _cts As CancellationTokenSource

        ' Whisper language code -> NLLB-200 language code
        Private Shared ReadOnly _langMap As New Dictionary(Of String, String) From {
            {"es", "spa_Latn"}, {"en", "eng_Latn"}, {"ca", "cat_Latn"},
            {"fr", "fra_Latn"}, {"de", "deu_Latn"}, {"pt", "por_Latn"},
            {"it", "ita_Latn"}, {"ro", "ron_Latn"}, {"nl", "nld_Latn"},
            {"pl", "pol_Latn"}, {"ru", "rus_Cyrl"}, {"uk", "ukr_Cyrl"},
            {"zh", "zho_Hans"}, {"ja", "jpn_Jpan"}, {"ko", "kor_Hang"},
            {"ar", "arb_Arab"}, {"hi", "hin_Deva"}, {"tr", "tur_Latn"},
            {"vi", "vie_Latn"}, {"th", "tha_Thai"}, {"cs", "ces_Latn"},
            {"el", "ell_Grek"}, {"hu", "hun_Latn"}, {"da", "dan_Latn"},
            {"fi", "fin_Latn"}, {"no", "nob_Latn"}, {"sv", "swe_Latn"},
            {"sk", "slk_Latn"}, {"bg", "bul_Cyrl"}, {"hr", "hrv_Latn"},
            {"sr", "srp_Cyrl"}, {"sl", "slv_Latn"}, {"et", "est_Latn"},
            {"lv", "lvs_Latn"}, {"lt", "lit_Latn"}, {"sq", "sqi_Latn"},
            {"mk", "mkd_Cyrl"}, {"bs", "bos_Latn"}, {"is", "isl_Latn"},
            {"ms", "zsm_Latn"}, {"sw", "swh_Latn"}, {"tl", "tgl_Latn"},
            {"ta", "tam_Taml"}, {"te", "tel_Telu"}, {"ml", "mal_Mlym"},
            {"bn", "ben_Beng"}, {"gu", "guj_Gujr"}, {"kn", "kan_Knda"},
            {"mr", "mar_Deva"}, {"ne", "npi_Deva"}, {"pa", "pan_Guru"},
            {"ur", "urd_Arab"}, {"my", "mya_Mymr"}, {"km", "khm_Khmr"},
            {"ga", "gle_Latn"}, {"cy", "cym_Latn"}, {"mt", "mlt_Latn"}
        }

        Public ReadOnly Property IsRunning As Boolean
            Get
                Return _isRunning
            End Get
        End Property

        Public ReadOnly Property IsModelLoaded As Boolean
            Get
                Return _modelLoaded
            End Get
        End Property

        Public Shared Function WhisperToNllbLang(whisperLang As String) As String
            Dim nllb As String = Nothing
            If _langMap.TryGetValue(whisperLang, nllb) Then Return nllb
            Return ""
        End Function

        Public Shared Function GetLangMap() As Dictionary(Of String, String)
            Return _langMap
        End Function

        Public Shared Function CheckDependenciesInstalled() As (pythonOk As Boolean, depsOk As Boolean, modelOk As Boolean)
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
            Dim pythonPath = Path.Combine(baseDir, "python-embed", "python.exe")
            Dim pythonOk = File.Exists(pythonPath)

            Dim depsOk = False
            If pythonOk Then
                Try
                    Dim psi As New ProcessStartInfo() With {
                        .FileName = pythonPath,
                        .Arguments = "-c ""import ctranslate2; import sentencepiece; import fastapi; import uvicorn""",
                        .UseShellExecute = False,
                        .RedirectStandardOutput = True,
                        .RedirectStandardError = True,
                        .CreateNoWindow = True
                    }
                    Using proc = Process.Start(psi)
                        proc.WaitForExit(10000)
                        depsOk = (proc.ExitCode = 0)
                    End Using
                Catch
                End Try
            End If

            Dim modelDir = Path.Combine(baseDir, "nllb-model")
            Dim modelOk = Directory.Exists(modelDir) AndAlso
                          File.Exists(Path.Combine(modelDir, "model.bin")) AndAlso
                          File.Exists(Path.Combine(modelDir, "sentencepiece.model"))

            Return (pythonOk, depsOk, modelOk)
        End Function

        Public Sub Start(port As Integer, modelPath As String, device As String, Optional glossaryPath As String = "")
            If _isRunning Then Return

            _port = port
            _modelPath = modelPath
            _device = device
            _glossaryPath = glossaryPath
            _restartCount = 0
            _cts = New CancellationTokenSource()

            StartProcess()
        End Sub

        Private Sub StartProcess()
            Dim resolvedModelPath = Models.AppConfig.ResolvePath(_modelPath)
            Dim serverScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nllb-server", "server.py")

            ' Look for embedded Python first, then system Python
            Dim pythonPath = FindPython()
            If String.IsNullOrEmpty(pythonPath) Then
                RaiseEvent StatusChanged(Me, "Translation: Python not found")
                Return
            End If

            Dim glossaryArg = ""
            If Not String.IsNullOrEmpty(_glossaryPath) Then
                Dim resolvedGlossary = Models.AppConfig.ResolvePath(_glossaryPath)
                If File.Exists(resolvedGlossary) Then
                    glossaryArg = $" --glossary ""{resolvedGlossary}"""
                End If
            End If

            Dim psi As New ProcessStartInfo() With {
                .FileName = pythonPath,
                .Arguments = $"""{serverScript}"" --port {_port} --model-path ""{resolvedModelPath}"" --device {_device}{glossaryArg}",
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }

            Try
                _process = New Process()
                _process.StartInfo = psi
                _process.EnableRaisingEvents = True

                AddHandler _process.ErrorDataReceived, Sub(s, e)
                                                           If e.Data IsNot Nothing Then
                                                               Dim line = e.Data.Trim()
                                                               If line.Length > 0 Then
                                                                   RaiseEvent StatusChanged(Me, $"Translation: {line}")
                                                               End If
                                                           End If
                                                       End Sub

                AddHandler _process.Exited, Sub(s, e)
                                                _isRunning = False
                                                _modelLoaded = False
                                                If _cts IsNot Nothing AndAlso Not _cts.IsCancellationRequested Then
                                                    ' Unexpected exit — auto-restart with backoff
                                                    _restartCount += 1
                                                    If _restartCount <= 5 Then
                                                        Dim delay = Math.Min(5000 * _restartCount, 30000)
                                                        RaiseEvent StatusChanged(Me, $"Translation server crashed, restarting in {delay / 1000}s...")
                                                        Task.Delay(delay).ContinueWith(
                                                            Sub(t)
                                                                If _cts IsNot Nothing AndAlso Not _cts.IsCancellationRequested Then
                                                                    StartProcess()
                                                                End If
                                                            End Sub)
                                                    Else
                                                        RaiseEvent StatusChanged(Me, "Translation server failed too many times, giving up")
                                                    End If
                                                End If
                                            End Sub

                _process.Start()
                _process.BeginErrorReadLine()
                ' Drain stdout to prevent deadlock
                Task.Run(Sub()
                             Try : _process.StandardOutput.ReadToEnd() : Catch : End Try
                         End Sub)

                _isRunning = True
                RaiseEvent StatusChanged(Me, $"Translation server starting on port {_port}...")

                ' Wait for health check in background
                Task.Run(Sub() WaitForReady(_cts.Token))
            Catch ex As Exception
                _isRunning = False
                RaiseEvent StatusChanged(Me, $"Translation: Failed to start: {ex.Message}")
            End Try
        End Sub

        Private Sub WaitForReady(ct As CancellationToken)
            Dim deadline = DateTime.UtcNow.AddSeconds(30)
            While DateTime.UtcNow < deadline AndAlso Not ct.IsCancellationRequested
                Try
                    Thread.Sleep(1000)
                    Dim response = _httpClient.GetAsync($"http://localhost:{_port}/health", ct).Result
                    If response.IsSuccessStatusCode Then
                        RaiseEvent StatusChanged(Me, "Translation server ready, loading model...")
                        LoadModelAsync().Wait()
                        Return
                    End If
                Catch
                End Try
            End While
            If Not ct.IsCancellationRequested Then
                RaiseEvent StatusChanged(Me, "Translation server: startup timeout")
            End If
        End Sub

        Public Async Function LoadModelAsync() As Task
            Try
                Dim json = $"{{""device"":""{_device}""}}"
                Dim content As New StringContent(json, Encoding.UTF8, "application/json")
                Dim response = Await _httpClient.PostAsync($"http://localhost:{_port}/load", content)
                If response.IsSuccessStatusCode Then
                    Dim body = Await response.Content.ReadAsStringAsync()
                    Using doc = JsonDocument.Parse(body)
                        Dim actualDevice = doc.RootElement.GetProperty("device").GetString()
                        _modelLoaded = doc.RootElement.GetProperty("model_loaded").GetBoolean()
                        RaiseEvent StatusChanged(Me, $"Translation model loaded on {actualDevice}")
                    End Using
                    _restartCount = 0
                End If
            Catch ex As Exception
                RaiseEvent StatusChanged(Me, $"Translation: model load failed: {ex.Message}")
            End Try
        End Function

        Public Async Function ReloadGlossaryAsync() As Task
            Try
                Dim content As New StringContent("{}", Encoding.UTF8, "application/json")
                Dim response = Await _httpClient.PostAsync($"http://localhost:{_port}/glossary/reload", content)
                If response.IsSuccessStatusCode Then
                    Dim body = Await response.Content.ReadAsStringAsync()
                    Using doc = JsonDocument.Parse(body)
                        Dim count = doc.RootElement.GetProperty("entries").GetInt32()
                        RaiseEvent StatusChanged(Me, $"Glossary reloaded: {count} entries")
                    End Using
                End If
            Catch ex As Exception
                RaiseEvent StatusChanged(Me, $"Glossary reload failed: {ex.Message}")
            End Try
        End Function

        Public Async Function UnloadModelAsync() As Task
            Try
                Dim content As New StringContent("{}", Encoding.UTF8, "application/json")
                Await _httpClient.PostAsync($"http://localhost:{_port}/unload", content)
                _modelLoaded = False
                RaiseEvent StatusChanged(Me, "Translation model unloaded")
            Catch
            End Try
        End Function

        Public Async Function TranslateAsync(text As String, sourceLang As String, targetLangs As List(Of String)) As Task(Of Dictionary(Of String, String))
            If Not _isRunning OrElse Not _modelLoaded OrElse targetLangs.Count = 0 Then
                Return New Dictionary(Of String, String)()
            End If

            Try
                Dim targetsJson As New StringBuilder("[")
                For i = 0 To targetLangs.Count - 1
                    If i > 0 Then targetsJson.Append(",")
                    targetsJson.Append($"""{targetLangs(i)}""")
                Next
                targetsJson.Append("]")

                Dim json = $"{{""text"":{EscapeJsonString(text)},""source_lang"":""{sourceLang}"",""target_langs"":{targetsJson}}}"
                Dim content As New StringContent(json, Encoding.UTF8, "application/json")

                Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(2))
                    Dim response = Await _httpClient.PostAsync($"http://localhost:{_port}/translate", content, cts.Token)
                    If response.IsSuccessStatusCode Then
                        Dim body = Await response.Content.ReadAsStringAsync()
                        Using doc = JsonDocument.Parse(body)
                            Dim result As New Dictionary(Of String, String)()
                            Dim translations = doc.RootElement.GetProperty("translations")
                            For Each prop In translations.EnumerateObject()
                                result(prop.Name) = prop.Value.GetString()
                            Next
                            Return result
                        End Using
                    End If
                End Using
            Catch
                ' Translation failed — caller gets empty dict
            End Try

            Return New Dictionary(Of String, String)()
        End Function

        Public Sub [Stop]()
            _cts?.Cancel()

            Try
                If _process IsNot Nothing AndAlso Not _process.HasExited Then
                    _process.Kill(True)
                    _process.WaitForExit(3000)
                End If
            Catch
            End Try

            _isRunning = False
            _modelLoaded = False
            _process = Nothing
            RaiseEvent StatusChanged(Me, "Translation server stopped")
        End Sub

        Private Shared Function FindPython() As String
            ' Check for embedded Python alongside the app
            Dim embedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-embed", "python.exe")
            If File.Exists(embedPath) Then Return embedPath

            ' Check system Python
            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = "python",
                    .Arguments = "--version",
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .CreateNoWindow = True
                }
                Using proc = Process.Start(psi)
                    proc.WaitForExit(5000)
                    If proc.ExitCode = 0 Then Return "python"
                End Using
            Catch
            End Try

            Return ""
        End Function

        Private Shared Function EscapeJsonString(s As String) As String
            Dim sb As New StringBuilder("""")
            For Each c In s
                Select Case c
                    Case """"c : sb.Append("\""")
                    Case "\"c : sb.Append("\\")
                    Case ChrW(10) : sb.Append("\n")
                    Case ChrW(13) : sb.Append("\r")
                    Case ChrW(9) : sb.Append("\t")
                    Case Else
                        If AscW(c) < 32 Then
                            sb.Append($"\u{AscW(c):X4}")
                        Else
                            sb.Append(c)
                        End If
                End Select
            Next
            sb.Append(""""c)
            Return sb.ToString()
        End Function

    End Class
End Namespace
