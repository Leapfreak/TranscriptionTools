Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports TranscriptionTools.Models

Namespace Pipeline
    Public Class LiveStreamRunner

        Public Event OutputLineUpdated As EventHandler(Of String)   ' \r - replace last line (in-progress refinement)
        Public Event OutputLineCommitted As EventHandler(Of String) ' \n - final line
        Public Event ErrorReceived As EventHandler(Of String)

        Private _process As Process
        Private _isRunning As Boolean = False
        Private _transcript As New StringBuilder()
        Private _stdoutCts As CancellationTokenSource

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

        Public Function EnumerateDevicesFromSDL(streamExePath As String, modelPath As String) As List(Of String)
            Dim devices As New List(Of String)

            If Not File.Exists(streamExePath) Then
                devices.Add("0: Default Device")
                Return devices
            End If

            ' Start whisper-stream with --capture -1 (default device) and the real model.
            ' SDL lists all capture devices during init, before model loading begins.
            ' We kill the process as soon as we have the device list.
            Dim psi As New ProcessStartInfo() With {
                .FileName = streamExePath,
                .Arguments = $"--capture -1 -m ""{modelPath}""",
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }

            Try
                Dim proc = Process.Start(psi)

                ' Drain stdout on a background thread to prevent deadlocks
                Dim stdoutDrain = proc.StandardOutput.ReadToEndAsync()

                Dim stderrReader = proc.StandardError
                Dim deadline = DateTime.UtcNow.AddSeconds(30)

                While Not stderrReader.EndOfStream AndAlso DateTime.UtcNow < deadline
                    Dim line = stderrReader.ReadLine()
                    If line Is Nothing Then Exit While

                    Dim trimmed = StripAnsi(line).Trim()

                    ' Parse device lines: "init:    - Capture device #0: 'Device Name'"
                    If trimmed.Contains("Capture device #") Then
                        Dim idxHash = trimmed.IndexOf("#"c)
                        Dim idxColon = trimmed.IndexOf(":"c, idxHash)
                        If idxHash >= 0 AndAlso idxColon > idxHash Then
                            Dim deviceId = trimmed.Substring(idxHash + 1, idxColon - idxHash - 1).Trim()
                            Dim deviceName = trimmed.Substring(idxColon + 1).Trim().Trim("'"c, " "c)
                            devices.Add($"{deviceId}: {deviceName}")
                        End If
                    End If

                    ' Kill as soon as we see model loading start (devices are already listed)
                    If trimmed.StartsWith("whisper_init") OrElse trimmed.Contains("loading model") Then
                        Exit While
                    End If
                End While

                Try
                    If Not proc.HasExited Then proc.Kill(True)
                Catch
                End Try
                proc.Dispose()
            Catch
            End Try

            If devices.Count = 0 Then
                devices.Add("0: Default Device")
            End If

            Return devices
        End Function

        Public Shared Function BuildArgs(config As AppConfig, captureDeviceId As Integer,
                                          inputLanguage As String, translateToEnglish As Boolean) As String
            Dim args As New List(Of String)

            args.Add($"-m ""{config.PathModel}""")
            args.Add($"--capture {captureDeviceId}")
            args.Add($"-t {config.Threads}")
            args.Add($"-l {inputLanguage}")

            args.Add("--step 3000")
            args.Add("--length 10000")
            args.Add("--keep-context")

            If translateToEnglish Then
                args.Add("-tr")
            End If

            If Not String.IsNullOrWhiteSpace(config.InitialPrompt) Then
                args.Add($"--prompt ""{config.InitialPrompt}""")
            End If

            If config.NoGpu Then args.Add("-ng")
            If config.FlashAttn Then args.Add("-fa")

            Return String.Join(" ", args)
        End Function

        Public Sub Start(streamExePath As String, arguments As String)
            If _isRunning Then Return

            If Not File.Exists(streamExePath) Then
                RaiseEvent ErrorReceived(Me, $"whisper-stream.exe not found: {streamExePath}")
                Return
            End If

            _transcript.Clear()
            _stdoutCts = New CancellationTokenSource()

            Dim psi As New ProcessStartInfo() With {
                .FileName = streamExePath,
                .Arguments = arguments,
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }

            _process = New Process()
            _process.StartInfo = psi
            _process.EnableRaisingEvents = True

            AddHandler _process.ErrorDataReceived, Sub(s, e)
                                                       If e.Data IsNot Nothing Then
                                                           Dim cleaned = StripAnsi(e.Data).Trim()
                                                           If cleaned.Length = 0 Then Return

                                                           ' Filter noise from display
                                                           If cleaned.Contains("deprecated") OrElse
                                                              cleaned.Contains("Please use") OrElse
                                                              cleaned.Contains("deprecation-warning") OrElse
                                                              cleaned.StartsWith("whisper_") OrElse
                                                              cleaned.StartsWith("whisper_model_load") OrElse
                                                              cleaned.StartsWith("whisper_backend") OrElse
                                                              cleaned.StartsWith("whisper_init") Then
                                                               Return
                                                           End If
                                                           RaiseEvent ErrorReceived(Me, cleaned)
                                                       End If
                                                   End Sub

            AddHandler _process.Exited, Sub(s, e)
                                            _isRunning = False
                                            _stdoutCts?.Cancel()
                                        End Sub

            Try
                _process.Start()
                _process.BeginErrorReadLine()
                _isRunning = True

                ' Read stdout manually to handle \r (carriage return) updates
                ' whisper-stream uses \r to overwrite lines in real-time, not \n
                Dim ct = _stdoutCts.Token
                Task.Run(Sub() ReadStdoutLoop(_process.StandardOutput, ct), ct)
            Catch ex As Exception
                _isRunning = False
                RaiseEvent ErrorReceived(Me, $"Failed to start stream: {ex.Message}")
            End Try
        End Sub

        Private Sub ReadStdoutLoop(reader As StreamReader, ct As CancellationToken)
            Dim buffer As New StringBuilder()
            Try
                Dim lastUpdated As String = ""

                While Not ct.IsCancellationRequested
                    Dim ch = reader.Read()
                    If ch = -1 Then Exit While ' EOF

                    Dim c = ChrW(ch)
                    If c = vbCr Then
                        ' Carriage return = in-progress refinement, replace last line
                        Dim line = StripAnsi(buffer.ToString()).Trim()
                        buffer.Clear()

                        If line.Length > 0 Then
                            lastUpdated = line
                            RaiseEvent OutputLineUpdated(Me, line)
                        End If
                    ElseIf c = vbLf Then
                        ' Newline = commit the current line
                        Dim line = StripAnsi(buffer.ToString()).Trim()
                        buffer.Clear()

                        ' If buffer was empty (text was already consumed by \r), commit the last updated line
                        If line.Length = 0 Then line = lastUpdated

                        If line.Length > 0 Then
                            _transcript.AppendLine(line)
                            RaiseEvent OutputLineCommitted(Me, line)
                            lastUpdated = ""
                        End If
                    Else
                        buffer.Append(c)
                    End If
                End While

                ' Flush remaining
                Dim remaining = StripAnsi(buffer.ToString()).Trim()
                If remaining.Length = 0 Then remaining = lastUpdated
                If remaining.Length > 0 Then
                    _transcript.AppendLine(remaining)
                    RaiseEvent OutputLineCommitted(Me, remaining)
                End If
            Catch ex As OperationCanceledException
                ' Expected on stop
            Catch ex As IOException
                ' Process killed, stream closed
            Catch
                ' Ignore other errors during read
            End Try
        End Sub

        Public Sub [Stop]()
            If Not _isRunning OrElse _process Is Nothing Then Return

            _stdoutCts?.Cancel()

            Try
                If Not _process.HasExited Then
                    _process.Kill(True)
                End If
            Catch
            End Try

            _isRunning = False
        End Sub

        Public Function SaveTranscript(filePath As String) As Boolean
            Try
                File.WriteAllText(filePath, _transcript.ToString(), Encoding.UTF8)
                Return True
            Catch
                Return False
            End Try
        End Function

        Private Shared Function StripAnsi(text As String) As String
            Return Regex.Replace(text, "\x1B\[[0-9;]*[A-Za-z]|\[2K", "")
        End Function
    End Class
End Namespace
