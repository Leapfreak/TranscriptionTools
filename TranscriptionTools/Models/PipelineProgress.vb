Namespace Models
    Public Class PipelineProgress
        Public Property StepIndex As Integer

        Public Property StepCount As Integer = 8

        Public Property StatusMessage As String = ""

        Public Property ChunkDone As Integer

        Public Property ChunkTotal As Integer

        Public ReadOnly Property OverallPercent As Integer
            Get
                If StepCount <= 1 Then Return 0
                Return CInt(Math.Min(100, (StepIndex * 100) \ (StepCount - 1)))
            End Get
        End Property
    End Class
End Namespace
