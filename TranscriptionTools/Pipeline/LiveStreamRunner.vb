Imports System.Diagnostics
Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports TranscriptionTools.Models

Namespace Pipeline
    Public Class LiveStreamRunner

        Public Event OutputLineUpdated As EventHandler(Of String)   ' interim - in-progress text
        Public Event OutputLineCommitted As EventHandler(Of String) ' final - committed text
        Public Event ErrorReceived As EventHandler(Of String)

        Private Shared ReadOnly _httpClient As New HttpClient() With {
            .Timeout = TimeSpan.FromMinutes(5)
        }

        Private _process As Process
        Private _isRunning As Boolean = False
        Private _serverReady As Boolean = False
        Private _shuttingDown As Boolean = False
        Private _transcript As New StringBuilder()
        Private _cts As CancellationTokenSource
        Private _port As Integer = 5091
        Private _restartCount As Integer = 0
        Private _lastConfig As AppConfig

        Public ReadOnly Property IsRunning As Boolean
            Get
                Return _isRunning
            End Get
        End Property

        Public ReadOnly Property Transcript As String
            Get
                Return _transcript.ToString()
            End Get
        End Property

        Public ReadOnly Property IsServerReady As Boolean
            Get
                Return _serverReady
            End Get
        End Property

        ''' <summary>
        ''' Enumerate audio input devices via the live-server's /devices endpoint,
        ''' or by running a quick Python one-liner if the server isn't up.
        ''' </summary>
        Public Function EnumerateDevicesAsync(pythonExePath As String) As List(Of String)
            Dim devices As New List(Of String)

            ' Try the running server first
            If _serverReady Then
                Try
                    Dim response = _httpClient.GetAsync($"http://127.0.0.1:{_port}/devices").Result
                    If response.IsSuccessStatusCode Then
                        Dim body = response.Content.ReadAsStringAsync().Result
                        Using doc = JsonDocument.Parse(body)
                            For Each dev In doc.RootElement.GetProperty("devices").EnumerateArray()
                                Dim id = dev.GetProperty("id").GetInt32()
                                Dim name = dev.GetProperty("name").GetString()
                                devices.Add($"{id}: {name}")
                            Next
                        End Using
                        If devices.Count > 0 Then Return devices
                    End If
                Catch
                End Try
            End If

            ' Fallback: run Python one-liner to get devices
            If Not String.IsNullOrEmpty(pythonExePath) AndAlso File.Exists(pythonExePath) Then
                Try
                    Dim script = "import sounddevice as sd; import json; a=sd.query_devices(sd.default.device[0])['hostapi']; ds=[{'id':i,'name':d['name']} for i,d in enumerate(sd.query_devices()) if d['max_input_channels']>0 and d['hostapi']==a]; print(json.dumps(ds))"
                    Dim psi As New ProcessStartInfo() With {
                        .FileName = pythonExePath,
                        .Arguments = $"-c ""{script}""",
                        .UseShellExecute = False,
                        .RedirectStandardOutput = True,
                        .RedirectStandardError = True,
                        .CreateNoWindow = True,
                        .StandardOutputEncoding = Encoding.UTF8
                    }

                    Using proc = Process.Start(psi)
                        Dim output = proc.StandardOutput.ReadToEnd()
                        proc.WaitForExit(10000)
                        If proc.ExitCode = 0 AndAlso Not String.IsNullOrWhiteSpace(output) Then
                            Using doc = JsonDocument.Parse(output.Trim())
                                For Each dev In doc.RootElement.EnumerateArray()
                                    Dim id = dev.GetProperty("id").GetInt32()
                                    Dim name = dev.GetProperty("name").GetString()
                                    devices.Add($"{id}: {name}")
                                Next
                            End Using
                        End If
                    End Using
                Catch
                End Try
            End If

            If devices.Count = 0 Then
                devices.Add("0: Default Device")
            End If

            Return devices
        End Function

        ''' <summary>
        ''' Start the live-server Python process and begin capturing.
        ''' </summary>
        Public Sub Start(config As AppConfig, deviceIndex As Integer, inputLanguage As String, translateToEnglish As Boolean)
            If _isRunning Then Return

            _port = config.LiveServerPort
            _transcript.Clear()
            _cts = New CancellationTokenSource()
            _restartCount = 0
            _shuttingDown = False
            _lastConfig = config

            ' If server is already running and healthy, reuse it
            Dim serverAlive = False
            If _process IsNot Nothing AndAlso Not _process.HasExited Then
                Try
                    Dim resp = _httpClient.GetAsync($"http://127.0.0.1:{_port}/health").Result
                    serverAlive = resp.IsSuccessStatusCode
                    If serverAlive Then _serverReady = True
                Catch
                End Try
            End If

            If Not serverAlive Then
                ' Kill any leftover process before starting fresh
                KillExistingServer()
                StartServer(config)
            End If

            ' Wait for server ready, then start capture
            Task.Run(Sub()
                         Dim ready = If(serverAlive, True, WaitForReady(_cts.Token))
                         If ready Then
                             StartCapture(config, deviceIndex, inputLanguage, translateToEnglish)
                             ' Connect to SSE stream
                             ReadSseLoop(_cts.Token)
                         End If
                     End Sub)
        End Sub

        Private Sub KillExistingServer()
            _serverReady = False
            Try
                If _process IsNot Nothing AndAlso Not _process.HasExited Then
                    ' Try graceful shutdown first
                    Try
                        Dim content As New StringContent("{}", Encoding.UTF8, "application/json")
                        _httpClient.PostAsync($"http://127.0.0.1:{_port}/shutdown", content).Wait(2000)
                        _process.WaitForExit(3000)
                    Catch
                    End Try

                    ' Force kill if still alive
                    If Not _process.HasExited Then
                        _process.Kill(True)
                        _process.WaitForExit(2000)
                    End If
                End If
            Catch
            End Try
            _process = Nothing
        End Sub

        Private Sub StartServer(config As AppConfig)
            Dim pythonPath = FindPython()
            If String.IsNullOrEmpty(pythonPath) Then
                RaiseEvent ErrorReceived(Me, "Python not found (need python-embed or system Python)")
                Return
            End If

            Dim serverScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live-server", "server.py")
            If Not File.Exists(serverScript) Then
                RaiseEvent ErrorReceived(Me, $"live-server/server.py not found: {serverScript}")
                Return
            End If

            Dim logDir = AppDomain.CurrentDomain.BaseDirectory
            Dim psi As New ProcessStartInfo() With {
                .FileName = pythonPath,
                .Arguments = $"""{serverScript}"" --port {_port} --log-dir ""{logDir.TrimEnd({"\"c, "/"c})}""",
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }

            ' Add whisper dir to PATH for CUDA DLLs
            Dim whisperDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper")
            If Directory.Exists(whisperDir) Then
                Dim currentPath = If(Environment.GetEnvironmentVariable("PATH"), "")
                psi.Environment("PATH") = whisperDir & ";" & currentPath
            End If

            Try
                _process = New Process()
                _process.StartInfo = psi
                _process.EnableRaisingEvents = True

                AddHandler _process.ErrorDataReceived, Sub(s, e)
                                                           If e.Data IsNot Nothing Then
                                                               Dim line = e.Data.Trim()
                                                               If line.Length > 0 Then
                                                                   RaiseEvent ErrorReceived(Me, line)
                                                               End If
                                                           End If
                                                       End Sub

                AddHandler _process.Exited, Sub(s, e)
                                                _isRunning = False
                                                _serverReady = False
                                                If Not _shuttingDown Then
                                                    RaiseEvent ErrorReceived(Me, "Live server process exited unexpectedly")
                                                End If
                                            End Sub

                _process.Start()
                _process.BeginErrorReadLine()
                ' Drain stdout to prevent deadlock
                Task.Run(Sub()
                             Try : _process.StandardOutput.ReadToEnd() : Catch : End Try
                         End Sub)

                _isRunning = True
            Catch ex As Exception
                _isRunning = False
                RaiseEvent ErrorReceived(Me, $"Failed to start live server: {ex.Message}")
            End Try
        End Sub

        Private Function WaitForReady(ct As CancellationToken) As Boolean
            Dim deadline = DateTime.UtcNow.AddSeconds(30)
            While DateTime.UtcNow < deadline AndAlso Not ct.IsCancellationRequested
                Try
                    Thread.Sleep(500)
                    Dim response = _httpClient.GetAsync($"http://127.0.0.1:{_port}/health", ct).Result
                    If response.IsSuccessStatusCode Then
                        _serverReady = True
                        _restartCount = 0
                        Return True
                    End If
                Catch
                End Try
            End While
            If Not ct.IsCancellationRequested Then
                RaiseEvent ErrorReceived(Me, "Live server: startup timeout (30s)")
            End If
            Return False
        End Function

        Private Sub StartCapture(config As AppConfig, deviceIndex As Integer, inputLanguage As String, translateToEnglish As Boolean)
            Try
                Dim modelPath = AppConfig.ResolvePath(config.PathFasterWhisperModel)
                Dim jsonBody = $"{{""device_index"":{deviceIndex}," &
                    $"""language"":""{inputLanguage}""," &
                    $"""translate"":{If(translateToEnglish, "true", "false")}," &
                    $"""initial_prompt"":""{EscapeJson(config.InitialPrompt)}""," &
                    $"""model_path"":""{EscapeJson(modelPath)}""," &
                    $"""compute_type"":""{config.LiveComputeType}""," &
                    $"""device"":""{If(config.NoGpu, "cpu", "cuda")}""," &
                    $"""beam_size"":{config.BeamSize}," &
                    $"""vad_min_silence_ms"":{config.LiveVadSilenceMs}," &
                    $"""vad_max_segment_s"":{config.LiveMaxSegmentSec}," &
                    $"""interim_interval_ms"":{config.LiveInterimIntervalMs}}}"

                Dim content As New StringContent(jsonBody, Encoding.UTF8, "application/json")
                Dim response = _httpClient.PostAsync($"http://127.0.0.1:{_port}/start", content).Result

                If Not response.IsSuccessStatusCode Then
                    Dim body = response.Content.ReadAsStringAsync().Result
                    RaiseEvent ErrorReceived(Me, $"Failed to start capture: {body}")
                End If
            Catch ex As Exception
                RaiseEvent ErrorReceived(Me, $"Failed to start capture: {ex.Message}")
            End Try
        End Sub

        Private Sub ReadSseLoop(ct As CancellationToken)
            Try
                Dim request As New HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}/stream")
                Dim response = _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).Result
                Using stream = response.Content.ReadAsStreamAsync().Result
                    Using reader As New StreamReader(stream, Encoding.UTF8)
                        Dim eventType = ""
                        While Not ct.IsCancellationRequested
                            Dim line = reader.ReadLine()
                            If line Is Nothing Then Exit While

                            If line.StartsWith("event:") Then
                                eventType = line.Substring(6).Trim()
                            ElseIf line.StartsWith("data:") Then
                                Dim dataStr = line.Substring(5).Trim()
                                Dim parsed = ParseJsonData(dataStr)
                                If Not String.IsNullOrEmpty(parsed.Text) Then
                                    If eventType = "update" Then
                                        RaiseEvent OutputLineUpdated(Me, parsed.Text)
                                    ElseIf eventType = "commit" Then
                                        _transcript.AppendLine(parsed.Text)
                                        ' Pass detected language with text (tab-separated)
                                        Dim commitData = If(String.IsNullOrEmpty(parsed.Lang), parsed.Text, parsed.Lang & vbTab & parsed.Text)
                                        RaiseEvent OutputLineCommitted(Me, commitData)
                                    ElseIf eventType = "error" Then
                                        RaiseEvent ErrorReceived(Me, parsed.Text)
                                    End If
                                End If
                                eventType = ""
                            End If
                            ' Empty line or comment — ignore
                        End While
                    End Using
                End Using
            Catch ex As OperationCanceledException
                ' Expected on stop
            Catch ex As Exception
                If Not ct.IsCancellationRequested Then
                    RaiseEvent ErrorReceived(Me, $"SSE connection lost: {ex.Message}")
                End If
            End Try
        End Sub

        Public Sub [Stop]()
            _shuttingDown = True
            _cts?.Cancel()

            ' Shut down the server completely
            Try
                Dim content As New StringContent("{}", Encoding.UTF8, "application/json")
                _httpClient.PostAsync($"http://127.0.0.1:{_port}/shutdown", content).Wait(3000)
            Catch
            End Try

            ' Force kill if still alive
            Try
                If _process IsNot Nothing AndAlso Not _process.HasExited Then
                    _process.WaitForExit(3000)
                    If Not _process.HasExited Then
                        _process.Kill(True)
                        _process.WaitForExit(2000)
                    End If
                End If
            Catch
            End Try

            _isRunning = False
            _serverReady = False
            _process = Nothing
        End Sub

        Public Sub ShutdownServer()
            _shuttingDown = True
            _cts?.Cancel()

            ' Ask the server to shut down gracefully
            Try
                Dim content As New StringContent("{}", Encoding.UTF8, "application/json")
                _httpClient.PostAsync($"http://127.0.0.1:{_port}/shutdown", content).Wait(3000)
            Catch
            End Try

            ' Wait for process to exit, then force-kill if needed
            Try
                If _process IsNot Nothing Then
                    If Not _process.WaitForExit(5000) Then
                        _process.Kill(True)
                        _process.WaitForExit(2000)
                    End If
                End If
            Catch
            End Try

            _isRunning = False
            _serverReady = False
            _process = Nothing
        End Sub

        Public Function SaveTranscript(filePath As String) As Boolean
            Try
                File.WriteAllText(filePath, _transcript.ToString(), Encoding.UTF8)
                Return True
            Catch
                Return False
            End Try
        End Function

        Private Shared Function FindPython() As String
            Dim embedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-embed", "python.exe")
            If File.Exists(embedPath) Then Return embedPath

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

        Private Structure ParsedData
            Public Text As String
            Public Lang As String
        End Structure

        Private Shared Function ParseJsonData(json As String) As ParsedData
            Dim result As New ParsedData()
            Try
                Using doc = JsonDocument.Parse(json)
                    Dim root = doc.RootElement
                    result.Text = If(root.TryGetProperty("text", Nothing), root.GetProperty("text").GetString(), "")
                    Dim langProp As JsonElement = Nothing
                    If root.TryGetProperty("lang", langProp) Then
                        result.Lang = langProp.GetString()
                    End If
                End Using
            Catch
            End Try
            Return result
        End Function

        Private Shared Function EscapeJson(s As String) As String
            If String.IsNullOrEmpty(s) Then Return ""
            Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "").Replace(vbLf, "\n")
        End Function

    End Class
End Namespace
