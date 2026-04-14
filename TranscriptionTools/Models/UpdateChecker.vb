Imports System.Net.Http
Imports System.Text.Json

Namespace Models
    Public Class UpdateInfo
        Public Property TagName As String = ""
        Public Property HtmlUrl As String = ""
        Public Property Body As String = ""
    End Class

    Public Class UpdateChecker
        Private Const Owner As String = "Leapfreak"
        Private Const Repo As String = "TranscriptionTools"

        Private Shared ReadOnly _client As New HttpClient()

        Shared Sub New()
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("TranscriptionTools-UpdateChecker")
        End Sub

        ''' <summary>
        ''' Checks GitHub for a newer release. Returns UpdateInfo if a newer version exists, otherwise Nothing.
        ''' </summary>
        Public Shared Async Function CheckForUpdateAsync() As Task(Of UpdateInfo)
            Try
                Dim url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest"
                Dim json = Await _client.GetStringAsync(url)

                Using doc = JsonDocument.Parse(json)
                    Dim root = doc.RootElement
                    Dim tagName = root.GetProperty("tag_name").GetString()
                    Dim htmlUrl = root.GetProperty("html_url").GetString()
                    Dim body = ""
                    Dim bodyEl As JsonElement
                    If root.TryGetProperty("body", bodyEl) AndAlso bodyEl.ValueKind <> JsonValueKind.Null Then
                        body = bodyEl.GetString()
                    End If

                    Dim remoteVersion = ParseVersion(tagName)
                    If remoteVersion Is Nothing Then Return Nothing

                    Dim localVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
                    If remoteVersion > localVersion Then
                        Return New UpdateInfo With {
                            .TagName = tagName,
                            .HtmlUrl = htmlUrl,
                            .Body = body
                        }
                    End If
                End Using
            Catch
                ' Silently fail — no network, rate limited, no releases yet, etc.
            End Try

            Return Nothing
        End Function

        Private Shared Function ParseVersion(tag As String) As Version
            If String.IsNullOrWhiteSpace(tag) Then Return Nothing
            ' Strip leading 'v' if present (e.g., "v1.2.0" -> "1.2.0")
            Dim cleaned = tag.TrimStart("v"c, "V"c)
            Dim result As Version = Nothing
            Version.TryParse(cleaned, result)
            Return result
        End Function
    End Class
End Namespace
