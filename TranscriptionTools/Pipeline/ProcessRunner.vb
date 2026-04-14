Imports System.Diagnostics
Imports System.Text
Imports System.Threading

Namespace Pipeline
    Public Class ProcessRunner

        Public Event OutputReceived As EventHandler(Of String)
        Public Event ErrorReceived As EventHandler(Of String)

        Public Async Function RunAsync(exePath As String, arguments As String, workingDir As String,
                                        Optional ct As CancellationToken = Nothing) As Task(Of Integer)
            Dim psi As New ProcessStartInfo() With {
                .FileName = exePath,
                .Arguments = arguments,
                .WorkingDirectory = workingDir,
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }

            Dim proc As New Process()
            proc.StartInfo = psi
            proc.EnableRaisingEvents = True

            Dim stdoutDone As New TaskCompletionSource(Of Boolean)()
            Dim stderrDone As New TaskCompletionSource(Of Boolean)()

            AddHandler proc.OutputDataReceived, Sub(s, e)
                                                    If e.Data IsNot Nothing Then
                                                        RaiseEvent OutputReceived(Me, e.Data)
                                                    Else
                                                        stdoutDone.TrySetResult(True)
                                                    End If
                                                End Sub

            AddHandler proc.ErrorDataReceived, Sub(s, e)
                                                   If e.Data IsNot Nothing Then
                                                       RaiseEvent ErrorReceived(Me, e.Data)
                                                   Else
                                                       stderrDone.TrySetResult(True)
                                                   End If
                                               End Sub

            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()

            ' Handle cancellation
            Dim registration As CancellationTokenRegistration = Nothing
            If ct.CanBeCanceled Then
                registration = ct.Register(
                    Sub()
                        Try
                            If Not proc.HasExited Then proc.Kill(True)
                        Catch
                        End Try
                    End Sub)
            End If

            Try
                ' Wait for process to exit
                Await proc.WaitForExitAsync(ct)
                ' Wait for stdout/stderr to flush
                Await Task.WhenAll(stdoutDone.Task, stderrDone.Task)
                Return proc.ExitCode
            Catch ex As OperationCanceledException
                Try
                    If Not proc.HasExited Then proc.Kill(True)
                Catch
                End Try
                Throw
            Finally
                registration.Dispose()
                proc.Dispose()
            End Try
        End Function

        Public Function StartNoWait(exePath As String, arguments As String, workingDir As String,
                                     Optional visible As Boolean = True) As Process
            Dim psi As New ProcessStartInfo() With {
                .FileName = exePath,
                .Arguments = arguments,
                .WorkingDirectory = workingDir,
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = Not visible,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }

            Dim proc As New Process()
            proc.StartInfo = psi
            proc.EnableRaisingEvents = True

            AddHandler proc.OutputDataReceived, Sub(s, e)
                                                    If e.Data IsNot Nothing Then
                                                        RaiseEvent OutputReceived(Me, e.Data)
                                                    End If
                                                End Sub

            AddHandler proc.ErrorDataReceived, Sub(s, e)
                                                   If e.Data IsNot Nothing Then
                                                       RaiseEvent ErrorReceived(Me, e.Data)
                                                   End If
                                               End Sub

            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()
            Return proc
        End Function
    End Class
End Namespace
