Imports System.Net.Http
Imports System.Text.Json

Namespace Models
    Public Class UpdateInfo
        Public Property TagName As String = ""
        Public Property HtmlUrl As String = ""
        Public Property Body As String = ""
        Public Property InstallerUrl As String = ""
        Public Property AppZipUrl As String = ""
        Public Property WhisperZipUrl As String = ""
        Public Property NeedsAppUpdate As Boolean = False
        Public Property NeedsWhisperUpdate As Boolean = False
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
                    If remoteVersion <= localVersion Then Return Nothing

                    ' Build update info with asset URLs
                    Dim info As New UpdateInfo With {
                        .TagName = tagName,
                        .HtmlUrl = htmlUrl,
                        .Body = body
                    }

                    ' Find assets
                    Dim assetsEl As JsonElement
                    If root.TryGetProperty("assets", assetsEl) Then
                        For Each asset In assetsEl.EnumerateArray()
                            Dim name = asset.GetProperty("name").GetString()
                            If name Is Nothing Then Continue For
                            Dim assetUrl = asset.GetProperty("browser_download_url").GetString()

                            If name.StartsWith("TranscriptionTools_Setup", StringComparison.OrdinalIgnoreCase) AndAlso
                               name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) Then
                                info.InstallerUrl = assetUrl
                            ElseIf name.StartsWith("TranscriptionTools_App_", StringComparison.OrdinalIgnoreCase) AndAlso
                                   name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                                info.AppZipUrl = assetUrl
                            ElseIf name.StartsWith("TranscriptionTools_Whisper_", StringComparison.OrdinalIgnoreCase) AndAlso
                                   name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                                info.WhisperZipUrl = assetUrl
                            End If
                        Next
                    End If

                    ' Determine which components need updating by reading the manifest
                    info.NeedsAppUpdate = True  ' App always needs update if version is newer
                    info.NeedsWhisperUpdate = Not HasMatchingWhisperVersion(root)

                    Return info
                End Using
            Catch
            End Try

            Return Nothing
        End Function

        ''' <summary>
        ''' Check if the local whisper version matches the release manifest.
        ''' </summary>
        Private Shared Function HasMatchingWhisperVersion(releaseRoot As JsonElement) As Boolean
            Try
                ' Read the manifest from release assets
                Dim assetsEl As JsonElement
                If Not releaseRoot.TryGetProperty("assets", assetsEl) Then Return False

                ' Check local whisper version
                Dim localWhisperVersion = GetLocalWhisperVersion()
                If String.IsNullOrEmpty(localWhisperVersion) Then Return False

                ' Find manifest asset and read it
                For Each asset In assetsEl.EnumerateArray()
                    Dim name = asset.GetProperty("name").GetString()
                    If name = "manifest.json" Then
                        Dim manifestUrl = asset.GetProperty("browser_download_url").GetString()
                        Dim manifestJson = _client.GetStringAsync(manifestUrl).GetAwaiter().GetResult()
                        Using manifestDoc = JsonDocument.Parse(manifestJson)
                            Dim whisperVer As JsonElement
                            If manifestDoc.RootElement.TryGetProperty("whisper", whisperVer) Then
                                Return localWhisperVersion = whisperVer.GetString()
                            End If
                        End Using
                    End If
                Next
            Catch
            End Try
            Return False
        End Function

        ''' <summary>
        ''' Read the local whisper component version from component-versions.json.
        ''' </summary>
        Public Shared Function GetLocalWhisperVersion() As String
            Try
                Dim versionFile = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "component-versions.json")
                If Not IO.File.Exists(versionFile) Then Return ""
                Dim json = IO.File.ReadAllText(versionFile)
                Using doc = JsonDocument.Parse(json)
                    Dim ver As JsonElement
                    If doc.RootElement.TryGetProperty("whisper", ver) Then
                        Return ver.GetString()
                    End If
                End Using
            Catch
            End Try
            Return ""
        End Function

        ''' <summary>
        ''' Save local component versions after an update.
        ''' </summary>
        Public Shared Sub SaveLocalVersions(appVersion As String, whisperVersion As String)
            Try
                Dim versionFile = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "component-versions.json")
                Dim versions As New Dictionary(Of String, String) From {
                    {"app", appVersion},
                    {"whisper", whisperVersion}
                }
                Dim json = JsonSerializer.Serialize(versions, New JsonSerializerOptions With {.WriteIndented = True})
                IO.File.WriteAllText(versionFile, json)
            Catch
            End Try
        End Sub

        Private Shared Function ParseVersion(tag As String) As Version
            If String.IsNullOrWhiteSpace(tag) Then Return Nothing
            Dim cleaned = tag.TrimStart("v"c, "V"c)
            Dim result As Version = Nothing
            Version.TryParse(cleaned, result)
            Return result
        End Function
    End Class
End Namespace
