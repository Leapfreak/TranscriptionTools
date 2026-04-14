Namespace Models
    Public Class PipelineException
        Inherits Exception

        Public Property MessageKey As String

        Public Sub New(messageKey As String, message As String, Optional innerException As Exception = Nothing)
            MyBase.New(message, innerException)
            Me.MessageKey = messageKey
        End Sub
    End Class
End Namespace
