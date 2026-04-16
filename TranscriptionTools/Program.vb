Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        Dim createdNew As Boolean
        Dim mtx As New Threading.Mutex(True, "TranscriptionTools_SingleInstance", createdNew)
        If Not createdNew Then
            mtx.Dispose()
            Return
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

End Module
