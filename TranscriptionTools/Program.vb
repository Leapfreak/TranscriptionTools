Imports System.Diagnostics

Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        Dim createdNew As Boolean
        Dim mtx As New Threading.Mutex(True, "TranscriptionTools_SingleInstance", createdNew)
        If Not createdNew Then
            ' Kill the old instance (e.g. stale process after update) and take over
            KillOtherInstances()
            mtx.Dispose()
            mtx = New Threading.Mutex(True, "TranscriptionTools_SingleInstance", createdNew)
            If Not createdNew Then
                ' Still can't acquire — give up
                mtx.Dispose()
                Return
            End If
        End If

        Try
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New FormMain)
        Finally
            mtx.ReleaseMutex()
            mtx.Dispose()
        End Try
    End Sub

    Private Sub KillOtherInstances()
        Dim currentId = Process.GetCurrentProcess().Id
        Dim myName = Process.GetCurrentProcess().ProcessName
        For Each p In Process.GetProcessesByName(myName)
            If p.Id <> currentId Then
                Try
                    p.Kill()
                    p.WaitForExit(3000)
                Catch
                End Try
            End If
            p.Dispose()
        Next
    End Sub

End Module
